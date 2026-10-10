# ObjectARX VSIX：类/添加项向导（Item Templates）实施方案

## Context（背景与目标）

本仓库是 Autodesk ObjectARX 的 VS 向导集合。工程 `ArxVsixWizard` 已用 C# 重做**项目向导**（VSIX `ProjectTemplates`），已真机跑通，支持 VS2022 + VS2026。

但当年 MSI 安装的 **7 个"添加类/添加项"向导**（`ArxWizCustomObject`、`ArxWizReactors`、`ArxWizJig`、`ArxWizMFCSupport`、`ArxWizNETWrapper`、`ArxAtlWizComWrapper`、`ArxAtlWizDynProp`）**从未纳入 VSIX**，且本机 VS2022/VS2026 都没有 `VCAddClass` 目录、未安装旧 MSI → 用户在"添加项"里无法按模板创建指定类。

**目标**：在同一个 VSIX 内，用 `ItemTemplates` 提供这 7 个类向导，同时支持 VS2022 与 VS2026。

**已确认的硬事实**
1. 老代码模板使用 `[!if X]` / `[!else]` / `[!endif]` / `[!output X]` —— 语法正好是现有 `TemplateRenderer` 支持的，模板可复用。
2. 现有 `TemplateRenderer.Render` **只按"整行"识别指令**（`Engine\TemplateRenderer.cs:41-73`），而 ATL 模板存在**同行内联指令**（如 `object.h:41-43,47`、`objco.idl`、`dynprop.h`、`dynpropint.idl`）→ 会被原样输出，**ATL 两向导必须先改渲染器**。
3. 生成的项目启用 PCH（`ArxProject.vcxproj:51-52` `PrecompiledHeader=Use` + `StdAfx.h`），而类模板均不含 stdafx 头 → 新增 `.cpp` 一编译就 C1010，**必须处理**。

**用户已决策**
- 界面：**声明式通用 WPF 对话框**（不逐像素还原老 HTML）。
- 节奏：**先打通 1 个（ArxWizNETWrapper）端到端，再铺开其余 6 个**。
- 目标版本：VS2022 + VS2026。

---

## 硬约束（阻塞项，先解决）

| # | 约束 | 应对 |
|---|---|---|
| C1 | 渲染器不支持同行内联指令 | 阶段 0 改造 `TemplateRenderer.Render`，支持行内 token 化；用现有 8 组项目向导用例做零 diff 回归 |
| C2 | 新 `.cpp` 与工程 PCH 冲突（C1010） | 默认给新增 `.cpp` 打 `<PrecompiledHeader>NotUsing</PrecompiledHeader>`；或在 `.cpp` 首行插入 `#include "<工程 PCH 名>"`。阶段 1 真机二选一后固定 |
| C3 | `TargetFileName` 能否用自注入键（如 `$ArxWrap0File$`）未验证 | 阶段 1 第一件验证；兜底：文件名降级为由 `$fileinputname$` 派生 |
| C4 | 注入内容中的 `$` 会触发 VS 二次参数替换 | 注入前对渲染结果做 `$` → `$$` 转义；冒烟测试断言产物无 `$...$` 残留 |

---

## 架构与关键决策

### 总体结构

- **单 VSIX、单程序集**：7 个 item 向导与现有 2 个 project 向导共用 `ArxVsixWizard.dll`。
- **三段式**：每个向导只写一个 `XxxItemModel`（字段、符号派生、模板清单）；**UI 与渲染/落盘由公共基类与通用对话框承担**，避免 7 份 WPF/XAML 重复。
- **模板资源全部 `EmbeddedResource` 进 DLL**；VSIX 的 `ItemTemplates` 目录只放 `*.vstemplate` + 薄包装文件 + `.ico`（`Content`）。
- **不修改已跑通的代码**：`ArxProjectWizard.cs`、`UI\WizardDialog.xaml` 一行不改；所需小工具方法（`ReadResource`/`EntryEncoding`/`LogError`/`GetForegroundWindow`）在 `ArxItemWizardBase` 中复刻。

### 建议目录

```
ArxVsixWizard\
├─ Engine\
│   ├─ TemplateRenderer.cs            ★ 改造：支持同行内联 if/else/endif
│   └─ ItemSymbols.cs                 ★ 新增：item 公共符号（PROJECT_NAME/SAFE_*/UPPER_CASE_*/ITEM_NAME）
├─ Items\
│   ├─ ArxItemWizardBase.cs           ★ 公共 IWizard 基类
│   ├─ ItemModel.cs / ItemFile.cs     ★ 模型契约（顺序须与 vstemplate 的 ProjectItem 一致）
│   ├─ ItemContext.cs                 ★ $fileinputname$ 读取、工程上下文、日志
│   ├─ ItemCatalog.cs                 ★ 解析 reactors.xml / DbxObjects.xml / MfcSupport.xml
│   ├─ ProjectSideEffects\            ★ ResourceFileEditor / ResourceIdAllocator / IdlEditor / PrecompiledHeaderFixer
│   ├─ ManagedWrapper\ Jig\ Reactors\ CustomObject\ AtlDynProp\ AtlComWrapper\ MfcSupport\
│   ├─ AtlCommon\AtlItemModelBuilder.cs   ★ ATL 两向导共用（GUID/.rgs/.rc/.idl）
│   ├─ Data\{reactors.xml,DbxObjects.xml,MfcSupport.xml}
│   └─ <Wizard>\Templates\*.{h,cpp,rc,idl,rgs}   ← EmbeddedResource
├─ UI\
│   ├─ ItemDialog.xaml(.cs)           ★ 通用对话框
│   └─ ItemField.cs                   ★ 字段描述（Text|Bool|Number|Combo|FileNamePair + VisibleWhenExpression）
└─ Packaging\ItemTemplates\VC\ObjectARX\1033\<Name>\{*.vstemplate, WrapN.txt, *.ico}
```

### 生成并「加入工程」的机制（方案 A 为主 + DTE 兜底）

老模板含 `[!if]/[!output]`，VS 引擎不认识，必须由我们渲染。因此：

1. `RunStarted`：从 `$fileinputname$` 取默认类名 → 弹 `ItemDialog` → 取消则 `throw WizardCancelledException`（零副作用）→ 建模型、渲染全部模板 → 注入：
   - `$ArxWrapNFile$` = 目标文件名（**即使是可跳过文件也要给合法默认名**，不能注入空串）
   - `$ArxWrapNContent$` = 渲染内容（**先做 `$`→`$$` 转义**）
   - 键必须带前后 `$`（沿用 `ArxProjectWizard.cs:58` 的既有经验）
2. VS 引擎按 vstemplate 的 `ProjectItem`（薄包装文件，内容仅 `$ArxWrapNContent$`）+ `TargetFileName="$ArxWrapNFile$"` 落盘、入工程、写 `.vcxproj`（`ClCompile/ClInclude/Midl/ResourceCompile`）、进撤销栈。
3. `ShouldAddProjectItem("...WrapN.txt")`：有内容返回 `true`，可跳过文件返回 `false`（可变文件集）。
4. `ProjectItemFinishedGenerating(item)`：捕获 `_project` / `_targetDir` / DTE（**不要**在 `RunStarted` 用 DTE）。
5. `RunFinished`：执行 `PostActions`（工程级副作用），全部 try/catch 并写 `%TEMP%\ArxVsixWizard\wizard.log`：
   - `PrecompiledHeaderFixer`（C2）
   - `ResourceFileEditor`（MFC 注入 DIALOG；ATL 注入 REGISTRY/RGS_ID）
   - `IdlEditor`（ATL 主 `.idl` 插入 coclass/interface）

> 说明：ATL 的 `.rgs`/`.idl` 与 MFC 的 `.rc` 需要修改**既有**文件，内容注入天然做不到，故用 `RunFinished` + DTE 兜底。

### 打包与登记（增量，最小改动）

- `ArxVsixWizard.csproj`：
  - `EmbeddedResource Include="Items\<Wizard>\Templates\*" LogicalName="ArxWiz<Wizard>.%(Filename)%(Extension)"`（7 组）
  - `EmbeddedResource Include="Items\Data\*" LogicalName="ArxWizData.%(Filename)%(Extension)"`
  - `Content Include="Packaging\ItemTemplates\**\*" Link="ItemTemplates\%(RecursiveDir)%(Filename)%(Extension)"`
- `source.extension.vsixmanifest` 增加：`<Asset Type="Microsoft.VisualStudio.ItemTemplate" Path="ItemTemplates" />`
- **不需要手写 `.vstman`**：构建会自动生成（现有 `obj\Release\TemplateManifests\templateManifest*.1033.vstman` 已含 Project 项，新增 Item 后自动多出 `TemplateType="Item"` 容器）。
- item 模板 `TemplateData`：`Type="Item"`、`ProjectType="VC"`、`TemplateID="ObjectARX.Item.<Name>"`（不冒用 `Microsoft.VisualC.*`）、`TemplateGroupID="VC-Native"`（MFC 先试 `VC-MFC`，真机不出现则回退 `VC-Native`）、`DefaultName`（无扩展名）、`ProvideDefaultName=true`、`LocationField=Disabled`、`ShowByDefault=true`、`SortOrder=200+`、`WizardExtension` 指向 `ArxVsixWizard` 对应类。
- 目录层级沿用已知可用先例（GoogleTest 扩展）：`ItemTemplates\VC\ObjectARX\1033\<Name>\`。
- 露出范围：**以"添加新项"为验收标准**；"添加类"对话框是否纳入由 VC 工程系统内部枚举决定，列为阶段 1 观察项（加分项，不做注册表 hack）。

---

## 实施阶段

### 阶段 0：引擎前置改造（硬前置）
1. 改 `Engine\TemplateRenderer.cs` 支持**同行内联** `[!if]/[!else]/[!endif]`（含嵌套 `[!if A][!if B]...`），保持 `Render(string, SymbolTable)` 签名不变；`[!output]` 行内替换已支持，保留。
2. 新增 `$` → `$$` 转义工具（C4）。
3. 扩 `TemplateSmokeTest`：对 ATL 的 `object.h`/`objco.idl`/`dynprop.h`/`dynpropint.idl` 做四组开关渲染断言（无 `[!` 残留、`dual,`/`oleautomation,`/`nonextensible,` 出现/消失正确）；**现有 8 组项目向导用例必须零 diff 通过**。

### 阶段 1：链路验证关卡 —— 只做 ArxWizNETWrapper
新增 `Items\` 骨架 + `UI\ItemDialog`，实现 NETWrapper（最简：2 个文件、仅 `[!output]`、无 GUID/资源），打包并在真机端到端。**本阶段必须给出结论的 5 个未知**：
1. C3：`TargetFileName="$ArxWrap0File$"` 是否生效（改文件名验证）。
2. `ShouldAddProjectItem` 是否被调用（决定 ATL/MFC 可变文件集实现）。
3. PCH 方案二选一（`NotUsing` 打标 vs 插入 include）。
4. "添加类"是否出现。
5. VS2022(17.x) 与 VS2026(18.x) 在模板列表/渲染上的差异。

### 阶段 2：ArxWizJig + ArxWizReactors
- Jig：复刻动态多行符号（`INPUT_PROMPTS/KEYWORDS/USERCTRLS/CURSORTYPES/SAMPLER_SWITCH/UPDATE_SWITCH`，`NUMBER_OF_INPUTS` 钳制 1..20）。
- Reactors：`ItemCatalog` 解析 `reactors.xml`，按所选基类取 27 组模板之一（注意别名项）。

### 阶段 3：ArxWizCustomObject
- 协议矩阵（`PROTOCOLS` 1/2/3 → `ACDBOBJECT_/ACDBENTITY_/ACDBCURVE_PROTOCOLS` + `CURVE_PROTOCOL`/`IMPL_VIEWPORT`）+ `DbxObjects.xml` 驱动的基类/头文件/模板联动；`MERGE_FILE` 先固定 `false`。

### 阶段 4：ATL 两向导（先 DynProp，后 ComWrapper）
- 先落 `AtlCommon\AtlItemModelBuilder`（GUID/`PROGID` 37 字符截断/`.rgs`/`.rc`/`.idl`/属性页字段），两个向导只保留模板清单与符号差异。
- 首次引入 `PostActions`：`ResourceIdAllocator` + `ResourceFileEditor`（RGS_ID）+ `IdlEditor`。
- 依赖阶段 0 的 C1。

### 阶段 5：ArxWizMFCSupport（最难）
- 90 条类表 + 11 过滤器（`MfcSupport.xml`）；Flag 语义 `R`=需资源、`C`=需子对话框、`N`=无、`RCW`=资源+子窗口。
- `R/RCW`：分配 DIALOG 资源 ID（读 `_APS_NEXT_RESOURCE_VALUE`）并注入既有 `.rc`；**兜底**：找不到 `.rc`/`resource.h` 时生成独立 `<Class>.rc` + `<Class>Resource.h`（ID 从 30000 起，不碰既有文件）。
- `C/RCW`：额外生成子对话框（文件集 2→4，依赖 `ShouldAddProjectItem`）。
- `PROJECT_SUPPORTS_AUTOMATION` 先固定 `false`。

---

## 每个向导移植要点

| 向导 | 复杂度 | 关键字段 | 特殊逻辑 |
|---|---|---|---|
| ArxWizNETWrapper | L1 最简（关卡） | MANAGED_WRAPPER_NAME / CUSTOM_OBJECTNAME / 命名空间 / 基类 / 头与实现文件 | 无 |
| ArxWizJig | L2 | CLASS_NAME / ARX_OBJECTNAME / NUMBER_OF_INPUTS / 文件 | 动态多行符号拼装 |
| ArxWizReactors | L3 | CLASS_NAME / BASE_CLASS / 文件 / INCLUDE_HEADER | 27 组模板按基类选；XML 别名 |
| ArxWizCustomObject | L3 | CLASS_NAME / BASE_CLASS / PROTOCOLS / DXFNAME / 文件 | 协议矩阵；DbxObjects.xml 联动 |
| ArxAtlWizDynProp | L4 | 类名/短名/接口名/ProgID/连接点/Attributed | 内联 if；GUID；.rgs→.rc；.idl 入工程 |
| ArxAtlWizComWrapper | L4 | 同上 + 线程模型/聚合/OPM/OBJECT_WITH_SITE... | 内联 if 密度最高；与 DynProp 共用 AtlItemModelBuilder |
| ArxWizMFCSupport | L5 最难 | 类名/基类/过滤器/子对话框/资源 ID | .rc 资源注入 + 资源 ID 分配；可变文件集 |

---

## 验证

1. **离线冒烟（每阶段必过）**：`TemplateSmokeTest.exe` 新增 item 模式，对每个向导用 2~3 组输入渲染全部文件，断言：无 `[!` 残留、无 `$ArxWrap` 残留、`.h/.cpp` 花括号配平、`.idl` 的 interface/library 闭合、`.rgs` 的 `{GUID}` 可 `Guid.Parse`；并断言 `ItemModel.Files` 与模板目录一一对应。
2. **vstemplate 交叉校验（新增，性价比最高）**：扫描 `Packaging\ItemTemplates\**\*.vstemplate`，校验每个 `<ProjectItem>` 源文件存在、`WrapN.txt` 无悬空/无未引用、`<Icon>` 存在、`TemplateID` 唯一、`WizardExtension/FullClassName` 能在 DLL 中反射到且实现 `IWizard`、`TargetFileName` 的键与 `ItemModel.FileNameSymbol` 一致。
3. **真机端到端（每阶段至少一次）**：统一流程
   - 关闭所有 VS 实例
   - `VSIXInstaller.exe /quiet /uninstall:ObjectARX.MultiYear.Wizard`（**注意：VSIX 版本号未变时 `VSIXInstaller /quiet` 会误判"已安装"而跳过，必须先卸载**；安装需写 `%LOCALAPPDATA%\Microsoft\VisualStudio\...`，在沙箱外执行）
   - 安装 `ObjectARXMultiYearWizards.vsix`（VS2022 与 VS2026 各一次）
   - 打开测试工程（用项目模板新建 ArxApp）→ 右键 → 添加 → **新建项** → `ObjectARX` 分类
   - 检查：文件落在所选目录、Solution Explorer 出现且类型正确、**编译通过（验证 C2）**、Ctrl+Z 可撤销、`%TEMP%\ArxVsixWizard\wizard.log` 无异常

---

## 风险与取舍

| 风险 | 缓解 |
|---|---|
| C1 内联指令（ATL 完全不可用） | 阶段 0 先改渲染器，用现有 8 组用例做零 diff 回归 |
| C3 `TargetFileName` 自定义键 | 阶段 1 首验；兜底降级为 `$fileinputname$` 派生 |
| `ShouldAddProjectItem` 在 18.x 行为 | 阶段 1 验证；兜底按最大文件集固定生成、`RunFinished` 用 DTE 删多余文件 |
| UI 保真度（老 HTML 极大） | **已决策**：声明式字段 + 通用对话框，保留字段/默认值/联动/校验，风格与现有向导统一（主动降级） |
| ATL `.idl`/`.rgs`、MFC `.rc` 注入保真度 | 文本级编辑器 + 资源 ID 分配器；与老 MSI 同参数产物 **diff 对照**，是阶段 4 主要返工点 |
| 部分符号依赖工程运行时状态（`MERGE_FILE`/`PROJECT_SUPPORTS_AUTOMATION`/`DLL_APP`/`LIB_NAME`） | 阶段 2-5 先固定保守默认；阶段 6 再从 vcxproj/`.idl` 读取真实值 |
| 本地化 LCID | 先只发 `1033`（VS 自动回落）；2052 与明文中文名放最后，**不引入资源 DLL** |
| 多目标年份 | item 向导与年份无关（只生成类代码），唯一耦合是 PCH，按工程实际配置探测 |

---

## 关键文件

- `ArxVsixWizard\Engine\TemplateRenderer.cs`（C1 改造，本轮核心）
- `ArxVsixWizard\ArxVsixWizard.csproj`（EmbeddedResource / Content 增量）
- `ArxVsixWizard\source.extension.vsixmanifest`（新增 ItemTemplate Asset）
- `ArxVsixWizard\ArxProjectWizard.cs`（**只读参考**，复用其注入/写盘经验，不改动）
- `ArxVsixWizard\TemplateSmokeTest\Program.cs`（回归 + vstemplate 交叉校验）
- 老模板来源：`ArxWiz*\Templates\1033\*`、`ArxWiz*\Scripts\1033\default.js`、`ArxWiz*\HTML\1033\*.htm|*.xml`、`ArxWizCommon\arxCommon.js`
- 规范参考：`...\IDE\ItemTemplates\VC\ATL\ATLControl\ATLControl.vstemplate`、`...\IDE\Extensions\o2gh2lwn.gwa\ItemTemplates\VC\Test\1033\GoogleTest\GoogleTest.vstemplate`