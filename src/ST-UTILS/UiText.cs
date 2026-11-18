using System.Text.Json;
using System.Text.RegularExpressions;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;

namespace SurfTimer;

/// <summary>
/// GAMEPOINT: translates the English texts of the menus / panels / HUD where they leave the plugin (PlayerHud.SendText,
/// MenuPresenter's chat menus, the classic HTML HUD, !help details) instead of in every page - so syncing the fork with
/// upstream z4lab touches only those few lines.
/// lang/ui_&lt;ui_language&gt;.json maps the English text to the translation:
///   "back to the map start": "kartaning boshiga qaytish"   exact text
///   "#{0} of {1}": "{1} tadan #{0}"                        template: {n} matches anything, may move in the translation
/// Unknown texts, a missing file and ui_language "en" (timer_settings.json) stay English.
/// </summary>
internal static class UiText
{
	internal sealed record Pattern(Regex Regex, string Template, int[] Groups, int Literal);

	private static readonly Regex Placeholder = new(@"\{(\d+)\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex Letters = new(@"\p{L}", RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly object Lock = new();

	private static Dictionary<string, string> _exact = new(StringComparer.Ordinal);
	private static List<Pattern> _patterns = [];
	/// <summary>Results of Tr - the HUD sends the same texts many times a second</summary>
	private static readonly Dictionary<string, string> Cache = new(StringComparer.Ordinal);
	private const int CacheLimit = 20000;

	private static string _moduleDirectory = "";
	private static ILogger? _logger;

	/// <summary>"en" = no translation</summary>
	internal static string Language { get; private set; } = "en";

	/// <summary>From the plugin's Load: reads the dictionary now and again on every map start (ui_language changes
	/// and dictionary edits apply without a rebuild)</summary>
	internal static void Init(BasePlugin plugin)
	{
		_moduleDirectory = plugin.ModuleDirectory;
		_logger = plugin.Logger;
		Load();
		plugin.RegisterListener<Listeners.OnMapStart>(_ => Load());
	}

	internal static void Load()
	{
		string language = ReadLanguage();
		var exact = new Dictionary<string, string>(StringComparer.Ordinal);
		var patterns = new List<Pattern>();

		if (!language.Equals("en", StringComparison.OrdinalIgnoreCase))
		{
			string path = Path.Combine(_moduleDirectory, "lang", $"ui_{language}.json");
			try
			{
				if (File.Exists(path))
					Parse(File.ReadAllText(path), exact, patterns);
				else
					_logger?.LogWarning("[UiText] {Path} not found - menus stay English", path);
			}
			catch (Exception ex)
			{
				_logger?.LogError(ex, "[UiText] Reading {Path} failed - menus stay English", path);
				exact.Clear();
				patterns.Clear();
			}
		}

		lock (Lock)
		{
			_exact = exact;
			_patterns = patterns;
			Cache.Clear();
			Language = exact.Count + patterns.Count > 0 ? language : "en";
		}
		_logger?.LogInformation("[UiText] UI language '{Language}': {Exact} texts, {Patterns} templates", Language, exact.Count, patterns.Count);
	}

	/// <summary>
	/// Builds the lookup from a dictionary file - also used by the tests. Keys starting with "//" are comments,
	/// empty values are skipped (the text stays English).
	/// </summary>
	internal static void Parse(string json, Dictionary<string, string> exact, List<Pattern> patterns)
	{
		using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
		foreach (var entry in doc.RootElement.EnumerateObject())
		{
			string key = entry.Name;
			string? value = entry.Value.ValueKind == JsonValueKind.String ? entry.Value.GetString() : null;
			if (key.StartsWith("//") || string.IsNullOrEmpty(value) || key.Length == 0)
				continue;

			var holes = Placeholder.Matches(key);
			if (holes.Count == 0)
			{
				exact[key] = value;
				continue;
			}

			// "{n}" -> a lazy group, the rest literally; group i+1 holds placeholder holes[i]
			var regex = new System.Text.StringBuilder("^");
			var groupOf = new Dictionary<int, int>();
			int at = 0;
			foreach (Match hole in holes)
			{
				regex.Append(Regex.Escape(key[at..hole.Index])).Append("(.+?)");
				groupOf.TryAdd(int.Parse(hole.Groups[1].Value), groupOf.Count + 1);
				at = hole.Index + hole.Length;
			}
			regex.Append(Regex.Escape(key[at..])).Append('$');

			// The translation may only use placeholders the key has
			var used = Placeholder.Matches(value).Select(m => int.Parse(m.Groups[1].Value)).ToList();
			if (used.Any(n => !groupOf.ContainsKey(n)))
			{
				_logger?.LogWarning("[UiText] '{Value}' uses a placeholder '{Key}' doesn't have - skipped", value, key);
				continue;
			}

			int max = groupOf.Keys.Max();
			var groups = new int[max + 1];
			foreach (var (n, g) in groupOf)
				groups[n] = g;
			patterns.Add(new Pattern(new Regex(regex.ToString(), RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline),
				value, groups, Placeholder.Replace(key, "").Length));
		}
		// The most specific template first: "Bonus {0} WR" before "Bonus {0}", then more parts ("{0} · #{1}" before "row {0}")
		patterns.Sort((a, b) => b.Literal != a.Literal ? b.Literal.CompareTo(a.Literal) : b.Groups.Length.CompareTo(a.Groups.Length));
	}

	/// <summary>The translation of an English UI text, or the text itself</summary>
	internal static string Tr(string? text)
	{
		if (string.IsNullOrEmpty(text))
			return text ?? "";

		lock (Lock)
		{
			if (_exact.Count == 0 && _patterns.Count == 0)
				return text;
			if (Cache.TryGetValue(text, out var cached))
				return cached;

			string result = Translate(text, _exact, _patterns, 0);
			if (Cache.Count >= CacheLimit)
				Cache.Clear(); // Changing numbers in texts (times, counts) would grow it forever
			Cache[text] = result;
			return result;
		}
	}

	/// <summary>Exact text, else the first matching template - its parts are translated too ("HUD layout: Default")</summary>
	internal static string Translate(string text, Dictionary<string, string> exact, List<Pattern> patterns, int depth)
	{
		if (exact.TryGetValue(text, out var hit))
			return hit;
		if (depth > 1 || !Letters.IsMatch(text))
			return text; // Numbers, times, keys - nothing to translate

		// Spaces around the text (e.g. "  (you)") stay where they are
		string trimmed = text.Trim();
		if (trimmed.Length != text.Length && trimmed.Length > 0 && exact.TryGetValue(trimmed, out var inner))
			return text[..text.IndexOf(trimmed, StringComparison.Ordinal)] + inner + text[(text.IndexOf(trimmed, StringComparison.Ordinal) + trimmed.Length)..];

		foreach (var p in patterns)
		{
			var m = p.Regex.Match(text);
			if (!m.Success)
				continue;
			return Placeholder.Replace(p.Template, h =>
			{
				int n = int.Parse(h.Groups[1].Value);
				return Translate(m.Groups[p.Groups[n]].Value, exact, patterns, depth + 1);
			});
		}
		return text;
	}

	/// <summary>ui_language from cfg/SurfTimer/timer_settings.json - "uz" when it isn't set</summary>
	private static string ReadLanguage()
	{
		try
		{
			string path = Path.Combine(Server.GameDirectory, "csgo", "cfg", "SurfTimer", "timer_settings.json");
			if (File.Exists(path))
			{
				using var doc = JsonDocument.Parse(File.ReadAllText(path),
					new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
				if (doc.RootElement.TryGetProperty("ui_language", out var value) && value.ValueKind == JsonValueKind.String
					&& value.GetString() is { Length: > 0 } language)
					return Regex.IsMatch(language, "^[A-Za-z_-]+$") ? language.ToLowerInvariant() : "en";
			}
		}
		catch (Exception ex)
		{
			_logger?.LogWarning(ex, "[UiText] Reading ui_language failed - using 'uz'");
		}
		return "uz";
	}
}
