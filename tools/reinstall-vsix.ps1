# Reinstalls the ObjectARX Multi-Version wizards VSIX into every Visual Studio on this
# machine (VS2022 = 17.x, VS2026 = 18.x).
#
# Why this exists: an interrupted install/uninstall leaves the extension folder on
# disk but empty while VS still keeps a template registration pointing at it. VS then
# fails to open "New Project" with HRESULT 0x80030002 (STG_E_FILENOTFOUND) and the
# ObjectARX item templates disappear from "Add New Item". Running this script clears
# those stale registrations before installing.
#
# Run it from a normal PowerShell window (NOT from inside the editor sandbox), with
# every Visual Studio instance closed.

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
# The VSIX is not committed: it lives in the build root after tools\build-and-pack.ps1 ran. Point
# ARX_BUILD_ROOT at that root, or pass -Vsix, when the file is somewhere else.
$candidates = @((Join-Path $repoRoot 'ObjectARXMultiVersionWizards.vsix'))
if ($env:ARX_BUILD_ROOT) { $candidates += (Join-Path $env:ARX_BUILD_ROOT 'vsix\ObjectARXMultiVersionWizards.vsix') }
$vsix = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not (Test-Path -LiteralPath $vsix)) { throw "VSIX not found: $vsix" }

$running = @(Get-Process devenv -ErrorAction SilentlyContinue)
if ($running.Count -gt 0) {
    throw "Close all Visual Studio instances first (running PID: $(($running.Id) -join ', '))"
}

$roots = @(Get-ChildItem "$env:LOCALAPPDATA\Microsoft\VisualStudio" -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^1[78]\.0_' })
if ($roots.Count -eq 0) { throw "No Visual Studio 17.x / 18.x instance found under $env:LOCALAPPDATA\Microsoft\VisualStudio" }

# Minimal JSON string encoder - ConvertTo-Json is not used because Windows PowerShell
# 5.1 unwraps single-element arrays into a bare object.
function ConvertTo-JsonText([string]$value) {
    $sb = New-Object System.Text.StringBuilder
    [void]$sb.Append('"')
    foreach ($ch in $value.ToCharArray()) {
        switch ($ch) {
            '"'  { [void]$sb.Append('\"') }
            '\'  { [void]$sb.Append('\\') }
            "`r" { [void]$sb.Append('\r') }
            "`n" { [void]$sb.Append('\n') }
            "`t" { [void]$sb.Append('\t') }
            default { [void]$sb.Append($ch) }
        }
    }
    [void]$sb.Append('"')
    return $sb.ToString()
}

foreach ($root in $roots) {
    Write-Host "== $($root.Name)"

    # 1. Extension folders with no extension.vsixmanifest cannot be loaded by VS -
    #    they are leftovers of an interrupted install and must go.
    [string]$extDir = $root.FullName + '\Extensions'
    if (Test-Path -LiteralPath $extDir) {
        Get-ChildItem $extDir -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            if (-not (Test-Path -LiteralPath ($_.FullName + '\extension.vsixmanifest'))) {
                Write-Host "   removing stale extension folder $($_.Name)"
                Remove-Item -LiteralPath $_.FullName -Recurse -Force
            }
        }
    }

    # 2. Drop template registrations whose target file no longer exists. Two formats:
    #    InstalledTemplates.json          -> flat array of id strings
    #    RecentlyInstalledTemplates.json  -> array of { "Id": ..., "ShownCount": n }
    # ConvertFrom-Json is walked with an explicit foreach: on PowerShell 5.1 it hands
    # the whole top-level array over as a single object, so a Where-Object pipeline
    # would see $_ = Object[] instead of one entry.
    foreach ($name in @('InstalledTemplates.json', 'RecentlyInstalledTemplates.json')) {
        $file = $root.FullName + '\' + $name
        if (-not (Test-Path -LiteralPath $file)) { continue }
        try {
            $raw = Get-Content -LiteralPath $file -Raw
            if ([string]::IsNullOrWhiteSpace($raw)) { continue }
            $items = ConvertFrom-Json $raw

            $kept = New-Object System.Collections.Generic.List[string]
            $dead = 0
            foreach ($item in $items) {
                if ($null -eq $item) { continue }
                $isRecord = $item.PSObject.Properties.Match('Id').Count -gt 0
                $id = if ($isRecord) { [string]$item.Id } else { [string]$item }
                if ($id -like 'Extensions\*') {
                    $target = $extDir + '\' + $id.Substring('Extensions\'.Length)
                    if (-not (Test-Path -LiteralPath $target)) { $dead++; continue }
                }
                if ($isRecord) {
                    $shown = 0
                    if ($null -ne $item.ShownCount) { $shown = [int]$item.ShownCount }
                    $kept.Add('{"Id":' + (ConvertTo-JsonText $id) + ',"ShownCount":' + $shown + '}')
                }
                else {
                    $kept.Add((ConvertTo-JsonText $id))
                }
            }

            if ($dead -gt 0) {
                Write-Host "   pruning $dead dead entry(ies) from $name"
                if ($kept.Count -eq 0) { Remove-Item -LiteralPath $file -Force }
                else { ('[' + ($kept -join ',') + ']') | Set-Content -LiteralPath $file -Encoding UTF8 }
            }
        }
        catch {
            Write-Warning "   could not prune $name : $($_.Exception.Message)"
        }
    }

    # 3. Template cache must be rebuilt so the new template list is picked up.
    $vtc = $root.FullName + '\VTC'
    if (Test-Path -LiteralPath $vtc) {
        Write-Host "   clearing template cache (VTC)"
        Remove-Item -LiteralPath $vtc -Recurse -Force
    }
}

# 4. Install into every instance.
#
# VSIXInstaller.exe is a GUI-subsystem launcher: invoking it with & gives an empty
# $LASTEXITCODE because PowerShell does not wait for it. Start-Process -Wait -PassThru
# is used instead so a real exit code is always available.
$vsixId = 'ObjectARX.MultiYear.Wizard'
$installed = @{}

function Invoke-VsixInstaller([string]$installer, [string[]]$arguments) {
    $proc = Start-Process -FilePath $installer -ArgumentList $arguments -Wait -PassThru -ErrorAction Stop
    return $proc.ExitCode
}

# A leftover installer process from an earlier interrupted run blocks new installs.
Get-Process VSIXInstaller -ErrorAction SilentlyContinue | ForEach-Object {
    Write-Host "   stopping leftover VSIXInstaller process $($_.Id)"
    Stop-Process -Id $_.Id -Force
}

foreach ($entry in @(
        @{ Installer = 'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\VSIXInstaller.exe'; Prefix = '17.' },
        @{ Installer = 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\VSIXInstaller.exe'; Prefix = '18.' })) {

    $installer = $entry.Installer
    if (-not (Test-Path -LiteralPath $installer)) { continue }
    Write-Host "== installing into $(Split-Path $installer -Parent)"

    # The old build stays registered even after its files were removed, and that stale
    # registration makes the install pass bail out. Drop it first; 1002 means "was not
    # installed", which is just as good.
    $code = Invoke-VsixInstaller $installer @('/quiet', "/uninstall:$vsixId")
    Write-Host "   uninstall exit code: $code"

    $code = Invoke-VsixInstaller $installer @('/quiet', $vsix)
    switch ($code) {
        0 { Write-Host '   install exit code: 0 (installed)' }
        1001 { Write-Host '   install exit code: 1001 (this exact version was already installed)' }
        default { Write-Warning "   install exit code: $code" }
    }

    # Trust the file system over the exit code. VSIXInstaller may hand off to a child
    # process, so poll for a while before declaring failure.
    $root = @($roots | Where-Object { $_.Name.StartsWith($entry.Prefix, [System.StringComparison]::Ordinal) })
    if ($root.Count -eq 0) { continue }
    $found = @()
    for ($i = 0; $i -lt 30; $i++) {
        $found = @(Get-ChildItem ($root[0].FullName + '\Extensions') -Recurse -Filter 'NetWrapper.vstemplate' -ErrorAction SilentlyContinue)
        if ($found.Count -gt 0) { break }
        Start-Sleep -Seconds 2
    }
    $installed[$root[0].Name] = $found.Count -gt 0
    if ($found.Count -gt 0) { Write-Host "   verified: $($found[0].FullName)" }
    else { Write-Warning "   NOT verified: no NetWrapper.vstemplate under $($root[0].FullName)\Extensions" }
}

Write-Host ''
$failed = @($installed.Keys | Where-Object { -not $installed[$_] })
if ($failed.Count -gt 0) {
    Write-Warning "Not installed for: $($failed -join ', ')"
    Write-Host 'Run this script again from an elevated PowerShell window (Run as Administrator).'
}
else {
    Write-Host 'Installed for: ' + ($installed.Keys -join ', ')
}
Write-Host ''
Write-Host 'Start Visual Studio and check:'
Write-Host '  - New Project  -> no STG_E_FILENOTFOUND error'
Write-Host '  - Add New Item -> category "ArxWizard" -> "ObjectARX .NET Wrapper Class"'