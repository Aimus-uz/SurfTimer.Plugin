<#
GAMEPOINT: copies our SurfTimer HUD layout (panorama/) into a CS2 addon and compiles it (layout + both stylesheets).
Upload the addon afterwards with the Workshop Tools - see README.md.

    .\gamepoint\ui\build.ps1
    .\gamepoint\ui\build.ps1 -Addon gp-pack-16.11.2026        # into an existing addon, e.g. the GAMEPOINT pack
    .\gamepoint\ui\build.ps1 -Cs2Dir "C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive"
#>
param(
	[string]$Cs2Dir = "D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive",
	[string]$Addon = "gp_surf_ui"
)
$ErrorActionPreference = "Stop"

$compiler = Join-Path $Cs2Dir "game\bin\win64\resourcecompiler.exe"
$content = Join-Path $Cs2Dir "content\csgo_addons\$Addon"
$game = Join-Path $Cs2Dir "game\csgo_addons\$Addon"

if (-not (Test-Path $compiler)) {
	Write-Error "resourcecompiler.exe not found in $Cs2Dir\game\bin\win64 - set -Cs2Dir, install the CS2 Workshop Tools"
}
if (-not (Test-Path $content)) {
	# A new addon: the same two folders the Workshop Tools create
	New-Item -ItemType Directory -Force $content, $game | Out-Null
	Write-Host "Created the addon '$Addon' ($content)" -ForegroundColor Yellow
}

Copy-Item (Join-Path $PSScriptRoot "panorama") $content -Recurse -Force
Write-Host "Copied panorama\ to $content"

# The layout pulls in both stylesheets
$layout = Join-Path $content "panorama\layout\custom_game\gp_surf_ui.xml"
Write-Host "- panorama\layout\custom_game\gp_surf_ui.xml"
$output = & $compiler -nop4 -i $layout 2>&1
$output | Where-Object { $_ -match "error|warning|failed" -and $_ -notmatch "Leaked KeyValues" } | ForEach-Object { Write-Host "  $_" -ForegroundColor Yellow }
if ($LASTEXITCODE -ne 0) {
	$output | Where-Object { $_ -notmatch "^Creating render device" } | ForEach-Object { Write-Host $_ -ForegroundColor Red }
	Write-Host "Build failed - see the errors above" -ForegroundColor Red
	exit 1
}

$built = @(
	"panorama\layout\custom_game\gp_surf_ui.vxml_c",
	"panorama\styles\custom_game\gp_surf_base.vcss_c",
	"panorama\styles\custom_game\gp_surf_brand.vcss_c"
) | ForEach-Object { Join-Path $game $_ }
$missing = $built | Where-Object { -not (Test-Path $_) }
if ($missing) {
	Write-Host "Not compiled: $($missing -join ', ')" -ForegroundColor Red
	exit 1
}
Write-Host "Done: $game - upload with the Workshop Tools, then set custom_hud_layout (README.md)" -ForegroundColor Green
