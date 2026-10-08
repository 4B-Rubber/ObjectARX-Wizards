; ============================================================================================
;  ObjectARX Multi-Year Wizards — Inno Setup installer (branch dev)
;
;  Replicates, on this branch, what the WiX line does on `main`:
;    * the wizard payload under a per-machine install folder,
;    * the target-year page (16 years, All / None / Invert / Compatible),
;    * install-time generation of Autodesk.arx-<year>.props for the ticked years,
;    * the HKLM registry contract the VS wizard reads back,
;    * the same four post-install file patches as the MSI custom actions,
;    * a silent VSIX install for the current user.
;
;  Where it deliberately differs from the MSI (all of it is a UI simplification, see
;  docs\Inno-Setup-Installer-Plan.md):
;    * the install folder is user-selectable, and the property sheet folder follows it by
;      default (the MSI's TARGETDIR was fixed and its props folder was a separate fixed one);
;    * one "Autodesk root" field instead of the MSI's four SDK/AutoCAD path fields. This is a
;      multi-year product, so the root is the prefix the generator appends the year to
;      (<root>ObjectARX 2026\, <root>AutoCAD 2026\) - nothing here appends a year itself;
;    * the UI is Chinese-first with English as fallback.
;
;  See docs\Inno-Setup-Installer-Plan.md for the behaviour matrix and the MSI quirks this avoids.
;
;  BUILD (both tools are installed outside PATH on the authoring machine):
;     msbuild tools\arx-genprops\arx-genprops.csproj -restore -p:Configuration=Release
;     & "C:\Program Files\Inno Setup 7\ISCC.exe" InnoSetupInstaller\ObjectARXMultiYearWizards.iss
;  Product: InnoSetupInstaller\Output\ObjectARXMultiYearWizardsSetup-Inno.exe
;  (The script holds Chinese text and therefore has to stay saved as UTF-8 *with* a BOM.)
;
;  The generator is shared with the MSI line: same year table and skeletons from
;  tools\arx-props, embedded verbatim. Its output is text-identical to
;  tools\arx-props\gen-arx-props.ps1 (verify with tools\arx-genprops\test-arx-genprops.ps1);
;  the only difference is that the .ps1 writes a UTF-8 BOM and this one does not, which is what
;  the MSI custom action does as well — the Inno line has to leave the same bytes on disk.
;
;  Silent / enterprise switches (also handy for testing):
;     /YEARS=2020,2024        initial year selection
;     /RDS=XYZ                registered developer symbol (ADSK -> XYZ in the HTML wizard);
;                             optional, and empty by default - an empty value leaves the
;                             ADSK placeholder alone, like the MSI custom action does
;     /DIR=<dir>              install folder (Inno's own switch)
;     /PROPSDIR=<dir>         property sheet folder (HKLM PropsDir); defaults to /DIR
;     /ARXROOT=<dir>          Autodesk root: the prefix the generated sheets use for the SDK and
;                             AutoCAD, and the value patched into the shipped 2026 template
;     /ARXSDKPATH=<dir>       ObjectARX SDK location (its inc folder is populated)
;     /VSROOT=<dir>           override the detected Visual Studio 2022 root
;     /SKIPVSIX=1             do not install the VSIX
;     /SKIPVSCHECK=1          install even while Visual Studio is running
; ============================================================================================

#define AppName "ObjectARX Multi-Year Wizards (2010-2027)"
#define AppVersion "0.1.2"
#define AppPublisher "Autodesk"
#define AppURL "http://www.autodesk.com/developautocad"
; Own identity: the Inno line is a separate product from the MSI line, so it must not share the
; MSI's UpgradeCode / ProductCode.
#define AppId "{{8B1E4C2A-5F37-4D9B-A6C1-0E7D3A9F5B24}"
#define RepoRoot ".."
#define GenExeSource RepoRoot + "\tools\arx-genprops\bin\Release\arx-genprops.exe"
#define VsixSource RepoRoot + "\ObjectARXMultiYearWizards.vsix"
#define AppIconSource RepoRoot + "\_Installs\VC\vcprojects\Autodesk\ArxAppWiz.ico"

[Setup]
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppURL}
AppUpdatesURL={#AppURL}
VersionInfoVersion={#AppVersion}.0
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}
; {autopf} is the 64-bit Program Files on an x64 install; the wizard payload is a modern x64
; product and the folder doubles as the default property sheet folder, so keep it out of (x86).
DefaultDirName={autopf}\Autodesk\{#AppName}
DisableProgramGroupPage=yes
DisableWelcomePage=no
AllowNoIcons=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=Output
OutputBaseFilename=ObjectARXMultiYearWizardsSetup-Inno
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
SetupIconFile={#AppIconSource}
UninstallDisplayIcon={uninstallexe}
; The bundle launcher refuses to start while Visual Studio is open; here it is checked in
; InitializeSetup instead, so nothing is touched before the check fails.
CloseApplications=no
RestartApplications=no

[Languages]
; Chinese first: Inno picks the language matching the system's UI language and falls back to the
; first entry, so a Chinese Windows gets Chinese and anything else gets English.
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
chinese.RdsCaption=注册开发者符号 (RDS)
chinese.RdsDesc=请指定创建新工程时使用的默认 RDS 前缀。可以留空，留空则保留原样。
chinese.RdsSubCaption=关于 RDS 符号的说明见 http://www.autodesk.com/objectarx -> ''Symbols Registration''。
chinese.RdsPrompt=注册开发者符号 (RDS)（可留空）(&R)：
chinese.PathsCaption=ObjectARX SDK 与目标位置
chinese.PathsDesc=安装向导会把这套向导写到本机，并按勾选的 AutoCAD 年份各生成一份属性表。
chinese.PathsSubCaption=SDK 或 AutoCAD 不在默认位置时才需要修改。
chinese.PathsRootPrompt=Autodesk 根目录（SDK 与 AutoCAD 都在它下面）(&A)：
chinese.PathsSdkPrompt=ObjectARX SDK 位置（会往它的 inc 目录写文件）(&O)：
chinese.PathsPropsPrompt=属性表目录（与 VS 向导共用）(&P)：
chinese.YearsCaption=目标 AutoCAD 版本
chinese.YearsDesc=勾选的年份各生成一份属性表，未勾选的不生成；勾了一个没装的年份没有副作用。
chinese.YearsLabel=AutoCAD 版本：
chinese.BtnAll=全部(&A)
chinese.BtnNone=全不选(&N)
chinese.BtnInvert=反选(&I)
chinese.BtnCompatible=兼容(&C)
chinese.ReadyInstallFolder=安装目录：
chinese.ReadyRds=注册开发者符号 (RDS)：
chinese.ReadyRoot=Autodesk 根目录：
chinese.ReadySdk=ObjectARX SDK 位置：
chinese.ReadyProps=属性表目录：
chinese.ReadyYears=目标 AutoCAD 版本：
chinese.NoneSelected=无。不生成任何属性表，并且会把以前生成的清掉。
chinese.VsMissing=本机必须安装 Microsoft Visual Studio Professional/Enterprise/Community 2022。
chinese.VsRunning=请先关闭 Visual Studio 再安装。%n%n当前正在运行：%1%n%n本安装包要把 ObjectARX 向导装进 Visual Studio，Visual Studio 开着时这一步无法完成。目前还没有写入任何东西——关掉 Visual Studio 后重新运行即可。
english.RdsCaption=Registered Developer Symbol (RDS)
english.RdsDesc=Please specify the symbol used as the default RDS prefix when creating new projects. This is optional; leave it empty to keep the placeholder as it is.
english.RdsSubCaption=Find more information about RDS symbols at http://www.autodesk.com/objectarx -> ''Symbols Registration''.
english.RdsPrompt=&Registered Developer Symbol (RDS) (optional):
english.PathsCaption=ObjectARX SDK and Target Locations
english.PathsDesc=The installer writes its wizards to this computer and generates one property sheet per ticked AutoCAD year.
english.PathsSubCaption=Only change these when the SDK or AutoCAD is not in the default location.
english.PathsRootPrompt=&Autodesk root folder (the SDK and AutoCAD live under it):
english.PathsSdkPrompt=&ObjectARX SDK location (its inc folder is populated):
english.PathsPropsPrompt=&Property sheet folder (shared with the VS wizard):
english.YearsCaption=Target AutoCAD Versions
english.YearsDesc=One property sheet per ticked year is generated; unticked years are not created, and ticking a year you do not have installed is harmless.
english.YearsLabel=AutoCAD versions:
english.BtnAll=&All
english.BtnNone=&None
english.BtnInvert=&Invert
english.BtnCompatible=&Compatible
english.ReadyInstallFolder=Install folder:
english.ReadyRds=Registered developer symbol (RDS):
english.ReadyRoot=Autodesk root folder:
english.ReadySdk=ObjectARX SDK location:
english.ReadyProps=Property sheet folder:
english.ReadyYears=Target AutoCAD versions:
english.NoneSelected=None. No property sheet is generated, and any generated earlier is removed.
english.VsMissing=Microsoft Visual Studio Professional/Enterprise/Community 2022 must be present on the target machine.
english.VsRunning=Please close Visual Studio before installing.%n%nRunning now: %1%n%nThe installer adds the ObjectARX wizards to Visual Studio, and that step cannot run while Visual Studio is open. Nothing has been installed yet - close Visual Studio and start the setup again.

[Files]
; ---- install folder root (MSI component C__2C63244EA9004D42B1A8ED79330F5B73) ----
; No Autodesk.arx-<year>.props ships as payload: arx-genprops generates the ticked years at install
; time, so a hard-wired single-year sheet would only go stale (or collide with the generated one,
; the property sheet folder being the install folder by default).
Source: "{#RepoRoot}\_Installs\rxsdk_common.props"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#RepoRoot}\_Installs\crx.props"; DestDir: "{app}"; Flags: ignoreversion

; ---- wizard payload trees ----
Source: "{#RepoRoot}\ArxWizCommon\arxCommon.js"; DestDir: "{app}\ArxWizCommon"; Flags: ignoreversion
Source: "{#RepoRoot}\ArxAppWiz\*"; DestDir: "{app}\ArxAppWiz"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\ArxAppWiz182\*"; DestDir: "{app}\ArxAppWiz182"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\ArxAtlWizComWrapper\*"; DestDir: "{app}\ArxAtlWizComWrapper"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\ArxAtlWizDynProp\*"; DestDir: "{app}\ArxAtlWizDynProp"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\ArxWizCustomObject\*"; DestDir: "{app}\ArxWizCustomObject"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\ArxWizJig\*"; DestDir: "{app}\ArxWizJig"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\ArxWizMFCSupport\*"; DestDir: "{app}\ArxWizMFCSupport"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\ArxWizNETWrapper\*"; DestDir: "{app}\ArxWizNETWrapper"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#RepoRoot}\ArxWizReactors\*"; DestDir: "{app}\ArxWizReactors"; Flags: ignoreversion recursesubdirs createallsubdirs

; ---- ObjectARX SDK \inc. Permanent, like the MSI component C__3CB44E97... (Permanent="yes"). ----
Source: "{#RepoRoot}\_Installs\rxsdk_common.props"; DestDir: "{code:GetArxSdkIncDir}"; Flags: ignoreversion uninsneveruninstall
Source: "{#RepoRoot}\_Installs\crx.props"; DestDir: "{code:GetArxSdkIncDir}"; Flags: ignoreversion uninsneveruninstall
Source: "{#RepoRoot}\_Installs\arxEntryPoint.h"; DestDir: "{code:GetArxSdkIncDir}"; Flags: ignoreversion uninsneveruninstall

; ---- shared property sheets. Permanent (MSI component C_OBJARX_PROPS_GLOBAL, Permanent="yes"),
;      so uninstall deliberately leaves them behind; that is also why the property sheet folder
;      does not have to be emptied. Autodesk.arx-*.props are NOT payload: arx-genprops generates
;      the ticked years instead. ----
Source: "{#RepoRoot}\_Installs\ObjectARX Props\*"; DestDir: "{code:GetPropsDir}"; Excludes: "Autodesk.arx-*.props"; Flags: ignoreversion uninsneveruninstall

; ---- Visual Studio 2022 integration. The MSI guards these with
;      <Condition>VS2022_ROOT_FOLDER &lt;&gt; TARGETDIR</Condition>. ----
Source: "{#RepoRoot}\_Installs\VC\vcprojects\Autodesk\*"; DestDir: "{code:GetVsVcProjectsDir}"; Flags: ignoreversion; Check: VsAvailable
Source: "{#RepoRoot}\_Installs\VC\VCAddClass\ObjectARX\*"; DestDir: "{code:GetVsVcProjectItemsDir}"; Excludes: "Maya*"; Flags: ignoreversion; Check: VsAvailable

; ---- used from [Code] only (extracted with ExtractTemporaryFile when needed) ----
Source: "{#GenExeSource}"; Flags: dontcopy
Source: "{#VsixSource}"; Flags: dontcopy

[Registry]
; The registry contract the VS wizard and the legacy HTML wizard read back
; (ArxVsixWizard\Models\ArxVersion.cs, ArxWizCommon\arxCommon.js). ArxRoot and AcadRoot carry the
; same value on purpose: this is a multi-year product, so both point at the Autodesk root and the
; generated sheets append the year themselves.
Root: HKLM64; Subkey: "SOFTWARE\Autodesk\ObjectARX Wizards"; ValueType: string; ValueName: "PropsDir"; ValueData: "{code:GetPropsDir}"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "SOFTWARE\Autodesk\ObjectARX Wizards"; ValueType: string; ValueName: "ArxRoot"; ValueData: "{code:GetArxRoot}"
Root: HKLM64; Subkey: "SOFTWARE\Autodesk\ObjectARX Wizards"; ValueType: string; ValueName: "AcadRoot"; ValueData: "{code:GetArxRoot}"

[Code]
const
  RegKey = 'SOFTWARE\Autodesk\ObjectARX Wizards';
  CsvYearsLeft = '2010,2012,2013,2014,2015,2016,2018,2019';
  CsvYearsRight = '2020,2021,2022,2023,2024,2025,2026,2027';
  CsvDefaultYears = '2020,2024,2026,2027';
  CsvCompatibleYears = '2014,2016,2018,2020,2024,2026,2027';
  FallbackArxRoot = 'C:\Program Files\Autodesk\';
  FallbackArxSdkPath = 'C:\ObjectARX';
  GenExeName = 'arx-genprops.exe';
  VsixName = 'ObjectARXMultiYearWizards.vsix';
  VsVcRelPath = '\Common7\IDE\VC';
  VsVsixInstallerRelPath = '\Common7\IDE\VSIXInstaller.exe';

var
  PageRds: TInputQueryWizardPage;
  PagePaths: TInputDirWizardPage;
  PageYears: TWizardPage;
  BoxLeft, BoxRight: TNewCheckListBox;
  LeftYears, RightYears: TArrayOfString;

  VsRoot: string;
  VsFound: Boolean;
  PrevPropsDir: string;
  UninstallPropsDir: string;
  LogFile: string;

  InitRds, InitArxRoot, InitArxSdkPath, InitPropsDir: string;
  // The install folder, tracked from the wizard's own directory edit. {app} cannot be used here:
  // it is not initialized yet while the wizard is being built.
  PropsDefault: string;
  // The property sheet folder follows the install folder until the user types into that field or
  // passes /PROPSDIR. PropsAuto holds the value we last put there ourselves, so a value that still
  // equals it counts as "untouched"; PropsPinned means /PROPSDIR already decided.
  PropsAuto: string;
  PropsPinned: Boolean;

// ---------------------------------------------------------------------------------------------
// small helpers
// ---------------------------------------------------------------------------------------------

function Cm(const Name: string): string;
begin
  Result := ExpandConstant('{cm:' + Name + '}');
end;

procedure Alog(const Msg: string);
var
  S: AnsiString;
begin
  Log('[arx] ' + Msg);
  if LogFile = '' then Exit;
  S := GetDateTimeString('yyyy-mm-dd hh:nn:ss', '-', ':') + ' ' + Msg + #13#10;
  SaveStringToFile(LogFile, S, True);
end;

function Chomp(const S: string): string;
begin
  Result := S;
  while (Result <> '') and ((Result[Length(Result)] = #13) or (Result[Length(Result)] = #10)) do
    Delete(Result, Length(Result), 1);
  Result := Trim(Result);
end;

function TrimTrailingSlash(const S: string): string;
begin
  Result := S;
  while (Result <> '') and (Result[Length(Result)] = '\') do
    Delete(Result, Length(Result), 1);
end;

/// <summary>
/// The Autodesk root and the SDK path are used as *prefixes*: the shipped 2026 template and the
/// generated sheets concatenate them with the year ("$(ArxSdkRoot)ObjectARX <year>\"). A trailing
/// backslash is part of the value, but the directory wizard trims it - without this normalisation
/// the generated sheets would read "C:\Program Files\AutodeskObjectARX 2020\".
/// </summary>
function EnsureTrailingSlash(const S: string): string;
begin
  Result := TrimTrailingSlash(S);
  if Result <> '' then Result := Result + '\';
end;

/// <summary>
/// Quotes a command-line argument. A backslash immediately before the closing quote would escape
/// it, so every trailing backslash has to be doubled - without this, passing the Autodesk root
/// (which legitimately ends in "\") makes CreateProcess swallow the rest of the command line into
/// that one argument.
/// </summary>
function QuoteArg(const S: string): string;
var
  T: string;
  n, i: Integer;
begin
  T := S;
  n := 0;
  i := Length(T);
  while (i > 0) and (T[i] = '\') do begin
    Inc(n);
    Dec(i);
  end;
  for i := 1 to n do T := T + '\';
  Result := '"' + T + '"';
end;

function SplitCsv(const S: string; var Parts: TArrayOfString): Integer;
var
  i, n: Integer;
  Cur: string;
begin
  n := 0;
  Cur := '';
  SetArrayLength(Parts, 0);
  for i := 1 to Length(S) do
    if S[i] = ',' then begin
      if Trim(Cur) <> '' then begin
        SetArrayLength(Parts, n + 1);
        Parts[n] := Trim(Cur);
        Inc(n);
      end;
      Cur := '';
    end else
      Cur := Cur + S[i];
  if Trim(Cur) <> '' then begin
    SetArrayLength(Parts, n + 1);
    Parts[n] := Trim(Cur);
    Inc(n);
  end;
  Result := n;
end;

function CsvHas(const Csv, Item: string): Boolean;
begin
  Result := Pos(',' + Item + ',', ',' + Csv + ',') > 0;
end;

function CsvToDisplay(const Csv: string): string;
var
  T: string;
begin
  if Csv = '' then
    Result := Cm('NoneSelected')
  else begin
    T := Csv;
    StringChangeEx(T, ',', ', ', False);
    Result := T;
  end;
end;

function PosFrom(const SubStr, S: string; FromIndex: Integer): Integer;
begin
  if (FromIndex < 1) or (FromIndex > Length(S)) then begin
    Result := 0;
    Exit;
  end;
  Result := Pos(SubStr, Copy(S, FromIndex, Length(S) - FromIndex + 1));
  if Result > 0 then Result := Result + FromIndex - 1;
end;

function ReadTextFile(const Path: string; var S: string): Boolean;
var
  A: AnsiString;
begin
  Result := LoadStringFromFile(Path, A);
  if Result then S := A;
end;

function WriteTextFile(const Path, S: string): Boolean;
var
  A: AnsiString;
begin
  A := S;
  Result := SaveStringToFile(Path, A, False);
end;

// ---------------------------------------------------------------------------------------------
// page value getters — they feed {code:...} in [Files] and [Registry], so they must keep
// working when the wizard pages were never created (silent runs included).
// ---------------------------------------------------------------------------------------------

function Pick(const EditValue, InitValue: string): string;
begin
  if EditValue <> '' then Result := EditValue else Result := InitValue;
end;

function GetPropsDir(Param: string): string;
begin
  // The property sheet folder defaults to the install folder.
  if Assigned(PagePaths) then Result := Pick(PagePaths.Values[2], PropsDefault)
  else Result := PropsDefault;
end;

function GetArxRoot(Param: string): string;
begin
  if Assigned(PagePaths) then Result := Pick(PagePaths.Values[0], InitArxRoot)
  else Result := InitArxRoot;
  Result := EnsureTrailingSlash(Result);
end;

function GetArxSdkPath(Param: string): string;
begin
  if Assigned(PagePaths) then Result := Pick(PagePaths.Values[1], InitArxSdkPath)
  else Result := InitArxSdkPath;
  Result := EnsureTrailingSlash(Result);
end;

function GetArxSdkIncDir(Param: string): string;
begin
  Result := AddBackslash(TrimTrailingSlash(GetArxSdkPath(''))) + 'inc';
end;

function GetRds(Param: string): string;
begin
  if Assigned(PageRds) then Result := Pick(PageRds.Values[0], InitRds)
  else Result := InitRds;
end;

function GetVsVcProjectsDir(Param: string): string;
begin
  Result := TrimTrailingSlash(VsRoot) + VsVcRelPath + '\vcprojects\Autodesk';
end;

function GetVsVcProjectItemsDir(Param: string): string;
begin
  Result := TrimTrailingSlash(VsRoot) + VsVcRelPath + '\vcprojectitems\ObjectARX';
end;

function VsAvailable(): Boolean;
begin
  Result := VsFound;
end;

// ---------------------------------------------------------------------------------------------
// Visual Studio 2022
// ---------------------------------------------------------------------------------------------

function RunAndCapture(const CmdLine, OutFile: string): string;
var
  Code: Integer;
  S: string;
begin
  Result := '';
  DeleteFile(OutFile);
  if not Exec(ExpandConstant('{cmd}'), '/c ' + CmdLine + ' > "' + OutFile + '" 2>nul', '',
              SW_HIDE, ewWaitUntilTerminated, Code) then Exit;
  if ReadTextFile(OutFile, S) then Result := Chomp(S);
  DeleteFile(OutFile);
end;

/// <summary>vswhere is the supported locator; the standard edition folders are the fallback.</summary>
function FindVs2022Root(): string;
var
  Vswhere: string;
begin
  Result := '';
  Vswhere := ExpandConstant('{pf32}') + '\Microsoft Visual Studio\Installer\vswhere.exe';
  if FileExists(Vswhere) then
    Result := RunAndCapture('""' + Vswhere + '" -latest -prerelease -products * -version "[17.0,18.0)" -property installationPath',
                            ExpandConstant('{%TEMP}') + '\arx-vswhere.txt');
  if (Result <> '') and DirExists(Result) then Exit;

  if DirExists(ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\Enterprise') then
    Result := ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\Enterprise'
  else if DirExists(ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\Professional') then
    Result := ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\Professional'
  else if DirExists(ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\Community') then
    Result := ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\Community'
  else if DirExists(ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\BuildTools') then
    Result := ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\BuildTools'
  else if DirExists(ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\Preview') then
    Result := ExpandConstant('{pf64}') + '\Microsoft Visual Studio\2022\Preview'
  else
    Result := '';
end;

function OnOff(const B: Boolean): string;
begin
  if B then Result := 'yes' else Result := 'no';
end;

/// <summary>The two processes VSIXInstaller refuses to work through. Mirrors MsiSetup.exe.</summary>
function FindRunningVs(): string;
var
  Captured: string;
begin
  Result := '';
  Captured := RunAndCapture('tasklist /FI "IMAGENAME eq devenv.exe" /NH',
                            ExpandConstant('{%TEMP}') + '\arx-tasklist1.txt');
  if Pos('devenv.exe', Captured) > 0 then Result := 'devenv.exe';
  if Result <> '' then Exit;
  Captured := RunAndCapture('tasklist /FI "IMAGENAME eq DevHub.exe" /NH',
                            ExpandConstant('{%TEMP}') + '\arx-tasklist2.txt');
  if Pos('DevHub.exe', Captured) > 0 then Result := 'DevHub.exe';
end;

// ---------------------------------------------------------------------------------------------
// setup lifecycle
// ---------------------------------------------------------------------------------------------

function InitializeSetup(): Boolean;
var
  Blocker: string;
begin
  Result := True;
  LogFile := ExpandConstant('{%TEMP}') + '\ObjectARXWizards-Inno.log';
  Alog('InitializeSetup');

  if not RegQueryStringValue(HKLM64, RegKey, 'PropsDir', PrevPropsDir) then PrevPropsDir := '';

  // page defaults: an explicit switch wins, then the previous install, then the shipped default.
  // RDS is optional: empty (the default) means "leave the ADSK placeholder in the HTML wizard
  // alone", which is exactly what the MSI's PatchHTMLWizFiles custom action does with an empty
  // RDS - it substitutes ADSK with ADSK. Substituting an empty string instead would delete text
  // the MSI never touches.
  InitRds := ExpandConstant('{param:RDS|}');
  InitArxRoot := ExpandConstant('{param:ARXROOT|}');
  if InitArxRoot = '' then
    if not RegQueryStringValue(HKLM64, RegKey, 'ArxRoot', InitArxRoot) then InitArxRoot := FallbackArxRoot;
  InitArxSdkPath := ExpandConstant('{param:ARXSDKPATH|' + FallbackArxSdkPath + '}');
  // Empty means "follow the install folder"; InitializeWizard fills the field in. The property
  // sheet folder is not remembered across installs on purpose - it belongs to the install folder,
  // and Inno already remembers that one.
  InitPropsDir := ExpandConstant('{param:PROPSDIR|}');
  PropsPinned := InitPropsDir <> '';

  VsRoot := ExpandConstant('{param:VSROOT|}');
  if VsRoot = '' then VsRoot := FindVs2022Root();
  VsFound := (VsRoot <> '') and DirExists(VsRoot);
  Alog('VS 2022 root: ' + VsRoot + ' (found=' + OnOff(VsFound) + ')');

  if not VsFound then begin
    MsgBox(Cm('VsMissing'), mbCriticalError, MB_OK);
    Result := False;
    Exit;
  end;

  if ExpandConstant('{param:SKIPVSCHECK|}') <> '1' then begin
    Blocker := FindRunningVs();
    if Blocker <> '' then begin
      MsgBox(FmtMessage(Cm('VsRunning'), [Blocker]), mbCriticalError, MB_OK);
      Result := False;
    end;
  end;
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  if not RegQueryStringValue(HKLM64, RegKey, 'PropsDir', UninstallPropsDir) then
    UninstallPropsDir := '';
end;

// ---------------------------------------------------------------------------------------------
// year page
// ---------------------------------------------------------------------------------------------

procedure RefreshYearBoxes(const Csv: string);
var
  i: Integer;
begin
  for i := 0 to GetArrayLength(LeftYears) - 1 do
    BoxLeft.Checked[i] := CsvHas(Csv, LeftYears[i]);
  for i := 0 to GetArrayLength(RightYears) - 1 do
    BoxRight.Checked[i] := CsvHas(Csv, RightYears[i]);
end;

function GetSelectedYearsCsv(): string;
var
  i: Integer;
begin
  Result := '';
  for i := 0 to GetArrayLength(LeftYears) - 1 do
    if BoxLeft.Checked[i] then Result := Result + LeftYears[i] + ',';
  for i := 0 to GetArrayLength(RightYears) - 1 do
    if BoxRight.Checked[i] then Result := Result + RightYears[i] + ',';
  if (Result <> '') and (Result[Length(Result)] = ',') then
    Delete(Result, Length(Result), 1);
end;

/// <summary>Every year whose props already exist in the previous folder was ticked before.</summary>
function DetectInstalledYearsCsv(const PropsDir: string): string;
var
  i: Integer;
  All: TArrayOfString;
  Dir: string;
begin
  Result := '';
  Dir := AddBackslash(TrimTrailingSlash(PropsDir));
  SplitCsv(CsvYearsLeft + ',' + CsvYearsRight, All);
  for i := 0 to GetArrayLength(All) - 1 do
    if FileExists(Dir + 'Autodesk.arx-' + All[i] + '.props') then
      Result := Result + All[i] + ',';
  if (Result <> '') and (Result[Length(Result)] = ',') then
    Delete(Result, Length(Result), 1);
end;

procedure OnYearsAll(Sender: TObject);
begin
  RefreshYearBoxes(CsvYearsLeft + ',' + CsvYearsRight);
end;

procedure OnYearsNone(Sender: TObject);
begin
  RefreshYearBoxes('');
end;

procedure OnYearsInvert(Sender: TObject);
var
  i: Integer;
begin
  for i := 0 to GetArrayLength(LeftYears) - 1 do
    BoxLeft.Checked[i] := not BoxLeft.Checked[i];
  for i := 0 to GetArrayLength(RightYears) - 1 do
    BoxRight.Checked[i] := not BoxRight.Checked[i];
end;

procedure OnYearsCompatible(Sender: TObject);
begin
  RefreshYearBoxes(CsvCompatibleYears);
end;

/// <summary>Default selection: re-tick what the previous install generated, else the shipped set.</summary>
function DefaultYearSelection(): string;
begin
  Result := DetectInstalledYearsCsv(GetPropsDir(''));
  if Result = '' then Result := CsvDefaultYears;
end;

procedure AddYearBox(const Parent: TWinControl; const Csv: string; var Box: TNewCheckListBox;
                     const ALeft, AWidth, ATop: Integer);
var
  Years: TArrayOfString;
  i: Integer;
begin
  SplitCsv(Csv, Years);
  Box := TNewCheckListBox.Create(PageYears);
  Box.Parent := Parent;
  Box.Left := ALeft;
  Box.Top := ATop;
  Box.Width := AWidth;
  Box.Height := PageYears.SurfaceHeight - ATop - ScaleY(6);
  for i := 0 to GetArrayLength(Years) - 1 do
    Box.AddCheckBox(Years[i], '', 0, False, True, False, False, nil);
end;

procedure AddYearButton(const ACaption: string; const ALeft, AWidth: Integer; const AOnClick: TNotifyEvent);
var
  Btn: TNewButton;
begin
  Btn := TNewButton.Create(PageYears);
  Btn.Parent := PageYears.Surface;
  Btn.Left := ALeft;
  Btn.Top := ScaleY(2);
  Btn.Width := AWidth;
  Btn.Height := ScaleY(21);
  Btn.Caption := ACaption;
  Btn.OnClick := AOnClick;
end;

procedure InitializeWizard();
var
  Lbl: TNewStaticText;
  BtnW, Gap, Y, ColW, Right: Integer;
begin
  SplitCsv(CsvYearsLeft, LeftYears);
  SplitCsv(CsvYearsRight, RightYears);

  // {app} is not initialized yet at this point, and the select-directory page holds the very value
  // it will be set to, so track the install folder from there.
  PropsDefault := TrimTrailingSlash(WizardForm.DirEdit.Text);

  // The pages come after wpSelectDir so the install folder is chosen first; the property sheet
  // folder then follows it.
  PageRds := CreateInputQueryPage(wpSelectDir,
    Cm('RdsCaption'), Cm('RdsDesc'), Cm('RdsSubCaption'));
  PageRds.Add(Cm('RdsPrompt'), False);
  PageRds.Values[0] := InitRds;

  PagePaths := CreateInputDirPage(PageRds.ID,
    Cm('PathsCaption'), Cm('PathsDesc'), Cm('PathsSubCaption'),
    False, '');
  PagePaths.Add(Cm('PathsRootPrompt'));
  PagePaths.Add(Cm('PathsSdkPrompt'));
  PagePaths.Add(Cm('PathsPropsPrompt'));
  PagePaths.Values[0] := InitArxRoot;
  PagePaths.Values[1] := InitArxSdkPath;
  PagePaths.Values[2] := Pick(InitPropsDir, PropsDefault);
  PropsAuto := PagePaths.Values[2];

  PageYears := CreateCustomPage(PagePaths.ID, Cm('YearsCaption'), Cm('YearsDesc'));

  Lbl := TNewStaticText.Create(PageYears);
  Lbl.Parent := PageYears.Surface;
  Lbl.Left := 0;
  Lbl.Top := ScaleY(2);
  Lbl.Caption := Cm('YearsLabel');

  BtnW := ScaleX(56);
  Gap := ScaleX(6);
  Right := PageYears.SurfaceWidth;
  AddYearButton(Cm('BtnAll'), Right - 3 * BtnW - ScaleX(96) - 3 * Gap, BtnW, @OnYearsAll);
  AddYearButton(Cm('BtnNone'), Right - 2 * BtnW - ScaleX(96) - 2 * Gap, BtnW, @OnYearsNone);
  AddYearButton(Cm('BtnInvert'), Right - BtnW - ScaleX(96) - Gap, BtnW, @OnYearsInvert);
  AddYearButton(Cm('BtnCompatible'), Right - ScaleX(96), ScaleX(96), @OnYearsCompatible);

  Y := ScaleY(28);
  ColW := (PageYears.SurfaceWidth - ScaleX(8)) div 2;
  AddYearBox(PageYears.Surface, CsvYearsLeft, BoxLeft, 0, ColW, Y);
  AddYearBox(PageYears.Surface, CsvYearsRight, BoxRight, ColW + ScaleX(8), ColW, Y);

  // A first install ticks the shipped default; an upgrade re-ticks what the previous install
  // generated, exactly like the MSI's DET_YEAR_* detection. /YEARS= wins over both.
  if ExpandConstant('{param:YEARS|}') <> '' then
    RefreshYearBoxes(ExpandConstant('{param:YEARS|}'))
  else
    RefreshYearBoxes(DefaultYearSelection());
end;

procedure CurPageChanged(CurPageID: Integer);
var
  App: string;
begin
  if (PagePaths = nil) then Exit;
  App := TrimTrailingSlash(WizardForm.DirEdit.Text);
  if App = '' then Exit;
  PropsDefault := App;
  if PropsPinned or (CurPageID <> PagePaths.ID) then Exit;
  // Still on the value we put there ourselves, so keep it in step with the install folder.
  if TrimTrailingSlash(PagePaths.Values[2]) = TrimTrailingSlash(PropsAuto) then begin
    PagePaths.Values[2] := App;
    PropsAuto := App;
  end;
end;

function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
                         MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: string): string;
begin
  Result :=
    Cm('ReadyInstallFolder') + NewLine + Space + PropsDefault + NewLine + NewLine +
    Cm('ReadyRds') + NewLine + Space + GetRds('') + NewLine + NewLine +
    Cm('ReadyRoot') + NewLine + Space + GetArxRoot('') + NewLine + NewLine +
    Cm('ReadySdk') + NewLine + Space + GetArxSdkPath('') + NewLine + NewLine +
    Cm('ReadyProps') + NewLine + Space + GetPropsDir('') + NewLine + NewLine +
    Cm('ReadyYears') + NewLine + Space + CsvToDisplay(GetSelectedYearsCsv()) + NewLine;
end;

// ---------------------------------------------------------------------------------------------
// post-install work: the props generator and the four file patches
// ---------------------------------------------------------------------------------------------

procedure RunGen(const Args: string);
var
  Code: Integer;
begin
  ExtractTemporaryFile(GenExeName);
  if Exec(ExpandConstant('{tmp}\' + GenExeName), Args + ' --log ' + QuoteArg(LogFile), '',
          SW_HIDE, ewWaitUntilTerminated, Code) then
    Alog('arx-genprops [' + IntToStr(Code) + '] ' + Args)
  else
    Alog('arx-genprops could not be started: ' + Args);
end;

/// <summary>
/// Replaces every occurrence of FromStr. Only writes when something really changed, so files that
/// are not ASCII (a few of the legacy HTML pages are UTF-8) are never rewritten needlessly - the
/// MSI's custom actions rewrote them through UTF-8 and this keeps that behaviour without having to
/// carry an encoding-aware reader around.
/// </summary>
function PatchFile(const Path, FromStr, ToStr: string): Boolean;
var
  S, T: string;
begin
  Result := False;
  if not FileExists(Path) then Exit;
  if not ReadTextFile(Path, S) then Exit;
  if Pos(FromStr, S) <= 0 then Exit;
  T := S;
  StringChangeEx(T, FromStr, ToStr, True);
  if T = S then Exit;   // the placeholder was already the right value; do not rewrite the file
  Result := WriteTextFile(Path, T);
  Alog('patched ' + Path + ' (' + FromStr + ' -> ' + ToStr + ')');
end;

/// <summary>CA_PatchVSFiles: the [TARGETDIR] placeholder in the nine project wizards.</summary>
procedure PatchVsFiles(const AppDir: string);
var
  Vc: string;
  Target: string;
begin
  if not VsFound then Exit;
  Vc := TrimTrailingSlash(VsRoot) + VsVcRelPath;
  Target := AddBackslash(AppDir);
  // Add-project wizards (MSI: _Installs\VC\vcprojects\Autodesk)
  PatchFile(Vc + '\vcprojects\Autodesk\ArxAppWiz.vsz', '[TARGETDIR]', Target);
  PatchFile(Vc + '\vcprojects\Autodesk\ArxAppWizOMF.vsz', '[TARGETDIR]', Target);
  // Add-class / add-object wizards (MSI: _Installs\VC\VCAddClass\ObjectARX)
  PatchFile(Vc + '\vcprojectitems\ObjectARX\ArxAtlWizComWrapper.vsz', '[TARGETDIR]', Target);
  PatchFile(Vc + '\vcprojectitems\ObjectARX\ArxAtlWizDynProp.vsz', '[TARGETDIR]', Target);
  PatchFile(Vc + '\vcprojectitems\ObjectARX\ArxWizCustomObject.vsz', '[TARGETDIR]', Target);
  PatchFile(Vc + '\vcprojectitems\ObjectARX\ArxWizJig.vsz', '[TARGETDIR]', Target);
  PatchFile(Vc + '\vcprojectitems\ObjectARX\ArxWizMFCSupport.vsz', '[TARGETDIR]', Target);
  PatchFile(Vc + '\vcprojectitems\ObjectARX\ArxWizNETWrapper.vsz', '[TARGETDIR]', Target);
  PatchFile(Vc + '\vcprojectitems\ObjectARX\ArxWizReactors.vsz', '[TARGETDIR]', Target);
end;

/// <summary>CA_PatchHTMLWizFiles: ADSK -> the chosen RDS, in the AppWiz HTML pages only.</summary>
procedure PatchHtmlWizardFiles(const AppDir: string);
var
  Rds: string;
begin
  Rds := GetRds('');
  if Rds = '' then begin
    Alog('RDS is empty; leaving the ADSK placeholder in the HTML wizard alone');
    Exit;
  end;
  PatchFile(AppDir + '\ArxAppWiz\HTML\1033\default.htm', 'ADSK', Rds);
  PatchFile(AppDir + '\ArxAppWiz182\HTML\1033\default.htm', 'ADSK', Rds);
end;

/// <summary>CA_PatchArxCommonJsFiles: the legacy HTML wizard has the props folder hardcoded.</summary>
procedure PatchArxCommonJs(const AppDir, PropsDir: string);
var
  Path, S, JsPath, Clean: string;
  i, P1, P2: Integer;
begin
  Path := AppDir + '\ArxWizCommon\arxCommon.js';
  if not FileExists(Path) then Exit;
  if not ReadTextFile(Path, S) then Exit;

  Clean := TrimTrailingSlash(PropsDir);
  if Clean = '' then Exit;
  // JavaScript string literal: every backslash has to be doubled.
  JsPath := '';
  for i := 1 to Length(Clean) do
    if Clean[i] = '\' then JsPath := JsPath + '\\' else JsPath := JsPath + Clean[i];
  JsPath := JsPath + '\\';

  P1 := Pos('var ARX_PROPS_DIR', S);
  if P1 <= 0 then Exit;
  P1 := PosFrom('"', S, P1);
  if P1 <= 0 then Exit;
  P2 := PosFrom('"', S, P1 + 1);
  if P2 <= 0 then Exit;

  // Copy(S, 1, P1) keeps the opening quote; starting the tail at P2 keeps the closing one.
  S := Copy(S, 1, P1) + JsPath + Copy(S, P2, Length(S) - P2 + 1);
  if WriteTextFile(Path, S) then
    Alog('patched ' + Path + ' (ARX_PROPS_DIR -> ' + JsPath + ')');
end;

/// <summary>
/// The bundle chains VsixSetup.exe for this, precisely because the MSI is per-machine and the VSIX
/// must not be (Bundle.wxs: "the VSIX must not be [per-machine], hence the extra process").
/// ExecAsOriginalUser is the Inno equivalent: VSIXInstaller runs unelevated, so the extension lands
/// in the user's own VS profile instead of the machine-wide extension folder.
/// </summary>
procedure InstallVsix();
var
  Installer, Vsix: string;
  Code: Integer;
begin
  Vsix := ExpandConstant('{tmp}\' + VsixName);
  ExtractTemporaryFile(VsixName);
  Installer := TrimTrailingSlash(VsRoot) + VsVsixInstallerRelPath;
  if not FileExists(Installer) then begin
    Alog('VSIXInstaller.exe not found under ' + VsRoot + '; install the extension by hand: ' + Vsix);
    Exit;
  end;
  if ExecAsOriginalUser(Installer, '/quiet "' + Vsix + '"', '', SW_SHOW, ewWaitUntilTerminated, Code) then
    // 1001 is VSIXInstaller's AlreadyInstalledException: the extension is already there (the
    // previous install put it in place), which is not a failure.
    Alog('VSIXInstaller (' + OnOff(Code = 0) + ' as success, 1001 = already installed) exited with ' + IntToStr(Code))
  else
    Alog('VSIXInstaller could not be started');
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  AppDir, Props, Root, Selected: string;
begin
  if CurStep <> ssPostInstall then Exit;

  AppDir := ExpandConstant('{app}');
  Props := GetPropsDir('');
  Root := GetArxRoot('');
  Selected := GetSelectedYearsCsv();
  Alog('ssPostInstall: app=' + AppDir + ' props=' + Props + ' root=' + Root +
       ' years=[' + Selected + ']');

  // The previous install may have used another props folder. Its generated sheets are unknown to
  // Inno, and the shared sheets there are permanent, so drop both from the old location.
  if (PrevPropsDir <> '') and (CompareText(TrimTrailingSlash(PrevPropsDir), TrimTrailingSlash(Props)) <> 0) then begin
    Alog('props folder moved from ' + PrevPropsDir);
    RunGen('remove --props-dir ' + QuoteArg(PrevPropsDir) + ' --include-shared');
  end;

  RunGen('generate --props-dir ' + QuoteArg(Props) + ' --sdk-root ' + QuoteArg(Root) +
         ' --acad-root ' + QuoteArg(Root) + ' --years ' + Selected);
  RunGen('cleanup --props-dir ' + QuoteArg(Props) + ' --keep ' + Selected);

  PatchVsFiles(AppDir);
  PatchHtmlWizardFiles(AppDir);
  PatchArxCommonJs(AppDir, Props);

  if ExpandConstant('{param:SKIPVSIX|}') <> '1' then
    InstallVsix()
  else
    Alog('VSIX install skipped (SKIPVSIX=1)');
end;

// ---------------------------------------------------------------------------------------------
// uninstall: the generated property sheets are not tracked by Inno, drop them by wildcard
// ---------------------------------------------------------------------------------------------

procedure DeleteGeneratedProps(const Dir: string);
var
  Rec: TFindRec;
  Base: string;
  Removed: Integer;
begin
  Removed := 0;
  if (Dir = '') or (not DirExists(Dir)) then Exit;
  Base := AddBackslash(Dir);
  if FindFirst(Base + 'Autodesk.arx-*.props', Rec) then begin
    try
      repeat
        if DeleteFile(Base + Rec.Name) then Inc(Removed);
      until not FindNext(Rec);
    finally
      FindClose(Rec);
    end;
  end;
  if DeleteFile(Base + 'ObjectARX.User.props') then Inc(Removed);
  Alog('uninstall: removed ' + IntToStr(Removed) + ' generated property sheet(s) from ' + Dir);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep <> usUninstall then Exit;
  DeleteGeneratedProps(UninstallPropsDir);
end;
