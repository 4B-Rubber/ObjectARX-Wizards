# ObjectARX 安装器：按年份 + SDK 路径可控安装方案（v2 · 安装时程序化生成）

## Context（背景与目标）

本仓库（`ObjectARX-Wizards`）同时维护两条分发线：

1. **VSIX**（`ArxVsixWizard\`）——项目向导 + 7 个类向导，已真机跑通（VS2022 / VS2026）。
2. **MSI**（`ObjectARXWizardsInstaller\`，WiX 3.10）——把 33 个 per-year props（16 个 `Autodesk.arx-<year>.props` + 16 个 `-net` + 1 个 `2027-Compat`）连同旧的 VCAddClass 注册装到 `C:\Program Files\Autodesk\ObjectARX Props\`。

两个痛点：

- **安装不可选**：所有 props 挤在一个 `<Component Id="C_OBJARX_PROPS_GLOBAL">`（[directory.wxi](../ObjectARXWizardsInstaller/directory.wxi) L528-593，62 个 File），只有单一 `Complete` Feature（[feature.wxi](../ObjectARXWizardsInstaller/feature.wxi)），无法"只装 2018–2027"。
- **路径写死**：17 个含路径的 props 把 `ArxSdkDir`/`AcadDir` **无条件硬编码**（如 [Autodesk.arx-2027.props](../_Installs/ObjectARX%20Props/Autodesk.arx-2027.props) L13/L16），没有任何判空或回退，装在别的根目录时外部覆盖会被**无条件覆盖**，直接导致找不到头文件。

**v2 的核心决策（用户已定）**：这 33 个文件结构基本一致，**改为安装时由自定义动作程序化生成，不再作为 MSI 载荷拷贝**。仓库里只保留「模板 + 年份表」，安装界面让用户给出 SDK / AutoCAD 根目录并勾选年份，CA 按结果生成文件；同时生成一个可手改的覆盖文件，使日后换路径不必重装。

**其余已定决策**
- **只做 Autodesk AutoCAD**；GStarCAD（`HCSoft.grx-*`）与 ZWCAD（`ZWSoft.zrx-*`）**本期不做**，仍按现状作为常装基础载荷。
- 勾选即决定生成哪些年份（"选哪些就落哪些"）。
- 默认勾选 **2018–2027**；进入对话框时按用户填的根目录**自动探测**该年份 SDK 是否存在并相应预勾选/置灰。

---

## 关键事实（已核实）

| # | 事实 | 依据 |
|---|---|---|
| F1 | UI **全部自绘**，无 `UIRef`、无 WixUIExtension；对话框链由 `*_NextArgs`/`*_PrevArgs` 属性驱动 | [property.wxi](../ObjectARXWizardsInstaller/property.wxi) L8-11；[UI.wxi](../ObjectARXWizardsInstaller/UI.wxi) |
| F2 | 整条对话框链跑在 **CostFinalize 之前**，AppSearch 更早 | 仓库内真实安装日志 `ObjectARXWizardsInstaller\testlog.txt`：AppSearch(L121) → CostInitialize(L184) → WelcomeForm(L211) → CostFinalize(L270) |
| F3 | 提权（UAC 托管）安装时客户端公共属性**不传服务端**，必须列入 `SecureCustomProperties` | MSDN；本工程现有 `RDS/ARXPATH/ACAD` 有同样隐患 |
| F4 | `UpgradeVersion` 区间 `[18.0.0, 26.1.0)` 是**开区间**，`ProductVersion` 也是 26.1.0 → 只换 ProductCode 不升版本会导致新旧产品并存 | [ObjectARXWizards.wxs](../ObjectARXWizardsInstaller/ObjectARXWizards.wxs) L5/L100-105 |
| F5 | 生成工程导入 props 那行**没有** `Exists()` → props 缺失是 `MSB4019` 硬失败 | `ArxProject.vcxproj` L40、`OmfProject.vcxproj` L40 |
| F6 | `AcadDir/AcadExe` **只用于调试器** `LocalDebuggerCommand`，不影响编译链接；`ArxSdkDir` 才是关键 | 各 props L55 |
| F7 | SDK 自带 props 链**不定义** `ArxSdkDir`，与我们的定义无冲突 | `C:\Program Files\Autodesk\ObjectARX 2027\inc\*.props` |
| F8 | `-net` 变体**不定义任何路径**（只设 DisplayName/TargetExt/框架版本/ReferencePath），16 个普通 props 才有路径；`2027-Compat` 与 2027 唯一差别是 `ArxSDKPlatform` = `v143`（正常为 `v145`） | `_Installs\ObjectARX Props\*.props` |
| F9 | 基础组件 `C_OBJARX_PROPS_GLOBAL` 是 `Permanent="yes"` → **卸载不删其文件**，升级会把旧年份 props 留在磁盘上（残留必须自行清理） | directory.wxi L531 |
| F10 | `ArxWizPatchFilesCA\` 是**死代码**（未进 wxs/sln）；`ArxWizCustomAction\` 才被使用 | wxs L52 |
| F11 | CA DLL **已作为 `<Binary>` 内嵌**（`Binary\ArxWizCustomAction.CA.dll`），可承载生成逻辑与内嵌资源 | wxs L52 |
| F12 | 向导年份表已有 15 项（2010/2012/2014/2015/2016/2018/2019/2020/2021/2022/2023/2024/2025/2026/2027），**只缺 2013**；`Sdk`/`X64Only` 字段未被引用 | [ArxVersion.cs](../ArxVsixWizard/Models/ArxVersion.cs) L29-46 |
| F13 | 冒烟测试用**显式年份**（2014/2024/2027），改 `DefaultYears` 不影响零 diff 基线；改 `Versions` 才影响 | `TemplateSmokeTest\Program.cs` L554-562 |

---

## 实施方案

### 第 1 部分：年份表 + 模板（仓库的唯一真相）

新增 `tools\arx-props\`：
- `arx-props-table.json`：16 行，字段 `year` / `sdkDirName`（如 `ObjectARX 2027`）/ `acadDirName` / `platformToolset` / `arxSdkVersion` / `hasWin32Acad`（2010–2018 为真）/ `netFramework`（`-net` 用）。
- `props-template.props`：普通 props 模板，占位符 `@@YEAR@@`、`@@SDKDIRNAME@@`、`@@ACADDIRNAME@@`、`@@TOOLSET@@`、`@@SDKVERSION@@`。
- `props-net-template.props`：`-net` 变体模板。
- `gen-arx-props.ps1`：本机可跑的生成器（供离线校验与 regen 用）。

**模板里路径的写法**（解决"路径写死"）：
```xml
<Import Project="$(MSBuildThisFileDirectory)ObjectARX.User.props"
        Condition="Exists('$(MSBuildThisFileDirectory)ObjectARX.User.props')" />
...
<ArxSdkRoot Condition="'$(ArxSdkRoot)' == ''">C:\Program Files\Autodesk\</ArxSdkRoot>
<AcadRoot  Condition="'$(AcadRoot)'  == ''">C:\Program Files\Autodesk\</AcadRoot>
<ArxSdkDir Condition="'$(ArxSdkDir)' == ''">$(ArxSdkRoot)@@SDKDIRNAME@@\</ArxSdkDir>
<AcadDir Condition="'$(Platform)'=='x64' And '$(AcadDir)' == ''">$(AcadRoot)@@ACADDIRNAME@@\</AcadDir>
```
（2010–2018 保留其原有 Win32 `AcadDir` 分支，再追加判空条件；`ArxSDKPlatform`/`ArxSDKVersion` 由表填充。）

**硬验收**：用生成器按**当前默认路径**产出这 33 个文件，与仓库现有文件**逐字节 diff 必须一致**（除上面刻意的"Import + 判空 + 根变量"三处差异）。这条先把"生成逻辑写歪"的风险清零，再把它搬进 CA。

---

### 第 2 部分：MSI 改为"安装时生成"

**2.1 载荷瘦身**（`directory.wxi`）
- 从 `C_OBJARX_PROPS_GLOBAL` 移除 33 个 `Autodesk.arx-*` File，**Id / 固定 GUID `{7F3A2C91-...}` / `Permanent="yes"` / `Win64` / `KeyPath` 全部保持原样**（该组件退化为 29 个非 Autodesk-year 的共享 props：`ObjectARX.*`、`ObjectDBX.*`、`HCSoft.*`、`ZWSoft.*`、`ObjectGRX/ZRX`）。
- `feature.wxi` **不需要**新增任何年份 Feature、**不需要**拆组件（生成的文件本来就不在 MSI 的 File 表里）——这是相对 v1 最大的简化。

**2.2 年份选择 + SDK 路径对话框**（`UI.wxi` / `property.wxi`）
- 新增 `SdkForm`，插在 `ObjectARXForm` 与 `ConfirmInstallForm` 之间，沿用现有 Banner/分隔线/按钮风格：
  - `SDK 根目录`：`<PathEdit Property="ARXROOT">` + `Browse`（复用现有 `SelectFolderDialog`），默认 `C:\Program Files\Autodesk\`。
  - `AutoCAD 根目录`：`<PathEdit Property="ACADROOT">`（默认同上；只影响调试器路径，见 F6）。
  - 16 行年份表：`<CheckBox Property="YEAR_<year>">` + 只读状态文本 `DET_<year>`（"已检测到 SDK" / "未找到，仍可勾选"）。
- `property.wxi` 只改 3 个属性：`ObjectARXForm_NextArgs` → `SdkForm`；新增 `SdkForm_NextArgs` = `ConfirmInstallForm`、`SdkForm_PrevArgs` = `ObjectARXForm`；`ConfirmInstallForm_PrevArgs` → `SdkForm`。**`ObjectARXWizards.wxs` 的四个 Sequence 一行不改**（F2）。
- 默认勾选：`property.wxi` 里只给 2018–2027 写 `<Property Id="YEAR_<year>" Value="1" />`，其余不写（属性不存在＝空串＝FALSE）。
- 探测：`<Property Id="DET_YEAR_<year>"><DirectorySearch Path="C:\Program Files\Autodesk\ObjectARX Props"><FileSearch Name="Autodesk.arx-<year>.props"/></DirectorySearch></Property>`（AppSearch 早于对话框，F2；升级机必然命中，等于保留上次选择）。进入 `SdkForm` 时由 `ObjectARXForm` 的 Next 上发布 16 组 `<Publish Property="YEAR_x" Value="1">DET_YEAR_x</Publish>` + 末尾一条 `YEARS_DETECTED` 标记，避免用户回退时被覆盖。
- **必须补 F3 的坑**：`<Property Id="SecureCustomProperties" Value="TARGETDIR;ARXPATH;RDS;ACAD;ARXROOT;ACADROOT;YEAR_2010;…;YEAR_2027"/>` 与 `AdminProperties`（列全 16 个年份 + 既有 4 个，否则会覆盖默认清单）。

**2.3 生成/清理自定义动作**（`ArxWizCustomAction\CustomAction.cs`）
新增三个 CA，模板与年份表以 **EmbeddedResource** 内嵌进 `ArxWizCustomAction.CA.dll`（F11，无需新增载荷文件）：

| CA | 类型 | 时机 | 行为 |
|---|---|---|---|
| `CreateArxProps` | deferred，`Impersonate="no"` | `After="InstallFiles"`，条件 `NOT Installed OR REINSTALL` | 按 `YEAR_<year>` 勾选，用表 + 模板生成 `Autodesk.arx-<year>.props` / `-net.props`（2027 追加 `-Compat`）写入 `ObjectARX Props\`；并把 `ArxSdkRoot`/`AcadRoot` 写进 `ObjectARX.User.props`（已存在则保留用户内容，只补缺失项） |
| `CleanupUnselectedArxProps` | deferred，`Impersonate="no"` | `After="InstallFiles"`，`NOT Installed OR REINSTALL` | 删除**未勾选**年份残留的 `Autodesk.arx-<year>*.props`（处理 F9：`Permanent="yes"` 的老组件升级后留下的孤儿） |
| `RemoveArxProps` | deferred，`Impersonate="no"` | `After="RemoveFiles"`，条件 `REMOVE~="ALL"` | 删除我们生成的全部 `Autodesk.arx-*` 与 `ObjectARX.User.props`（这些文件不在 MSI File 表里，必须自己清） |

- 三个 CA 都 `Return="ignore"` 并把详情写进 `%TEMP%\ObjectARXWizardsInstaller.log`，避免删/写失败直接中断安装。
- **不需要** `ProductCode` 之外的组件改动；`RemoveExistingProducts Before="InstallInitialize"` 保持不变（旧版本的全量 props 由旧产品卸载 + 本 CA 的清理共同收敛）。

**2.4 升级链修复（F4）**
- `ProductVersion` 26.1.0 → **26.2.0**，并换新 `ProductCode`（`UpgradeCode` 不变）。否则已装 26.1.0 的机器不会被 `RemoveExistingProducts` 命中。

**2.5 静默安装语义**
- `msiexec /qn ARXROOT=D:\Autodesk\ YEAR_2027=1 YEAR_2024=1` 应只生成这两年；不带属性则落到默认 2018–2027。写进 README。

---

### 第 3 部分：向导侧——SDK 缺失不再生成必然失败的工程

- **骨架 Import 加条件 + 明确报错**（F5）：
  - `ArxProject.vcxproj` / `OmfProject.vcxproj` L40 改为 `Condition="'$(ArxYear)' != '' And Exists('$ArxPropsDir$Autodesk.arx-$(ArxYear).props')"`。
  - 同文件加 target，在"配置选了该年份但 props 缺失"时给中文提示而不是一堵头文件报错（照搬同文件 L39/L63 已有的 `Exists(...)` 风格）：
    ```xml
    <Target Name="CheckArxSdkProps" BeforeTargets="ClCompile">
      <Error Condition="'$(ArxYear)' != '' And !Exists('$ArxPropsDir$Autodesk.arx-$(ArxYear).props')"
             Text="未找到 ObjectARX $(ArxYear) 的属性表（$ArxPropsDir$Autodesk.arx-$(ArxYear).props）。请重新运行安装程序勾选该年份，或改用已安装的年份配置。" />
    </Target>
    ```
- **向导只列本机可用年份**：在 `WizardDialog.BuildYearCheckboxes()`（[WizardDialog.xaml.cs](../ArxVsixWizard/UI/WizardDialog.xaml.cs) L44-58）对候选做 `File.Exists(PropsDir + "Autodesk.arx-<year>.props")` 探测；不可用年份**置灰 + 提示**（不直接消失，否则用户会以为向导不支持）。**只过滤 UI 候选，不动 `ArxVersionTable.Versions`**（F13）。
- 默认勾选改为"本机可用 ∩ 2018–2027"；顺手把 `WizardDialog.xaml` L48-49 那句硬编码的 "Defaults: …" 文案改为由 `DefaultYears` 动态生成。
- **补齐 2013**：`ArxVersionTable.Versions` 增 `new ArxVersion("2013", "v100", "19.1", false, false)`（F12 显示只缺 2013）。

---

## 验收与测试

**可本机验证（第 1、3 部分）**
1. **生成器逐字节校验**（最关键）：`gen-arx-props.ps1` 按默认路径生成 33 个文件 → 与仓库现有文件 diff，除刻意的 3 处差异外必须完全一致；每个文件的 `ArxSdkDir` 判空条件齐全。
2. 冒烟与回归：`MSBuild TemplateSmokeTest.csproj -t:Build -p:Configuration=Release` → `ALL CHECKS PASSED`；再对比 `.tmp\gen_baseline`（GUID 归一化），预期只有第 3 部分改动引起的 8 个 vcxproj 变化，其余零 diff。
3. 编译验证：重新生成的项目构建 2027 与 2024，产出 `.arx`。
4. **新增"props 缺失"用例**：把某年份 props 改名或把 `ArxPropsDir` 指到空目录后构建 → 必须报出中文提示，而不是 MSB4019。
5. **覆盖机制验证**：在 `ObjectARX Props\ObjectARX.User.props` 写错误根目录 → 构建失败；改回 → 成功（证明判空 + 覆盖生效）。

**必须在装有 WiX 的机器上验证（第 2 部分）**
> 本机**没有** WiX Toolset，我无法构建 MSI。需要 WiX Toolset **v3.x（3.14.1 / 3.11.2，不要 v4/v5）** + 对应 VS 扩展；`wixproj` 的 `PostBuildEvent` 还依赖 `7z` 在 PATH 里。
6. `msbuild ObjectARXWizard.wixproj /p:Configuration=Debug`（Debug 带 Pedantic + ICE）确认无警告；CA 工程 `ArxWizCustomAction.csproj` 单独可编译。
7. 干净机：只勾 2018–2027 → `ObjectARX Props\` 只有这些年份 + 29 个基础文件，且 `ObjectARX.User.props` 的根目录 = 安装界面所填。
8. 升级机：先装旧 26.1.0 全量 → 装新版只勾若干 → 未勾选年份被 `CleanupUnselectedArxProps` 删除（F9 的残留收敛）。
9. **卸载/修复**：卸载后生成的文件被 `RemoveArxProps` 清掉；"修复"后文件被重建。
10. **标准用户（UAC 提权）**：`msiexec /i … /l*v`，grep `PROPERTY CHANGE: Adding YEAR_` 与 `Switching to server`，确认 `YEAR_*`/`ARXROOT` 传到服务端（F3 生效）。

---

## 风险与限制

| 风险 | 说明 / 缓解 |
|---|---|
| **生成的文件不被 MSI 跟踪** | 这是"安装时生成"的固有代价：卸载/修复必须由我们的 CA 负责（2.3 的 `RemoveArxProps`/`CreateArxProps`），并在第 9 项验收里专门验证 |
| **本机无法构建 MSI** | 第 2 部分只能交付源码 + 第 6-10 项验收集；第 1、3 部分可在本机完整验证 |
| CA 写文件需提权 | 三个 CA 均 `deferred` + `Impersonate="no"`；`Return="ignore"` 防止中断安装 |
| 老版本 `Permanent="yes"` 残留 | 由 `CleanupUnselectedArxProps` 按未勾选年份删除；基础组件保持 `Permanent="yes"` 不动（其文件可能与 Autodesk 官方 SDK 安装器共用，不宜由我们删） |
| 维护安装（`Installed<>""`）不进 `SdkForm` | 本期不做年份修改，列入后续项；需在 README 写明"改年份请重新运行安装程序" |
| 生成器与现有文件的偏差 | 第 1 项"逐字节 diff"作为硬门槛，先把风险清零再搬进 CA |
| 第三方 CAD 未纳入 | 按决策，`HCSoft/ZWSoft/GRX/ZRX` 仍为常装基础载荷；后续纳入时只需给表加列/加模板 |
| props 载荷移出后 `_Installs` 目录变化 | `_Installs\ObjectARX Props\` 仍保留 29 个非年份文件；`ArxVersionTable.PropsDir`（[ArxVersion.cs](../ArxVsixWizard/Models/ArxVersion.cs) L27）指向的安装目录不变，VSIX 侧无需改动 |

---

## 关键文件

- `tools\arx-props\`（新增：年份表 + 两个模板 + 离线生成器）
- `ObjectARXWizardsInstaller\directory.wxi`（从 `C_OBJARX_PROPS_GLOBAL` 移除 33 个年份 File，L528-593）
- `ObjectARXWizardsInstaller\UI.wxi`（新增 `SdkForm`）
- `ObjectARXWizardsInstaller\property.wxi`（`YEAR_*` 默认值、`SecureCustomProperties`、导航属性）
- `ObjectARXWizardsInstaller\ObjectARXWizards.wxs`（版本号 26.2.0 + 新 `ProductCode` + 三个新 CA 的声明与序列挂载）
- `ObjectARXWizardsInstaller\ArxWizCustomAction\CustomAction.cs`（三个新 CA + 内嵌模板/表）
- `_Installs\ObjectARX Props\`（移除 33 个 `Autodesk.arx-*`；保留 29 个共享 props）
- `ArxVsixWizard\Packaging\ProjectTemplates\VC\1033\{ArxApp\ArxProject.vcxproj, OmfApp\OmfProject.vcxproj}`（Import 加 `Exists` + 缺失报错 target）
- `ArxVsixWizard\UI\WizardDialog.xaml.cs` / `.xaml`（年份候选探测、默认勾选、文案）
- `ArxVsixWizard\Models\ArxVersion.cs`（补 2013）
- `ArxVsixWizard\TemplateSmokeTest\Program.cs`（新增 props 缺失用例）