# 安装器去除 ObjectARX SDK 目录写入 · 说明与实施记录

> 状态：**三条线已全部完成**
> - `classic-wizards`：0.2.7，提交 `72882e6`（feat）+ `c05b6bb`（版本与文档）
> - `dev`：0.2.8，提交 `4b9f7f2`（同步本改造）+ `472df78`（补修路径页残留的 `Values[2]`，见 3.4）
> - `main`（MSI/Burn）：0.2.9，提交 `283d4fd`（改造与版本），实施记录见第 5 节
>
> 最后更新：2026-10-13（main 完成后重写第 5 节）

---

## 1. 背景与结论

安装向导第 4 页（"ObjectARX SDK 与目标位置"）原有一个 **ObjectARX SDK 位置** 输入框（默认 `C:\ObjectARX`），安装时会把 3 个文件写入该 SDK 的 `inc` 目录：`rxsdk_common.props`、`crx.props`、`arxEntryPoint.h`。这是 ADN 老版 ObjectARX Wizard 的历史遗留做法。

2026-10-13 对本机三套正式 SDK 的实测结论：

| 文件 | SDK 2022 | SDK 2024 | SDK 2027 | 判断 |
|---|---|---|---|---|
| `arxEntryPoint.h` | 自带 | 自带 | 自带（2026 版权，更新） | 不需要写；样板已 `#pragma warning(disable:4786 4996 4838)`，安装器旧版的 `static_cast<int>(-1)` 非必需，覆盖新版反而是降级 |
| `rxsdk_common.props` | 自带 | 自带 | 自带（内容已更新） | 不需要写；仅"解压不完整的 SDK"才缺 |
| `crx.props` | **缺** | 自带 | **缺**（全盘无此文件） | 唯一真缺口：年份属性表裸 `Import $(ArxSdkDir)\inc\crx.props`，2022/2027 上选 CRX 配置必报 MSB4019 |

改造方案：**不写任何 SDK 文件**，把 `crx.props` 的三项作用（导入 `dbx.props`、输出扩展名 `.crx`、追加 `accore.lib`）内联进安装器生成的年份属性表；同时删除已无消费者的 SDK 位置输入框、`/ARXSDKPATH` 参数与安装目录根下的死载荷。

---

## 2. 改造前的属性表链（问题所在）

```
Autodesk.arx-<年份>.props                 ← 安装时由本安装器生成
 ├─ inc\arx.props / dbx.props / crx.props ← 按 arx/dbx/crx 类型三选一（裸 Import，无 Exists 判断）
 └─ inc\rxsdk_Debugcfg.props / Releasecfg.props   ← SDK 自带
       └─ inc\rxsdk_common.props          ← SDK 自带，cfg 表同目录裸 Import
```

- `arx.props` / `dbx.props` 所有年份 SDK 均自带；
- `rxsdk_common.props`、`arxEntryPoint.h`（SDK 的 `arxHeaders.h` 同目录 include 它）所有受支持 SDK 均自带；
- 只有 `crx.props` 在 2013 年才引入，且 2022 / 2027 的 SDK 包中缺失。

安装目录（Multi-Version Wizards）根下复制的 `rxsdk_common.props`、`crx.props` 两份文件**没有任何引用者**，是死载荷；经典向导模板目录中的 `ArxAppWiz/Templates/1033/crx.props` 同样未被 `Templates.inf` 引用。

---

## 3. Inno 线已实施内容（classic 0.2.7 / dev 0.2.8）

### 3.1 CRX 设置内联进年份属性表（核心）

`tools/arx-props` 与 `tools/arx-genprops` 三处同源规则同步修改，新增两个占位键，替换原 `CRXIMPORT` 的指向：

| 键 | 内容（仅 `hasCrx=true` 的年份输出，2010/2012 为空） |
|---|---|
| `CRXIMPORT`（改） | `<Import Condition="'$(ArxAppType)'=='crx' or 'crxnet'" Project="$(ArxSdkDir)\inc\dbx.props" />`（由 crx.props 改为 SDK 三年都有的 dbx.props） |
| `CRXTARGETEXT`（新） | `<TargetExt Condition=…>.crx</TargetExt>`，位于骨架主 PropertyGroup |
| `CRXLIB`（新） | `<AdditionalDependencies Condition=…>accore.lib;%(AdditionalDependencies)</…>`，位于 Link ItemDefinitionGroup（与 `acgeoment.lib` 条件行并列） |

`_CRXAPP` 宏仍由既有 `CRXDEF` 片段提供；两张 .NET 骨架（输出固定 `.dll`）不引用 `crx.props`，无需改动。

涉及文件：

- `tools/arx-props/props-template.props`（骨架加两个占位）
- `tools/arx-props/gen-arx-props.ps1`（TokenMap 三键）
- `tools/arx-genprops/Program.cs`（同上，C# 实现）

### 3.2 安装脚本不再碰 SDK（两条 Inno 线）

- 删除 `[Files]` 中写入 SDK `inc` 的 3 行与安装目录根的 2 个死载荷；
- 删除 `GetArxSdkPath` / `GetArxSdkIncDir` 函数、`FallbackArxSdkPath` 常量、`InitArxSdkPath` 变量；
- 路径页由 3 个字段减为 2 个（当时为 Autodesk 根目录、属性表目录），删除中英两套 `PathsSdkPrompt` / `ReadySdk` 文案与就绪页 SDK 行；
- 删除静默参数 `/ARXSDKPATH`（含头部注释的参数说明）；
- 删除 4 个载荷源文件：`_Installs/rxsdk_common.props`、`_Installs/crx.props`、`_Installs/arxEntryPoint.h`、`ArxAppWiz/Templates/1033/crx.props`。

### 3.3 沙箱测试改为反向契约（方案外新增，提交 `72882e6` 同期完成）

`InnoSetupInstaller/test-inno-sandbox.ps1`：

- 删除 `/ARXSDKPATH` 传参与"SDK inc folder populated"断言；
- 安装目录期望清单去掉两份 props；
- 新断言 **"the installer leaves the SDK folder alone"**：安装后 SDK 目录根本不被创建（`-not (Test-Path $arxSdk)`）。
- 随后 `72f0a95` 修正该测试，使其完全不触碰真实 Visual Studio。

### 3.4 与原方案的差异说明

1. **测试基线无需更新**：原方案预估 `tools/arx-genprops/testdata/ObjectARX.Common.props` 要随新片段更新，实际该基线不包含 CRX 片段，提交中未改动，测试保持通过；
2. 沙箱反向断言原列在"可选增强"之外，实际随主改动一并落地；
3. 使用指南在 `c05b6bb` 同步：删除 SDK 位置行、就绪页项、`/ARXSDKPATH` 参数、卸载说明中的"inc 保留 3 文件"，改为"本版安装器不再向 SDK 目录写入任何文件"；
4. dev 线在 `4b9f7f2` 直接同步同一改动并升到 0.2.8（dev 无经典向导树，iss 改动面更小）；但该次同步漏掉了路径页的三处 `PagePaths.Values[2]`（`GetPropsDir` 与属性表自动跟随），页面只剩两个字段后安装会以 `List index out of bounds (2)` 崩溃并回滚——2026-10-13 由 dev `472df78` 修回 `Values[1]`，同日沙箱测试重新对齐（`acd89be`，41 项全过）；
5. main 线在 `283d4fd` 一次完成，CA 与 MsiSetup 二进制随提交重建（详见第 5 节）。

### 3.5 兼容性

- **已生成的旧工程**：重新安装后年份属性表更新，CRX 链改走内联规则，工程文件无需改动；arx / dbx 工程完全不受影响；
- **历史机器的 inc 残留**：0.2.7 之前版本写入的 3 个文件为永久保留设计，新版不清理也不再引用，残留无害（见第 6 节可选项）；
- 解压不完整、连 `dbx.props` / `rxsdk_common.props` 都缺失的非官方 SDK 不再被兜底（官方 ADN SDK 均含这些文件）。

---

## 4. 改造目标（验收标准，已达成）

1. 安装器对 ObjectARX SDK 目录**零写入、零创建**（沙箱测试断言）；
2. CRX 工程在 2013+ 任意年份 SDK（含缺 `crx.props` 的 2022 / 2027）可构建；
3. 安装页只剩 Autodesk 根目录、属性表目录两个路径；
4. 安装目录根与载荷源中不再有死文件；
5. exe 与 ps1 两个 props 生成器输出保持一致。

---

## 5. main 线（MSI/Burn）实施记录（0.2.9，提交 `283d4fd`）

> 2026-10-13 完成；下面按原"待同步清单"的 a)–d) 记录实施内容与验证结果。三条线的提交都尚未推送，待用户真机验收。

**a) CRX 内联（与 classic 相同的三处同源规则）— 已完成**

- 三份 tools 文件（`props-template.props`、`gen-arx-props.ps1`、`arx-genprops/Program.cs`）用 `git checkout 72882e6 -- …` 从 classic 直接取：两棵树的改前版本逐字相同，worktree 共享对象库，无需手工复刻；
- `ArxWizCustomAction/CustomAction.cs`：`m["CRXIMPORT"]` 改指 `inc\dbx.props`，补 `CRXTARGETEXT`、`CRXLIB` 两键；`msbuild -p:Platform=x86` 重建 `Binary\ArxWizCustomAction.dll` 与 `.CA.dll` 并提交；
- 验证：32 位 PowerShell 反射确认 CA 内嵌模板与仓库文件逐字一致、含两个新占位，DLL 中已无 `crx.props`。

**b) 删除 inc 写入与死载荷 — 已完成**

- `directory.wxi`：`ARXPATH` 整块与永久组件 `C__3CB44E97…`、TARGETDIR 根的两个 `<File>` 与组件 `C__2C63244E…` 全删；
- `feature.wxi`：上述两个 ComponentRef 删除；
- `_Installs\rxsdk_common.props`、`_Installs\crx.props`、`_Installs\arxEntryPoint.h` 已 `git rm`（`settings.xml` 保留）；
- 验证：MSI File 表由 5 行变为 **0 行**（本线的 Visual Studio 集成全在 VSIX 里），cab 为空的 LGHT1079 警告属预期。

**c) 删除 SDK 位置字段与参数 — 已完成**

- `property.wxi` 删 `<Property Id="ARXPATH">`；`UI.wxi` 删 `SdkLabel / SdkEdit / BrowseSdk`，属性表行上移到 Y 152/164；`Bundle.wxs` 删 `ArxSdkPath` 变量与 Install/Repair 两处 `--arxsdk=`；`MsiSetup.cs` 删 `--arxsdk` 与 `AppendProperty(sb, "ARXPATH", …)`，`Bundle\ArxWizardsMsiSetup.exe` 重建并提交；
- 确认 `CustomAction.cs` 无 `ARXPATH` 消费者（props 生成只用 `ARXROOT`）；
- 验证：MSI 表核对——Property 表无 `ARXPATH`、Control 表无 SDK 控件（PathsForm 仍保留三个属性表控件）。

**d) 版本与验证 — 已完成**

- 整线 **0.2.9**：VSIX（csproj / manifest / 9 个 `.vstemplate`）、MSI `ProductVersion` + 新 `ProductCode {4F2ADFF1-6B7E-4E10-BC73-E65E2B4F3B54}`、Burn `Version`；文档（两份 README、`VS-AddNewItem-Crash.md`、本线使用指南）同步；
- `test-version-consistency.ps1`、`test-years-consistency.ps1`、`test-arx-genprops.ps1` 全绿；产物 bundle `1,115,579` B、MSI `827,392` B、VSIX `245,238` B，已随三条线汇总到 `release\` 与仓库 `dist\`；
- 待用户真机回归：2022/2027 的 CRX 命令行编译（验证内联）、2024 arx/dbx/crx 各一次；全新安装确认 SDK 的 `inc` 无新增文件；维护模式与 bundle 静默变量（去掉 `ArxSdkPath` 后其余变量）回归。

---

## 6. 可选增强（三条线均未做，备忘）

1. **历史残留清理**：卸载时仅当 `inc\crx.props` 等文件与历史载荷哈希一致才删除，避免误删 SDK 2024 自带的同名文件；
2. **SDK 完整性只读检测**：路径页选定 Autodesk 根目录后，检测已勾选年份的 `inc\dbx.props` 是否存在，缺失时红字提示"SDK 似乎解压不完整"，不做任何写入。

---

## 7. 背景资料：三个文件原本的作用

- **`arxEntryPoint.h`**：样板命令框架。定义 `AcRxArxApp`（派生自 SDK 的 `AcRxDbxApp`），利用 PE 段（`ARXCOMMAND$__a/__m/__z`）提供 `ACED_ARXCOMMAND_ENTRY_AUTO`、`ACED_ADSSYMBOL_ENTRY_AUTO` 等自动注册宏，加载/卸载时批量 `acedRegCmds->addCommand/removeCmd`。各年 SDK 均在 `arxHeaders.h` 中同目录自带此文件。
- **`rxsdk_common.props`**：显示名 "Sdk Sample"，SDK 样例公共编译设置（相对 inc/lib 目录、`_UNICODE`、stdafx 预编译头、`MultiThreadedDLL`、RTTI、MIDL、x64 目标机、CLR 混合镜像等），被 SDK 的 `rxsdk_Debugcfg/Releasecfg.props` 导入。
- **`crx.props`**：仅 16 行，导入 `dbx.props`、`TargetExt=.crx`、链接 `accore.lib`——这三项现在由年份属性表内联表达。
