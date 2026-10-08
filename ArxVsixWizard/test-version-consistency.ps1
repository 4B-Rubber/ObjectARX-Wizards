#Requires -Version 5.1
<#
  Keeps the VSIX's three copies of the version in step.

  The wizard assembly identity is written out in three shapes and they have to agree, or Visual Studio
  refuses to load the wizard with "this template tried to load the assembly component ...". The
  renumbering to 0.1.1 missed the nine .vstemplate files here, so every wizard failed until it was
  caught by eye; this turns that into a failed test instead.

    * ArxVsixWizard.csproj                  <Version>            -> the assembly version, "<v>.0"
    * source.extension.vsixmanifest         Identity/@Version           = <v>
    * source.extension.vsixmanifest         Asset/@AssemblyName version = "<v>.0"
    * Packaging\**\*.vstemplate             <Assembly> identity         = "<v>.0"

  When the built ..\ObjectARXMultiYearWizards.vsix is present it is checked too, since that is the file
  that actually gets installed.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$csprojPath = Join-Path $repo 'ArxVsixWizard\ArxVsixWizard.csproj'
$manifestPath = Join-Path $repo 'ArxVsixWizard\source.extension.vsixmanifest'
$packaging = Join-Path $repo 'ArxVsixWizard\Packaging'
$vsixPath = Join-Path $repo 'ObjectARXMultiYearWizards.vsix'

$fail = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
  if ($ok) { Write-Host ('  PASS  ' + $name) }
  else { Write-Host ('  FAIL  ' + $name + '  ' + $detail); $script:fail++ }
}

$csproj = [System.IO.File]::ReadAllText($csprojPath)
$m = [regex]::Match($csproj, '<Version>([^<]+)</Version>')
if (-not $m.Success) { throw 'no <Version> in ArxVsixWizard.csproj' }
$v = $m.Groups[1].Value.Trim()          # e.g. 0.1.3
$assembly = $v + '.0'                   # e.g. 0.1.3.0
Write-Host ('project version: ' + $v + '  (assembly ' + $assembly + ')')

# ---- the manifest
$manifest = [System.IO.File]::ReadAllText($manifestPath)
$idv = [regex]::Match($manifest, 'Identity[^>]*\bVersion="([^"]+)"').Groups[1].Value
Check 'vsixmanifest Identity version matches the project' ($idv -ceq $v) ("manifest=" + $idv + " project=" + $v)

$asset = [regex]::Match($manifest, '<Asset[^>]*AssemblyName="ArxVsixWizard,\s*Version=([^,]+),').Groups[1].Value
Check 'vsixmanifest Asset assembly version matches' ($asset -ceq $assembly) ("manifest=" + $asset + " expected=" + $assembly)

# ---- every template the VSIX ships
$templates = @(Get-ChildItem $packaging -Recurse -Filter *.vstemplate)
Check 'the packaging tree has templates' ($templates.Count -gt 0) ($templates.Count.ToString())
Write-Host ('  info  ' + $templates.Count + ' .vstemplate files under Packaging')

$bad = @()
foreach ($t in $templates) {
  $text = [System.IO.File]::ReadAllText($t.FullName)
  $identity = [regex]::Match($text, '<Assembly>\s*(ArxVsixWizard[^<]*?)\s*</Assembly>').Groups[1].Value
  if ($identity -ne ('ArxVsixWizard, Version=' + $assembly + ', Culture=neutral, PublicKeyToken=null')) {
    $bad += ($t.FullName.Substring($repo.Length + 1) + ' -> [' + $identity + ']')
  }
}
Check 'every .vstemplate names the current assembly identity' ($bad.Count -eq 0) ($bad -join ' ; ')

# ---- the artifact that actually gets installed
if (Test-Path $vsixPath) {
  Add-Type -AssemblyName System.IO.Compression.FileSystem
  $zip = [System.IO.Compression.ZipFile]::OpenRead($vsixPath)
  try {
    $entries = @($zip.Entries | Where-Object { $_.FullName -like '*.vstemplate' })
    Check 'the built VSIX carries templates' ($entries.Count -gt 0) ($entries.Count.ToString())

    $stale = @()
    foreach ($e in $entries) {
      $sr = New-Object System.IO.StreamReader($e.Open())
      $text = $sr.ReadToEnd(); $sr.Close()
      if ($text -notmatch [regex]::Escape('ArxVsixWizard, Version=' + $assembly + ',')) {
        $stale += $e.FullName
      }
    }
    Check 'the built VSIX templates name the current assembly' ($stale.Count -eq 0) ($stale -join ' ; ')

    $manifestEntry = $zip.Entries | Where-Object { $_.FullName -eq 'extension.vsixmanifest' }
    if ($manifestEntry) {
      $sr = New-Object System.IO.StreamReader($manifestEntry.Open())
      $text = $sr.ReadToEnd(); $sr.Close()
      Check 'the built VSIX manifest is the current version' ($text -match [regex]::Escape('Version="' + $v + '"')) ''
    }
  }
  finally { $zip.Dispose() }
} else {
  Write-Host ('  skip  ' + $vsixPath + ' not built yet - artifact checks skipped')
}

if ($fail -gt 0) { Write-Host ('' + $fail + ' CHECK(S) FAILED'); exit 1 }
Write-Host 'ALL CHECKS PASSED'