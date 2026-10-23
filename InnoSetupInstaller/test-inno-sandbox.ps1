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
$test = $test.Replace('OutputBaseFilename=ObjectARXMultiVersionWizards-classic', 'OutputBaseFilename=InnoSandboxTest')
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
# {$appDir} is deliberately NOT created here: Inno only removes a directory it created itself,
# so the uninstall checks below need the installer to make it.
$vsVc        = Join-Path $vsRoot 'Common7\IDE\VC'
$vsProjects  = Join-Path $vsVc 'vcprojects\Autodesk'
$vsItemsOld  = Join-Path $vsVc 'vcprojectitems\ObjectARX'
$vsItemsNew  = Join-Path $vsVc 'vcprojectitems\ObjectARXMV'
foreach ($d in @($propsDir, $vsProjects, $vsItemsOld)) {
  New-Item -ItemType Directory -Force $d | Out-Null
}

# --------------------------------------------------------------- official wizard placeholders
# The official yearly ObjectARX wizard ships the very names this line used up to 0.2.7: the two
# project triples in vcprojects\Autodesk and its item wizards under vcprojectitems\ObjectARX
# (including the two Maya entries). Their ABSOLUTE_PATH points at the official install folder, so
# the guarded legacy cleanup must leave every one of these files alone through install AND
# uninstall - this line only owns the MV-suffixed names and the ObjectARXMV folder.
function Set-OfficialPlaceholder([string]$path, [string]$wizard) {
  Set-Content -Path $path -Encoding ASCII -Value @(
    'VSWIZARD 7.0',
    'Wizard=VsWizard.VsWizardEngine.17.0',
    ('Param="WIZARD_NAME = ' + $wizard + '"'),
    ('Param="ABSOLUTE_PATH = C:\Program Files\Autodesk\ObjectARX 2026 Wizards\' + $wizard + '"'),
    'Param="FALLBACK_LCID = 1033"'
  )
}
Set-OfficialPlaceholder (Join-Path $vsProjects 'ArxAppWiz.vsz') 'ArxAppWiz'
Set-OfficialPlaceholder (Join-Path $vsProjects 'ArxAppWizOMF.vsz') 'ArxAppWiz182'
Set-Content (Join-Path $vsProjects 'ArxAppWiz.vsdir') 'ArxAppWiz.vsz| |ARX/DBX Project For AutoCAD 2026' -Encoding ASCII
Set-Content (Join-Path $vsProjects 'ArxAppWiz.ico') 'official-icon-1' -Encoding ASCII
Set-Content (Join-Path $vsProjects 'ArxAppWizOMF.vsdir') 'ArxAppWizOMF.vsz| |OMF Project For AutoCAD 2026' -Encoding ASCII
Set-Content (Join-Path $vsProjects 'ArxAppWizOMF.ico') 'official-icon-2' -Encoding ASCII
Set-OfficialPlaceholder (Join-Path $vsItemsOld 'ArxWizCustomObject.vsz') 'ArxWizCustomObject'
Set-Content (Join-Path $vsItemsOld 'ArxWizCustomObject.ico') 'official-icon-3' -Encoding ASCII
Set-Content (Join-Path $vsItemsOld 'MayaCommand.vsz') 'official maya wizard' -Encoding ASCII
Set-Content (Join-Path $vsItemsOld 'MayaCustomNode.vsz') 'official maya wizard' -Encoding ASCII
$officialSurvivors = @(
  (Join-Path $vsProjects 'ArxAppWiz.vsz'), (Join-Path $vsProjects 'ArxAppWiz.vsdir'),
  (Join-Path $vsProjects 'ArxAppWiz.ico'), (Join-Path $vsProjects 'ArxAppWizOMF.vsz'),
  (Join-Path $vsProjects 'ArxAppWizOMF.vsdir'), (Join-Path $vsProjects 'ArxAppWizOMF.ico'),
  (Join-Path $vsItemsOld 'ArxWizCustomObject.vsz'), (Join-Path $vsItemsOld 'ArxWizCustomObject.ico'),
  (Join-Path $vsItemsOld 'MayaCommand.vsz'), (Join-Path $vsItemsOld 'MayaCustomNode.vsz')
)
function Assert-OfficialSurvivors([string]$stage) {
  $lost = @($officialSurvivors | Where-Object { -not (Test-Path $_) })
  Check ('official wizard files survive ' + $stage) ($lost.Count -eq 0) ($lost -join ', ')
  $stillOurs = (Get-Content (Join-Path $vsProjects 'ArxAppWiz.vsz') -Raw) -match [regex]::Escape('ObjectARX 2026 Wizards')
  Check ('official wizard content untouched after ' + $stage) ($stillOurs)
}

$years = '2020,2024,2026,2027'
$installArgs = @(
  '/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART',
  ('/LOG="' + (Join-Path $work 'install.log') + '"'),
  ('/DIR="' + $appDir + '"'),
  ('/PROPSDIR="' + $propsDir + '"'),
  ('/ARXROOT="' + $sdkDir + '"'),
  ('/ACADROOT="' + $sdkDir + '"'),

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
# With no /PROPSDIR and an empty registry the property sheet folder must follow the install
# folder; this round also proves the generated sheets are cleared from it on uninstall, and that
# an omitted (empty) RDS leaves the ADSK placeholder in the HTML wizard untouched - the MSI
# substituted ADSK with ADSK in that case, so deleting it here would diverge.
Remove-Item $regKey -Recurse -Force -ErrorAction SilentlyContinue
$argsFresh = ($installArgs -replace ' /PROPSDIR="[^"]*"', '') -replace ' /RDS=ABC', ''
$p = Start-Process -FilePath $setup -ArgumentList $argsFresh -NoNewWindow -Wait -PassThru
Check 'fresh install exits 0 (property sheet folder follows the install folder)' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)
Check 'generated props land in the install folder' (
  (Test-Path (Join-Path $appDir 'Autodesk.arx-2020.props')) -and
  (Test-Path (Join-Path $appDir 'ObjectARX.User.props')) -and
  (Test-Path (Join-Path $appDir 'ObjectARX.WindowsSdk.props')))
$k0 = Get-ItemProperty $regKey -ErrorAction SilentlyContinue
Check 'registry PropsDir follows the install folder' ($k0 -and ($k0.PropsDir -eq $appDir)) ($(if ($k0) { $k0.PropsDir } else { 'missing' }))
$jsHtml = Get-Content (Join-Path $appDir 'ArxAppWiz\HTML\1033\default.htm') -Raw
Check 'empty RDS leaves the ADSK placeholder alone' ($jsHtml -match 'ADSK')
Check 'empty RDS does not inject a value' ($jsHtml -notmatch 'ABC')
Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -NoNewWindow -Wait | Out-Null
Wait-Gone (Join-Path $appDir 'Autodesk.arx-2020.props')
Check 'uninstall clears the generated props from the install folder' (-not (Test-Path (Join-Path $appDir 'Autodesk.arx-2020.props')))
Check 'uninstall clears the shared sheet too' (-not (Test-Path (Join-Path $appDir 'ObjectARX.WindowsSdk.props')))
# Back to a clean slate for the rounds below. The install folder stays because the shared sheets
# are permanent by design, so drop it by hand.
Remove-Item $regKey -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $appDir -Recurse -Force -ErrorAction SilentlyContinue

# ---------------------------------------------------------------- install
$p = Start-Process -FilePath $setup -ArgumentList $installArgs -NoNewWindow -Wait -PassThru
Check 'installer exited 0' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)

# ---- install folder ----
$expectApp = @(

  'ArxWizCommon\arxCommon.js', 'ArxAppWiz\1033\styles.css', 'ArxAppWiz182\1033\styles.css',
  'ArxAtlWizComWrapper\Templates\1033\object.h', 'ArxAtlWizDynProp\Templates\1033\dynprop.h',
  'ArxWizCustomObject\Templates\1033\object.h', 'ArxWizJig\Templates\1033\Jig.h',
  'ArxWizMFCSupport\Templates\1033\Palette.h', 'ArxWizNETWrapper\Templates\1033\managedWrapper.h',
  'ArxWizReactors\Templates\1033\AcEditorReactor_tmpl.h'
)
$missing = @($expectApp | Where-Object { -not (Test-Path (Join-Path $appDir $_)) })
Check 'install folder payload present' ($missing.Count -eq 0) ($missing -join ', ')

# ---- the line's own registrations live under MV-suffixed names / the ObjectARXMV folder ----
$mvProject = @('ArxAppWizMV.vsz','ArxAppWizMV.vsdir','ArxAppWizMV.ico',
               'ArxAppWizOMFMV.vsz','ArxAppWizOMFMV.vsdir','ArxAppWizOMFMV.ico')
$missingProject = @($mvProject | Where-Object { -not (Test-Path (Join-Path $vsProjects $_)) })
Check 'six MV project registrations present' ($missingProject.Count -eq 0) ($missingProject -join ', ')
$mvItemCount = @(Get-ChildItem $vsItemsNew -File -ErrorAction SilentlyContinue).Count
Check 'ObjectARXMV holds exactly the fourteen owned item files' ($mvItemCount -eq 14) ("$mvItemCount files")

# ---- patch 1: [TARGETDIR] in the nine owned .vsz files ----
$vsz = @(
  (Join-Path $vsProjects 'ArxAppWizMV.vsz'),
  (Join-Path $vsProjects 'ArxAppWizOMFMV.vsz'),
  (Join-Path $vsItemsNew 'ArxAtlWizComWrapper.vsz'),
  (Join-Path $vsItemsNew 'ArxAtlWizDynProp.vsz'),
  (Join-Path $vsItemsNew 'ArxWizCustomObject.vsz'),
  (Join-Path $vsItemsNew 'ArxWizJig.vsz'),
  (Join-Path $vsItemsNew 'ArxWizMFCSupport.vsz'),
  (Join-Path $vsItemsNew 'ArxWizNETWrapper.vsz'),
  (Join-Path $vsItemsNew 'ArxWizReactors.vsz')
)
$badVsz = @($vsz | Where-Object {
  (-not (Test-Path $_)) -or ((Get-Content $_ -Raw) -match '\[TARGETDIR\]') -or
  ((Get-Content $_ -Raw) -notmatch [regex]::Escape($appDir + '\'))
})
Check 'nine owned .vsz files carry the real TARGETDIR' ($badVsz.Count -eq 0) (($badVsz | ForEach-Object { Split-Path $_ -Leaf }) -join ', ')
Check 'no Maya wizard dropped into ObjectARXMV' (-not (Test-Path (Join-Path $vsItemsNew 'MayaCommand.vsz')))

# ---- coexistence: the official wizard's legacy-named files stay untouched next to ours ----
Assert-OfficialSurvivors 'install'

# ---- patch 2: ADSK -> RDS ----
$html = Get-Content (Join-Path $appDir 'ArxAppWiz\HTML\1033\default.htm') -Raw
Check 'default.htm ADSK replaced by the RDS' (($html -match 'ABC') -and ($html -notmatch 'ADSK')) ''

# No shipped Autodesk.arx-<year>.props: the only sheets in the install folder are the ones the
# generator just produced for the ticked years, so a single-year payload cannot linger stale.
Check 'no single-year props template ships as payload' (
  -not (Test-Path (Join-Path $repo '_Installs\Autodesk.arx-2026.props')) -and
  -not (Test-Path (Join-Path $appDir 'ArxAppWiz\Templates\1033\Autodesk.arx-2026.props')))

# ---- patch 4: arxCommon.js props folder ----
$js = Get-Content (Join-Path $appDir 'ArxWizCommon\arxCommon.js') -Raw
$jsExpect = $propsDir.Replace('\', '\\') + '\\'
$jsLine = (($js -split "`n") | Where-Object { $_ -match 'var ARX_PROPS_DIR =' } | Select-Object -First 1)
Check 'arxCommon.js points at the props folder' `
      ($null -ne $jsLine -and $jsLine.Contains('var ARX_PROPS_DIR ="' + $jsExpect + '" ;')) $jsLine

# ---- the SDK folder is never touched ----
Check 'the installer leaves the SDK folder alone' (-not (Test-Path $arxSdk))

# ---- no property sheet ships: the props folder holds the generated ones and nothing else ----
# Our own files are the per-year sheets, the user roots and the one shared sheet every year imports.
$ours = @('ObjectARX.User.props', 'ObjectARX.WindowsSdk.props')
$foreign = @(Get-ChildItem $propsDir -File | Where-Object { $_.Name -notlike 'Autodesk.arx-*' -and $ours -notcontains $_.Name })
Check 'no foreign props installed' ($foreign.Count -eq 0) (($foreign | ForEach-Object { $_.Name }) -join ', ')
Check 'the shared Windows SDK sheet follows the year sheets' (Test-Path (Join-Path $propsDir 'ObjectARX.WindowsSdk.props'))
Check 'the year sheets import the shared Windows SDK sheet' `
      ((Get-Content (Join-Path $propsDir 'Autodesk.arx-2020.props') -Raw) -match 'ObjectARX\.WindowsSdk\.props')

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
Check 'ObjectARX.WindowsSdk.props removed on uninstall' (-not (Test-Path (Join-Path $propsDir 'ObjectARX.WindowsSdk.props')))
Check 'a foreign props file survives the uninstall' (Test-Path $foreign)

Check 'install folder removed' (-not (Test-Path $appDir))
Check 'MV project registrations removed' (-not (Test-Path (Join-Path $vsProjects 'ArxAppWizMV.vsz')))
Check 'ObjectARXMV item folder removed' (-not (Test-Path $vsItemsNew))
# The official wizard, sitting in the same Visual Studio folders under the legacy names, must
# survive this line's uninstall as untouched as it survived the install.
Assert-OfficialSurvivors 'uninstall'

# ------------------------------------------------- upgrade path: this product's own 0.2.7 files
# Simulate the pre-0.2.8 layout of THIS product: legacy names, but ABSOLUTE_PATH pointing at our
# install folder. The next install must reclaim exactly these (content-guarded), while the
# official placeholders (recreated here, they shared the names) stay afterwards.
$ownItemNames = @('ArxAtlWizComWrapper','ArxAtlWizDynProp','ArxWizCustomObject',
                  'ArxWizJig','ArxWizMFCSupport','ArxWizNETWrapper','ArxWizReactors')
function Set-OwnLegacy([string]$path, [string]$wizard) {
  Set-Content -Path $path -Encoding ASCII -Value @(
    'VSWIZARD 7.0',
    'Wizard=VsWizard.VsWizardEngine.17.0',
    ('Param="WIZARD_NAME = ' + $wizard + '"'),
    ('Param="ABSOLUTE_PATH = C:\Program Files\Autodesk\ObjectARX Multi-Version Wizards\' + $wizard + '"')
  )
}
Remove-Item (Join-Path $vsProjects '*') -Force
foreach ($pair in @(@('ArxAppWiz.vsz','ArxAppWiz'), @('ArxAppWizOMF.vsz','ArxAppWiz182'))) {
  Set-OwnLegacy (Join-Path $vsProjects $pair[0]) $pair[1]
  Set-Content (Join-Path $vsProjects ($pair[0] -replace '\.vsz$','.vsdir')) 'own legacy' -Encoding ASCII
  Set-Content (Join-Path $vsProjects ($pair[0] -replace '\.vsz$','.ico')) 'own legacy icon' -Encoding ASCII
}
Remove-Item (Join-Path $vsItemsOld '*') -Force
foreach ($n in $ownItemNames) {
  Set-OwnLegacy (Join-Path $vsItemsOld ($n + '.vsz')) $n
  Set-Content (Join-Path $vsItemsOld ($n + '.ico')) 'own legacy icon' -Encoding ASCII
}
$p = Start-Process -FilePath $setup -ArgumentList $installArgs -NoNewWindow -Wait -PassThru
Check 'upgrade over own 0.2.7 layout exited 0' ($p.ExitCode -eq 0) ('exit=' + $p.ExitCode)
$leftOldProject = @($mvProject | ForEach-Object { Join-Path $vsProjects ($_ -replace 'MV','') } | Where-Object { Test-Path $_ })
Check 'own legacy project registrations reclaimed' ($leftOldProject.Count -eq 0) ($leftOldProject -join ', ')
$leftOldItems = @($ownItemNames | ForEach-Object { Join-Path $vsItemsOld ($_ + '.vsz') } | Where-Object { Test-Path $_ })
Check 'own legacy item registrations reclaimed and the empty folder removed' `
      (($leftOldItems.Count -eq 0) -and -not (Test-Path $vsItemsOld)) ($leftOldItems -join ', ')
Check 'new MV registrations land next to the upgrade cleanup' ((Test-Path (Join-Path $vsProjects 'ArxAppWizMV.vsz')) -and (Test-Path $vsItemsNew))
Start-Process -FilePath (Join-Path $appDir 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -NoNewWindow -Wait | Out-Null
Wait-Gone $appDir
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
      ($p.ExitCode -eq 0 -and (Test-Path (Join-Path $remembered 'ArxWizCommon\arxCommon.js'))) ('exit=' + $p.ExitCode)
Check 'its props folder follows it' (Test-Path (Join-Path $remembered 'Autodesk.arx-2020.props'))
Start-Process -FilePath (Join-Path $remembered 'unins000.exe') -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -NoNewWindow -Wait | Out-Null
Wait-Gone $remembered
Remove-Item -Path 'HKCU:\SOFTWARE\Autodesk\ObjectARX Wizards' -Recurse -Force -ErrorAction SilentlyContinue

Write-Host ''
if ($fail -eq 0) { Write-Host ('ALL CHECKS PASSED (' + $work + ')') } else { Write-Host ($fail.ToString() + ' CHECK(S) FAILED (' + $work + ')') }
exit $fail
