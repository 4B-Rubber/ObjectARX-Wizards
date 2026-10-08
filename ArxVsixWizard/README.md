# ObjectARX 多年份 VSIX 向导（ArxVsixWizard）开发交接文档

> 状态：**工程可编译、VSIX 可安装、离/在线模板渲染与命令行编译已验证。P0（真实 VS 下不生成源文件）已修复；但 Visual Studio 内"新建项目"端到端流程仍未做真机验证，其余问题见第 6 节。**
> 最后更新：2026-10-07

---

## 1. 背景与目标

- 老式 ObjectARX 向导（`ArxAppWiz` / `ArxAppWiz182`）基于 `VsWizardEngine` + `.vsz/.vsdir` + HTML/JScript。
- 已实测确认：**VS2026（VS18，路径 `C:\Program Files\Microsoft Visual Studio\18\Enterprise`，v145 工具集）已移除项目级向导落点**——`VC\vcprojects` 目录不存在，1935 个相关程序集扫描无该机制引用；`.vsz` 仅保留在 `VCProjectItems`（添加项/添加类）级别。因此老 MSI 安装后 VS2026 的"新建项目"里不会出现向导。
- 目标：用 **VSIX + .vstemplate + IWizard（C#/WPF）** 重做项目向导，一个 VSIX 同时支持 **VS2022 (17.x) 和 VS2026 (18.x)**，功能对齐两个老向导：
  - ObjectARX/DBX/CRX 应用（对应 ArxAppWiz）
  - ObjectARX/DBX + OMF/MAP 垂直 SDK（对应 ArxAppWiz182，含 Enu 资源 DLL 子项目）
  - 目标年份 2010–2027，单工程含每年 `YYYY`/`YYYYd` 两套配置
- 注意区分：**2027 工程本身早已能用 VS2026 + v145 命令行编译**（老 JS 向导产物实测通过）；本工程解决的是"在 VS2026 里点新建项目走向导 UI"。

## 2. 工具链与构建

- 项目：`ArxVsixWizard.csproj`，SDK 风格，**net472 + WPF（UseWPF）**，C# LangVersion 9.0。
- NuGet 依赖（可离线还原，NuGet.org 可达）：
  - `Microsoft.VisualStudio.SDK` 17.14.40265
  - `Microsoft.VisualStudio.TemplateWizardInterface` 17.10.40170
  - `Microsoft.VSSDK.BuildTools` **18.9.820**（含 VS18 的 manifest schema；必须设 `<VSSDKBuildToolsAutoSetup>true</VSSDKBuildToolsAutoSetup>` 自动导入 VsSDK.targets）
- 用 VS2022 的 MSBuild 即可构建出 VSIX：

```powershell
$msb = "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe"
& $msb ArxVsixWizard.csproj -t:Restore,Build -p:Configuration=Release -v:m -nologo
# 产物：bin\Release\ArxVsixWizard.vsix
```

- 坑：
  - 改 csproj 后若直接 Build 报 `NETSDK1004 project.assets.json 找不到`，先跑 `-t:Restore`。
  - VSIX manifest（`source.extension.vsixmanifest`）在 VSSDK18 下 **`ProductArchitecture` 必须写成 `<InstallationTarget> 的子元素** `<ProductArchitecture>amd64</ProductArchitecture>`，写成属性会报 VSSDK1311。
  - 安装目标：Community/Pro/Enterprise，`Version="[17.0,19.0)"`，ProductArchitecture=amd64。

安装（实测可同时装到 VS2022 与 VS2026）：

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\VSIXInstaller.exe" /quiet ObjectARXMultiYearWizards.vsix
# 日志：%TEMP%\vsixinstall.log
# VS2026 每用户扩展目录：%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_436ebb14\Extensions\<随机名>\
```

## 3. 代码结构

```
ArxVsixWizard/
├── ArxVsixWizard.csproj
├── source.extension.vsixmanifest      # VSIX 清单（Id=ObjectARX.MultiYear.Wizard）
├── ArxProjectWizard.cs                # IWizard 主实现 + OmfProjectWizard（薄子类）
├── ArxWizard.cs                       # ⚠ 早期骨架残留的空类，可删
├── Models/
│   ├── ArxVersion.cs                  # 15 行年份表（2010 v90 … 2027 v145），移植 arxCommon.js
│   ├── WizardKind.cs                  # 枚举：WizardKind/AppType/MfcSupport/ComServer/ComImport
│   └── WizardOptions.cs               # WPF 对话框收集的全部用户选择
├── Engine/
│   ├── VersionXml.cs                  # 生成 ARX_CONFIG_XML/YEAR_REGEX/TOOLSET/CLR 等片段
│   ├── TemplateRenderer.cs            # 经典 [!if]/[!else]/[!endif]/[!output] 指令渲染器（C#）
│   ├── Names.cs                       # CreateSafeName 移植（只留 [A-Za-z0-9_]）
│   └── ProjectModel.cs                # 符号表构建 + 文件生成计划 + vcxproj ItemGroup/Filters XML
├── UI/
│   ├── WizardDialog.xaml(.cs)         # 单页 WPF 选项窗口（RDS/类型/年份/MFC/COM/.NET/OMF）
├── Templates/
│   ├── ArxApp/*                       # 嵌入资源：老 ArxAppWiz 的 12 个源模板（原样复制）
│   └── OmfApp/*                       # 嵌入资源：老 ArxAppWiz182 的 18 个模板（含 OmfEnuRes.*）
├── Packaging/
│   └── ProjectTemplates/VC/1033/{ArxApp,OmfApp}/
│       └── <名>.vstemplate + <Skel>.vcxproj + <名>.ico   # 打进 VSIX 后必须位于 ProjectTemplates 下
└── TemplateSmokeTest/                 # 独立控制台回归测试（不打进 VSIX）
    └── Program.cs                     # 60 种选项组合×33 模板渲染 + 8 个工程落盘
```

### 关键设计

- **参数化 vcxproj**：`Packaging/*/*.vcxproj` 是把老 `x64win32.vcxproj` 的 `[!output XXX]` 换成 `$ArxXxx$` 的骨架，由 IWizard 在 `RunStarted` 往 `replacementsDictionary` 注入自定义键完成替换；年份条件/工具集/CLR/全局 props 引用逻辑与老模板 1:1 一致（全局 props 仍取 `C:\Program Files\Autodesk\ObjectARX Props\`）。
- **源文件仍走老模板**：`Templates/**` 是嵌入资源，保持 `[!if SYMBOL]…[!output X]` 原样，由 C# `TemplateRenderer` 渲染。表达式支持 `!`、`&&`、`||`、括号（&& 优先级高于 ||），覆盖了两个老向导全部模板里出现的条件形式（已枚举核对）。
- **符号表**：`ProjectModel.BuildSymbols()` 复刻两个 `default.js` 的 OnFinish：APP_ARX/DBX/CRX_TYPE、MFC_*、*_COM_*、DOTNET_*、OMF_APP/MAP_API、PRJ_TYPE_APP(arx/dbx/crx/arxnet/dbxnet/crxnet)、ARX_MFC/ATL_SUPPORT、ARX_OMF_DEFS/ZM、三个 GUID 等；GUID 用 `Guid.NewGuid().ToString("D").ToUpper()`（等价 FormatGuid(...,0)，无花括号大写）。
- **文件计划**：`BuildPlan()` 复刻两个 `Templates.inf` 的条件包含关系与重命名规则。注意 OMF 的 GetTargetName 只对小写 `omf`/`Root` 前缀做替换，`OmfApp.cpp/.h`、`OmfHeaders.h` **保持原名**（此处曾误改为 `<项目名>App.cpp`，已修复）。
- **OMF Enu 子项目**：`CreateOmfResourceProject()` 渲染 Enu\Resource.h / `<名>Enu.rc` / `<名>Enu.vcxproj(.filters)`，`Solution.AddFromFile` 加入并用 `BuildDependencies.AddProject` 设主项目依赖资源 DLL。
- 以后新增年份（如 2028）：只需在 [ArxVersion.cs](file:///D:/Demo/ObjectARX-Wizards/ArxVsixWizard/Models/ArxVersion.cs) 的表中加一行；WPF 年份勾选框由表动态生成。

## 4. 已完成的验证（离线，均通过）

1. VSIX 编译成功（仅 VSTHRD010 线程分析器警告，无错误）。
2. `TemplateSmokeTest`：**60 种选项组合 × 33 个嵌入模板**全部渲染，无残留 `[!if]/[!output]` 指令、无渲染异常。
3. 离线端到端（绕过 VS，直接调引擎）生成 8 个工程到 `_E2EGen\`，vcxproj 全部为合法 XML：ArxPlain、ArxAtl、ArxMfcExt、DbxClr、Crx、OmfArx、OmfDbx、OmfClr。
4. 用 VS2022 MSBuild 命令行编译 2024(v143)/x64 实测通过并产出：
   - ArxPlain / ArxAtl / ArxMfcExt → `ADSK*.arx`
   - Crx → `ADSKCrx2024.crx`
   - DbxClr（.NET Framework mixed, CLR）→ `ADSKDbxClr2024.dll`
   - OmfDbx（OMF 向导非 OMF 模式）→ `ADSKOmfDbx2024.dll`
   - OmfArx（真 OMF 模式）编译停在缺 `AecCore.h`——**本机无 AEC OMF SDK，环境限制，非模板问题**（与老向导测试结论一致）。
5. VSIX 用 VSIXInstaller 静默安装成功，日志确认同时落盘 VS2022 与 VS2026；VS2026 启动后新模板引擎 VTC 缓存中已出现 `~PC\ArxApp`、`~PC\OmfApp`（说明扩展被扫描到，但**尚未确认在新建项目对话框可见/可选**）。

## 5. 未验证项

- VS2026 / VS2022 "新建项目"对话框中两个模板是否出现、IWizard 程序集能否激活、WPF 窗口能否弹出。
- VSIX 内模板是松散目录还是被 VSSDK 自动压成 zip（需打开 vsix 检查；传统要求 zip，VS2022+ 对扩展目录模板的支持要实测）。
- 真实 VS 流程下 vcxproj 的 `$ArxXxx$` 自定义替换、文件生成、OMF 子项目加入与依赖。
- 2027/v145 配置在新 VSIX 产物下的编译（离线只编了 2024；老 JS 向导产物的 2027 编译此前已通过，模板相同，风险低但应复测）。
- 扩展升级/卸载。

## 6. 已知问题与待修复（按优先级）

### ~~P0（真正的阻断点）— 模板未放在 `ProjectTemplates` 下，VS 根本不识别~~（已修复）
原来 `Packaging\ArxApp\**` 被 `Link="ArxApp\..."` 打进去，安装后落盘为 `Extensions\<id>\ArxApp\ArxApp.vstemplate`；**VS 只把扩展里的 `ProjectTemplates` 目录当作项目模板扫描根**（对照微软官方模板扩展：`ProjectTemplates\VC\1033\<名>\<名>.vstemplate`，asset 为 `Path="ProjectTemplates"`）。因此模板从来不会出现在"新建项目"里——这才是"向导不能用"的主因，P0b 的源文件问题是它下游的。
- 修法：`Packaging` 重组为 `ProjectTemplates\VC\1033\{ArxApp,OmfApp}\`；csproj 的 `Content` 改为 `Packaging\ProjectTemplates\**\*` → `Link="ProjectTemplates\%(RecursiveDir)..."`；manifest 两个 ProjectTemplate asset 合并为一个 `<Asset Type="Microsoft.VisualStudio.ProjectTemplate" Path="ProjectTemplates" />`；顺带把 `.ico` 移进各自模板目录（原来放在 `Packaging\*.ico` 不被 `ArxApp\**` 匹配，压根没进 VSIX）。
- 打包形式结论（原第 5 节存疑项）：**松散目录即可，不需要压 zip**——官方扩展也是松散 `ProjectTemplates` 目录。
- 重装坑：VSIX 版本号未变时 `VSIXInstaller /quiet` 会认为"已安装"而静默跳过，必须先 `/uninstall:ObjectARX.MultiYear.Wizard` 再装。安装需写 `%LOCALAPPDATA%\Microsoft\VisualStudio\...`，沙箱内会被拒。
- 已实测：卸载+重装后 VS2022/VS2026 扩展目录均出现 `ProjectTemplates\VC\1033\{ArxApp,OmfApp}\...` 与 `templateManifest0.1033.vstman`。

### ~~P0b — vstemplate 没有列任何 ProjectItem，真实 VS 里源文件不会被生成~~（已修复）
`Packaging/ArxApp/ArxApp.vstemplate`（OMF 同）的 `<TemplateContent>` 只有 `<Project File="..." >`，没有 `<ProjectItem>`，所以 VS 不复制任何源文件、`ProjectItemFinishedGenerating` 不会被调用。

**已采用的修法**（比原推荐方案更简单，不依赖模板 item、也不需要 `AddFile`）：
- 关键点：生成的 vcxproj 本身已通过 `$ArxItemsXml$`（`ProjectModel.BuildItemsXml()`）声明了全部 `ClCompile/ClInclude/ResourceCompile/Midl` 项，且 StdAfx.cpp 的 `Create` PCH、AssemblyInfo.cpp 的 `NotUsing` PCH 元数据都在其中。因此**不需要** `VCProject.AddFile`——那反而会与 vcxproj 已有项重复。
- 改为在 [ArxProjectWizard.cs](file:///D:/Demo/ObjectARX-Wizards/ArxVsixWizard/ArxProjectWizard.cs) 的 `ProjectFinishedGenerating(Project project)` 中，遍历 `_model.Files` 用 `TemplateRenderer` 渲染后直接写盘到项目目录（vcxproj/filters 用 UTF-8 无 BOM，其余用 Windows-1252）；随后照旧写 `.filters`、OMF 子项目、`project.Save()`。
- `ShouldAddProjectItem` 改为恒返回 `true`，`ProjectItemFinishedGenerating` 改为空实现。
- 尚未真机验证（属 P1）：文件写盘时机在项目加载之后，Solution Explorer 是否立即刷新显示需实测；若个别场景不显示，再考虑 `project.Save()` 后触发刷新。

### ~~P0c — 真实 VS 下生成的 vcxproj 非法："无法识别元素 `<ItemGroup>` 下面的元素 `<#text>`"~~（已修复）
真机新建项目时报：`无法读取项目文件"ArxApp1.vcxproj"。...vcxproj(4,3): 无法识别元素 <ItemGroup> 下面的元素 <#text>。` 打开生成的 vcxproj 可见第 5 行变成 `$    <ProjectConfiguration Include=...>`、结尾 `$  </ItemGroup>`——**自定义参数两侧的 `$` 没被替换掉**。
- 根因：VS 模板引擎按字典 **key 的字面量**替换；标准 key 形如 `$projectname$`（代码读的也是带 `$` 的），而 `RunStarted` 注入时用的是裸 key（`replacementsDictionary[kv.Key]`，key=`ArxConfigXml`），只匹配到 `$ArxConfigXml$` 中间的 `ArxConfigXml`，两边的 `$` 残留 → 非法 XML。离线冒烟测试写的是 `"$" + kv.Key + "$"`，正好补上了 `$`，所以离线通过、真机失败。
- 修法：[ArxProjectWizard.cs](file:///D:/Demo/ObjectARX-Wizards/ArxVsixWizard/ArxProjectWizard.cs#L54-L59) 改为 `replacementsDictionary["$" + kv.Key + "$"] = kv.Value;`。

### P1 — 必须做一次真实 UI 端到端
装新版 VSIX 后在 VS2026 新建项目搜 "ObjectARX"，验证：模板可见→弹出 WPF→选年份→生成工程→2024 与 2027 配置各编译一次（arx/dbx/arxnet/ATL/MFC/OMF 非 OMF 模式）。若模板不出现：
- 打开 `ArxVsixWizard.vsix`（zip）确认模板路径是否符合 `ProjectTemplates\...` 约定或是否需要打成 zip；
- 用 `devenv /log` 看 ActivityLog，或删 `%LOCALAPPDATA%\Microsoft\VisualStudio\18.0_436ebb14\VTC` 后重启。

### P1 — VSIX 打包了多余的程序集
扩展目录里出现 `Microsoft.VisualStudio.Extensibility.Editor.Contracts.dll`、`Microsoft.VisualStudio.Linux.ConnectionManager.Store.dll`、`System.Text.Encodings.Web.dll`（SDK 包引用的 CopyLocal 依赖，VS 自带）。在 csproj 中对相关引用设 `Private=false` / `<ExcludeFromVsix>true</ExcludeFromVsix>`，避免与宿主版本冲突。

### P2 — 杂项
- [ArxWizard.cs](file:///D:/Demo/ObjectARX-Wizards/ArxVsixWizard/ArxWizard.cs) 是空 IWizard 骨架残留，删除。
- csproj 中 `<BaseIntermediateOutputPath>obj\</BaseIntermediateOutputPath>` 是排查时加的，无用可删；真正防止把 TemplateSmokeTest 源码 glob 进主项目的是下面的 `<Compile Remove="TemplateSmokeTest\**"/>` 一组，保留。
- `Packaging/ProjectTemplates/VC/1033/*/*.ico` 是 60 字节占位 ICO，换成正式图标。
- 已删除 vcxproj 里的 `/Wv:17.00.61030`（老模板为压 VS2015 警告所加）：低版本工具集会报"无效的数值参数"，高版本也不需要。OMF 骨架保留了同行的 `$ArxOmfZm$`（`-Zm1000`），只去掉 `/Wv:`。
- VSTHRD010 警告：IWizard 回调本就在 UI 线程，可在方法入口加 `ThreadHelper.ThrowIfNotOnUIThread()` 明示。
- 模板源文件为 Windows-1252 编码，渲染器按 1252 解码；P0 修法落盘时建议对 cpp/h/rc 保持与老向导一致的 ANSI 写出（vcxproj/filters 用 UTF-8 无 BOM），避免 .rc 中扩展字符乱码。
- RDS 仅 MaxLength=4，未做字符集校验；空 RDS 已在 vcxproj 条件处理。
- WizardExtension 程序集标识写死在三处，版本号变更时必须同步，否则 VS 会以"此模板尝试加载组件程序集 …"拒绝加载向导：`ArxVsixWizard.csproj` 的 `<Version>`、`source.extension.vsixmanifest` 的 `Identity/@Version` 与 `Asset/@AssemblyName`、以及 `Packaging\**\*.vstemplate` 的 `<Assembly>`。**0.1.1 那次改版就漏了这 9 个 `.vstemplate`**（里面还是 26.2.0.0），导致所有向导都加载失败。现由 [test-version-consistency.ps1](file:///D:/Demo/ObjectARX-Wizards/ArxVsixWizard/test-version-consistency.ps1) 对四处 + 已构建的 VSIX 做断言，改版本后跑它即可；可考虑强名称签名。
- VS2022 上老 MSI（.vsz）向导与新 VSIX 会**并存显示两个入口**，分发策略要决定：共存、MSI 退役、还是改名区分。
- TemplateSmokeTest 在项目子目录中、不打进 VSIX；别把它的 obj/bin 当产物分发。
- 当前 VS2026 (devenv) 可能仍在运行，调试/重装扩展前先全部关闭。

## 7. 快速复现命令

```powershell
# 构建
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  D:\Demo\ObjectARX-Wizards\ArxVsixWizard\ArxVsixWizard.csproj -t:Restore,Build -p:Configuration=Release -v:m -nologo

# 离线渲染回归
& "D:\Demo\ObjectARX-Wizards\ArxVsixWizard\TemplateSmokeTest\bin\ArxVsixSmokeTest.exe" D:\Demo\ObjectARX-Wizards\_E2EGen

# 编译一个生成的工程（2024/v143）
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  D:\Demo\ObjectARX-Wizards\_E2EGen\ArxPlain\ArxPlain.vcxproj -p:Configuration=2024 -p:Platform=x64 -v:m -nologo

# 安装 VSIX（先关闭所有 VS）
& "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\VSIXInstaller.exe" /quiet `
  D:\Demo\ObjectARX-Wizards\ObjectARXMultiYearWizards.vsix
```

产物 VSIX：[ObjectARXMultiYearWizards.vsix](file:///D:/Demo/ObjectARX-Wizards/ObjectARXMultiYearWizards.vsix)（约 175 KB）。版本号见 `ArxVsixWizard.csproj`，改完务必跑 `test-version-consistency.ps1`。
