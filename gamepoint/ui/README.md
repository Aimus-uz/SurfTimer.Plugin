# GAMEPOINT HUD для SurfTimer (Workshop-аддон)

Копия лейаута [z4lab-custom-ui](https://github.com/z4lab/z4lab-custom-ui) (HUD + всплывающее меню SurfTimer)
под нашими именами и с палитрой GAMEPOINT. Тексты в меню присылает плагин (`UiText`, `lang/ui_uz.json`),
в лейауте только оформление.

| Файл | Что это | Править? |
|---|---|---|
| `panorama/layout/custom_game/gp_surf_ui.xml` | лейаут z4lab, подключает две CSS | нет — генерируется `sync_upstream.ps1` |
| `panorama/styles/custom_game/gp_surf_base.css` | оригинальная CSS z4lab | нет — генерируется `sync_upstream.ps1` |
| `panorama/styles/custom_game/gp_surf_brand.css` | **наши цвета** поверх оригинала | да |

Имена `gp_surf_*` не совпадают с z4lab, поэтому аддон z4lab (у него ещё paint-частицы для SurfTimer.Paint)
может оставаться подключённым — конфликтов путей нет.

## Сборка и публикация

1. `.\gamepoint\ui\build.ps1` — копирует `panorama/` в `content/csgo_addons/gp_surf_ui` и компилирует
   (CS2 по умолчанию на `D:\SteamLibrary`, другой путь: `-Cs2Dir`). Можно собрать в общий пак:
   `-Addon gp-pack-16.11.2026`.
2. Workshop Tools → аддон `gp_surf_ui` (или пак) → Upload, запомнить **ID**.
3. На сервере: ID в `mm_client_extra_addons` (MultiAddonManager).
4. `cfg/SurfTimer/timer_settings.json`:
   ```json
   "custom_hud_layout": "panorama/layout/custom_game/gp_surf_ui.xml",
   ```
   Именно `.xml`, не `.vxml`. Применяется после смены карты / рестарта плагина.

Откат — вернуть `panorama/layout/custom_game/z4lab_custom_ui.xml`.

## Обновление с upstream

```powershell
.\gamepoint\ui\sync_upstream.ps1            # z4lab/z4lab-custom-ui, ветка main
.\gamepoint\ui\build.ps1                     # затем снова Upload
```

Если в новой версии z4lab появились новые классы/цвета для команды T (`team-t`), продублировать их
в `gp_surf_brand.css`, иначе на стороне T вернётся жёлтый z4lab.

## Палитра

| Роль | Цвет |
|---|---|
| Фон | `#0B0D12` / `#12151C` / `#1A1F2A` |
| Текст / приглушённый | `#F5F7FA` / `#8D97A8` |
| Акцент CT (по умолчанию) | `#009BED` |
| Акцент T | `#F59E0B` |
| Бренд (активная вкладка — градиент CT→бренд, закрыть) | `#F3009A` |
| Статус, значения справа, переключатель ON | `#00E5ED` |
| Опасные действия | `#FF4D6A` |

Цвета данных (таймер, градиент скорости, цвета из `!options`) — оригинальные Nord: у них есть смысл
и названия в настройках.
