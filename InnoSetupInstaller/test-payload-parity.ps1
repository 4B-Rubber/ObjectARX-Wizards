#Requires -Version 5.1
<#
  Payload parity between the two installer lines.

  Reads ObjectARXWizardsInstaller\directory.wxi, rebuilds the file set the MSI drops into
  TARGETDIR (the install folder), and compares it with what InnoSetupInstaller\
  ObjectARXMultiYearWizards.iss installs into {app}. The two must be identical: the Inno line is
  meant to install the same wizards, neither more nor less.

  Run it after touching either payload - a file added under ArxAppWiz\ but not to directory.wxi
  (or the other way round) shows up here instead of on a user's machine.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path

$doc = New-Object System.Xml.XmlDocument
$doc.Load((Join-Path $repo 'ObjectARXWizardsInstaller\directory.wxi'))
$ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
$ns.AddNamespace('w', 'http://schemas.microsoft.com/wix/2006/wi')

# ---- MSI side: walk the install tree. A File without @Source uses the directory tree path,
#      relative to the FileSource ("..\"); TARGETDIR itself is named SourceDir, which is stripped.
$msi = New-Object System.Collections.Generic.List[string]
function Get-One([string]$p) { $p -replace '^\.\.\\', '' }

function Walk($node, [string]$prefix) {
  foreach ($dir in $node.SelectNodes('w:Directory', $ns)) {
    $name = $dir.GetAttribute('Name')
    if ([string]::IsNullOrEmpty($name)) { continue }
    $path = if ($prefix) { $prefix + '\' + $name } else { $name }
    foreach ($f in $dir.SelectNodes('w:Component/w:File', $ns)) {
      $src = $f.GetAttribute('Source')
      if ($src) { $script:msi.Add((Get-One $src)) }
      else { $script:msi.Add($path + '\' + $f.GetAttribute('Name')) }
    }
    Walk $dir $path
  }
}

$root = $doc.DocumentElement
foreach ($f in $root.SelectNodes('w:Directory[@Id="TARGETDIR"]/w:Component/w:File', $ns)) {
  $src = $f.GetAttribute('Source')
  if ($src) { $msi.Add((Get-One $src)) } else { $msi.Add($f.GetAttribute('Name')) }
}
Walk $root ''
$msiSet = @($msi | ForEach-Object { ($_ -replace '^SourceDir\\', '').ToLowerInvariant() } | Sort-Object -Unique)

# ---- Inno side: the recursion of the [Files] entries that land in {app}
$rootFiles = 'rxsdk_common.props', 'crx.props'
$wizDirs = 'ArxWizCommon', 'ArxAppWiz', 'ArxAppWiz182', 'ArxAtlWizComWrapper', 'ArxAtlWizDynProp',
           'ArxWizCustomObject', 'ArxWizJig', 'ArxWizMFCSupport', 'ArxWizNETWrapper', 'ArxWizReactors'
$inno = New-Object System.Collections.Generic.List[string]
foreach ($n in $rootFiles) { $inno.Add('_installs\' + $n) }
foreach ($d in $wizDirs) {
  Get-ChildItem (Join-Path $repo $d) -Recurse -File | ForEach-Object {
    $inno.Add($_.FullName.Substring($repo.Length + 1).ToLowerInvariant())
  }
}
$innoSet = @($inno | Sort-Object -Unique)

Write-Host ('MSI files under TARGETDIR : ' + $msiSet.Count)
Write-Host ('Inno files under {app}     : ' + $innoSet.Count)

$onlyMsi = @(Compare-Object $msiSet $innoSet | Where-Object { $_.SideIndicator -eq '<=' } | ForEach-Object { $_.InputObject })
$onlyInno = @(Compare-Object $msiSet $innoSet | Where-Object { $_.SideIndicator -eq '=>' } | ForEach-Object { $_.InputObject })

if ($onlyMsi.Count -gt 0) { Write-Host '--- in the MSI only ---'; $onlyMsi | ForEach-Object { Write-Host ('  ' + $_) } }
if ($onlyInno.Count -gt 0) { Write-Host '--- in the Inno .iss only ---'; $onlyInno | ForEach-Object { Write-Host ('  ' + $_) } }

if ($onlyMsi.Count -eq 0 -and $onlyInno.Count -eq 0) {
  Write-Host ''
  Write-Host ('PAYLOAD MATCHES (' + $msiSet.Count + ' files)')
  exit 0
}
Write-Host ''
Write-Host ('PAYLOAD MISMATCH: ' + $onlyMsi.Count + ' only in MSI, ' + $onlyInno.Count + ' only in Inno')
exit 1
