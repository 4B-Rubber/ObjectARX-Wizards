# 安装器去除 ObjectARX SDK 目录写入 · 说明与实施方案

> 状态：**待实施**（本文只做分析与方案，不含已提交改动）
> 适用分支：`classic-wizards`、`dev`（两条 Inno 线）、`main`（MSI/Burn 线）
> 最后更新：2026-10-13

---

## 1. 背景

安装向导第 4 页（"ObjectARX SDK 与目标位置"）有一个 **ObjectARX SDK 位置** 输入框，标注"会往它的 inc 目录写文件"（默认 `C:\ObjectARX`）。安装时安装器会把以下 3 个文件写入用户选择的 SDK 的 `inc` 目录：

| 文件 | 来源（仓库内） |
|---|---|
| `rxsdk_common.props` | [_Installs/rxsdk_common.props](../_Installs/rxsdk_common.props) |
| `crx.props` | [_Installs/crx.props](../_Installs/crx.props) |
| `arxEntryPoint.h` | [_Installs/arxEntryPoint.h](../_Installs/arxEntryPoint.h) |

疑问：用户只是"选择 SDK 位置"，安装器为何要向第三方 SDK 目录写文件？而且安装器还把其中两个文件放进了自己的安装目录（Multi-Version Wizards）根下。

**结论先行：这一步是 ADN 老版 ObjectARX Wizard 的历史遗留做法。经实测，3 个文件中 2 个纯属多余覆盖、1 个（`crx.props`）在个别年份 SDK 中确实缺失，但可以用"把 CRX 设置内联进我们生成的年份属性表"的方式彻底绕开。改造后安装器可以做到对 SDK 目录零写入，并删掉该输入框。**

---

## 2. 现状与证据

### 2.1 文件目前的两个去向（以 classic 线 Inno 脚本为例）

[InnoSetupInstaller/ObjectARXMultiVersionWizards.iss](../InnoSetupInstaller/ObjectARXMultiVersionWizards.iss)：

- **安装目录根 `{app}`**：`rxsdk_common.props`、`crx.props`（第 176–177 行）；
- **SDK 的 `inc` 目录**：上述两个 + `arxEntryPoint.h`（第 192–194 行，`uninsneveruninstall`，卸载不删）。

`dev` 线同一脚本的第 168–180 行内容相同（dev 只写 `inc` 与根，不含经典向导树）。`main` 线（以下路径均指 **main 分支** 仓库中的文件，行号以 0.2.8 为准；当前 classic 分支没有 `ObjectARXWizardsInstaller` 目录）：

- `ObjectARXWizardsInstaller/directory.wxi` 中安装根组件 `C__2C63244E…`（根目录两份 props）与 `ARXPATH\inc` 下的永久组件 `C__3CB44E97…`（3 个文件，`Permanent="yes"`）；
- `ObjectARXWizardsInstaller/property.wxi` 第 48 行 `<Property Id="ARXPATH" Value="C:\ObjectARX">`；
- `ObjectARXWizardsInstaller/UI.wxi` 第 402–406 行 `SdkLabel / SdkEdit / Browse` 控件；
- `ObjectARXWizardsInstaller/Bundle.wxs` 的 `ArxSdkPath` 变量与命令行 `--arxsdk`（`MsiSetup/MsiSetup.cs` 第 18、177 行把它映射到 MSI 属性 `ARXPATH`）。

### 2.2 安装目录根下的两份 props 是死载荷

全仓库搜索 `rxsdk_common` / `crx.props` 的引用者，只有：两条 Inno 脚本（复制动作）、props 生成器源码（生成的年份表只引用 **SDK** 路径下的副本）、MSI 自定义动作。**没有任何工程、props、脚本引用安装目录根下这两份文件**，可以直接删除。

另外经典向导模板目录 [ArxAppWiz/Templates/1033/crx.props](../ArxAppWiz/Templates/1033/crx.props) 也是死文件：[Templates.inf](../ArxAppWiz/Templates/1033/Templates.inf) 没有列它，老 vcxproj 模板也不导入它。

### 2.3 三个文件各自是否真的需要写入 inc

生成工程的属性表链（由 [tools/arx-props/props-template.props](../tools/arx-props/props-template.props) 决定）：

```
Autodesk.arx-<年份>.props                 ← 安装时由本安装器生成
 ├─ inc\arx.props / dbx.props / crx.props ← 按 arx/dbx/crx 类型三选一（裸 Import，无 Exists 判断）
 └─ inc\rxsdk_Debugcfg.props / Releasecfg.props   ← SDK 自带
       └─ inc\rxsdk_common.props          ← SDK 自带，cfg 表同目录裸 Import
```

对本机三套正式 SDK 的实测结果：

| 文件 | SDK 2022 | SDK 2024 | SDK 2027 | 判断 |
|---|---|---|---|---|
| `arxEntryPoint.h` | 自带 | 自带 | 自带 | **不需要写**。SDK 的 `arxHeaders.h` 以同目录 include 方式引用它，样板 `StdAfx.h` 已 `#pragma warning(disable:4786 4996 4838)`，安装器版唯一的实质差异（`static_cast<int>(-1)` 窄化修复）并非必需 |
| `rxsdk_common.props` | 自带 | 自带 | 自带（内容已更新） | **不需要写**。仅"解压不完整的 SDK"才缺；覆盖 2027 的新版反而是降级 |
| `crx.props` | **缺** | 自带 | **缺**（全盘搜索无此文件） | **唯一真缺口**。年份表对 CRX 类型裸 `Import $(ArxSdkDir)\inc\crx.props`，在 2022/2027 上选 CRX 配置构建必报 MSB4019 |

`crx.props` 自身内容极小，只有三件事：

```xml
<Import Project="dbx.props" />                    <!-- 拿到 acdb/acge/rxapi 等基础库 -->
<TargetExt>.crx</TargetExt>                      <!-- 输出扩展名 -->
<AdditionalDependencies>accore.lib;…</…>        <!-- 追加核心库 -->
```

这三项我们自己的年份属性表完全可以直接表达，无需依赖 SDK 目录里存在该文件。

### 2.4 SDK 路径输入框在改造后失去用途

props 生成器（`arx-genprops generate`）只接收 `--sdk-root`（Autodesk 根目录，再拼 `ObjectARX <年份>`）与 `--acad-root`，**不使用单独的 SDK 位置参数**。`inc` 写入删除后，"ObjectARX SDK 位置"字段、Inno 的 `/ARXSDKPATH`、MSI 的 `ARXPATH` / `--arxsdk` 全部失去消费者，应一并移除。

---

## 3. 改造目标

1. 安装器对 ObjectARX SDK 目录**零写入**（不再写 `inc`，也不需要用户指定 SDK 位置）；
2. CRX 工程在任何 2013+ 年份 SDK 上都可构建，不依赖 SDK 是否自带 `crx.props`；
3. 不覆盖 SDK 自带的任何文件（`arxEntryPoint.h`、`rxsdk_common.props` 各年版本由 Autodesk 管理）；
4. 删除安装目录根下无消费者的死载荷；
5. 安装页只保留两个路径：Autodesk 根目录、属性表目录。

非目标（明确不做）：

- 不处理已安装机器上 `inc` 目录里的历史残留（文件为永久保留设计，残留无害；可选清理见 §6）；
- 不改变 arx / dbx 类型的属性表链（它们 import 的 `arx.props` / `dbx.props` 所有年份 SDK 均自带）；
- 不动 .NET 混合模块的两张骨架（`props-net-fx-template.props` / `props-net-core-template.props` 固定输出 `.dll`，从不引用 `crx.props`）。

---

## 4. 实施方案

### 4.1 把 CRX 设置内联进年份属性表（核心改动）

替换 [props-template.props](../tools/arx-props/props-template.props) 第 41 行的 `@@CRXIMPORT@@` 机制。CRX 原来经 `crx.props → dbx.props` 间接拿到的东西，改为三处直接表达（均按 `$(ArxAppType)` 条件，`hasCrx=false` 的 2010/2012 仍渲染为空）：

**a) ImportGroup 内（原 `@@CRXIMPORT@@` 位置）**——crx/crxnet 直接导入 SDK 自带的 `dbx.props`：

```xml
<Import Condition="'$(ArxAppType)'=='crx' or '$(ArxAppType)'=='crxnet'"
        Project="$(ArxSdkDir)\inc\dbx.props" />
```

**b) 主 PropertyGroup 内**——输出扩展名：

```xml
<TargetExt Condition="'$(ArxAppType)'=='crx' or '$(ArxAppType)'=='crxnet'">.crx</TargetExt>
```

**c) ItemDefinitionGroup/Link 内**（与现有 `acgeoment.lib` 条件行同样写法）——追加核心库：

```xml
<AdditionalDependencies Condition="'$(ArxAppType)'=='crx' or '$(ArxAppType)'=='crxnet'">accore.lib;%(AdditionalDependencies)</AdditionalDependencies>
```

`_CRXAPP` 预处理宏由现有 `@@CRXDEF@@` 片段提供，保持不变。

**三处同源规则必须同步修改：**

| 文件 | 位置 |
|---|---|
| [tools/arx-props/props-template.props](../tools/arx-props/props-template.props) | 骨架，加占位（建议保留 `@@CRXIMPORT@@` 放 import 行，新增 `@@CRXTARGETEXT@@`、`@@CRXLIB@@` 两个占位） |
| [tools/arx-props/gen-arx-props.ps1](../tools/arx-props/gen-arx-props.ps1) | 第 94 行 `$map.CRXIMPORT` 附近，补两个新键 |
| [tools/arx-genprops/Program.cs](../tools/arx-genprops/Program.cs) | 第 305–307 行 `m["CRXIMPORT"]` 附近，补两个新键 |
| [ArxWizCustomAction/CustomAction.cs](../ArxWizCustomAction/CustomAction.cs)（仅 `main` 分支） | 第 574–576 行 `m["CRXIMPORT"]` 附近同步；该工程通过链接方式内嵌 `tools\arx-props` 的表与三张骨架（见其 csproj 的 `EmbeddedResource`），骨架文件本身不用复制 |

两个新键的空值规则与 `CRXIMPORT` 相同：`hasCrx` 为 false（2010、2012）时输出空字符串。

### 4.2 删除 SDK `inc` 写入

**classic / dev 线（[InnoSetupInstaller/ObjectARXMultiVersionWizards.iss](../InnoSetupInstaller/ObjectARXMultiVersionWizards.iss)，dev 线同名文件）：**

- 删除 `[Files]` 中 `DestDir: "{code:GetArxSdkIncDir}"` 的 3 行（classic 第 192–194 行 / dev 第 178–180 行）；
- 删除函数 `GetArxSdkIncDir`（已无调用者）。

**main 线：**

- `directory.wxi`：删除 `<Directory Id="ARXPATH">…inc…` 整块（约第 10–17 行）及永久组件 `C__3CB44E97F42E46E887E1610C0898A33F`；
- `feature.wxi`：删除该组件的 `<ComponentRef>`；
- 确认 `CustomAction.cs` 不读取 `ARXPATH`（已核实：props 生成只用 `ARXROOT`，`ARXPATH` 唯一消费者就是 inc 落盘）。

### 4.3 删除安装目录根下的死载荷

- 两条 iss：删除 `DestDir: "{app}"` 的 `rxsdk_common.props`、`crx.props` 两行（classic 第 176–177 行 / dev 第 168–169 行）；
- main 线 `directory.wxi`：删除 TARGETDIR 根下两个 `<File>`（第 6–7 行）及组件 `C__2C63244EA9004D42B1A8ED79330F5B73`，并从 `feature.wxi` 删除其 ComponentRef。

### 4.4 删除"ObjectARX SDK 位置"页面字段与相关参数

**classic / dev 线 iss：**

- `[CustomMessages]`：删除中英两套 `PathsSdkPrompt` / `ReadySdk` 共 4 条；
- `InitializeWizard`：`PagePaths` 由 3 个字段减为 2 个（Autodesk 根目录、属性表目录），删除 `Add(PathsSdkPrompt)`、`Values[1]` 赋值及后续索引调整（原 `Values[2]` 属性表改为 `Values[1]`）；删除常量 `FallbackArxSdkPath`、变量 `InitArxSdkPath`；
- 删除 `GetArxSdkPath` 函数；
- `UpdateReadyMemo`：删除 SDK 位置两行；
- `InitializeSetup`：删除 `/ARXSDKPATH` 读取；
- 头部注释的静默参数表删除 `/ARXSDKPATH=<dir>` 一行；
- 页面版式：路径页只剩两个输入框，调整 Y 坐标与标签位置（`PathsRoot…` / `PathsProps…`）。

**main 线：**

- `property.wxi`：删除 `<Property Id="ARXPATH" …>`（第 48 行）；
- `UI.wxi`：删除 `SdkLabel / SdkEdit / SdkBrowse` 及其 `SelectFolderDialog` Publish（第 402–406 行一带），属性表控件上移；
- `Bundle.wxs`：删除 `ArxSdkPath` 变量声明、Install/Repair 命令中的 `--arxsdk="[ArxSdkPath]"` 及注释中的说明；
- `MsiSetup/MsiSetup.cs`：删除 `--arxsdk` 选项（注释第 18 行、`AppendProperty(sb, "ARXPATH", …)` 第 177 行）；
- 维护/确认页若引用了 SDK 路径摘要，同步删除（`BuildYearsSummary` 只汇总年份，无需改）。

### 4.5 清理仓库中的载荷源文件（全部引用删除后）

- 删除 [_Installs/rxsdk_common.props](../_Installs/rxsdk_common.props)、[_Installs/crx.props](../_Installs/crx.props)、[_Installs/arxEntryPoint.h](../_Installs/arxEntryPoint.h)；
- 删除死副本 [ArxAppWiz/Templates/1033/crx.props](../ArxAppWiz/Templates/1033/crx.props)；
- 提交前再全仓库搜一遍 `rxsdk_common|crx\.props|arxEntryPoint|ARXSDKPATH|ARXPATH|ArxSdkPath|GetArxSdk`，确认只剩文档与无关引用。

### 4.6 文档与版本号

- 使用指南 [docs/ObjectARX多年份向导使用指南.md](ObjectARX多年份向导使用指南.md)：第 4 页改为两个路径（删除 SDK 位置行与"向 inc 写文件"说明）、静默参数表删除 `/ARXSDKPATH`；
- 根 README / ArxVsixWizard/README 中若提到 inc 写入，同步修正；
- 按仓库规矩**升级版本号并保持四处一致**：`ArxVsixWizard.csproj` 的 `<Version>`、`source.extension.vsixmanifest`（Identity 与 AssemblyName）、9 个 `.vstemplate` 的 `<Assembly>`、（MSI/Burn 线）`ObjectARXWizards.wxs` 的 `ProductVersion`/新 `ProductCode` 与 `Bundle.wxs` 的 `Version`；Inno 线为 `#define AppVersion`。改完跑 `ArxVsixWizard\test-version-consistency.ps1`。

---

## 5. 兼容性与风险

| 事项 | 评估 |
|---|---|
| 已生成的旧工程 | 只通过 `Autodesk.arx-<年份>.props` 间接受影响；重新安装后年份表更新，CRX 链改走内联规则，旧工程无需改动即可继续构建 |
| arx / dbx 工程 | 完全不受影响（不经过 CRX 片段） |
| 已往 SDK `inc` 写过文件的机器 | 文件为永久保留设计，卸载/升级都不会删；残留对构建无害（MSBuild 优先导入的是我们年份表指向的路径，内联后不再 import 那份 `crx.props`） |
| SDK 自带 `arxEntryPoint.h` 差异 | 各年 SDK 自带版本与样板配套；样板已禁用 4838 警告，使用 SDK 原版无回归 |
| 解压不完整的第三方 SDK（缺 `rxsdk_common.props` / `dbx.props`） | 改造后不再兜底。官方 ADN 发布的 ObjectARX SDK zip 均含这些文件；如确需照顾残缺环境，可在安装完成页增加"检测到 SDK 缺文件"的**警告**（只读检测，不写入），列为可选增强，不在本期范围 |
| props 生成器两个实现必须一致 | `arx-genprops.exe` 与 `gen-arx-props.ps1` 输出字节级一致是既有约束，改完必须跑 `test-arx-genprops.ps1`；MSI 自定义动作内嵌同一套规则，三处漏一处就会出现线路间行为分叉 |
| 测试基线变化 | 新片段会改变生成的 props 文本，[tools/arx-genprops/testdata/ObjectARX.Common.props](../tools/arx-genprops/testdata/ObjectARX.Common.props) 基线需按新输出更新（先用实际输出人工审阅，确认只有 CRX 三处变化后再覆盖基线） |

---

## 6. 可选增强（不阻塞主方案）

1. **历史残留清理**：卸载时检测 `inc\crx.props` 等 3 文件，仅当其内容与本安装器历史载荷哈希一致时删除（避免误删 SDK 2024 自带的同名文件）；
2. **SDK 完整性检测**：安装页"Autodesk 根目录"选定后，检测一个已勾选年份的 `inc\dbx.props` 是否存在，缺失时在页面红字提示"该 SDK 似乎解压不完整"；
3. **CRX 真机矩阵**：把 2022/2024/2027 × (arx/dbx/crx) 的命令行编译纳入发布前自测清单。

---

## 7. 验证清单

**自动化（按仓库 AGENTS.md 要求）：**

1. `tools\arx-props\test-years-consistency.ps1`——年份表与安装器枚举一致；
2. 构建 `tools\arx-genprops` 后运行 `tools\arx-genprops\test-arx-genprops.ps1`——exe 与 ps1 输出一致（先更新基线）；
3. `ArxVsixWizard\test-version-consistency.ps1`——版本号四处 + VSIX 一致；
4. `ArxVsixWizard\TemplateSmokeTest`——模板渲染回归零异常。

**真机验证：**

5. 在装有 SDK 2022 / 2024 / 2027 的机器上安装新包，确认：
   - 安装页不再出现"ObjectARX SDK 位置"字段；
   - 三个 SDK 的 `inc` 目录**无新增/修改文件**（对比安装前后目录时间戳与哈希）；
   - 安装目录根下不再出现 `rxsdk_common.props`、`crx.props`；
6. 分别生成并命令行编译：2022/2027 的 **CRX**（验证内联规则替代缺失的 `crx.props`）、2024 的 arx / dbx / crx，Debug 与 Release 各一次；
7. 生成 .NET mixed 工程（crxnet / arxnet 各一年份）确认 net 骨架未受影响；
8. 维护模式：更改年份（增/减）重装后 props 正确增删；卸载行为正常；
9. main 线额外验证 Burn bundle 的静默参数（去掉 `ArxSdkPath` 后）`ArxQuiet/ArxYears/ArxRds/ArxRoot/ArxPropsDir/ArxSkipVsix` 仍全部生效。

---

## 8. 建议实施顺序

1. **先在 `classic-wizards`（当前检出）改 tools 与 iss**：props 三处规则 → 自测 1/2 → iss 删文件与字段 → 真机验证 5/6；
2. 同步 **`dev`** 线（同一 iss 结构，仅经典树部分不同）；
3. 同步 **`main`** 线：tools 规则相同，额外改 CustomAction.cs、wxi/wxs、MsiSetup.cs，并升 MSI ProductCode 与 bundle 版本；
4. 三条线各自更新使用指南与 README，统一版本号，最后跑完整验证清单。
