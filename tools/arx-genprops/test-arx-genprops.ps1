#Requires -Version 5.1
<#
  Regression + functional test for tools\arx-genprops against the offline generator
  tools\arx-props\gen-arx-props.ps1 (the repository's single source of truth for the year table
  and the three skeletons).

  Checks:
    1. generate (all years) is text-identical to gen-arx-props.ps1 output.
       The only allowed difference is the UTF-8 BOM the .ps1 writes and the .exe does not: the MSI
       custom action (ArxWizCustomAction.ArxProps) writes UTF8Encoding(false) as well, and the Inno
       line must reproduce what the MSI leaves on disk.
    2. generate with an empty --years writes nothing.
    3. generate <subset> then cleanup --keep <subset> removes the other years.
    4. cleanup --keep "" removes every generated sheet.
    5. remove drops Autodesk.arx-*.props + ObjectARX.User.props.
#>
param(
  [string]$Exe = "$PSScriptRoot\bin\Release\arx-genprops.exe"
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$root = Join-Path $env:TEMP ('arx-genprops-test-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Force $root | Out-Null

$fail = 0
function Check([string]$name, [bool]$ok, [string]$detail = '') {
  if ($ok) { Write-Host ("  PASS  " + $name) }
  else { Write-Host ("  FAIL  " + $name + " " + $detail); $script:fail++ }
}

if (-not (Test-Path $Exe)) { throw "arx-genprops.exe not found: $Exe (build it first)" }

# arx-genprops is a GUI-subsystem binary: the Inno setup runs it and a console-subsystem one would
# flash a console window on every call (SW_HIDE does not suppress that when the default terminal
# application is Windows Terminal). PowerShell's & does not wait for GUI-subsystem programs, so
# every call below is followed by Wait-GenProps before the files are looked at.
function Wait-GenProps() {
  Wait-Process -Name 'arx-genprops' -ErrorAction SilentlyContinue
}

# ---- 1. zero-diff against the offline generator ----
$base = Join-Path $root 'baseline'
$gen = Join-Path $root 'exe'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'tools\arx-props\gen-arx-props.ps1') -OutDir $base | Out-Null; Wait-GenProps
& $Exe generate --props-dir $gen --log (Join-Path $root 'gen.log') | Out-Null; Wait-GenProps

$baseFiles = Get-ChildItem $base -Filter 'Autodesk.arx-*.props'
$genFiles = Get-ChildItem $gen -Filter 'Autodesk.arx-*.props'
Check "same file count ($($baseFiles.Count))" ($baseFiles.Count -eq $genFiles.Count) "got $($genFiles.Count)"
$diff = @()
$bomOnly = 0
foreach ($f in $baseFiles) {
  $g = Join-Path $gen $f.Name
  if (-not (Test-Path $g)) { $diff += $f.Name; continue }
  $bt = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($f.FullName))
  $gt = [System.Text.Encoding]::UTF8.GetString([System.IO.File]::ReadAllBytes($g))
  if ($gt -cne $bt) { $diff += $f.Name }
  $bb = [System.IO.File]::ReadAllBytes($f.FullName); $gb = [System.IO.File]::ReadAllBytes($g)
  if (($bb[0] -eq 239) -ne ($gb[0] -eq 239)) { $bomOnly++ }
}
Check 'generated text identical to gen-arx-props.ps1' ($diff.Count -eq 0) ($diff -join ',')
Check 'BOM difference is exactly the .ps1-only BOM' ($bomOnly -eq $baseFiles.Count) "bomOnly=$bomOnly of $($baseFiles.Count)"
Check 'objectARX.User.props written' (Test-Path (Join-Path $gen 'ObjectARX.User.props'))

# user props must not be clobbered on a second run
$userProps = Join-Path $gen 'ObjectARX.User.props'
$before = Get-Content $userProps -Raw
& $Exe generate --props-dir $gen --years 2020 | Out-Null; Wait-GenProps
Check 'ObjectARX.User.props not clobbered' ((Get-Content $userProps -Raw) -ceq $before)

# ---- 2. empty year selection writes nothing ----
$empty = Join-Path $root 'empty'
& $Exe generate --props-dir $empty --years '' | Out-Null; Wait-GenProps
Check 'empty --years writes no props' ((Get-ChildItem $empty -Filter 'Autodesk.arx-*.props' -ErrorAction SilentlyContinue).Count -eq 0)
Check 'empty --years writes no User.props' (-not (Test-Path (Join-Path $empty 'ObjectARX.User.props')))

# ---- 3. cleanup keeps only the ticked years ----
$keep = Join-Path $root 'keep'
& $Exe generate --props-dir $keep --years 2020,2024,2026,2027 | Out-Null; Wait-GenProps
# 2020/2024/2026 -> plain + net, 2027 -> plain + net + Compat
$n1 = (Get-ChildItem $keep -Filter 'Autodesk.arx-*.props').Count
& $Exe cleanup --props-dir $keep --keep 2020,2024,2026,2027 | Out-Null; Wait-GenProps
$n2 = (Get-ChildItem $keep -Filter 'Autodesk.arx-*.props').Count
Check "cleanup --keep 4 years keeps them ($n1 -> $n2)" ($n1 -eq 9 -and $n2 -eq 9)
& $Exe cleanup --props-dir $keep --keep 2020 | Out-Null; Wait-GenProps
$left = @(Get-ChildItem $keep -Filter 'Autodesk.arx-*.props' | ForEach-Object { $_.Name } | Sort-Object)
$expected = @('Autodesk.arx-2020.props', 'Autodesk.arx-2020-net.props')
Check 'cleanup --keep 2020 leaves only the 2020 sheets' `
      (($left -join ',') -ceq ($expected -join ',')) ($left -join ',')

# ---- 4. cleanup with an empty keep list removes everything ----
& $Exe cleanup --props-dir $keep --keep '' | Out-Null; Wait-GenProps
Check 'cleanup --keep "" removes all generated sheets' ((Get-ChildItem $keep -Filter 'Autodesk.arx-*.props').Count -eq 0)

# ---- 5. remove: only what this tool generated, whatever else shares the folder ----
$rm = Join-Path $root 'remove'
& $Exe generate --props-dir $rm --years 2025,2026 | Out-Null; Wait-GenProps
Copy-Item (Join-Path $repo '_Installs\ObjectARX Props\ObjectARX.Common.props') $rm
& $Exe remove --props-dir $rm | Out-Null; Wait-GenProps
Check 'remove drops generated sheets' ((Get-ChildItem $rm -Filter 'Autodesk.arx-*.props').Count -eq 0)
Check 'remove drops ObjectARX.User.props' (-not (Test-Path (Join-Path $rm 'ObjectARX.User.props')))
Check 'remove leaves a foreign props file alone' (Test-Path (Join-Path $rm 'ObjectARX.Common.props'))

# ---- 6. bad command line ----
# Start-Process -Wait, not &: an exit code is only reliable that way for a GUI-subsystem binary.
$p = Start-Process -FilePath $Exe -ArgumentList 'nonsense' -Wait -PassThru
Check 'unknown command exits 2' ($p.ExitCode -eq 2) "got $($p.ExitCode)"
$p = Start-Process -FilePath $Exe -ArgumentList 'generate' -Wait -PassThru
Check 'generate without --props-dir exits 2' ($p.ExitCode -eq 2) "got $($p.ExitCode)"

# ---- 7. probe: the Inno setup's "is Visual Studio running?" check ----
# Probing for this script's own process makes the positive case work without depending on which
# programs happen to be running on the machine.
$selfName = (Get-Process -Id $PID).ProcessName
$outFile = Join-Path $root 'probe.txt'
Remove-Item $outFile -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $Exe -ArgumentList ("probe --out `"$outFile`" --names $selfName") -Wait -PassThru
$probeText = if (Test-Path $outFile) { ([IO.File]::ReadAllText($outFile)).Trim() } else { '' }
Check 'probe reports a running process (exit 3 + name in the file)' `
      ($p.ExitCode -eq 3 -and $probeText -eq $selfName) "exit=$($p.ExitCode) text='$probeText'"
Remove-Item $outFile -Force -ErrorAction SilentlyContinue
$p = Start-Process -FilePath $Exe -ArgumentList ("probe --out `"$outFile`" --names no-such-process-xyz") -Wait -PassThru
Check 'probe writes no file when nothing is running (exit 0)' `
      ($p.ExitCode -eq 0 -and -not (Test-Path $outFile)) "exit=$($p.ExitCode) file=$(Test-Path $outFile)"

Write-Host ""
if ($fail -eq 0) { Write-Host "ALL CHECKS PASSED ($root)" } else { Write-Host "$fail CHECK(S) FAILED ($root)" }
exit $fail
