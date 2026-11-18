// GAMEPOINT: collects the English texts the menus / panels / HUD show (string literals and interpolated strings,
// "$"#{rank} of {total}"" -> "#{0} of {1}") and compares them with lang/ui_uz.json - see src/ST-UTILS/UiText.cs.
// Heuristic: logs, console, chat (translated through lang/*.json), commands, setting keys and the admin panel are
// skipped; what's left over and isn't UI goes to tools/ui_ignore.json.
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

string root = FindRoot();
string src = Path.Combine(root, "src");
string langDir = Path.Combine(root, "serverfiles/game/csgo/addons/counterstrikesharp/plugins/SurfTimer.Plugin/lang");
string lang = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "uz";
bool check = args.Contains("--check");

// Player-facing parts only: the admin panel (ST-Admin) stays English, the database / API have no UI
string[] skipDirs = ["ST-Admin", "ST-Data", "ST-Api", "bin", "obj"];
string[] skipCalls =
[
	"LogDebug", "LogInformation", "LogWarning", "LogError", "LogCritical", "LogTrace", "Log", "WriteLine", "Write",
	"PrintToConsole", "PrintToServerConsole", "PrintToChat", "PrintToChatAll", "PrintToCenter", "PrintToCenterAll", "PrintToCenterHtml",
	"ReplyToCommand", "ExecuteClientCommand", "ExecuteClientCommandFromServer", "ExecuteCommand", "ExecuteServerCommand",
	"AddCommand", "RemoveCommand", "GetBool", "GetInt", "GetString", "GetFloat", "SetSetting", "GetSetting", "RemoveSetting",
	"TryGetValue", "ContainsKey", "Contains", "StartsWith", "EndsWith", "Equals", "Replace", "Split", "Trim", "TrimStart", "TrimEnd",
	"IndexOf", "GetProperty", "TryGetProperty", "FindAllEntitiesByDesignerName", "CreateEntityByName", "AcceptInput", "AddEntityIOEvent",
	"EmitSound", "PrecacheResource", "SetClass", "SendClass", "SendExclusive", "SetStateChanged",
	"GetType", "GetMethod", "GetField", "nameof", "Exception", "InvalidOperationException", "ArgumentException",
	"ParseExact", "TryParseExact", "ToString", "Format", "Join", "Regex", "IsMatch", "Match", "Matches",
	"FakeConVar", "ConVar", "Find", "LoadAsync", "QueryAsync", "ExecuteAsync", "SaveAsync", "WriteKey",
];

var found = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
foreach (var file in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
	string rel = Path.GetRelativePath(src, file).Replace('\\', '/');
	if (rel.Split('/').Any(part => skipDirs.Contains(part)))
		continue;

	var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file));
	foreach (var node in tree.GetRoot().DescendantNodes())
	{
		string? text = node switch
		{
			LiteralExpressionSyntax l when l.IsKind(SyntaxKind.StringLiteralExpression)
				=> l.Token.ValueText,
			InterpolatedStringExpressionSyntax i => Template(i),
			_ => null,
		};
		if (text == null || !LooksLikeUi(text) || Skipped(node))
			continue;

		int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
		if (!found.TryGetValue(text, out var where))
			found[text] = where = new SortedSet<string>(StringComparer.Ordinal);
		where.Add($"{rel}:{line}");
	}
}

var json = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
File.WriteAllText(Path.Combine(root, "tools/ui_strings_en.json"),
	JsonSerializer.Serialize(found.ToDictionary(p => p.Key, p => string.Join(", ", p.Value)), json) + "\n");

var translated = ReadKeys(Path.Combine(langDir, $"ui_{lang}.json"));
var ignored = ReadKeys(Path.Combine(root, "tools/ui_ignore.json"));
var missing = found.Where(p => !translated.Contains(p.Key) && !ignored.Contains(p.Key)).ToList();
var unused = translated.Where(k => !found.ContainsKey(k) && !k.Contains("{0}")).Order(StringComparer.Ordinal).ToList();

Console.WriteLine($"UI strings: {found.Count} | ui_{lang}.json: {translated.Count} | ignored: {ignored.Count} | missing: {missing.Count}");
foreach (var (text, where) in missing)
	Console.WriteLine($"  MISSING  {JsonSerializer.Serialize(text, json)}   ({where.First()})");
// Keys the source no longer has (upstream changed the text) - harmless, the new text just stays English
foreach (var key in unused)
	Console.WriteLine($"  UNUSED   {JsonSerializer.Serialize(key, json)}");
return check && missing.Count > 0 ? 1 : 0;

// "$"#{rank} of {total:0}"" -> "#{0} of {1}" - the same placeholders UiText matches at runtime
static string Template(InterpolatedStringExpressionSyntax s)
{
	var sb = new StringBuilder();
	int n = 0;
	foreach (var part in s.Contents)
	{
		if (part is InterpolatedStringTextSyntax t)
			sb.Append(t.TextToken.ValueText);
		else
			sb.Append('{').Append(n++).Append('}');
	}
	return sb.ToString();
}

// Text a player reads: has words, isn't an id / key / path / sound / colour / markup
static bool LooksLikeUi(string s)
{
	string letters = System.Text.RegularExpressions.Regex.Replace(s, @"\{\d+\}", "");
	if (!System.Text.RegularExpressions.Regex.IsMatch(letters, @"[A-Za-z]{2}"))
		return false;
	if (s.Contains('\n') || System.Text.RegularExpressions.Regex.IsMatch(s, @"^\s*(SELECT|INSERT|UPDATE|DELETE|REPLACE|CREATE|ALTER|WITH)\b"))
		return false; // SQL, multi-line text
	if (s.Contains('<') && s.Contains('>') && s.Contains("</"))
		return false; // HTML (classic HUD)
	if (!s.Contains(' ') && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-z0-9_.\-/:{}@#]+$") && !System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-z]+$"))
		return false; // key, id, command, path, sound event, setting name - plain words ("current", "off") stay
	if (s.StartsWith("css_") || s.StartsWith("z4_") || s.StartsWith("#") && !s.Contains(' ') || s.Contains("://"))
		return false;
	return true;
}

// Logs, console, chat, keys, comparisons, attributes - anything that isn't drawn in a menu / HUD
bool Skipped(SyntaxNode node)
{
	foreach (var a in node.Ancestors())
	{
		switch (a)
		{
			case AttributeSyntax:
			case ElementAccessExpressionSyntax e when e.ArgumentList.Arguments.Any(x => x.Span.Contains(node.Span)):
			case CaseSwitchLabelSyntax or SwitchExpressionArmSyntax { } when IsPatternPart(node, a):
			case ThrowExpressionSyntax or ThrowStatementSyntax:
				return true;
			case BinaryExpressionSyntax b when b.IsKind(SyntaxKind.EqualsExpression) || b.IsKind(SyntaxKind.NotEqualsExpression):
				return true;
			case InvocationExpressionSyntax inv:
				string name = inv.Expression switch
				{
					MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
					MemberBindingExpressionSyntax mb => mb.Name.Identifier.ValueText, // logger?.LogWarning(...)
					IdentifierNameSyntax id => id.Identifier.ValueText,
					GenericNameSyntax g => g.Identifier.ValueText,
					_ => "",
				};
				if (skipCalls.Contains(name) || name.StartsWith("Log"))
					return true;
				break;
			case ObjectCreationExpressionSyntax oc when oc.Type.ToString().EndsWith("Exception") || oc.Type.ToString().Contains("ConVar")
				|| oc.Type.ToString() is "Regex" or "ChatMenu":
				return true;
			case MemberDeclarationSyntax:
				return false;
		}
	}
	return false;
}

// "case "x":" / "x => ..." arms compare against the string - their results (after =>) can be UI
static bool IsPatternPart(SyntaxNode node, SyntaxNode label) => label switch
{
	CaseSwitchLabelSyntax c => c.Value.Span.Contains(node.Span),
	SwitchExpressionArmSyntax arm => arm.Pattern.Span.Contains(node.Span),
	_ => false,
};

static HashSet<string> ReadKeys(string path)
{
	if (!File.Exists(path))
		return [];
	using var doc = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
	return doc.RootElement.ValueKind == JsonValueKind.Array
		? doc.RootElement.EnumerateArray().Select(e => e.GetString()!).ToHashSet(StringComparer.Ordinal)
		: doc.RootElement.EnumerateObject().Where(p => !p.Name.StartsWith("//")).Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
}

static string FindRoot()
{
	for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
		if (File.Exists(Path.Combine(dir.FullName, "CS2SurfTimer.sln")))
			return dir.FullName;
	return Directory.GetCurrentDirectory();
}
