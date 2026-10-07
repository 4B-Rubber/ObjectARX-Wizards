#Requires -Version 5.1
<#
  gen-arx-props.ps1 — 离线生成 ObjectARX per-year props。

  仓库唯一真相：arx-props-table.json + 三个骨架模板（props-template / props-net-fx-template /
  props-net-core-template）。安装器里的 CA (ArxWizCustomAction) 复用同一份表与骨架，所以
  这里生成的字节应与安装产物一致（用于本地校验与手工 regen）。

  用法：
    pwsh gen-arx-props.ps1                          # 全部年份 -> .\out
    pwsh gen-arx-props.ps1 -Years 2027 -OutDir D:\t
    pwsh gen-arx-props.ps1 -Root "D:\Autodesk\"    # 换根目录（只影响写进文件的默认值）
#>
[CmdletBinding()]
param(
  [string]$TablePath,
  [string]$TemplateDir,
  [string]$OutDir,
  [string[]]$Years,
  [string]$Root
)

$ErrorActionPreference = 'Stop'

$here = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
if (-not $TablePath)   { $TablePath   = Join-Path $here 'arx-props-table.json' }
if (-not $TemplateDir) { $TemplateDir = $here }
if (-not $OutDir)      { $OutDir      = Join-Path $here 'out' }

function Read-Template([string]$name) {
  (Get-Content -LiteralPath (Join-Path $TemplateDir $name) -Raw -Encoding UTF8) -replace "`r`n", "`n"
}

$table      = Get-Content -LiteralPath $TablePath -Raw -Encoding UTF8 | ConvertFrom-Json
$root       = if ($PSBoundParameters.ContainsKey('Root') -and $Root) { $Root } else { $table.defaultRoot }
if (-not $root.EndsWith('\')) { $root += '\' }

$normalTpl  = Read-Template 'props-template.props'
$netFxTpl   = Read-Template 'props-net-fx-template.props'
$netCoreTpl = Read-Template 'props-net-core-template.props'

if (-not (Test-Path -LiteralPath $OutDir)) { New-Item -ItemType Directory -Path $OutDir -Force | Out-Null }

# 行级占位符（整行）优先，其次内联；反复替换直到收敛，以便插入块里残留的占位符也被解析。
function Expand-Template {
  param([string]$Text, [hashtable]$Map)
  for ($pass = 0; $pass -lt 8; $pass++) {
    $before = $Text
    foreach ($k in @($Map.Keys)) {
      $v = [string]$Map[$k]
      $Text = [regex]::Replace($Text, "(?m)^[ \t]*@@" + [regex]::Escape($k) + "@@[ \t]*`n", { param($m) $v })
    }
    foreach ($k in @($Map.Keys)) {
      $v = [string]$Map[$k]
      $Text = [regex]::Replace($Text, "@@" + [regex]::Escape($k) + "@@", { param($m) $v })
    }
    if ($Text -ceq $before) { break }
  }
  return $Text
}

function New-TokenMap {
  param($y, [string]$toolsetOverride)

  $map = @{}
  $map.YEAR         = $y.year
  $map.PFV          = $y.projectFileVersion
  $map.SDKVERSION   = $y.sdkVersion
  $map.TOOLSET      = if ($toolsetOverride) { $toolsetOverride } else { $y.toolset }
  $map.TOOLSVERSION = $y.toolsVersion
  $map.CFGB         = "$($y.year)`B"
  $map.CFGS         = "$($y.year)`S"
  $map.CFGD         = "$($y.year)`d"
  $map.SDKROOT      = $root
  $map.ACADROOT     = $root
  $map.WIN32ROOT    = if ($y.win32X86) { $table.win32RootX86 } else { $root }
  $map.NETTFV       = $y.netTfVersion
  $map.NETTF        = $y.netTargetFramework

  $map.CRXNETCOND   = if ($y.hasCrx) { "'`$(ArxAppType)'=='crxnet' or " } else { '' }
  $map.ARXLIBINCS   = if ($y.libPathArxLibIncs) { '$(ArxLibIncs);' } else { '' }
  $map.TFCOMMENT    = if ($y.netTfComment) { ' <!-- That will force Platform Toolset to vc9 in Visual Studio 2010 -->' } else { '' }

  # --- 行级块 ---
  $map.USERIMPORT = "`t<Import Project=`"`$(MSBuildThisFileDirectory)ObjectARX.User.props`" Condition=`"Exists('`$(MSBuildThisFileDirectory)ObjectARX.User.props')`" />`n"
  $map.NO32COMMENT = if ($y.no32Comment) { "`t<!--There is No 32 Bit AutoCAD Starting From AutoCAD 2020-->`n" } else { '' }
  $map.WIN32ACAD = if ($y.hasWin32) { "`t`t<AcadDir Condition=`"'`$(Platform)'=='Win32' And '`$(AcadDir)' == ''`">@@WIN32ROOT@@AutoCAD @@YEAR@@\</AcadDir>`n" } else { '' }
  $map.ACADEXE = if ($y.hasCrx) {
    "`t`t<AcadExe Condition=`"'`$(ArxAppType)'=='dbx' or '`$(ArxAppType)'=='dbxnet' or '`$(ArxAppType)'=='arx' or '`$(ArxAppType)'=='arxnet'`">acad.exe</AcadExe>`n" +
    "`t`t<AcadExe Condition=`"'`$(ArxAppType)'=='crx' or '`$(ArxAppType)'=='crxnet'`">accoreconsole.exe</AcadExe>`n"
  } else { '' }
  $map.SDKINCS_WIN32 = if ($y.hasWin32) { "`t`t<ArxSdkIncs Condition=`"'`$(Platform)'=='Win32'`">`$(ArxSdkDir)\inc;`$(ArxSdkDir)\inc-win32</ArxSdkIncs>`n" } else { '' }
  $map.SDKLIBSS_WIN32 = if ($y.hasWin32) { "`t`t<ArxSdkLibs Condition=`"'`$(Platform)'=='Win32'`">`$(ArxSdkDir)\lib-win32</ArxSdkLibs>`n" } else { '' }
  $map.CRXIMPORT = if ($y.hasCrx) { "`t`t<Import Condition=`"'`$(ArxAppType)'=='crx' or '`$(ArxAppType)'=='crxnet'`" Project=`"`$(ArxSdkDir)\inc\crx.props`" />`n" } else { '' }
  $map.TF35BLOCK = if ($y.legacyV35) {
    "`t<PropertyGroup>`n`t`t<TargetFrameworkVersion>v3.5</TargetFrameworkVersion> <!-- That will force Platform Toolset to vc9 in Visual Studio 2010 -->`n`t</PropertyGroup>`n"
  } else { '' }
  $map.DBGCMD = if ($y.legacyV35) {
    "`t`t<LocalDebuggerCommand Condition=`"'`$(AcadDir)' != ''`">`$(AcadDir)\acad.exe</LocalDebuggerCommand>`n"
  } else {
    "`t`t<LocalDebuggerCommand>`$(AcadDir)`$(AcadExe)</LocalDebuggerCommand>`n"
  }
  $map.DBGCOMMENT = if ($y.debugComments) {
    "`t`t<!-- LocalDebuggerMergeEnvironment>true</LocalDebuggerMergeEnvironment -->`n" +
    "`t`t<!-- LocalDebuggerAttach>False</LocalDebuggerAttach -->`n" +
    "`t`t<!-- LocalDebuggerSQLDebugging>False</LocalDebuggerSQLDebugging -->`n"
  } else { '' }
  $map.CRXDEF = if ($y.hasCrx) { "`t`t`t<PreprocessorDefinitions Condition=`"'`$(ArxAppType)'=='crx' or '`$(ArxAppType)'=='crxnet'`">_CRXAPP;%(PreprocessorDefinitions)</PreprocessorDefinitions>`n" } else { '' }
  $map.TMWIN32 = if ($y.hasWin32) { "`t`t`t<TargetMachine Condition=`"'`$(Platform)'=='Win32'`">MachineX86</TargetMachine>`n" } else { '' }
  $map.FORACAD = if ($y.netForAcad) { "`t`t<!--  $($y.netForAcad) -->`n" } else { '' }

  return $map
}

$enc = New-Object System.Text.UTF8Encoding($true)
$written = New-Object System.Collections.Generic.List[string]

function Emit([string]$name, [string]$text) {
  $path = Join-Path $OutDir $name
  [System.IO.File]::WriteAllText($path, ($text -replace "`n", "`r`n"), $enc)
  $written.Add($name)
}

foreach ($y in $table.years) {
  if ($Years -and ($Years -notcontains $y.year)) { continue }

  $map = New-TokenMap $y $null
  Emit "Autodesk.arx-$($y.year).props" (Expand-Template $normalTpl $map)

  if ($y.compatToolset) {
    $c = New-TokenMap $y $y.compatToolset
    Emit "Autodesk.arx-$($y.year)-Compat.props" (Expand-Template $normalTpl $c)
  }

  $netTpl = if ($y.netKind -eq 'core') { $netCoreTpl } else { $netFxTpl }
  Emit "Autodesk.arx-$($y.year)-net.props" (Expand-Template $netTpl $map)
}

Write-Host ("Generated {0} file(s) into {1}:" -f $written.Count, $OutDir)
$written | Sort-Object | ForEach-Object { Write-Host "  $_" }