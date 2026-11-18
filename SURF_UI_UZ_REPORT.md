# SurfTimer: меню на узбекском и HUD GAMEPOINT — отчёт

Ветка `gamepoint/ui-uz`, база — `3308f9e` (форк на уровне z4lab `upstream/main`, 1.4.5.7).

## Как сделано

Перевод выполняется **в момент вывода**, а не в страницах. Все тексты Panorama-HUD (меню `!help`, `!options`, `!top`,
`!map`, `!profile`, `!saveloclist`, `!replay`, поля HUD, верхняя полоса, список зрителей, сообщения у прицела)
проходят через один метод `PlayerHud.SendText`. Поэтому перевод — одна строка там. Ещё три места:
чат-меню без Panorama (`MenuPresenter.ShowChat`), классический HTML-HUD и `!help` (детали команды режутся на строки
до вывода, поэтому переводятся до переноса).

`src/ST-UTILS/UiText.cs`:
- `lang/ui_<ui_language>.json`: ключ — английский текст, значение — перевод;
- ключ без `{n}` — точное совпадение; с `{0}`, `{1}` — шаблон (компилируется в Regex при загрузке);
  в переводе плейсхолдеры можно переставлять (`"#{0} of {1}": "{1} tadan #{0}"`);
- части шаблона тоже переводятся (`"HUD layout: Default"` → `"HUD maketi: Standart"`);
- приоритет — у самого конкретного шаблона (длиннее литерал, затем больше частей);
- кэш результатов (до 20 000 строк, затем очищается — меняющиеся таймеры не раздувают его);
- строки без букв (время, скорость, числа) не ищутся вообще;
- язык — `ui_language` в `cfg/SurfTimer/timer_settings.json` (по умолчанию `"uz"`, `"en"` — без перевода);
  словарь перечитывается **на каждой смене карты**, пересборка не нужна;
- нет файла / битый JSON → английский + строка в логе, без исключений.

## Изменённые файлы

Код upstream (то, что может конфликтовать при Sync fork) — **5 файлов, 12 строк**:

| Файл | Изменение |
|---|---|
| `src/SurfTimer.cs` | `UiText.Init(this)` в `Load` |
| `src/ST-Player/PlayerHUD.cs` | `SendText`: 1 строка; `ToHtml`: заголовок/суффикс |
| `src/ST-Player/Menu/MenuPresenter.cs` | заголовок, строки, «Back», вкладки чат-меню |
| `src/ST-Commands/HelpCommands.cs` | детали `!help` до переноса, текст/детали в чате |
| `serverfiles/.../cfg/SurfTimer/timer_settings.json` | `"ui_language": "uz"` |

Новые файлы (конфликтов не дают):

| Файл | Что это |
|---|---|
| `src/ST-UTILS/UiText.cs` | переводчик |
| `serverfiles/.../plugins/SurfTimer.Plugin/lang/ui_uz.json` | словарь, едет в релиз через workflow |
| `tools/UiStrings/` | экстрактор (Roslyn): все UI-строки → `tools/ui_strings_en.json`, список непереведённых |
| `tools/ui_ignore.json` | строки, которые осознанно не переводятся |
| `.github/workflows/build.yml` | шаг «Untranslated UI texts» — список в summary джоба, сборку не валит |
| `gamepoint/ui/` | брендовый Panorama-лейаут (см. `gamepoint/ui/README.md`) |

Вместо `tools/ui_missing.py` из ТЗ — `dotnet run --project tools/UiStrings`: Python на рабочей машине нет,
а dotnet есть и локально, и в CI. Скрипт печатает `MISSING` (новые английские строки) и `UNUSED`
(ключи, которых больше нет в коде — upstream поменял текст).

## Словарь

| | Строк |
|---|---|
| Найдено экстрактором (без `ST-Admin`, `ST-Data`, `ST-Api`) | 903 |
| Переведено в `ui_uz.json` | 630 (≈519 точных + 111 шаблонов) |
| Осознанно без перевода (`ui_ignore.json`) | 280 |
| Пропущено | 0 |

Без перевода (280): команды и алиасы (`saveloc`, `tp`, `!telenext`), внутренние имена/ключи/SQL/логи/пути,
звуковые события, цвета чата (`{gold}` и т.п.), тексты, которые идут только в чат, а не в меню (чат переведён
через `lang/*.json`), и то, что по-узбекски пишется так же (`PB`, `WR`, `CP`, `Tier`, `Sync`, `Replay`, `Top 10`).
Плюс два слишком общих шаблона (`{0} up`, `{0} down`): они могли бы зацепить ник игрока.

Термины — как в ТЗ: bosqich, bonus, CP, rekord, WR/PB, saqlangan joy, replay, tier, ochko, o'rin, uslub, mashq,
kuzatish. Длинные описания `!help` чуть длиннее английских, но они переносятся по 60 символов; названия вкладок —
1–2 слова (самое длинное — `SOZLAMALAR`). Меню в брендовом лейауте расширено с 540 до 600px.

**Админ-панель** (`!surfadmin`) не переводилась. Но так как перевод идёт на выводе, общие слова и в ней станут
узбекскими (`Bekor qilish`, `YONIQ/O'CHIQ`, `Yuklanmoqda…`). На поведение это не влияет, меняется только текст.

## Брендовый HUD

`gamepoint/ui/` — лейаут z4lab под именами `gp_surf_ui.xml` / `gp_surf_base.css` (оригинал, генерируется
`sync_upstream.ps1`) + `gp_surf_brand.css` (наша палитра: фон `#0B0D12`, акцент CT `#009BED` / T `#F59E0B`,
активная вкладка — градиент в `#F3009A`, статус/значения `#00E5ED`, скругления, тень). Кнопки страниц — `‹` `›`
вместо `< Prev` / `Next >` (не зависят от языка). Скомпилирован локально (`build.ps1`, resourcecompiler: 0 ошибок),
**ещё не загружен в Workshop** — шаги в `gamepoint/ui/README.md`. Пока `custom_hud_layout` указывает на
`z4lab_custom_ui.xml`, игроки видят перевод в старом оформлении.

## Проверка

Выполнено:
- `dotnet build src/SurfTimer.Plugin.csproj -c Release` — 0 ошибок (новых предупреждений нет);
- `UiText.Parse/Translate` на реальном словаре (34 строки): точные, шаблоны с перестановкой, вложенный перевод,
  ники/карты/время без изменений, `#5  Aimus  (you)` → `#5  Aimus  (siz)`, `row 1 · #2` → `1-qator · #2`;
- `tools/UiStrings`: missing 0;
- лейаут компилируется (`gp_surf_ui.vxml_c`, `gp_surf_base.vcss_c`, `gp_surf_brand.vcss_c`);
- Sync fork: `upstream/main` уже влит (0 новых коммитов) — конфликт возможен только если upstream поменяет
  те же 12 строк.

Нужно проверить на сервере:
- [ ] Actions зелёный, `css_plugins list` — SurfTimer загружен, в логе `[UiText] UI language 'uz': … texts`;
- [ ] `!help` (все вкладки + детали), `!options` (все страницы), `!top`, `!maptop`, `!map`, `!profile`,
      `!saveloclist`, `!replay`, нижний HUD, верхняя строка `PB · O'RIN · WR` — по-узбекски, ничего не обрезано;
- [ ] `"ui_language": "en"` + смена карты → английский; удалить `ui_uz.json` → английский, без ошибок;
- [ ] `!surfadmin` работает как раньше;
- [ ] после загрузки аддона и смены `custom_hud_layout` — новое оформление на CT и T.
