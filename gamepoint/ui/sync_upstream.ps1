<#
GAMEPOINT: refreshes our copy of the z4lab-custom-ui layout (the SurfTimer HUD + popup menu) under our own names, so our
Workshop addon never clashes with z4lab's (both may be mounted):
  z4lab_custom_ui.xml -> panorama/layout/custom_game/gp_surf_ui.xml   (includes gp_surf_base.css, then gp_surf_brand.css)
  z4lab_custom_ui.css -> panorama/styles/custom_game/gp_surf_base.css (upstream, unchanged)
Our colours live only in gp_surf_brand.css - never edit gp_surf_base.css / gp_surf_ui.xml by hand.

    .\gamepoint\ui\sync_upstream.ps1                        # from GitHub (z4lab/z4lab-custom-ui, main)
    .\gamepoint\ui\sync_upstream.ps1 -Ref <commit>
    .\gamepoint\ui\sync_upstream.ps1 -Source C:\path\to\z4lab-custom-ui   # from a local clone
Then compile and upload with build.ps1 (see README.md).
#>
param(
	[string]$Ref = "main",
	[string]$Source = ""
)
$ErrorActionPreference = "Stop"

$layoutDir = Join-Path $PSScriptRoot "panorama\layout\custom_game"
$stylesDir = Join-Path $PSScriptRoot "panorama\styles\custom_game"
New-Item -ItemType Directory -Force $layoutDir, $stylesDir | Out-Null

function Get-Upstream([string]$path) {
	if ($Source) {
		return [System.IO.File]::ReadAllText((Join-Path $Source $path))
	}
	[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
	$url = "https://raw.githubusercontent.com/z4lab/z4lab-custom-ui/$Ref/$path"
	return (Invoke-WebRequest -UseBasicParsing $url).Content
}

$xml = Get-Upstream "panorama/layout/custom_game/z4lab_custom_ui.xml"
$css = Get-Upstream "panorama/styles/custom_game/z4lab_custom_ui.css"

$include = '<include src="file://{resources}/styles/custom_game/z4lab_custom_ui.css" />'
if (-not $xml.Contains($include)) {
	throw "The upstream layout no longer includes z4lab_custom_ui.css the way this script expects - update sync_upstream.ps1"
}
$indent = "`t`t"
$xml = $xml.Replace($include,
	'<include src="file://{resources}/styles/custom_game/gp_surf_base.css" />' + "`n" + $indent +
	'<include src="file://{resources}/styles/custom_game/gp_surf_brand.css" />')
# The only fixed texts of the layout - arrows read the same in every language
$xml = $xml.Replace('text="&lt; Prev"', 'text="&#8249;"').Replace('text="Next &gt;"', 'text="&#8250;"')

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $layoutDir "gp_surf_ui.xml"), $xml.Replace("`r`n", "`n"), $utf8)
[System.IO.File]::WriteAllText((Join-Path $stylesDir "gp_surf_base.css"), $css.Replace("`r`n", "`n"), $utf8)
Write-Host "Updated gp_surf_ui.xml and gp_surf_base.css from z4lab-custom-ui ($(if ($Source) { $Source } else { $Ref }))" -ForegroundColor Green
