<#
.SYNOPSIS
  Builds what the MSI/Burn line needs - the VSIX the bundle carries - with every output going to a
  separate build root.

.DESCRIPTION
  Nothing is written into the source tree: the repository is first mirrored to
  <BuildRoot>\src (excluding .git, bin, obj, Output, ...), and the build then runs inside that
  copy. The build-root convention is documented in AGENTS.local.md
  (on this machine: D:\builder\ObjectARX-Wizards).

  This is the MSI/Burn line (the original one): the Inno single-file installer is built on `dev`,
  the classic-wizard shape on `classic-wizards`. The MSI and the bundle are built from the mirror
  after this script has put the VSIX in place:

    msbuild ObjectARXWizardsInstaller\ObjectARXWizardsBundle.wixproj -p:Configuration=Release -p:Platform=x86

  (that project runs ObjectARXWizardsInstaller\make.bat for the MSI itself).

.PARAMETER BuildRoot
  Build root directory. Can also come from the ARX_BUILD_ROOT environment variable.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tools\build-and-pack.ps1 -BuildRoot D:\builder\ObjectARX-Wizards
#>
[CmdletBinding()]
param(
    [string] $BuildRoot = $env:ARX_BUILD_ROOT,
    [string] $Configuration = 'Release',
    [switch] $SkipSync
)

$ErrorActionPreference = 'Stop'

function Step([string] $text) { Write-Output ('== ' + $text) }
function Fail([string] $text) { Write-Output ('!! ' + $text); exit 1 }

if (-not $BuildRoot) {
    Fail 'Build root missing: pass -BuildRoot <path> or set ARX_BUILD_ROOT (see AGENTS.local.md).'
}

# This script lives in <repo>\tools, so the repository root is its parent.
$repo = Split-Path $PSScriptRoot -Parent
$proj = Join-Path $repo 'ArxVsixWizard\ArxVsixWizard.csproj'
if (-not (Test-Path $proj)) { Fail ('Cannot find the source repository (expected ' + $proj + ').') }

$srcDir  = Join-Path $BuildRoot 'src'
$vsixDir = Join-Path $BuildRoot 'vsix'
foreach ($d in @($BuildRoot, $srcDir, $vsixDir)) {
    New-Item -ItemType Directory -Force -Path $d | Out-Null
}

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -find 'MSBuild\**\Bin\MSBuild.exe' 2>$null
        foreach ($f in @($found)) { if ($f -and (Test-Path $f)) { return $f } }
    }
    foreach ($root in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if (-not $root) { continue }
        $vsRoot = Join-Path $root 'Microsoft Visual Studio'
        if (-not (Test-Path $vsRoot)) { continue }
        $hits = Get-ChildItem $vsRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            Get-ChildItem $_.FullName -Directory -ErrorAction SilentlyContinue | ForEach-Object {
                Join-Path $_.FullName 'MSBuild\Current\Bin\MSBuild.exe'
            }
        }
        foreach ($h in $hits) { if (Test-Path $h) { return $h } }
    }
    return $null
}

$msbuild = Find-MSBuild
if (-not $msbuild) { Fail 'MSBuild.exe not found (it comes with Visual Studio).' }

Step ('Build root : ' + $BuildRoot)
Step ('MSBuild    : ' + $msbuild)

if (-not $SkipSync) {
    Step ('Mirroring the repository to ' + $srcDir + ' (source tree stays read-only)')
    $rcArgs = @($repo, $srcDir, '/MIR', '/NFL', '/NDL', '/NJH', '/NJS', '/NP', '/R:2', '/W:1')
    foreach ($x in @('.git', '.vs', 'bin', 'obj', '_E2EGen', '.tools', 'Output', 'node_modules')) {
        $rcArgs += @('/XD', $x)
    }
    & robocopy @rcArgs | Out-Null
    if ($LASTEXITCODE -ge 8) { Fail ('robocopy exit code ' + $LASTEXITCODE + ' (>=8 means failure).') }
}

Step ('MSBuild: VSIX (' + $Configuration + ')')
& $msbuild (Join-Path $srcDir 'ArxVsixWizard\ArxVsixWizard.csproj') -t:Restore,Build ('-p:Configuration=' + $Configuration) -v:m -nologo
if ($LASTEXITCODE -ne 0) { Fail 'VSIX build failed.' }

$builtVsix = Join-Path $srcDir ('ArxVsixWizard\bin\' + $Configuration + '\ArxVsixWizard.vsix')
if (-not (Test-Path $builtVsix)) { Fail ('Build output not found: ' + $builtVsix) }

# Refresh the VSIX inside the copy (the installer packs it) and keep a distributable copy in
# <BuildRoot>\vsix. The committed copy in the repository is left untouched on purpose.
Copy-Item $builtVsix (Join-Path $srcDir 'ObjectARXMultiVersionWizards.vsix') -Force
$distVsix = Join-Path $vsixDir 'ObjectARXMultiVersionWizards.vsix'
Copy-Item $builtVsix $distVsix -Force
Step ('VSIX: ' + $distVsix + '  ' + (Get-Item $distVsix).Length + ' bytes')

# The generator is not part of what this line ships - the MSI writes the year sheets through
# ArxWizCustomAction, which carries the same table and skeletons embedded. It is still built here so
# the mirror carries it for tools\arx-genprops\test-arx-genprops.ps1: that test compares the EXE
# against tools\arx-props\gen-arx-props.ps1 (the offline source of truth the custom action follows),
# so it is how the generation rules stay covered on this branch.
Step ('MSBuild: props generator arx-genprops (' + $Configuration + ')')
& $msbuild (Join-Path $srcDir 'tools\arx-genprops\arx-genprops.csproj') -t:Restore,Build ('-p:Configuration=' + $Configuration) -v:m -nologo
if ($LASTEXITCODE -ne 0) { Fail 'arx-genprops build failed.' }

$genExe = Join-Path $srcDir ('tools\arx-genprops\bin\' + $Configuration + '\arx-genprops.exe')
if (-not (Test-Path $genExe)) { Fail ('Build output not found: ' + $genExe) }
Step ('Generator: ' + $genExe + '  ' + (Get-Item $genExe).Length + ' bytes')

Step 'Done. Nothing was written into the source tree.'
Step 'Next: msbuild ObjectARXWizardsInstaller\ObjectARXWizardsBundle.wixproj -p:Configuration=Release -p:Platform=x86'
