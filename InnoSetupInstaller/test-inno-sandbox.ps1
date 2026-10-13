#Requires -Version 5.1
<#
  Sandboxed end-to-end test for InnoSetupInstaller\ObjectARXMultiVersionWizards.iss.

  The shipped installer needs administrator rights (PrivilegesRequired=admin) and writes to
  HKLM and Program Files, neither of which an unattended run can do. This harness therefore
  derives a test copy of the very same script with only the privilege/registry bits swapped:
      PrivilegesRequired=admin            -> lowest
      HKLM64                              -> HKCU
      ArchitecturesInstallIn64BitMode     -> (dropped; it needs admin)
  Everything else - the [Files] set, the patches and the props generation - is byte-identical to
  the shipped script.

  Every machine path is redirected into a scratch directory through the script's own silent
  switches, then the install is verified and uninstalled again. One part is not switchable: the
  uninstaller's RemoveVsRegistrations() / RefreshVsCache() / RemoveVsix() re-enumerate the machine's
  own Visual Studio instances (FindVsRootByMajor reads %ProgramData%), so the derived copy disables
  all three - a sandbox run must never delete files from, refresh or uninstall anything in the real
  Visual Studio.

  This branch installs no classic wizard payload at all: the VSIX is the whole Visual Studio
  integration, and [InstallDelete] takes the classic line's files out of the VS folders and {app}
  on the way in. The rounds below check that, plus the props generation and the VSIX step.
#>
[CmdletBinding()]
param(
  [string]$Iscc = 'C:\Program Files\Inno Setup 7\ISCC.exe'
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$sourceIss = Join-Path $repo 'InnoSetupInstaller\ObjectARXMultiVersionWizards.iss'
$work = Join-Path $env:TEMP ('arx-inno-sandbox-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $work | Out-Null

$fail = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
  if ($ok) { Write-Host ('  PASS  ' + $name) }
  else { Write-Host ('  FAIL  ' + $name + '  ' + $detail); $script:fail++ }
}

# ---------------------------------------------------------------- derive the test copy
$text = (Get-Content $sourceIss -Raw) -replace "`r`n", "`n"
$test = $text
# the derived script lives in %TEMP%, so its relative payload root has to become absolute
$test = $test.Replace('#define RepoRoot ".."', '#define RepoRoot "' + $repo + '"')
# the shipped script expects the VSIX beside the sources (tools\build-and-pack.ps1 puts it there
# when it compiles the real package); the sandbox has no build, so point the payload at a stand-in
$vsix = Join-Path $work 'ObjectARXMultiVersionWizards.vsix'
Set-Content -Path $vsix -Value 'sandbox stand-in' -Encoding ASCII
$test = $test.Replace('#define VsixSource RepoRoot + "\ObjectARXMultiVersionWizards.vsix"', '#define VsixSource "' + $vsix + '"')
# The props generator is built by tools\build-and-pack.ps1 into the *build root*'s mirror (the source
# tree stays free of build outputs), and this test compiles the .iss straight from the tree - so take
# the tree copy when a local build left one, else the mirror (set ARX_BUILD_ROOT). Unlike the VSIX
# stand-in this has to be a real exe: the rounds below assert the sheets it generates.
$genExe = Join-Path $repo 'tools\arx-genprops\bin\Release\arx-genprops.exe'
if (-not (Test-Path $genExe) -and $env:ARX_BUILD_ROOT) {
  $genExe = Join-Path $env:ARX_BUILD_ROOT 'src\tools\arx-genprops\bin\Release\arx-genprops.exe'
}
if (-not (Test-Path $genExe)) {
  throw ('arx-genprops.exe not found - build it via tools\build-and-pack.ps1 or set ARX_BUILD_ROOT: ' + $genExe)
}
$test = $test.Replace('#define GenExeSource RepoRoot + "\tools\arx-genprops\bin\Release\arx-genprops.exe"', '#define GenExeSource "' + $genExe + '"')
if ($test.Contains('RepoRoot + "\tools\arx-genprops\bin\Release\arx-genprops.exe"')) {
  throw 'the GenExeSource define changed shape - the sandbox would fall back to the tree copy'
}
$test = $test.Replace('PrivilegesRequired=admin', 'PrivilegesRequired=lowest')
$test = $test.Replace('HKLM64', 'HKCU')
$test = $test.Replace("ArchitecturesInstallIn64BitMode=x64compatible`n", '')
# The uninstaller's VS steps are not switch-redirectable: RemoveVsRegistrations() / RefreshVsCache()
# / RemoveVsix() all re-enumerate the machine's own Visual Studio instances (FindVsRootByMajor reads
# %ProgramData%\Microsoft\VisualStudio\Packages\_Instances). Blank them out in the copy - a sandbox
# run must never delete files from, refresh or uninstall anything in the real Visual Studio. Inno's
# own uninstall still removes what the install copied into the redirected root.
$test = $test.Replace("  RemoveVsRegistrations();", "  // sandbox: the real VS roots are off limits")
$test = $test.Replace("  RefreshVsCache();", "  // sandbox: never run devenv on the real VS")
$test = $test.Replace("  RemoveVsix();", "  // sandbox: never uninstall the real VS extension")
foreach ($call in @("  RemoveVsRegistrations();", "  RefreshVsCache();", "  RemoveVsix();")) {
  if ($test.Contains($call)) { throw ('sandbox: could not disable ' + $call.Trim()) }
}
$test = $test.Replace('OutputBaseFilename=ObjectARXMultiVersionWizards-dev', 'OutputBaseFilename=InnoSandboxTest')
if ($test -ceq $text) { throw 'no substitution applied - the .iss changed shape' }

$testIss = Join-Path $work 'sandbox.iss'
Set-Content -Path $testIss -Value $test -Encoding UTF8 -NoNewline

$p = Start-Process -FilePath $Iscc -ArgumentList ('"' + $testIss + '"') -RedirectStandardOutput (Join-Path $work 'iscc.out') -RedirectStandardError (Join-Path $work 'iscc.err') -NoNewWindow -Wait -PassThru
if ($p.ExitCode -ne 0) {
  Get-Content (Join-Path $work 'iscc.err') | Write-Host
  throw 'test copy did not compile'
}
$setup = Join-Path $work 'Output\InnoSandboxTest.exe'
Check 'sandbox installer compiled' (Test-Path $setup) $setup

# ---------------------------------------------------------------- scratch layout
$appDir = Join-Path $work 'app'
$propsDir = Join-Path $work 'props'
$sdkDir = Join-Path $work 'sdkroot\Autodesk'
$arxSdk = Join-Path $work 'ObjectARX'
$vsRoot = Join-Path $work 'vs2022'
# The classic line's files, as an older install would have left them: [InstallDelete] has to take
# them away again the moment this line is installed. This branch ships no wizard payload of its own.
# {$appDir} is normally NOT created here (Inno only removes a directory it created itself), but this
# pre-seeded leftover needs a home; the round below drops the folder by hand afterwards.
$classicProjectWizard = Join-Path $vsRoot 'Common7\IDE\VC\vcprojects\Autodesk\ArxAppWiz.vsz'
$classicItemWizards = Join-Path $vsRoot 'Common7\IDE\VC\vcprojectitems\ObjectARX'
$classicAppTree = Join-Path $appDir 'ArxWizCommon'
foreach ($d in @($propsDir,
                 (Split-Path $classicProjectWizard),
                 $classicItemWizards,
                 $classicAppTree)) {
  New-Item -ItemType Directory -Force $d | Out-Null
}
Set-Content -Path $classicProjectWizard -Value 'classic leftover' -Encoding ASCII
Set-Content -Path (Join-Path $classicItemWizards 'ArxWizReactors.vsz') -Value 'classic leftover' -Encoding ASCII
Set-Content -Path (Join-Path $classicAppTree 'arxCommon.js') -Value 'classic leftover' -Encoding ASCII

$years = '2020,2024,2026,2027'
$installArgs = @(
  '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
  ('/LOG="' + (Join-Path $work 'install.log') + '"'),
  ('/DIR="' + $appDir + '"'),
  ('/PROPSDIR="' + $propsDir + '"'),
  ('/ARXROOT="' + $sdkDir + '"'),

  ('/VSROOT="' + $vsRoot + '"'),
  ('/YEARS=' + $years),
  '/RDS=ABC',
  '/SKIPVSIX=1',
  '/SKIPVSCHECK=1'
) -join ' '

$regKey = 'HKCU:\SOFTWARE\Autodesk\ObjectARX Wizards'
function Wait-Gone([string]$Path) {
  for ($i = 0; $i -lt 60 -and (Test-Path $Path); $i++) { Start-Sleep -Milliseconds 500 }
}

# ---------------------------------------------------------------- fresh install
# With no /PROPSDIR and an empty registry the property sheet folder must follow the install folder.
# This round also proves the generated sheets are cleared from it on uninstall and that
# [InstallDelete] takes the classic line's leftovers away on the way in.
Remove-Item $regKey -Recurse -Force -ErrorAction SilentlyContinue
$argsFresh = ($installArgs -replace ' /PROPSDIR="[^"]*"', '') -replace ' /RDS=ABC', ''
$p = Start-Process -FilePath $setup -ArgumentList $argsFresh -NoNewWindow -Wait -PassThru
Check 'fresh install exits 0 (property sheet folder follows the install folder)' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)
Check 'generated props land in the install folder' (
  (Test-Path (Join-Path $appDir 'Autodesk.arx-2020.props')) -and
  (Test-Path (Join-Path $appDir 'ObjectARX.User.props')))
Check 'classic project wizard removed from the VS folder' (-not (Test-Path $classicProjectWizard))
Check 'classic item wizard folder removed from the VS tree' (-not (Test-Path $classicItemWizards))
Check 'classic wizard tree removed from the install folder' (-not (Test-Path $classicAppTree))
$k0 = Get-ItemProperty $regKey -ErrorAction SilentlyContinue
Check 'registry PropsDir follows the install folder' ($k0 -and ($k0.PropsDir -eq $appDir)) ($(if ($k0) { $k0.PropsDir } else { 'missing' }))
Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -NoNewWindow -Wait | Out-Null
Wait-Gone (Join-Path $appDir 'Autodesk.arx-2020.props')
Check 'uninstall clears the generated props from the install folder' (-not (Test-Path (Join-Path $appDir 'Autodesk.arx-2020.props')))
# Back to a clean slate for the rounds below: the registry, and the install folder by hand. The
# folder exists because this round pre-seeded a leftover in it, and Inno does not remove a
# directory it did not create itself. The props folder inside stays by design (uninsneveruninstall).
Remove-Item $regKey -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue

# ---------------------------------------------------------------- install
$p = Start-Process -FilePath $setup -ArgumentList $installArgs -NoNewWindow -Wait -PassThru
Check 'installer exited 0' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)

# ---- no wizard payload: the VSIX is the Visual Studio integration on this line ----
$leftovers = @(
  (Join-Path $appDir 'ArxWizCommon'), (Join-Path $appDir 'ArxAppWiz'), (Join-Path $appDir 'ArxAppWiz182'),
  (Join-Path $appDir 'ArxAtlWizComWrapper'), (Join-Path $appDir 'ArxAtlWizDynProp'),
  (Join-Path $appDir 'ArxWizCustomObject'), (Join-Path $appDir 'ArxWizJig'),
  (Join-Path $appDir 'ArxWizMFCSupport'), (Join-Path $appDir 'ArxWizNETWrapper'),
  (Join-Path $appDir 'ArxWizReactors'),
  (Join-Path $vsRoot 'Common7\IDE\VC\vcprojectitems\ObjectARX')
)
$kept = @($leftovers | Where-Object { Test-Path $_ })
Check 'no classic wizard tree landed in the install folder or the VS tree' ($kept.Count -eq 0) ($kept -join ', ')
Check 'no classic project wizard landed in the VS folder' (-not (Test-Path $classicProjectWizard))

# No shipped Autodesk.arx-<year>.props anywhere: arx-genprops generates the ticked years at install
# time, so a hard-wired single-year sheet would only go stale (or collide with the generated one,
# the property sheet folder being the install folder by default).
Check 'no property sheet template ships as payload' `
      (@(Get-ChildItem (Join-Path $repo '_Installs') -Filter 'Autodesk.arx-*.props' -ErrorAction SilentlyContinue).Count -eq 0)

# ---- the SDK folder is never touched ----
Check 'the installer leaves the SDK folder alone' (-not (Test-Path $arxSdk))

# ---- no property sheet ships: the props folder holds the generated ones and nothing else ----
$foreign = @(Get-ChildItem $propsDir -File | Where-Object { $_.Name -notlike 'Autodesk.arx-*' -and $_.Name -ne 'ObjectARX.User.props' })
Check 'no foreign props installed' ($foreign.Count -eq 0) (($foreign | ForEach-Object { $_.Name }) -join ', ')

# ---- generated props ----
$generated = @(Get-ChildItem $propsDir -Filter 'Autodesk.arx-*.props' | ForEach-Object { $_.Name } | Sort-Object)
$expectGenerated = @(
  'Autodesk.arx-2020-net.props', 'Autodesk.arx-2020.props',
  'Autodesk.arx-2024-net.props', 'Autodesk.arx-2024.props',
  'Autodesk.arx-2026-net.props', 'Autodesk.arx-2026.props',
  'Autodesk.arx-2027-Compat.props', 'Autodesk.arx-2027-net.props', 'Autodesk.arx-2027.props'
) | Sort-Object
Check 'generated props match the ticked years' (($generated -join ',') -ceq ($expectGenerated -join ',')) ($generated -join ', ')
Check 'ObjectARX.User.props generated' (Test-Path (Join-Path $propsDir 'ObjectARX.User.props'))
$user = Get-Content (Join-Path $propsDir 'ObjectARX.User.props') -Raw
Check 'User.props carries the chosen SDK root' ($user -match [regex]::Escape('<ArxSdkRoot>' + $sdkDir))
Check 'generated props carry the chosen SDK root' ((Get-Content (Join-Path $propsDir 'Autodesk.arx-2020.props') -Raw) -match [regex]::Escape('<ArxSdkRoot Condition'))

# The roots are used as *prefixes*: a generated sheet concatenates "$(ArxSdkRoot)ObjectARX <year>\"
# with plain string concatenation, so a value without its trailing backslash produces
# "…\sdkroot\AutodeskObjectARX 2020\". The directory wizard trims that backslash, hence the
# normalisation this pins down. Match the single line, not the whole file.
$sdkSheet = Get-Content (Join-Path $propsDir 'Autodesk.arx-2020.props') -Raw
$rootLine = @(($sdkSheet -split "`n") | Where-Object { $_ -match '<ArxSdkRoot Condition' })[0]
$rootVal = [regex]::Match($rootLine, '>(.*?)</ArxSdkRoot>').Groups[1].Value
Write-Host ('        raw ArxSdkRoot line: ' + $rootLine)
Check 'generated root value keeps its trailing backslash' ($rootVal -eq ($sdkDir + '\')) $rootVal
Check 'generated sheet does not glue root to the year' ($sdkSheet -notmatch 'AutodeskObjectARX') ''

# ---- registry contract ----
$k = Get-ItemProperty -Path 'HKCU:\SOFTWARE\Autodesk\ObjectARX Wizards' -ErrorAction SilentlyContinue
Check 'registry PropsDir written' ($k -and ($k.PropsDir -eq $propsDir)) ($(if ($k) { $k.PropsDir } else { 'missing' }))
# the roots are prefixes, so the registry value carries the trailing backslash (the MSI's
# ARXROOT_PROBE default was "C:\Program Files\Autodesk\" for the same reason)
Check 'registry ArxRoot written (with trailing backslash)' ($k -and ($k.ArxRoot -eq ($sdkDir + '\'))) ($(if ($k) { $k.ArxRoot } else { 'missing' }))
Check 'registry AcadRoot written (with trailing backslash)' ($k -and ($k.AcadRoot -eq ($sdkDir + '\'))) ($(if ($k) { $k.AcadRoot } else { 'missing' }))

# ---- re-run over the same install (upgrade path: years detected, no duplicate work) ----
$p = Start-Process -FilePath $setup -ArgumentList $installArgs -NoNewWindow -Wait -PassThru
Check 're-install exited 0' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)
$generated2 = @(Get-ChildItem $propsDir -Filter 'Autodesk.arx-*.props' | ForEach-Object { $_.Name } | Sort-Object)
Check 're-install keeps exactly the same generated set' (($generated2 -join ',') -ceq ($expectGenerated -join ',')) ($generated2 -join ', ')

# ---- narrowing the selection must delete the other years ----
$argsNarrow = ($installArgs -replace ('/YEARS=' + $years), '/YEARS=2026')
$p = Start-Process -FilePath $setup -ArgumentList $argsNarrow -NoNewWindow -Wait -PassThru
Check 're-install with a narrower selection exited 0' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)
$generated3 = @(Get-ChildItem $propsDir -Filter 'Autodesk.arx-*.props' | ForEach-Object { $_.Name } | Sort-Object)
$expectNarrow = @('Autodesk.arx-2026.props', 'Autodesk.arx-2026-net.props') | Sort-Object
Check 'unticked years were cleaned up' (($generated3 -join ',') -ceq ($expectNarrow -join ',')) ($generated3 -join ', ')

# ---- the VSIX step, against a VS root that has no VSIXInstaller.exe ----
$argsVsix = ($installArgs -replace '/SKIPVSIX=1', '/SKIPVSIX=0')
$p = Start-Process -FilePath $setup -ArgumentList $argsVsix -NoNewWindow -Wait -PassThru
Check 'install with the VSIX step enabled still exits 0' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)
$alog = Join-Path $env:TEMP 'ObjectARXWizards-Inno.log'
Check 'VSIX step reports the missing VSIXInstaller' ((Get-Content $alog -Raw) -match 'VSIXInstaller\.exe not found')

# A property sheet the installer did not put there (the machine's own, from another project): the
# installers no longer ship any .props, so nothing of ours may delete it - not even a props folder
# that moves, and certainly not the uninstall.
$foreign = Join-Path $propsDir 'ObjectARX.Common.props'
Set-Content -Path $foreign -Value '<Project />' -Encoding UTF8

# ---------------------------------------------------------------- uninstall
$uninst = Join-Path $appDir 'unins000.exe'
Check 'uninstaller written into the install folder' (Test-Path $uninst)
$p = Start-Process -FilePath $uninst -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -NoNewWindow -Wait -PassThru
Check 'uninstaller exited 0' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)

# Inno's uninstaller hands the work to a copy of itself and returns, so give it a moment.
Wait-Gone $appDir

Check 'generated props removed on uninstall' ((Get-ChildItem $propsDir -Filter 'Autodesk.arx-*.props' -ErrorAction SilentlyContinue).Count -eq 0)
Check 'ObjectARX.User.props removed on uninstall' (-not (Test-Path (Join-Path $propsDir 'ObjectARX.User.props')))
Check 'a foreign props file survives the uninstall' (Test-Path $foreign)

Check 'install folder removed' (-not (Test-Path $appDir))
$k = Get-ItemProperty -Path 'HKCU:\SOFTWARE\Autodesk\ObjectARX Wizards' -ErrorAction SilentlyContinue
Check 'registry key removed on uninstall' ($null -eq $k)

# ---------------------------------------------------------------- remembered install folder
# The registry value is what makes the folder survive a switch between the two installers: with no
# Inno install of its own to remember, the shipped Program Files default would win. Simulate exactly
# that - registry InstallDir pointing at a scratch folder, no /DIR on the command line.
$remembered = Join-Path $work 'remembered'
New-Item -ItemType Directory -Force $remembered | Out-Null
New-Item -Path 'HKCU:\SOFTWARE\Autodesk\ObjectARX Wizards' -Force | Out-Null
Set-ItemProperty -Path 'HKCU:\SOFTWARE\Autodesk\ObjectARX Wizards' -Name 'InstallDir' -Value $remembered
$argsRemembered = ($installArgs -replace ' /DIR="[^"]*"', '') -replace ' /PROPSDIR="[^"]*"', ''
$p = Start-Process -FilePath $setup -ArgumentList $argsRemembered -NoNewWindow -Wait -PassThru
Check 'install folder defaults to the remembered folder' `
      ($p.ExitCode -eq 0 -and (Test-Path (Join-Path $remembered 'unins000.exe'))) ('exit=' + $p.ExitCode)
Check 'its props folder follows it' (Test-Path (Join-Path $remembered 'Autodesk.arx-2020.props'))
Start-Process -FilePath (Join-Path $remembered 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -NoNewWindow -Wait | Out-Null
Wait-Gone $remembered
Remove-Item -Path 'HKCU:\SOFTWARE\Autodesk\ObjectARX Wizards' -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
if ($fail -eq 0) { Write-Host ('ALL CHECKS PASSED (' + $work + ')') } else { Write-Host ($fail.ToString() + ' CHECK(S) FAILED (' + $work + ')') }
exit $fail
