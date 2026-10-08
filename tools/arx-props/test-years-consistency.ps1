#Requires -Version 5.1
<#
  Guards the one thing the year list cannot avoid duplicating.

  The year list lives in four places and MSI cannot loop, so each of them has to enumerate the years
  literally:
    * tools\arx-props\arx-props-table.json  - the source of truth (also drives props generation)
    * ObjectARXWizardsInstaller\property.wxi - YEAR_* / ARXINV_* / DET_YEAR_* properties
    * ObjectARXWizardsInstaller\UI.wxi       - the check boxes and the four preset buttons
    * InnoSetupInstaller\...iss              - the CsvYears* constants
  A WiX v3 <?include?> cannot inject the per-year <Publish> elements either, because it requires the
  included file to have <Include> as its document element, so the enumeration is inherent to MSI -
  the check boxes only repaint from declarative publishes on the dialog itself.

  This therefore checks that all four agree with the JSON, rather than generating three of them. Add a
  year to the JSON and forget one site and this fails loudly, which is the failure mode that matters.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$jsonPath = Join-Path $repo 'tools\arx-props\arx-props-table.json'
$propsPath = Join-Path $repo 'ObjectARXWizardsInstaller\property.wxi'
$uiPath = Join-Path $repo 'ObjectARXWizardsInstaller\UI.wxi'
$issPath = Join-Path $repo 'InnoSetupInstaller\ObjectARXMultiVersionWizards.iss'

$fail = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
  if ($ok) { Write-Host ('  PASS  ' + $name) }
  else { Write-Host ('  FAIL  ' + $name + '  ' + $detail); $script:fail++ }
}
function Eq([string]$name, $actual, $expected) {
  $a = ($actual | ForEach-Object { [string]$_ }) -join ','
  $e = ($expected | ForEach-Object { [string]$_ }) -join ','
  Check $name ($a -ceq $e) ("actual=[$a] expected=[$e]")
}

# ---------------------------------------------------------------- the source of truth
$table = [System.IO.File]::ReadAllText($jsonPath, (New-Object System.Text.UTF8Encoding($false))) | ConvertFrom-Json
$years = @($table.years | ForEach-Object { [string]$_.year })
$defaults = @($table.years | Where-Object { $_.default } | ForEach-Object { [string]$_.year })
$compat = @($table.years | Where-Object { $_.compatible } | ForEach-Object { [string]$_.year })
Write-Host ('source of truth: ' + $years.Count + ' years, ' + $defaults.Count + ' default, ' + $compat.Count + ' compatible')

$propsText = [System.IO.File]::ReadAllText($propsPath)
$uiText = [System.IO.File]::ReadAllText($uiPath)
$issText = [System.IO.File]::ReadAllText($issPath)

function Ids([string]$text, [string]$prefix) {
  @([regex]::Matches($text, [regex]::Escape($prefix) + '(\d{4})"') |
    ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
}
function ButtonBlock([string]$text, [string]$controlId) {
  $i = $text.IndexOf('<Control Id="' + $controlId + '"')
  if ($i -lt 0) { throw "control not found: $controlId" }
  $j = $text.IndexOf('</Control>', $i)
  if ($j -lt 0) { throw "control not closed: $controlId" }
  return $text.Substring($i, $j - $i)
}
function Csv([string]$text, [string]$name) {
  $m = [regex]::Match($text, [regex]::Escape($name) + "\s*=\s*'([^']*)'")
  if (-not $m.Success) { throw "constant not found: $name" }
  return @($m.Groups[1].Value.Split(',') | Where-Object { $_ -ne '' })
}

# ---------------------------------------------------------------- property.wxi
Eq 'property.wxi declares one YEAR_* per year'      (Ids $propsText 'YEAR_')      $years
Eq 'property.wxi declares one ARXINV_* per year'    (Ids $propsText 'ARXINV_')    $years
Eq 'property.wxi declares one DET_YEAR_* per year'  (Ids $propsText 'DET_YEAR_')  $years

$detNames = @([regex]::Matches($propsText, 'Name="Autodesk\.arx-(\d{4})\.props"') |
              ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
Eq 'DET_* searches look for the right per-year sheet' $detNames $years

# ---------------------------------------------------------------- UI.wxi
Eq 'UI.wxi shows one check box per year'            (Ids $uiText 'YEARBOX_')      $years

function Publishes([string]$buttonId) {
  $b = ButtonBlock $uiText $buttonId
  @([regex]::Matches($b, 'Property="(YEAR_\d{4})"\s+Value="([^"]*)"') |
    ForEach-Object { [pscustomobject]@{ Year = $_.Groups[1].Value; Value = $_.Groups[2].Value } })
}
function ExpectSet([string]$buttonId, [string[]]$ticked) {
  $p = Publishes $buttonId
  $expected = @($years | ForEach-Object {
    [pscustomobject]@{ Year = 'YEAR_' + $_; Value = $(if ($ticked -contains $_) { '1' } else { '{}' }) }
  })
  $a = ($p | ForEach-Object { $_.Year + '=' + $_.Value }) -join ','
  $e = ($expected | ForEach-Object { $_.Year + '=' + $_.Value }) -join ','
  Check ("UI.wxi " + $buttonId + " publishes the right set") ($a -ceq $e) ("actual=[$a] expected=[$e]")
}

ExpectSet 'AllYearsButton'    $years
ExpectSet 'NoYearsButton'     @()
ExpectSet 'CompatYearsButton' $compat

# Invert snapshots every year into ARXINV_* and then flips against the snapshot - a pair per year,
# because MSI cannot toggle a property on its own.
$inv = ButtonBlock $uiText 'InvertYearsButton'
$snap = @([regex]::Matches($inv, 'Property="(ARXINV_\d{4})"\s+Value="\[(YEAR_\d{4})\]"') |
          ForEach-Object { if ($_.Groups[1].Value.Replace('ARXINV_', 'YEAR_') -ne $_.Groups[2].Value) { 'mismatch' } else { $_.Groups[2].Value } })
Eq 'UI.wxi Invert snapshots every year into its ARXINV_*' ($snap | Sort-Object -Unique) (($years | ForEach-Object { 'YEAR_' + $_ }))

$flips = @([regex]::Matches($inv, 'Property="(YEAR_\d{4})"\s+Value="(1|\{\})">.*?ARXINV_\d{4}(<>|=)"1"') |
           ForEach-Object { $_.Groups[1].Value + '=' + $_.Groups[2].Value + $_.Groups[3].Value })
$expectedFlips = @()
foreach ($y in $years) {
  $expectedFlips += ('YEAR_' + $y + '=1<>')   # not ticked in the snapshot -> tick it
  $expectedFlips += ('YEAR_' + $y + '={}=')   # was ticked -> clear it
}
Eq 'UI.wxi Invert flips each year exactly twice' ($flips | Sort-Object) ($expectedFlips | Sort-Object)

# ---------------------------------------------------------------- the Inno script
$left = Csv $issText 'CsvYearsLeft'
$right = Csv $issText 'CsvYearsRight'
Eq 'iss CsvYearsLeft+CsvYearsRight is the year list in order' @($left + $right) $years
Eq 'iss CsvDefaultYears matches the JSON'                     (Csv $issText 'CsvDefaultYears') $defaults
Eq 'iss CsvCompatibleYears matches the JSON'                  (Csv $issText 'CsvCompatibleYears') $compat
Check 'iss splits the two columns evenly' ($left.Count -eq $right.Count) ($left.Count.ToString() + ' vs ' + $right.Count.ToString())

# ---------------------------------------------------------------- custom action
# The CA reads the JSON's "default" flags now, so it must not carry its own copy of the list.
$caPath = Join-Path $repo 'ArxWizCustomAction\CustomAction.cs'
$ca = [System.IO.File]::ReadAllText($caPath)
Check 'the custom action no longer hardcodes the default years' `
      (-not ($ca -match 'DefaultYears\s*=\s*\{\s*"\d{4}"')) 'found a literal DefaultYears array'

if ($fail -gt 0) { Write-Host ('' + $fail + ' CHECK(S) FAILED'); exit 1 }
Write-Host 'ALL CHECKS PASSED'