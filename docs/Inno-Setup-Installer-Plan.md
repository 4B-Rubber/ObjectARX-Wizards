# ObjectARX 多年度安装器：Inno Setup 方案（分支 dev）

> **状态**：本分支已合入 MSI 线的全部修复（merge `8ecde18`）。MSI 版定版 **0.1.1 / tag `v0.1.1-msi`**；Inno 侧**已按本文落地**（见第 10 节，产物 `InnoSetupInstaller\Output\ObjectARXMultiVersionWizardsSetup-Inno.exe`，尚未打 `v0.1.2-inno` tag）。
> 版本号已从 **0.1.1** 重新起算，旧的 26.x 线作废。**两条线现统一为 0.1.2**（MSI 的 ProductVersion + ProductCode、Burn 的 Version、VSIX 的 Identity/程序集、Inno 的 AppVersion）。
>
> **MSI 侧已跟进到 0.1.2**（见第 11 节）：第 10.2 节列出的 UI/行为取舍已同步回 WiX 线，两版现在对齐（除了 Inno 才有的中英双语自动选择）。

## 1. 两条线

| 分支 | 方案 | 状态 |
|---|---|---|
| `main` | WiX 3.14 MSI + Burn 引导程序 + VSIX | 真机验收通过 -> `v0.1.1-msi`；UI 取舍已跟进到 **0.1.2**（第 11 节，待真机重验） |
| `dev` | Inno Setup 复刻同一套安装行为 | 已落地（第 10 节），待打 `v0.1.2-inno` |

## 2. 要对齐的行为（按 MSI 实际实现整理）

| # | 行为 | MSI 落点 | Inno 对应物 |
|---|---|---|---|
| 1 | 载荷：`_Installs\` 下的 VCAddClass / `*.vsz` / HTML 向导 / 共享 props | `directory.wxi` | `[Files]` |
| 2 | **年份选择**：16 个年份，默认勾 **2020/2024/2026/2027**，四个预设按钮 **All / None / Invert / Compatible** | `UI.wxi` 的 `SdkForm` | 自定义页 + CheckListBox |
| 3 | **props 安装时生成**，按用户给的 SDK 根 / AutoCAD 根产出 | `ArxProps` 类 | 调共享生成器（第 4 节） |
| 4 | **注册表契约**（第 3 节） | `C_ARXPROPS_REG` | `[Registry]` |
| 5 | 老向导路径修补：`*.vsz` 的 `[TARGETDIR]`、`default.htm` 的 `ADSK->RDS`、`arxCommon.js` 的 `ARX_PROPS_DIR` | 三个 Patch 动作 | `[Code]` 直接改文件 |
| 6 | **维护页** Repair / **Change（重选年份后覆盖安装）** / Remove | `MaintenanceForm` | `[Code]` + `AppId` |
| 7 | **VSIX 静默安装**；**VS 未关闭则提示并中止**（避免装一半） | `Bundle.wxs` + `MsiSetup` | `[Run]` + 启动前检测 |
| 8 | 卸载：删掉生成的 props（年份通配）与注册表 | `CA_REMOVEARXPROPS` | `[UninstallDelete]` |

**年份清单（16 个，没有 2011 和 2017）**

```
2010 2012 2013 2014 2015 2016 2018 2019 2020 2021 2022 2023 2024 2025 2026 2027
```

- **默认勾选**：`2020 2024 2026 2027`
- **Compatible**（每个二进制兼容代系取最新那年）：`2014 2016 2018 2020 2024 2026 2027`
- 2017 不在表里：它与 2018 同属 R22，`Autodesk.arx-2018.props` 已覆盖两者
- 年份页之后的"确认页"（`YearSummaryForm`）用文字列出已选年份，是 MSI 特有的补偿手段，Inno 不需要

## 3. 注册表契约（Inno 版必须遵守）

VSIX 项目向导靠这些值定位 props：

```
HKLM\SOFTWARE\Autodesk\ObjectARX Wizards
    PropsDir   REG_SZ   <属性表目录，默认跟随安装目录>
    ArxRoot    REG_SZ   <Autodesk 根目录（SDK 与 AutoCAD 都在它下面）>
    AcadRoot   REG_SZ   <同上，与 ArxRoot 同值>
```

- `PropsDir` 由 VSIX 向导（`ArxVsixWizard/Models/ArxVersion.cs` 的 `PropsDir`）读取，读不到退回默认值
- `ArxRoot` 用于**重装时预填**上次的值（MSI 用 `RegistrySearch` 回填）；`AcadRoot` 写同一个值——多年度产品只有一个根，年份由生成的属性表自己拼
- `PropsDir` **不作为下次安装的默认值**：属性表目录属于安装目录，默认永远跟随它（用户想改就在界面里改）。它仍会被记下来，用于**检测上次生成了哪些年份**以及换目录后清理旧位置
- 卸载时三项都要清掉

生成到 `PropsDir` 的文件：

```
Autodesk.arx-<year>.props        # 16 个年份各一份
Autodesk.arx-<year>-net.props    # 对应的 .NET 变体
Autodesk.arx-2027-Compat.props   # 2027 的 v143 工具集变体
ObjectARX.User.props             # 可手改的覆盖文件
```

## 4. props 生成器：抽成共享 EXE

唯一真相是 `tools/arx-props/` 的年份表 + 三个骨架模板。生成逻辑现在内嵌在 `ArxWizCustomAction/CustomAction.cs` 的 `ArxProps` 类里。

计划抽成独立 `arx-genprops.exe`，两分支共用：

```
arx-genprops generate --props-dir <dir> --sdk-root <dir> --acad-root <dir> --years 2020,2024,...
arx-genprops cleanup  --props-dir <dir> --keep 2020,...
arx-genprops remove   --props-dir <dir>
```

**抽取时丢掉 MSI 绑定的部分**：`PrepArxPropsData`（拼 CustomActionData）、`BuildYearsSummary`（填 `ARX_YEARS_SUMMARY`）、以及"从 `YEAR_*` 属性读选择"。生成器只接收一个明确的年份列表。

## 5. MSI 上踩过的坑（Inno 多半不会遇到，但要知道为什么换）

| 坑 | 现象 | 根因 |
|---|---|---|
| `CheckBox` 表以 Property 为主键 | 两页无法绑同一属性，镜像页方案编译失败 | 一个属性全包只能绑一个勾选框 |
| 勾选框状态只在对话框创建时读取 | 改了属性框不动 | MSI 的控件绑定语义 |
| **"取消勾选"必须置空值** | 用 "0" 清空 -> 框反而全部显示为已勾选 | 属性只要非空就渲染成选中 |
| 勾选框没写 `CheckBoxValue` | 点击完全没反应 | 勾选时 MSI 回写"属性表初始值"，为空则刷回未勾选 |
| 重进自身对话框 | 安装中断，错误 2856 | MSI 不允许同一对话框的第二份副本 |
| 版本号与 ProductCode 必须成对变更 | 报 1638"已装了另一个版本" | 同码不同版 / 同版不同包都被拒 |
| Burn 跳过"已安装"的 MSI 包 | 重复运行 Setup.exe 什么都不做 | Burn 按 ProductCode 判在机状态 |
| 反选按钮两条条件互相抵消 | 无论原来什么状态结果都被清空 | 控制事件按顺序执行，前一条改变了后一条的条件 |

Inno 侧这些基本不存在：有真正的 CheckListBox、Pascal 可直接读写状态、`[UninstallDelete]` 原生支持通配、单文件产物没有 MSI 的组件语义。

## 6. 版本号与升级

- 新线从 **0.1.1** 起算；MSI 侧 ProductVersion 与 ProductCode 每次发版成对更新
- **0.1.1 是新项目，不是旧线的升级**：MSI 与 Burn 的 UpgradeCode 都已换新，旧 26.x 线在这里完全不可见，升级判定回到标准两条——"发现更新的产品"无上限（判降级），"发现旧版本"区间为 `[0.0.0, 当前版本)`。区间不含当前版本这点很关键：重复运行同一个包会进维护页，而不是把自己卸载重装
- Inno 侧用自己的 `AppId`，同样注意别让"降级安装"被拒

- **0.1.2** 沿用同一 UpgradeCode，只换 ProductVersion + ProductCode，所以它能正常覆盖 0.1.1（`Bundle.wxs` 的 `UninstallCommand` 里的 ProductCode 也要跟着换）

## 7. 目录、产物与对比

- 新增 **`InnoSetupInstaller/`**：`ObjectARXMultiVersionWizards.iss`、payload、图标
- 产物名 **`ObjectARXMultiVersionWizardsSetup-Inno.exe`**，与 MSI/Burn 的 `...Setup.exe` 区分
- 构建（ISCC 不在 PATH，写全路径）：`& "C:\Program Files\Inno Setup 7\ISCC.exe" InnoSetupInstaller\ObjectARXMultiVersionWizards.iss`
- 对比：`git diff --stat v0.1.1-msi v0.1.2-inno`，并实测体积 / 耗时 / 静默参数 / 卸载残留（两处 `Autodesk\` 目录 + 注册表三项）/ 企业分发

## 8. 未决与风险

| # | 问题 | 现状 |
|---|---|---|
| R1 | Inno Setup 本机已装两版：**7.0.2**（`C:\Program Files\Inno Setup 7\ISCC.exe`）与 **6.7.3**（`%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe`） | 都不在 PATH，脚本里写全路径；建议先定一版为准（6.7.3 资料多，7.0.2 更新） |
| R2 | 生成器抽 EXE 还是各写一份 | 倾向抽 EXE（第 4 节） |
| R3 | `VSIXInstaller` 返回码语义、失败是否回滚 | 参考 MSI：先检测 VS 是否在运行并中止 |
| R4 | 代码签名 | 两版都没有，暂不在范围内 |

## 9. 落地顺序

1. 定下用哪一版 ISCC（R1）并写进构建命令
2. 抽 `arx-genprops.exe`，用 `tools\arx-props\gen-arx-props.ps1` 的零 diff 基线做回归
3. 写 `.iss` 骨架：装文件 -> 生成 props -> 写注册表 -> 装 VSIX
4. 补年份页（16 个年份 + 四个预设按钮）、目录选择、路径修补、卸载清理
5. 打 tag `v0.1.2-inno`，跑第 7 节对比

## 10. 实现情况（dev 已落地）

### 10.1 产物

| 路径 | 说明 |
|---|---|
| `InnoSetupInstaller\ObjectARXMultiVersionWizards.iss` | 安装脚本（载荷 + 目录页 + 年份页 + 注册表 + 修补 + VSIX + 卸载清理） |
| `InnoSetupInstaller\Output\ObjectARXMultiVersionWizardsSetup-Inno.exe` | 产物，约 2.4 MB |
| `tools\arx-genprops\` | 共享 props 生成器（net48 控制台 + 内嵌年份表/骨架 + 测试） |
| `InnoSetupInstaller\test-inno-sandbox.ps1` | 沙箱端到端测试（见 10.5） |
| `InnoSetupInstaller\test-payload-parity.ps1` | 两条线的载荷对齐检查（对着 `directory.wxi` 比） |
| `tools\arx-genprops\test-arx-genprops.ps1` | 生成器回归 + 功能测试 |

构建（ISCC 与 msbuild 都不在 PATH，写全路径）：

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
  tools\arx-genprops\arx-genprops.csproj -restore -p:Configuration=Release
& "C:\Program Files\Inno Setup 7\ISCC.exe" InnoSetupInstaller\ObjectARXMultiVersionWizards.iss
```

- **R1 已定**：用 **Inno Setup 7.0.2** 编译（6.7.3 的 `TNewCheckListBox.AddCheckBox` 签名与 7 相同，脚本对两版都兼容）。
- **`.iss` 必须存成带 BOM 的 UTF-8**：里面是中文文案，不带 BOM 编译虽然能过但要靠系统代码页猜。改完脚本记得确认 BOM 还在。

### 10.2 为什么这几处和 MSI 不一样（都是 UI 取舍，已按你的意见定稿）

> 这几处**已经同步回 MSI 线**（0.1.2，见第 11 节）；下表保留当时的对照。

| 差异 | MSI（0.1.1） | Inno | 理由 |
|---|---|---|---|
| 安装目录 | 固定 `[ProgramFilesFolder]\Autodesk\<产品名>`，无目录页 | **可选**，默认 `{autopf}\Autodesk\<产品名>` | 你要求可选；默认从 (x86) 改成 64 位 `Program Files`，因为 props 现在默认跟随它，把 MSBuild 属性表放进 (x86) 不合适（**已确认**：插件都是 64 位，2026 年不再考虑 32 位） |
| 属性表目录 | 独立固定目录 `…\ObjectARX Props` | **默认跟随安装目录**，字段可改，改了就不会再被覆盖 | 你要求“props 就在安装目录，也允许用户自己指定”。`/PROPSDIR=` 一旦给出即视为已定 |
| 路径字段 | 4 个（SDK 根 / AutoCAD 根 / SDK 位置 / AutoCAD 位置） | **3 个**：Autodesk 根目录、ObjectARX SDK 位置、属性表目录 | 你确认合并。两个“根”本来就是同一个值；**且不再拼 `AutoCAD 2026\`**——这是多年度产品，生成的属性表自己按年份拼 `<根>\ObjectARX <年>\` 与 `<根>\AutoCAD <年>\` |
| UI 语言 | 英文 | **中文优先 + 英文兜底** | 你要求。`[Languages]` 里中文在前，中文系统显示中文，其它显示英文 |
| RDS 默认值 | `TST` | **空（留空即不替换 ADSK）** | 你要求可留空 |

`arxCommon.js` 里 `ARX_PROPS_DIR` 的写回沿用 MSI 的形态：`var ARX_PROPS_DIR ="<双反斜杠路径>\" ;`（含结尾的 `\" ;`）。

**RDS 可以留空**（你指出的）：默认值由 `TST` 改成**空**；留空时**不做 ADSK 替换**。这里刻意不按“空就替换成空串”处理——MSI 的 `PatchHTMLWizFiles` 是 `RDS 为空 ? "ADSK" : RDS`，即空值时 ADSK→ADSK（什么都不改）；替换成空串会把 MSI 从不触碰的文本删掉。

### 10.2.1 单年度 2026 属性表：曾短暂保留，现已移除

早期缺省安装里带一份写死 2026 的属性表（`_Installs\Autodesk.arx-2026.props` 与 `-net` 变体，装到 `{app}` 根和 `ArxAppWiz\Templates\1033\`），安装后再由一个 Patch 动作把里面的出图机器路径改成用户给的根目录（当时定的口径是“退到目录、不拼 `AutoCAD 2026\`”）。

**这个方案已整体作废并删除**，理由见第 12 节：多年度产品固定带一份单年度模板本身就是版本特定的设定；而且属性表目录默认跟随安装目录之后，生成器写出的同名文件会直接把它覆盖，未勾选 2026 时它又只会以陈旧内容留在盘上。现在这些路径一律由生成器按勾选年份产出。

### 10.3 三处按 Inno 习惯实现（结果等价）

1. **路径修补**：MSI 在目录里递归找 `*.vsz` / `default.htm` / `arxCommon.js`；Inno 侧对刚装下去的确定文件逐个替换（结果集相同），且**只在内容真的变了才写回**——几个老 HTML 页含非 ASCII 字节，避免无谓重写。
2. **卸载清理**：用 Pascal 的 `FindFirst('Autodesk.arx-*.props')` 通配删除（等价于 `[UninstallDelete]`，但能带上运行期才知道的 props 目录）。
3. **维护页**：用 Inno 自带的修改/修复/卸载流程，不复刻 MSI 的 `MaintenanceForm`；“改年份”＝带新的 `/YEARS` 再装一次。

### 10.4 生成器的两处约定

- 年份表与三个骨架**内嵌自 `tools\arx-props`**（与 MSI 的 CA 是同一份文件），逻辑从 CA 的 `ArxProps` 类移植；MSI 线的 CA 没有改动，避免动 MSI 构建。
- 输出与 `gen-arx-props.ps1` **文本逐字节一致**，唯一差异是 **BOM**：`.ps1` 写 UTF-8 BOM，EXE 不写——因为 MSI 的 CA 用的是 `UTF8Encoding(false)`，Inno 线要留下和 MSI 一样的字节。测试里把这条差异单独断言。

### 10.5 已跑过的验证

- `tools\arx-genprops\test-arx-genprops.ps1`：33 个 props 文本零 diff（+ BOM 差异断言）、generate/cleanup/remove 语义、空年份、`ObjectARX.User.props` 不被覆盖 → **全部通过**。
- `InnoSetupInstaller\test-inno-sandbox.ps1`：把同一份 `.iss` 派生出一份 `PrivilegesRequired=lowest` + `HKCU` 的测试副本（不动 HKLM / Program Files），全部路径用静默开关重定向到临时目录，跑 5 轮真实安装/卸载 → **40 项检查全部通过**：
  1. 不带 `/PROPSDIR` 装一次：props 落进安装目录、注册表 `PropsDir` 跟着走、**留空 RDS 时 ADSK 保留**、卸载清掉生成的属性表；
  2. 常规安装：载荷、9 个 `.vsz` 的 `[TARGETDIR]`、RDS 替换、**载荷里不再有单年度属性表**、`arxCommon.js`、共享 props、生成 props、注册表三项；
  3. 原样重装：生成集合不变；
  4. 收窄年份重装：未勾年份被清掉；
  5. 带 VSIX 步骤重装 + 卸载：不留生成的属性表与注册表，永久项（共享 props、SDK `inc`）保留。
- `InnoSetupInstaller\test-payload-parity.ps1`：解析 `directory.wxi` 得到的 `TARGETDIR` 集合与 `.iss` 的安装集合一致（**215 = 215，双向零差异**；去掉 4 个单年度属性表之前是 219）。

### 10.6 还没做的 / 需要你确认的

- **界面还没被人眼看过**：上面全是无 GUI 的自动断言。向导的实际观感（中文是否正常显示、控件有没有重叠/截断）**需要手动双击** `InnoSetupInstaller\Output\ObjectARXMultiVersionWizardsSetup-Inno.exe` 确认一次。建议带上 `/DIR= /PROPSDIR= /ARXROOT= /ARXSDKPATH= /VSROOT= /SKIPVSIX=1 /SKIPVSCHECK=1` 指向临时目录，这样即使误点安装也不会动到真机。
- **默认安装位置：已确认**用 64 位 `Program Files`（`{autopf}` 配合 `ArchitecturesInstallIn64BitMode=x64compatible` + `PrivilegesRequired=admin` 解析而来）。理由：插件都是 64 位，不再考虑 32 位。与 MSI 的 `(x86)` 布局不一致是刻意的。
- 打 `v0.1.2-inno` tag 与第 7 节的实测对比（体积 / 耗时 / 静默参数 / 企业分发）需要管理员真机跑一次。
- 代码签名（R4）两版都还没有。

## 11. MSI 侧同步（0.1.2）

按要求把第 10.2 节的取舍**同步回 MSI 线**，版本与 ProductCode 成对升到 **0.1.2**。

### 11.1 改了什么

| 文件 | 改动 |
|---|---|
| `property.wxi` | `CA_TARGETDIR` 由 `[ProgramFilesFolder]` 改为 `[ProgramFiles64Folder]`（(x86) → 64 位 `Program Files`）；`CA_DefaultPropsDir` 由 `[PROPSDIR_PROBE]`（注册表记忆）改为 `[TARGETDIR]`；新增 `CA_DefaultPropsDirAuto` + `ARXPROPSDIR_AUTO`；`RDS` 默认值由 `TST` 改为**空**；删除 `ACAD`、`ACADROOT`、`ACADROOT_PROBE` 及其 `RegistrySearch`；新增 `InstallDirForm` 的导航属性 |
| `UI.wxi` | 删除 ObjectARXForm 的“AutoCAD location”与 SdkForm 的“AutoCAD root folder”两个字段（路径字段 4→3）；新增 `InstallDirForm` 目录选择页（绑 `TARGETDIR`）；**文案整体中文化** |
| `ObjectARXWizards.wxs` | `Codepage` 1252→936、`Language`/`Languages` 1033→2052；`ProductVersion` 0.1.1→0.1.2 且 `ProductCode` 换新；`Condition` 提示改中文；序列里删掉 `CA_DefaultAcadRoot`、补上 `CA_DefaultPropsDirAuto` |
| `directory.wxi` | `RV_ACADROOT` 的值由 `[ACADROOT]` 改为 `[ARXROOT]`（注册表仍是 `ArxRoot` + `AcadRoot` 两个值，都指向同一个 Autodesk 根）；注释更新 |
| `Bundle.wxs` | `Version` 0.1.1→0.1.2；`UninstallCommand` 里的 ProductCode 换新 |
| `ArxWizCustomAction\CustomAction.cs` | `PatchPropsWizFiles` 的 AutoCAD 侧由 `ACAD` 改为 `ARXROOT`；`PrepArxPropsData` 去掉 `ACADROOT`，`CreateArxProps` 里 `acadRoot = sdkRoot`；年份摘要的空文案改中文；新增 `Paths.EnsureTrailingSlash` 并在上述两处套用 |

**尾反斜杠**：根目录是**前缀**——`props-template.props` 里是 `$(AcadRoot)AutoCAD @@YEAR@@\` 与 `$(ArxSdkRoot)ObjectARX @@YEAR@@\`，缺了尾反斜杠会拼成 `…\AutodeskAutoCAD 2026\`。三处默认值（`ARXROOT_PROBE`、年份表的 `defaultRoot` 与 `win32RootX86`）本来就带，所以默认安装没问题；但用户在向导里**手打**的路径不一定带，之前 MSI 没有这道防线（Inno 侧真机踩过同一个坑，就是那次 `EnsureTrailingSlash` 修复）。

### 11.2 属性表目录跟随安装目录（MSI 版怎么做的）

MSI 没有 Inno 那种“在 Pascal 里比较两个字段”的能力，所以用一对属性模拟：

- `CA_DefaultPropsDir` 在 costing 之前把 `ARXPROPSDIR` 种成 `[TARGETDIR]`（沿用 MSI 既有的“开工前先填目录属性”手法）；
- `CA_DefaultPropsDirAuto` 同时把 `ARXPROPSDIR_AUTO` 种成同一个值；
- `InstallDirForm` 的 Next 只在 `ARXPROPSDIR=""` 或 `ARXPROPSDIR=[ARXPROPSDIR_AUTO]` 时才把 `ARXPROPSDIR` 重新同步成 `[TARGETDIR]`，随后无条件刷新 `ARXPROPSDIR_AUTO`。

这样：用户没动过属性表目录时它一直跟着安装目录；一旦在 SdkForm 里自己填了（或命令行给了 `ARXPROPSDIR=`），值与 AUTO 不再相等，**从安装目录页 Back 回来再 Next 也不会把它冲掉**，等价于 Inno 的 `PropsAuto` / `PropsPinned`。

顺序有讲究：`CA_DefaultPropsDir` 必须在 `CA_DefaultPropsDirAuto` **之前**，否则第二个动作的 `ARXPROPSDIR=""` 条件已被第一个动作弄假、AUTO 就种不上了；InstallDirForm 里两条 `Publish` 的先后同理不能换。

### 11.3 语言

一个 MSI 只能带一种 UI 语言（不像 Inno 的 `[Languages]` 能按系统语言挑），所以这里选了**整体中文**：`Codepage=936` / `Language=2052`，`UI.wxi` 的文案直接写在原位。“中文优先 + 英文兜底”在 MSI 线上做不到，这是两条线**唯一剩下的差异**。

### 11.4 验证情况

- 首次校验时本机没有 WiX，先临时取了 **WiX 3.14.1** 二进制到 `%USERPROFILE%\.cache\wix314`；**随后已正式安装**：`winget` 的 `WiXToolset.WiXToolset 3.14.1.8722`（= 官方 `wix314.exe`，需 UAC），装到 `C:\Program Files (x86)\WiX Toolset v3.14\`，注册表 `InstallRoot` 与 `…\MSBuild\Microsoft\WiX\v3.x\Wix.targets` 均已就位。
- 用 `candle` + `light`（含 `-v` 全量 ICE 校验）编译：**通过**；只有一个改动前就存在的 `ICE48` 警告（`ARXPATH` 默认值是写死的本地盘路径）。
- 装好 WiX 后两条构建入口都实测通过：`ObjectARXWizardsInstaller\make.bat`（直接调 candle/light）与 `msbuild ObjectARXWizard.wixproj`（VS 走的那条）。
- **构建产物不再弄脏工作区**：`make.bat` 的输出名原来叫 `ObjectARX2026Wizards.msi`（单年度"ObjectARX 2026 Wizards"时代的遗留，和年份表无关），现已改为年份中性的 **`ObjectARXMultiVersionWizards.msi`**，连 `.wixpdb` 一并写进 `.gitignore`。另外把三个**本就声明要忽略、却被上游误提交**的构建产物取消跟踪：`ObjectARX2026Wizards.wixpdb`（改名后成无主文件，已删除）、`ObjectARXWizards.wixpdb`、`temp/ObjectARXWizards.wixobj`（后两个由 `.gitignore` 第 6-7 行声明）—— 文件仍在磁盘上，历史里也能找回。
- `.wixproj` 的 `PostBuildEvent` 用 `7z` 打 zip，本机没装 `7z`，所以走 MSBuild 时要把 `PostBuildEvent` 清空（`/p:PostBuildEvent=`）才能整体返回 0；`make.bat` 不受影响。
- `ArxWizCustomAction.csproj` 已用 MSBuild 重新编译，`Binary\ArxWizCustomAction.CA.dll` 已更新（MSI 引用的就是这个）。
- `Bundle.wxs` 能 `candle` + `light` 通过（链上重新构建的 MSI 一起链接成功，产物约 1.37 MB）。
- 用 `dark.exe` 反编译核对过关键表：`CA_TARGETDIR` 用 `ProgramFiles64Folder`、`CA_DefaultPropsDir` = `[TARGETDIR]`、`ARXPROPSDIR_AUTO` 存在且序列顺序正确、`RDS` 无默认值、`ACAD`/`ACADROOT` 已消失、`InstallDirForm` 挂进 `WelcomeForm → InstallDirForm → ObjectARXForm` 导航。
- 中文文案经 MSI 数据库往返读取正常（`Codepage=936` 生效）。
- **尚未真机安装验证**：`InstallDirForm` 的实际观感、以及“改安装目录后属性表目录跟着走”这条链需要装一次看。

### 11.5 遗留

- `ArxWizPatchFilesCA\` 是历史遗留的第二份 CA 源码，**不在** `ObjectARXWizard.sln` 里、也不参与 MSI 构建（MSI 用的是 `ArxWizCustomAction`）。它里面还留着 `session["ACAD"]`，本次没有动。
- VSIX 的静默安装仍由 `Bundle.wxs` 的 `ArxWizardsVsixSetup.exe` 负责；引导程序与载荷逻辑未改，但 VSIX 本身已随版本对齐重建（见 11.7）。

### 11.7 版本对齐到 0.1.2（含 VSIX 重建）

MSI 升到 0.1.2 之后，另外三处版本号还停在 0.1.1，一并拉齐：

| 位置 | 原值 | 现值 |
|---|---|---|
| `ArxVsixWizard\ArxVsixWizard.csproj` 的 `<Version>` | 0.1.1 | 0.1.2（程序集因此为 0.1.2.0） |
| `ArxVsixWizard\source.extension.vsixmanifest` 的 `Identity/@Version` 与 `Asset/@AssemblyName` | 0.1.1 / 0.1.1.0 | 0.1.2 / 0.1.2.0 |
| `InnoSetupInstaller\ObjectARXMultiVersionWizards.iss` 的 `AppVersion` | 0.1.1 | 0.1.2 |

**VSIX 必须重建，不只是改号**：`ArxVsixWizard.csproj` 把 `Templates\ArxApp\*` 作为 **EmbeddedResource** 编译进 `ArxVsixWizard.dll`（`LogicalName="ArxApp.%(Filename)%(Extension)"`），所以第 12 节对 `Templates\ArxApp\ReadMe.txt` 的中性化改动会落进扩展里 —— 仓库根那份提交过的 `ObjectARXMultiVersionWizards.vsix` 一度因此过期。已用 MSBuild 重建（VSSDK 自动还原），并验证内嵌资源里是新的 `Autodesk.arx-<year>.props`，旧的 `Autodesk.arx-2026.props` 与 “installer also provided…” 已不存在。

### 11.8 四个产物（本次构建）

| 产物 | 路径 | 体积 |
|---|---|---|
| Burn 引导程序（MSI 线入口，双击用这个） | `ObjectARXMultiVersionWizardsSetup.exe`（仓库根） | 1.30 MB |
| MSI 本体 | `ObjectARXMultiVersionWizards.msi`（仓库根） | 1.10 MB |
| VSIX 扩展 | `ObjectARXMultiVersionWizards.vsix`（仓库根） | 0.17 MB |
| Inno 单文件安装包 | `InnoSetupInstaller\Output\ObjectARXMultiVersionWizardsSetup-Inno.exe` | 2.43 MB |

`make.bat` 的输出名已是年份中性的 `ObjectARXMultiVersionWizards.msi`；`bin\Release\ObjectARXWizard.msi` 是引导程序真正内嵌的那份载荷（两者内容一致，只是名字与位置不同，历史遗留）。

### 11.6 构建产物不再弄脏工作区

`make.bat` 的输出名原来叫 `ObjectARX2026Wizards.msi`（单年度“ObjectARX 2026 Wizards”时代的遗留，和年份表无关），现已改为年份中性的 **`ObjectARXMultiVersionWizards.msi`**，连 `.wixpdb` 一并写进 `.gitignore`。另外把三个**本就声明要忽略、却被上游误提交**的构建产物取消跟踪：`ObjectARX2026Wizards.wixpdb`（改名后成无主文件，已删除）、`ObjectARXWizards.wixpdb`、`temp/ObjectARXWizards.wixobj`（后两个由 `.gitignore` 第 6-7 行声明）—— 文件仍在磁盘上，历史里也能找回。

## 12. 去版本化：多年度产品不该带单年度设定

原则：**这是 2010–2027 的多年度向导，产品里不该有绑定单个年份的设定。** 一次盘查后清掉了这些：

| 项 | 原状 | 处理 |
|---|---|---|
| 单年度属性表载荷 | `_Installs\Autodesk.arx-2026.props` + `-net`，装到 `{app}` 与 `ArxAppWiz\Templates\1033\`，再由 `PatchPropsWizFiles` 按 `*2026.props` 通配修补 | **删除载荷与修补动作**，两线同步（MSI：4 个 `File` 项 + `CA_PatchPropsWizFiles` 及其 3 处序列；Inno：2 条 `[Files]` + `PatchPropsWizardFiles` 过程） |
| 构建产物名 | `ObjectARX2026Wizards.msi` | 改为 `ObjectARXMultiVersionWizards.msi`（见 11.6） |
| Add Class 分类目录 | `_Installs\VC\VCAddClass\ObjectARX2026\` —— 该目录**没有 `.vsdir`**，所以目录名本身就是 VS「添加类」里显示的分类名，会显示成 “ObjectARX2026” | `git mv` 改为 `ObjectARX`（历史安装日志显示旧版原名就是 `ObjectARX`，且 MSI 的目标目录本来也叫 `ObjectARX`） |
| 注释 / 文档文本 | `ObjectARXWizards.wxs` 的 `for ObjectARX 2024/2025/2026`、`CustomAction.cs` 里的旧路径注释、两份 `ReadMe.txt` 里的 `Autodesk.arx-2026.props` 与“installer also provided an ObjectARX 2025.props” | 全部改成中性表述（`<year>` 或 “retired per-year release”） |

**刻意保留**（它们是“多年度”本身，不是版本绑定）：16 个 `YEAR_*` / `ARXINV_*` / `DET_YEAR_*` 属性、年份复选框、默认勾选集 `2020/2024/2026/2027`、Compatible 集、产品名里的 `(2010-2027)`。

> 0.1.4 起 `_Installs\ObjectARX Props\` 下那些 HCSoft/ZWSoft/ObjectARX.Common 之类的属性表**不再是载荷**（见第 17 节）：它们是制作者本机其它项目的实验品，不是这套向导需要的东西。

### 12.1 验证

- MSI（`candle` + `light`，含全量 ICE）与 Bundle 均编译通过；`dark.exe` 反编译确认 `PatchPropsWizFiles` 出现 **0** 次、`ObjectARX2026` 出现 **0** 次，残留的 `2026` 全部来自年份表与第三方载荷。
- Inno `.iss` 编译通过（UTF-8 BOM 保持），产物 `ObjectARXMultiVersionWizardsSetup-Inno.exe`。
- 三套测试全绿：载荷对齐 **215 = 215**、Inno 沙箱 **40 项全过**、生成器回归全过。
- **待真机验证**：`ArxAppWiz\Templates\1033\` 不再有属性表模板之后，老 HTML 向导建工程能否仍从属性表目录取到 `Autodesk.arx-<年>.props`（`Templates.inf` 并不引用该文件，`arxCommon.js` 的 `ARX_PROPS_DIR` 才是入口，理论上无碍）。

## 13. 第二轮的完善（已提交后追加）

### 13.1 清理死代码与残留

- **删除 `ArxWizPatchFilesCA\`**：第二份自定义动作源码，**不在** `ObjectARXWizard.sln` 里、不参与任何构建，且仍引用已删除的 `session["ACAD"]`。
- **删除 `ObjectARXWizardsInstaller\testlog.txt`（839 KB）与 `ObjectARXWizard.zip`**（后者是 `.wixproj` 的 `PostBuildEvent` 用 `7z` 打的 zip，`7z` 本机都没有，纯产物），并把 `.gitignore` 补了 `ObjectARXWizard.zip`。
- `temp\ObjectARXWizards.wixobj` 上一轮已随 `ObjectARXWizards.wixobj` 一起取消跟踪，本轮把目录一并删掉。

### 13.2 载荷组件全部 64 位

MSI 的 `InstallPrivileges=elevated InstallScope=perMachine Platform=x64`、安装目录在 64 位 `Program Files`，但 45 个组件**都没有 `Win64="yes"`**——ICE80 在 WiX 链路里只查包摘要、不逐组件报，所以一直没暴露；后果是组件注册（含 `C_ARXPROPS_REG` 那段注册表）落在 **32 位视图**。

修法不逐组件加标记，而是给构建器加架构：`make.bat` 的 `candle` 加 `-arch x64`，`ObjectARXWizard.wixproj` 加 `<InstallerPlatform>x64</InstallerPlatform>`（否则 MSBuild 路径下它默认取 `$(Platform)`=x86，因为自定义动作必须 32 位）。这样 candle 把全部组件标为 64 位，`dark` 反编译确认 **45/45 带 `Win64="yes"`**。SfxCA 桩的自定义动作仍按 32 位进程跑，与载荷位数无关。

### 13.3 Burn 引导程序中文

MSI 已是中文，但双击入口 `Setup.exe`（`WixStandardBootstrapperApplication`）一直是英文。新增 `ObjectARXWizardsInstaller\Bundle.zh-CN.wxl`（照着 WiX 自带的 `SDK\themes\HyperlinkTheme.wxl` 翻译，保留 `&amp;` 助记符），并：

- `Bundle.wxs` 的 BA 加 `LocalizationFile="Bundle.zh-CN.wxl"`；
- `ObjectARXWizardsBundle.wixproj` 加 `<Cultures>zh-CN</Cultures>` 并把 `.wxl` 加进 `<WixLocalization>`。

构建时用 `light -cultures:zh-CN`。`dark` 反编译最终产物确认 `thm.wxl` 已是中文（"欢迎"、"安装(&amp;I)" 等）。

### 13.4 年份清单单一真相 + 一致性检查

年份清单共 4 处需要逐字枚举，MSI 又无法循环（`<?include?>` 要求被包含文件以 `<Include>` 为根元素，无法注入逐年的 `<Publish>`；自定义动作改属性又刷不动勾选框），所以选**一致性检查**而不是生成三份：

- `tools\arx-props\arx-props-table.json`（唯一真相）给每年加 `"default"`（默认勾选）与 `"compatible"`（兼容集）标记；
- `ArxWizCustomAction\CustomAction.cs` 不再硬编码 `DefaultYears = { "2020", ... }`，改为 `DefaultYears()` 从内嵌 JSON 读 `default` 标记；
- 新增 `tools\arx-props\test-years-consistency.ps1`：断言 JSON 与 `property.wxi`（`YEAR_*`/`ARXINV_*`/`DET_YEAR_*`）、`UI.wxi`（16 个复选框 + All/None/Compatible/Invert 的发布集合）、`.iss`（`CsvYearsLeft/Right/Default/Compatible`）、以及 CA 不再硬编码，**四方全部一致**。

以后增删年份只改 JSON；有站点漏改，这个测试会当场失败。

### 13.5 验证

- `test-years-consistency.ps1`：**ALL CHECKS PASSED**。
- 重编 MSI 后用 `dark` 反编译与改造前的基线**逐行 diff：312 行完全一致**——证明本轮只补了构建属性，没动任何对话框/行为；`PatchPropsWizFiles`、`ObjectARX2026` 仍为 0。
- 三套既有测试全绿：载荷对齐 **215 = 215**、Inno 沙箱 40 项全过、生成器回归全过。
- 四产物已重建：`Setup.exe` 1.30 MB（中文）、`ObjectARXMultiVersionWizards.msi` 1.10 MB（45/45 Win64）、`...-Inno.exe` 2.43 MB。

## 14. VSIX 向导加载失败（真机测出，已修）

真机装完在 VS 里新建工程时报：

```
错误: 此模板尝试加载组件程序集 "ArxVsixWizard, Version=26.2.0.0, Culture=neutral,
      PublicKeyToken=null"。有关此问题和如何启用此模板的详细信息，请参阅有关"自定义项目模板"的文档。
```

### 14.1 根因

`ArxVsixWizard\Packaging\**` 下 **9 个 `.vstemplate`** 的 `<Assembly>` 仍写着 **`Version=26.2.0.0`** —— 那是旧 26.x 产品线的版本号。而程序集本身已经是 `0.1.2.0`，于是 VS 按模板声明的标识去加载程序集，找不到就报上面那句。

**0.1.1 那次"版本号重新起算"漏了这 9 个文件。** 这不是新引入的问题，而是这一轮"去版本化"的同一类残留 —— 之前只清了 MSI/Inno 侧，VSIX 模板侧没扫到。`ArxVsixWizard\README.md` 里其实早就把它记成了已知坑（"版本号变更时要同步"），但没有测试兜住，所以静默漂移了一个版本。

### 14.2 修法与防回归

- 9 个 `.vstemplate` 的 `<Assembly>` 由 `26.2.0.0` 改为 `0.1.2.0`；
- 重建 VSIX 并刷新仓库根的 `ObjectARXMultiVersionWizards.vsix`；
- 新增 `ArxVsixWizard\test-version-consistency.ps1`：断言**四份版本声明**一致，并直接解包**已构建的 VSIX** 复核其中的每个 `.vstemplate` 与 manifest——

  | 位置 | 应等于 |
  |---|---|
  | `ArxVsixWizard.csproj` 的 `<Version>` | `0.1.2` |
  | `source.extension.vsixmanifest` 的 `Identity/@Version` | `0.1.2` |
  | 同文件的 `Asset/@AssemblyName` 里的版本 | `0.1.2.0` |
  | `Packaging\**\*.vstemplate` 的 `<Assembly>` 标识（9 个） | `ArxVsixWizard, Version=0.1.2.0, Culture=neutral, PublicKeyToken=null` |

这个测试在修之前**精准复现了报错**（当时报告 9 个模板全是旧的），修完全绿 —— 所以它确实拦得住这类漂移。

### 14.3 验证

- `test-version-consistency.ps1`：**ALL CHECKS PASSED**（9/9 模板 + manifest + 已构建产物）。
- 从 Bundle 里解包内嵌的 VSIX 复核：**9/9 模板带 `0.1.2.0`、manifest `0.1.2`** —— 证明装到用户机器上的那份是对的，不只是仓库根那份。
- 五套测试全绿：VSIX 版本一致性、年份一致性、载荷对齐 215 = 215、生成器回归、Inno 沙箱 40 项。
- 两个安装包均已重建（VSIX 17:35 → 安装包 18:13）。

## 15. 生成 Resource.h 时新 ID 的位置（真机测出，已修）

### 15.1 现象

用 MFC Support 向导加类后，`Resource.h` 长这样：

```c
#define IDS_PROJNAME 100

// Next default values for new objects
//
#ifdef APSTUDIO_INVOKED
...
#define _APS_NEXT_RESOURCE_VALUE 103
#endif
#endif
#define IDD_MYMFCCLASS 102          // <-- 被追加到了文件最末尾
```

新 ID 落在**资源编辑器自己那块 bookkeeping 之后**。正确位置是 `IDS_PROJNAME` 这类真实 ID 的后面、`// Next default values for new objects` 之前 —— 那一段往下都是 VS 资源编辑器拥有的 `_APS_NEXT_*` 记账区，追加到它后面等于不在编辑器管理的范围内。

### 15.2 根因与修法

两处都是 `TrimEnd()` 后直接拼接，所以永远追加到文件尾：

| 文件 | 方法 | 用途 |
|---|---|---|
| `Items\MfcSupport\MfcResourceEditor.cs` | `UpdateResourceHeader` | 写 `#define IDD_<类名> <值>` |
| `Items\AtlCommon\AtlItemSupport.cs` | `EnsureResourceDefine` | 写 `#define IDR_<名> <值>`（ATL/COM 的 .rgs 资源） |

新增 `Items\ResourceHeader.cs`（同在 `ArxVsixWizard.Items`），`InsertDefine(text, definition)` 把定义行插到 `// Next default values for new objects` **之前**；该标记缺失时退到 `#ifdef APSTUDIO_INVOKED` 之前，再没有才追加到末尾。两处调用都改成走它。

修完的实际输出：

```c
#define IDS_PROJNAME 100
#define IDR_MAINFRAME 101
#define IDD_MYMFCCLASS 102

// Next default values for new objects
...
#define _APS_NEXT_RESOURCE_VALUE 103
```

### 15.3 防回归

模板冒烟测试（`TemplateSmokeTest`）原有的 `_rescheck` 用例里，合成 `Resource.h` 只有 `_APS_NEXT_RESOURCE_VALUE` 一行、没有 bookkeeping 块，正好只覆盖了「退到追加」这条分支。新增 `_rescheck2`：用与 `Templates\ArxApp\Resource.h` 同形的完整文件（含 `// Next default values` + `#ifdef APSTUDIO_INVOKED`），断言新 ID 的偏移 **小于** 该标记的偏移，且 `IDS_PROJNAME` 不被破坏。

### 15.4 验证

- 模板冒烟测试：**ALL CHECKS PASSED**（含新增两条断言，既有 6 条 MfcResourceEditor 断言未受影响）。
- 其余四套同样全绿：VSIX 版本一致性、年份一致性、载荷对齐 215 = 215、Inno 沙箱 40 项。
- VSIX 与两个安装包均已重建（VSIX 模板是内嵌资源，必须重建才会带出去）。

## 16. 版本升到 0.1.3（为了能覆盖安装）

### 16.1 之前到底能不能覆盖

两条线的判定完全不同，之前之所以"装上去没变化"，问题只在 VSIX：

| 线 | 判定依据 | 结论 |
|---|---|---|
| **MSI / Burn** | `UpgradeCode` 相同 → `UPGRADEFOUND` 区间 `[0.0.0, ProductVersion)` 命中即 `RemoveExistingProducts`；`NEWERPRODUCTFOUND` 只拦更高的版本 | **0.1.2 能自动覆盖 0.1.1**。但区间**不含当前版本**，所以重跑同一个 0.1.2 只会进维护页——必须升号才能产生一次真正的升级 |
| **Inno** | 只看 `AppId`（常量 GUID），没有任何版本比较 | 永远直接覆盖，不受版本限制 |
| **VSIX** | `VSIXInstaller` 比 `Identity/@Version` | **版本不变就判定"已安装"、静默跳过** —— 这正是前面反复要手动 `/uninstall` 的原因 |

所以结论：升最后一位数字就能让三处都干净地覆盖。

### 16.2 改动（0.1.2 → 0.1.3）

| 位置 | 改动 |
|---|---|
| `ObjectARXWizards.wxs` | `ProductVersion` → 0.1.3，**`ProductCode` 同步换新**（`{2D2AA7F2-…}`）—— 项目硬约束：两者必须成对变更，只改一个会报 1638 |
| `Bundle.wxs` | `Version` → 0.1.3；`UninstallCommand` 里的 ProductCode 跟着换 |
| `ArxVsixWizard.csproj` | `<Version>` → 0.1.3（程序集由此为 0.1.3.0） |
| `source.extension.vsixmanifest` | `Identity/@Version`、`Asset/@AssemblyName` → 0.1.3 / 0.1.3.0 |
| `Packaging\**\*.vstemplate`（9 个） | `<Assembly>` → `Version=0.1.3.0` —— **上次 0.1.1 就是漏了这里才导致向导全部加载失败**，这次由 `test-version-consistency.ps1` 兜着 |
| `ObjectARXMultiVersionWizards.iss` | `AppVersion` → 0.1.3 |

### 16.3 验证

- MSI 内部：`Version="0.1.3"`、新 ProductCode、`Codepage=936 Language=2052`；升级区间为 `[0.0.0, 0.1.3)`，不含自身。
- Bundle 内嵌的 VSIX 与仓库根那份**逐字节一致**；其 manifest 为 0.1.3、9/9 模板为 0.1.3.0。
- 五套测试全绿：VSIX 版本一致性、年份一致性、模板冒烟、载荷对齐 215 = 215、Inno 沙箱 40 项。

### 16.4 升级路径（这次不用先卸载）

0.1.3 换了 ProductCode，所以 Burn/MSI 会把它识别为一次正常升级；VSIX 版本也变了，`VSIXInstaller` 不再跳过。**任意旧版（0.1.1 / 0.1.2，两条线）都能直接装 0.1.3**。

## 17. 0.1.4 / 0.1.5：改名 Multi-Version、去掉实验性属性表、记住上次安装路径、修图标

0.1.4 是前三项；0.1.5 只做图标（见 17.4），顺带把版本号再推一格，好让新 VSIX 能替换掉刚装上去的 0.1.4。

### 17.1 改名

`Multi-Year` → `Multi-Version`，`Multi-Year Wizards (2010-2027)` → `Multi-Version Wizards (2010-2027)`。文件与文案一起改（`.iss`、`.vsix`、`.msi`、`Setup.exe`、`.wixproj` 的 `OutputName`、`make.bat`、`.gitignore`、`Bundle.wxs` 的 `Payload`、`VsixSetup.cs` 的默认文件名、两份 `.vstemplate` 的 `<Name>`、`MsiSetup.cs` 的两个消息框）。

**没改**的是 VSIX 的 `Identity Id="ObjectARX.MultiYear.Wizard"`：它是扩展标识而不是显示名，保持不变才能让新 VSIX 替换掉已装的那一份而不是在 VS 里多出一个扩展。若要一并改，`Identity`、`reinstall-vsix.ps1` 的 `$vsixId` 和 README 里的三处引用要同时改，并且旧扩展必须先手动卸载。

三者一起变、缺一不行：MSI 的 `ProductVersion` + `ProductCode`（`{4E9C1F38-…}`）、`Bundle.wxs` 的 `Version` + `UninstallCommand`、VSIX 的 csproj/manifest/9 个 `.vstemplate`。

### 17.2 属性表载荷清空

`_Installs\ObjectARX Props\` 下除 `Autodesk.arx-*` 外的 23 个 `.props`（HCSoft/ZWSoft/ObjectARX.Common/CLI/CSharp、ObjectDBX、ObjectGRX、ObjectZRX）**全部撤出载荷**：

| 位置 | 改动 |
|---|---|
| `directory.wxi` | 删掉 23 个 `FGP_*` File，`C_OBJARX_PROPS_GLOBAL` 改为只 `<CreateFolder Directory="ARXPROPSDIR" />`（Permanent 保留：卸载后目录仍在，下次安装生成的属性表还落在这里） |
| `ObjectARXMultiVersionWizards.iss` | 删掉 `Source: ...ObjectARX Props\*`，改为 `[Dirs]` 建目录（`uninsneveruninstall`） |
| `arx-genprops` / `CustomAction.cs` | 删掉 `SharedPropsPatterns` 与 `--include-shared`：**安装器不再拥有那些文件，就一个也不许删**。属性表目录搬迁时只清 `Autodesk.arx-*` 与 `ObjectARX.User.props` |

理由：这些表是制作者本机其它项目的实验品，两个安装器真正安装的向导（`.vsz`/`.vcxproj`/HTML）都只 import `Autodesk.arx-<year>.props`，没有一个引用它们。仓库里的源文件保留，只是不再分发。

### 17.3 记住上次安装路径

两条线都往 `HKLM\SOFTWARE\Autodesk\ObjectARX Wizards` 写 `InstallDir`，安装时优先读它当默认值——所以**跨安装器也记得住**（上次 MSI 装的，这次 Inno 也会带出同一个目录）。

| 线 | 实现 |
|---|---|
| MSI | `C_ARXPROPS_REG` 加 `RV_INSTALLDIR`（`[TARGETDIR]`）；`property.wxi` 加 `INSTALLDIR_PROBE`（`RegistrySearch`）+ `CA_TARGETDIR_PREV`，两个序列里排在 `CA_TARGETDIR` 之前，只在 `TARGETDIR="" AND INSTALLDIR_PROBE<>""` 时生效 |
| Inno | `[Registry]` 写 `InstallDir={app}`；`InitializeSetup` 读回，`InitializeWizard` 里**只在目录框仍是出厂默认值**时才替换（`/DIR=`、Inno 自己记的旧目录都不动） |

注：0.1.3 及更早**没有**写过这个值，所以第一次装 0.1.4 时读不到（仍用出厂默认目录）；从 0.1.4 起（以及两条线互切）才生效。

### 17.4 0.1.5：向导图标

0.1.4 的 VSIX 里 9 个 `.ico` 全是同一个 **60 字节占位文件**（哈希一模一样），所以「添加类」和「新建项目」列表里每一项的图标长得完全一样、毫无区分度；模板自己的 `<Icon>` 是对的，冒烟测试只断言"图标文件存在"，所以没兜住。

改法是把老安装器那套真图标拷回打包目录（`ArxVsixWizard\Packaging`）：

| 模板 | 图标来源（`_Installs\VC`） |
|---|---|
| AtlComWrapper / AtlDynProp / CustomObject / Jig / MfcSupport / NetWrapper / Reactors | `VCAddClass\ObjectARX\Arx{Xxx}Wiz{...}.ico`（各 766 B，逐个对应） |
| ArxApp / OmfApp | `vcprojects\Autodesk\ArxAppWiz.ico` / `ArxAppWizOMF.ico`（181 KB 多尺寸） |

**保持与旧版一致的地方**：CustomObject 与 MfcSupport 共用一张图、两个项目模板共用一张图 —— 旧版就是这样，没有擅自拆分。要区分开的话再说。

冒烟测试加了一条断言（`TemplateSmokeTest\Program.cs`）：图标必须存在且 **> 200 字节**，占位桩会直接判失败。

### 17.5 0.1.6：去掉 Trial 配置

生成出来的 `Autodesk.arx-<年>.props` 里那行 `rxsdk_Releasecfg.props` 的导入条件原本还带着一个 `'$(Configuration)'=='Trial'` —— 那是老 Autodesk 产品线的 "Trial" 配置，这套向导从不产生这种配置，留着只是噪声。

唯一活源头是 **`tools/arx-props/props-template.props`**（MSI 的自定义动作和 `arx-genprops.exe` 都把这份内嵌成资源，`gen-arx-props.ps1` 读的也是它），删掉那一段即可；顺带把 `_Installs\ObjectARX Props\Autodesk.arx-*.props` 那 17 份退役副本也清了，免得以后 grep 又翻出来。

**没动的**是 `_Installs\ObjectARX Props\` 下 HCSoft/ZWSoft 那 7 份——那是别的项目的实验文件，不属于这套产品。

### 17.6 验证

- 四产物重建（0.1.6）：`ObjectARXMultiVersionWizardsSetup.exe` 1.31 MB、`ObjectARXMultiVersionWizards.msi` 1.09 MB、`ObjectARXMultiVersionWizards.vsix` 226 KB、`InnoSetupInstaller\Output\ObjectARXMultiVersionWizardsSetup-Inno.exe` 2.45 MB；旧的 `*MultiYear*` 产物已删除。Bundle 内嵌的 MSI 与 VSIX 与仓库根那份**逐字节一致**（哈希比对）。
- 生成器实测：`arx-genprops generate --years 2026` 产出的 3 份 sheet 里 `Trial` 出现 **0** 次，Release 配置导入条件只剩 `Release/2026/2026B/2026S`。
- 六套测试全绿：VSIX 版本一致性、年份一致性、模板冒烟（含新增的图标非占位断言）、载荷对齐 215 = 215、生成器回归、Inno 沙箱（新增"没装任何外来属性表""外来属性表在卸载后仍在""不给 `/DIR` 时默认落进注册表记住的目录"三条断言）。
- `dark.exe` 反编译的 MSI：`InstallDir` 注册表项、`INSTALLDIR_PROBE`、`CA_TARGETDIR_PREV` 均在；File 表里 HCSoft/ZWSoft/ObjectARX.Common 等出现 **0** 次；45/45 组件仍是 `Win64="yes"`。
- VSIX 里的 9 个图标与 `_Installs\VC` 那套逐字节相同（不再是 60 字节占位桩）。VSIX 因此从 179 KB 长到 226 KB，主要是两张 181 KB 的项目图标（zip 压缩后约 +50 KB）。
- **未在真机跑过** MSI 的"记住路径"（需要管理员权限装进 HKLM）；MSI/WiX 侧只有静态反编译核对，真机请连续装两次确认目录页预填的是上一次的目录。
- 图标观感同样只能真机看：装 0.1.5 之后打开「添加类」和「新建项目」确认 7 个类和 2 个项目模板各自有自己的图。

## 18. VS 崩溃分析：0x80040201 / ElementNotAvailable（不是本向导的代码）

### 18.1 现象

在 VS 18 Insiders（18.10.12224.181）里创建项目后，继续创建下一个项目 / 添加项时，VS 直接**进程消失**（不是普通卡死），可复现；打开已有项目再加就没有。

### 18.2 证据（来自本机留下的崩溃转储）

`%LOCALAPPDATA%\CrashDumps` 里有 3 份 devenv 转储（20:59、21:20、21:21，WER bucket 三次完全相同 `1925513046915056743`）。用 `dotnet-dump` 读出：

| 项 | 值 |
|---|---|
| 事件 | `CLR20r3`，`Exception code 0xe0434352`（未处理的托管异常），faulting module `PresentationFramework 10.0.108.26451` |
| 异常 | `System.Reflection.TargetInvocationException` → 内层 **`System.Windows.Automation.ElementNotAvailableException`**，`HResult 0x80040201`，消息 *"Element does not exist or it is virtualized; use VirtualizedItem Pattern if it is supported."* |
| 抛出点 | `Marshal.ThrowExceptionForHRInternal` ← `Microsoft.VisualStudio.Dialogs.ServiceHelper.GenerateItemName(IVsProject, UInt32, IVsTemplate)` ← `NewProjectDialog.UpdateNameField` ← `ApplyTemplateSelection` ← `TemplateSelectionChangedDelayed` |
| 触发路径 | 资源管理器右键 → 上下文菜单命令 → `SVsDialogService.InvokeDialog(VSNEWPROJECTDLGINFO)` → `DialogWindow.ShowModal`（即**在已有解决方案里"添加 → 新建项目"**） |
| 关键否定证据 | 94 个线程的托管栈里 **没有一帧属于 `ArxVsixWizard`**（`clrstack -all` 全表检索） |

### 18.3 结论

0x80040201 是 UIA 的 `UIA_E_ELEMENTNOTAVAILABLE`：VS 的「新建项目」对话框在**为选中的模板生成默认项目名**时，去问已有项目/解决方案要一个不重名的名字，这一步走到了自己的 UI Automation 提供程序，而那个元素已经被虚拟化掉了；异常从 VS 的对话框代码里逃出来，没人接 → 进程直接没了。

**这是 VS 自己的 Bug，不在本扩展的调用路径上**，两条硬证据：① 抛异常的调用链全是 `Microsoft.VisualStudio.Dialogs.*`；② 崩溃瞬间所有托管线程栈里都没有我们的程序集。

对照 VS 自带的 VC 项目模板（`Common7\IDE\ProjectTemplates\VC\MFCATL\*`）：它们同样是 `<DefaultName>` + `<ProvideDefaultName>true</ProvideDefaultName>`，所以这条 `GenerateItemName` 调用**不是我们模板独有的**——但用户当前环境里每次都在我们的模板上触发，所以 0.1.7 先按 18.5 把我们的模板从这条路径上摘出去（能摘掉就说明触点在调用侧而不是模板内容）。

### 18.4 第二轮复现（21:47 转储）追加的事实

用户装完 0.1.6 后再次复现，新增第 4 份转储，结论修正两点：

- 崩溃点与前三份**完全一致**（同一 `UpdateNameField` → `GenerateItemName` → UIA `0x80040201`），外层路径也一致（资源管理器右键 → 上下文菜单命令 → `SVsDialogService.InvokeDialog`）。所以「先建项目再选模板」这条路径每次都崩，换版本号并不影响。
- `ArxVsixWizard.dll` **已加载**（来自 `18.0_436ebb14` 那个扩展目录），但崩溃瞬间**没有任何一帧在跑我们的代码**，项目目录里也**没有任何项文件**产生 —— 崩在"选中模板 → 自动填名字"这一步，早于向导的 `RunStarted`。
- 安装是干净的：17.0 与 18.0 两个 VS 实例各只有**一个**扩展目录，dll = 0.1.6.0，9/9 模板 `<Assembly>` = 0.1.6.0，**不存在版本错配**（这条假设可以排除）。

### 18.5 0.1.7 的规避改动

我们改不了 VS，但能改**自己模板的元数据**，让 VS 不去走那条崩掉的调用：

- **项目模板（`ArxApp` / `OmfApp`）**：`<ProvideDefaultName>true</ProvideDefaultName>` 改成 **false**（`<DefaultName>` 保留）。依据：`GenerateItemName` 唯一的用途就是自动填 Name 框，而 `Microsoft.VisualStudio.Dialogs.dll` 里 `ProvideDefaultName` 与 `GenerateItemName` 两个字符串同时存在，说明这段逻辑确实认这个标志；设为 false 后 VS 不应再向项目系统要默认名，也就不会碰那个虚拟化元素。4 份崩溃转储的触发点全在 `NewProjectDialog`（18.2 / 18.4），所以这 2 个模板才是要在意的地方。
- **项模板（7 个）**：**保持 `true`**。起初一刀切全改成 false，结果 `false` 的语义（微软文档明确写着：Name 框会被填成占位值而非真实名字）让「添加新项」的名称框留空、**「添加」按钮一直是灰的**，用户表现为"选完模板却加不进去"——比崩溃更直接地把功能废掉。项模板不在崩溃路径上，没有理由付这个代价。

代价（仅剩项目模板）：新建项目时 Name 框不再自动预填。**这是规避不是定论**——如果 0.1.7 还崩，说明这条 UIA 调用另有触发点，需要按 18.4 的对照实验定位。

### 18.6 定位用的对照实验（按顺序做，做一步就能少一半可能）

1. **换成 VS 自带的 VC 模板**（如「空项目」「控制台应用」），在同样的"已有解决方案里添加"路径下点选 → 若同样崩，就是 VS 侧的问题（本扩展无关），报 Microsoft / 换 Release 通道。
2. **换到 VS 2022（17.x）**试同一套操作：本扩展在 `17.0_c051676b` 那个实例里也装了同一份 0.1.6。17.x 不崩、18.x 崩 → 确认是 VS 18 Insiders 的问题，演示时走 17.x。
3. 资源管理器树**收起来**（减少被虚拟化的节点）后再点选模板，看是否还崩 —— 崩溃消息本身就是"元素被虚拟化"。
4. 装 0.1.7 复测：Name 框不再预填，若不再崩 → 就是那条 `GenerateItemName` 调用；若照崩 → 触点在别处，把新转储给我继续挖。

### 18.7 其他

- 本扩展侧够不着这条异常：它发生在 WPF 的 dispatcher 里、属于 VS 的对话框，向导拿到控制权**之前**就崩了。
- 转储文件 4×约 350-360 MB，在 `%LOCALAPPDATA%\CrashDumps\devenv.exe.*.dmp`，确认无用后可删。
- 读转储的方法：`dotnet tool install dotnet-dump --tool-path <dir>` → `dotnet-dump analyze <dump> -c "pe" -c "printexception <内层异常地址>" -c "clrstack"`（`pe` 拿到内外层异常类型，`printexception` 拿到抛出点的托管栈，`clrstack -all` 可确认某个程序集在不在栈上）。