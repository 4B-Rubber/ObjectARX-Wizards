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

# ---- 1. zero-diff against the offline generator ----
$base = Join-Path $root 'baseline'
$gen = Join-Path $root 'exe'
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $repo 'tools\arx-props\gen-arx-props.ps1') -OutDir $base | Out-Null
& $Exe generate --props-dir $gen --log (Join-Path $root 'gen.log') | Out-Null

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
& $Exe generate --props-dir $gen --years 2020 | Out-Null
Check 'ObjectARX.User.props not clobbered' ((Get-Content $userProps -Raw) -ceq $before)

# ---- 2. empty year selection writes nothing ----
$empty = Join-Path $root 'empty'
& $Exe generate --props-dir $empty --years '' | Out-Null
Check 'empty --years writes no props' ((Get-ChildItem $empty -Filter 'Autodesk.arx-*.props' -ErrorAction SilentlyContinue).Count -eq 0)
Check 'empty --years writes no User.props' (-not (Test-Path (Join-Path $empty 'ObjectARX.User.props')))

# ---- 3. cleanup keeps only the ticked years ----
$keep = Join-Path $root 'keep'
& $Exe generate --props-dir $keep --years 2020,2024,2026,2027 | Out-Null
# 2020/2024/2026 -> plain + net, 2027 -> plain + net + Compat
$n1 = (Get-ChildItem $keep -Filter 'Autodesk.arx-*.props').Count
& $Exe cleanup --props-dir $keep --keep 2020,2024,2026,2027 | Out-Null
$n2 = (Get-ChildItem $keep -Filter 'Autodesk.arx-*.props').Count
Check "cleanup --keep 4 years keeps them ($n1 -> $n2)" ($n1 -eq 9 -and $n2 -eq 9)
& $Exe cleanup --props-dir $keep --keep 2020 | Out-Null
$left = @(Get-ChildItem $keep -Filter 'Autodesk.arx-*.props' | ForEach-Object { $_.Name } | Sort-Object)
$expected = @('Autodesk.arx-2020.props', 'Autodesk.arx-2020-net.props')
Check 'cleanup --keep 2020 leaves only the 2020 sheets' `
      (($left -join ',') -ceq ($expected -join ',')) ($left -join ',')

# ---- 4. cleanup with an empty keep list removes everything ----
& $Exe cleanup --props-dir $keep --keep '' | Out-Null
Check 'cleanup --keep "" removes all generated sheets' ((Get-ChildItem $keep -Filter 'Autodesk.arx-*.props').Count -eq 0)

# ---- 5. remove ----
$rm = Join-Path $root 'remove'
& $Exe generate --props-dir $rm --years 2025,2026 | Out-Null
Copy-Item (Join-Path $repo '_Installs\ObjectARX Props\ObjectARX.Common.props') $rm
& $Exe remove --props-dir $rm | Out-Null
Check 'remove drops generated sheets' ((Get-ChildItem $rm -Filter 'Autodesk.arx-*.props').Count -eq 0)
Check 'remove drops ObjectARX.User.props' (-not (Test-Path (Join-Path $rm 'ObjectARX.User.props')))
Check 'remove without --include-shared keeps shared props' (Test-Path (Join-Path $rm 'ObjectARX.Common.props'))

# ---- 6. moved props folder: remove --include-shared ----
$mv = Join-Path $root 'moved'
& $Exe generate --props-dir $mv --years 2026 | Out-Null
Copy-Item (Join-Path $repo '_Installs\ObjectARX Props\HCSoft.grx-2026.props') $mv
& $Exe remove --props-dir $mv --include-shared | Out-Null
Check 'remove --include-shared also drops shared props' ((Get-ChildItem $mv).Count -eq 0)

# ---- 7. bad command line ----
$old = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
& $Exe nonsense *> $null
Check 'unknown command exits 2' ($LASTEXITCODE -eq 2)
& $Exe generate *> $null
Check 'generate without --props-dir exits 2' ($LASTEXITCODE -eq 2)
$ErrorActionPreference = $old

Write-Host ""
if ($fail -eq 0) { Write-Host "ALL CHECKS PASSED ($root)" } else { Write-Host "$fail CHECK(S) FAILED ($root)" }
exit $fail
