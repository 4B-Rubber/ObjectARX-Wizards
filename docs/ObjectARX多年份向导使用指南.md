# ObjectARX 多年份向导（经典向导版）· 使用指南

**适用安装包：`ObjectARXMultiVersionWizards-classic.exe`（单文件安装包）**

本指南面向拿到安装包的最终用户。读完本文，您可以独立完成：安装 → 新建 ObjectARX 工程 → 添加类 → 编译与在 AutoCAD 中加载调试 → 日后修改年份/修复/卸载。

> 本安装包提供的是 **经典多页式 ObjectARX 向导**（HTML 向导界面，英文），项目向导与类向导都集成在 Visual Studio 自身的向导体系中，操作方式与 Autodesk 历年发布的 ObjectARX Wizard 一致。

> 📷 **插图约定**：本文以引用块标注截图位置，每块写明了应截取的画面与建议文件名。截图统一保存到本文同目录的 `images\` 文件夹（即 `docs\images\`），文件就位后把对应占位块替换为 `![图 N：标题](images/文件名.png)` 即可。

---

## 目录

1. [这套向导是什么](#1-这套向导是什么)
2. [安装前的准备](#2-安装前的准备)
3. [图形化安装（逐页说明）](#3-图形化安装逐页说明)
4. [静默安装（企业/批量部署）](#4-静默安装企业批量部署)
5. [安装后检查](#5-安装后检查)
6. [新建第一个 ObjectARX 工程](#6-新建第一个-objectarx-工程)
7. [向工程中添加 ObjectARX 类](#7-向工程中添加-objectarx-类)
8. [编译、加载与调试](#8-编译加载与调试)
9. [年份 / 工具集 / 平台速查表](#9-年份--工具集--平台速查表)
10. [修复、增减目标年份与卸载](#10-修复增减目标年份与卸载)
11. [常见问题](#11-常见问题)

---

## 1. 这套向导是什么

安装后，Visual Studio 中会多出两类向导，用于 AutoCAD ObjectARX 二次开发：

**2 个项目向导（文件 → 新建 → 项目）**

| 向导名称 | 用途 |
|---|---|
| ObjectARX/DBX/CRX Application (Multi-Version) | 创建 ObjectARX（`.arx`）、ObjectDBX（`.dbx`）或 AutoCAD 控制台（`.crx`）应用 |
| ObjectARX/DBX Application with OMF Support (Multi-Version) | 同上，并支持 OMF / MAP 垂直 SDK（面向 AutoCAD Architecture 等建筑类产品），会额外生成一个 Enu 资源 DLL 子项目 |

**7 个类向导（向已有工程添加文件）**

自定义实体、反应器、Jig、MFC 界面类、.NET 包装、ATL COM 包装、动态特性（详见[第 7 节](#7-向工程中添加-objectarx-类)）。

如果本机同时安装了 Visual Studio 2022 和 2026，安装器会把这套向导读入**两个版本**的 Visual Studio。

**“多年份单工程”是什么意思？**

一个生成的工程同时包含 AutoCAD 2010–2027 各年份的编译配置：每个年份两套配置，例如 `2024|x64`（Release）和 `2024d|x64`（Debug）。在配置工具栏切换即可为不同版本的 AutoCAD 编译，无需维护多个工程；输出文件名自动带年份后缀，例如 `MyApp2024.arx`、`MyApp2024d.arx`。

---

## 2. 安装前的准备

| 项目 | 要求 |
|---|---|
| 操作系统 | 64 位 Windows |
| Visual Studio | **Visual Studio 2022（17.x）或 Visual Studio 2026（18.x）**，Community / Professional / Enterprise 均可；需在 Visual Studio Installer 中勾选“使用 C++ 的桌面开发”工作负荷。安装程序启动时会检测 VS，检测不到将直接退出 |
| ObjectARX SDK | 从 Autodesk Developer Network 下载您要支持的各年份 ObjectARX SDK（编译时需要，安装向导本身不强制） |
| AutoCAD | 用于加载、调试生成的 `.arx/.dbx/.crx` |
| 旧年份工具集（按需） | 编译 2010–2019 的配置时，还需在 VS Installer 中加装对应旧版生成工具（如 **MSVC v140、v120/v110/v100、v90 生成工具**）与旧版 Windows SDK；只做 2020 以后版本可忽略 |
| .NET（按需） | 勾选“.NET mixed managed code”时：2025 及以后配置需要 .NET 8（2027 需要 .NET 10）托管工具链；更早年份需要对应 .NET Framework 开发包（3.5 / 4.x） |
| 权限 | 需要管理员权限，启动时会弹出 UAC，请点击“是” |

**安装前请关闭所有 Visual Studio 窗口**（以及 Visual Studio Installer）。安装程序检测到 VS 正在运行会拒绝继续，这是为了避免向导读写到一半失败——此时尚未写入任何内容，关闭 VS 后重新运行即可。

---

## 3. 图形化安装（逐页说明）

双击 `ObjectARXMultiVersionWizards-classic.exe`，在 UAC 提示中点击“是”。安装包为简体中文界面（非中文系统自动显示英文），依次出现以下页面：

### 第 1 页：欢迎

直接点击“下一步”。

### 第 2 页：选择安装位置

- 向导文件的安装目录，默认 `C:\Program Files\Autodesk\ObjectARX Multi-Version Wizards\`，可点击“浏览…”自行选择；
- **属性表目录默认自动跟随安装目录**（下一页可单独修改）；
- 如果本机以前安装过，这里会自动回填上次的目录。

> 📷 **【图 1 · 截图占位】选择安装位置**
> 应截取：安装向导第 2 页完整窗口，包含目录输入框（默认 `…\Autodesk\ObjectARX Multi-Version Wizards`）、“浏览…”按钮和底部“上一步 / 下一步 / 取消”按钮。
> 文件名：`images/setup-02-install-dir.png`

### 第 3 页：注册开发者符号（RDS）

- 新建工程时使用的默认开发者前缀（最长 4 个字符），生成代码和输出文件名都会用到它；
- **可以留空**，留空则使用内置的 `ADSK`；
- RDS 的官方含义见 ObjectARX 文档的 *Symbols Registration*，不确定就直接点“下一步”。

> 📷 **【图 2 · 截图占位】注册开发者符号（RDS）页**
> 应截取：第 3 页完整窗口，包含页面说明文字、RDS 输入框（框中可保留默认的空值或填入示例 `ACME`）。
> 文件名：`images/setup-03-rds.png`

### 第 4 页：ObjectARX SDK 与目标位置

三个路径，**SDK 和 AutoCAD 装在默认位置时整页都不用改**：

| 字段 | 含义 | 默认值 |
|---|---|---|
| Autodesk 根目录 | SDK 与 AutoCAD 的共同上级目录，年份由属性表自动拼接 | `C:\Program Files\Autodesk\` |
| ObjectARX SDK 位置 | 安装器会向其 `inc` 子目录写入 3 个公共文件 | `C:\ObjectARX` |
| 属性表目录 | 各年份属性表（`Autodesk.arx-<年份>.props`）的存放位置，Visual Studio 编译时读取；只要不手动改，它会一直跟随第 2 页的安装目录 | 跟随安装目录 |

> 📷 **【图 3 · 截图占位】ObjectARX SDK 与目标位置页**
> 应截取：第 4 页完整窗口，三个带“浏览…”按钮的路径输入框及各自默认值清晰可读。
> 文件名：`images/setup-04-paths.png`

### 第 5 页：目标 AutoCAD 版本

- 年份分两列显示，勾选您需要支持的 AutoCAD 年份。**每勾选一个年份，就生成一份对应的属性表**；未勾选的不生成；
- 勾选了本机暂时没装 SDK 的年份也没有任何副作用，以后装好 SDK 直接就能编译；
- 首次安装默认勾选 **2020、2024、2026、2027**；如果是覆盖安装，会自动回显上次已生成属性表的年份；
- 四个快捷按钮：**全部**（勾上 16 个年份）、**全不选**、**反选**、**兼容**（只勾各二进制兼容代系的代表年份：2014、2016、2018、2020、2024、2026、2027，同代系共用一套属性表）。

> 📷 **【图 4 · 截图占位】目标 AutoCAD 版本页**
> 应截取：第 5 页完整窗口，两列年份复选框（2010–2019 / 2020–2027）、右上角“全部 / 全不选 / 反选 / 兼容”四个按钮；建议保持默认勾选 2020、2024、2026、2027 的状态拍摄。
> 文件名：`images/setup-05-years.png`

### 第 6 页：准备安装

本页以清单形式汇总前面的全部选择（安装目录、RDS、Autodesk 根目录、SDK 位置、属性表目录、目标年份），确认无误后点击“安装”。

> 📷 **【图 5 · 截图占位】准备安装页（设置汇总清单）**
> 应截取：第 6 页完整窗口，清单中六项设置（安装目录、RDS、Autodesk 根目录、SDK 位置、属性表目录、目标年份）全部可见，目标年份显示为 2020, 2024, 2026, 2027。
> 文件名：`images/setup-06-ready.png`

### 安装过程中

1. 复制向导文件、生成勾选年份的属性表、向 ObjectARX SDK 的 `inc` 目录写入公共文件；
2. 把项目向导和类向导读入 Visual Studio（2022、2026 都会处理），并请求 VS 刷新向导列表；
3. 自动安装 Visual Studio 扩展组件（它负责让 VS 重新生成向导读取列表，本身不提供额外模板入口）。此步骤可能弹出 Visual Studio 自带的扩展安装窗口并持续 1–2 分钟，属正常现象，**请勿手动关闭它**。

看到“安装完成”后，**重新启动 Visual Studio**。

---

## 4. 静默安装（企业/批量部署）

以管理员身份运行，例如：

```bat
ObjectARXMultiVersionWizards-classic.exe /VERYSILENT /SUPPRESSMSGBOXES /YEARS=2020,2024,2026,2027 /RDS=ACME
```

| 参数 | 作用 |
|---|---|
| `/VERYSILENT` | 完全静默，不显示向导界面与进度框 |
| `/SUPPRESSMSGBOXES` | 抑制提示框（建议与 `/VERYSILENT` 一起用） |
| `/YEARS=2020,2024` | 预设勾选的年份（逗号分隔，不要加空格） |
| `/RDS=符号` | RDS 前缀；省略则使用 ADSK |
| `/DIR=路径` | 安装目录 |
| `/PROPSDIR=路径` | 属性表目录；省略则跟随安装目录 |
| `/ARXROOT=路径` | Autodesk 根目录（SDK 与 AutoCAD 的共同上级） |
| `/ARXSDKPATH=路径` | ObjectARX SDK 位置 |
| `/VSROOT=路径` | 手工指定 Visual Studio 安装根目录（自动检测不到时使用） |
| `/SKIPVSIX=1` | 只装文件和属性表，跳过扩展组件（此时需自行让 VS 刷新向导列表） |
| `/SKIPVSCHECK=1` | Visual Studio 正在运行也继续安装（不推荐） |

---

## 5. 安装后检查

打开 Visual Studio 2022 或 2026：

1. **文件 → 新建 → 项目**（或按 `Ctrl+Shift+N`）；
2. 搜索框输入 **ObjectARX**（或在语言中选 C++）；
3. 应看到两个向导：**ObjectARX/DBX/CRX Application (Multi-Version)** 与 **ObjectARX/DBX Application with OMF Support (Multi-Version)**。

能看到即表示安装成功。若看不到，见[第 11 节](#11-常见问题)。

> 📷 **【图 6 · 截图占位】新建项目中搜索 ObjectARX**
> 应截取：VS“创建新项目”对话框，搜索框中输入 `ObjectARX`，结果列表中两个模板（ObjectARX/DBX/CRX Application (Multi-Version)、ObjectARX/DBX Application with OMF Support (Multi-Version)）及其 C++ 标记清晰可见。
> 文件名：`images/vs-new-project-search.png`

---

## 6. 新建第一个 ObjectARX 工程

### 6.1 操作步骤

1. **文件 → 新建 → 项目**，搜索 ObjectARX，选择 **ObjectARX/DBX/CRX Application (Multi-Version)**，点击“下一步/确定”；
2. 输入工程名（例如 `MyArxApp`）和保存位置，确认后弹出经典的 **ObjectARX/DBX Application Wizard** 多页向导（英文界面，左侧为步骤导航栏）。

> 📷 **【图 7 · 截图占位】经典向导启动后的概览页（Overview）**
> 应截取：ObjectARX/DBX Application Wizard 完整窗口，左侧步骤导航栏（Overview / Application Type / Version / MFC / COM / .NET 等）与右侧概览页内容同框；右侧应能看到 “Your Registered Developer Symbol” 输入框（ADSK）与 “Implement _DEBUG” 复选框。
> 文件名：`images/wizard-01-overview.png`

### 6.2 向导各页说明

向导通过左侧导航在各步骤间切换，按“Next >”逐页前进：

**① 概览页（Overview）**

- **Your Registered Developer Symbol**：开发者前缀，最长 4 个字符，默认 `ADSK`，输出文件与样板代码都会使用它；
- **Implement _DEBUG**：勾选后在工程中预置 `_DEBUG` 宏，一般不勾。

**② Application Type（应用程序类型）**

| 单选项 | 含义 |
|---|---|
| ObjectARX | 标准 AutoCAD 扩展，输出 `.arx`（最常用） |
| ObjectDBX (Custom DBX application) | 宿主无关的数据库应用，输出 `.dbx`（可被 RealDWG/DBX 宿主加载） |
| Console | AutoCAD 控制台程序，输出 `.crx` |

> 📷 **【图 8 · 截图占位】Application Type 页**
> 应截取：向导的 Application Type 页，三个单选项 ObjectARX / ObjectDBX / Console 同框，默认选中 ObjectARX，右侧各选项说明文字可见。
> 文件名：`images/wizard-02-apptype.png`

**③ Version（目标版本）**

- 两列复选框列出 2010–2027 全部年份，勾选要纳入工程的年份；
- **本机未安装属性表的年份会显示为灰色锁定状态**（无法勾选）——需要时重新运行安装包补勾该年份（见[第 10 节](#10-修复增减目标年份与卸载)）；
- 页面提示：每勾选一个年份，工程中就增加一对配置（例如 `2026` 和 `2026d`），并把年份追加到输出模块名（例如 `MyApp2026.arx`）。

> 📷 **【图 9 · 截图占位】Version 页（年份勾选）**
> 应截取：向导的 Version 页，两列年份复选框同框；最好能同时拍到一个可勾选年份与一个**灰色锁定**年份的对比，以及页面上 “Each ticked year adds a pair of build configurations…” 的提示文字。
> 文件名：`images/wizard-03-version.png`

**④ MFC（MFC 支持）**

| 单选项 | 含义 |
|---|---|
| No MFC | 不使用 MFC（纯命令/实体开发通常选这个） |
| Regular DLL with MFC Statically | 静态链接 MFC 的规则 DLL |
| Regular DLL using MFC Shared | 动态 MFC 规则 DLL |
| Extension DLL using MFC Shared | MFC 扩展 DLL；要使用 AutoCAD AdUi/AcUi 界面类时选它 |
| AutoCAD MFC Extension（勾选项） | 同时链接 AutoCAD MFC 扩展 |

> 📷 **【图 10 · 截图占位】MFC 页**
> 应截取：向导的 MFC 页，四个 MFC 单选项（No MFC / Regular Static / Regular Shared / Extension Shared）与 “AutoCAD MFC Extension” 复选框同框。
> 文件名：`images/wizard-04-mfc.png`

**⑤ COM（COM 选项）**

- Not a COM server：不做 COM（多数人选这个）；
- Implement a COM server / using ATL：向 VBA、.NET 等 COM 客户端暴露对象；
- 下方还可选择是否 `#import` ObjectDBX 或 AutoCAD 类型库。

> 📷 **【图 11 · 截图占位】COM 页**
> 应截取：向导的 COM 页，三个 COM 单选项与类型库 `#import` 的一组单选项（None / ObjectDBX / AutoCAD）同框。
> 文件名：`images/wizard-05-com.png`

**⑥ .NET（托管选项）**

- **.NET mixed managed code**：生成 C++/CLI 混合模块（2025 起为 .NET 8/10，更早年份为 .NET Framework）；
- 下面两项为 AutoCAD Architecture (ACA) / AutoCAD MEP 垂直产品的 .NET 支持，按需勾选。

> 📷 **【图 12 · 截图占位】.NET 页**
> 应截取：向导的 .NET 页，三个复选框（.NET mixed managed code、ACA support、MEP support）同框。
> 文件名：`images/wizard-06-dotnet.png`

**OMF 模板额外的一步：Verticals（仅第二个项目向导有）**

- **OMF application support**：生成 OMF 垂直应用，会强制采用 MFC Extension DLL；
- **MAP API support**：加入 MAP API 支持，会强制 ObjectARX 应用类型；
- OMF 工程创建后，解决方案中会多一个 **Enu 资源 DLL 子项目**，主项目自动依赖它。

> 📷 **【图 13 · 截图占位】Verticals 页（仅 OMF 模板有此步）**
> 应截取：第二个项目向导（with OMF Support）的 Verticals 页，OMF application support 与 MAP API 两个勾选项同框；如页面还有 OMF 版本单选项也一并纳入画面。
> 文件名：`images/wizard-07-verticals.png`

### 6.3 完成创建

各页选择完毕后，在任意一页点击 **Finish**（概览页）生成工程。生成内容包括：

- `acrxEntryPoint.cpp`（命令入口）、`StdAfx.h/.cpp`、`DocData.h/.cpp`、`Resource.h`、`root.cpp/.idl/.rgs/.rc` 等样板文件（随选项增删）；
- 每个选中年份两套配置 `<年份>|x64` 和 `<年份>d|x64`；2010–2019 年另含 Win32 平台，**2020 年起只有 x64**。

> 📷 **【图 14 · 截图占位】生成后的工程（解决方案资源管理器）**
> 应截取：VS 中刚生成的工程——解决方案资源管理器展开源文件与头文件节点（可见 acrxEntryPoint.cpp、StdAfx、DocData、root.* 等），上方配置工具栏可看到年份配置（如 2024 / x64）。若拍 OMF 工程，请把 Enu 资源 DLL 子项目一起纳入画面。
> 文件名：`images/vs-solution-generated.png`

> 建议：第一次使用时保持所有默认选项（ObjectARX + 默认可勾选的年份），先跑通“创建 → 编译 → 加载”全流程，再尝试其他选项。

---

## 7. 向工程中添加 ObjectARX 类

### 7.1 操作方式

在“解决方案资源管理器”中**右键工程 → 添加 → 类**（也可以用“添加 → 新建项”），在弹出的 Visual Studio 向导中找到 **ObjectARX** 分类，下面列出 7 个向导（列表中显示的是英文内部名，与中文含义的对照见 [7.3](#73-七个类向导分别做什么)），选择后点击“添加/打开”。

> 📷 **【图 15 · 截图占位】“添加类”中的 ObjectARX 分类**
> 应截取：VS“添加类”（或添加新项）对话框，左侧/上方类别树选中 **ObjectARX**，右侧模板列表中可见 7 个向导（ArxWizCustomObject、ArxWizReactors、ArxWizJig、ArxWizMFCSupport、ArxWizNETWrapper、ArxAtlWizComWrapper、ArxAtlWizDynProp）及其图标。
> 文件名：`images/vs-add-class-list.png`

每个类向导同样是英文 HTML 选项界面，按页选择后点击完成即可。例如：

- MFC 类：选择要继承的 AdUi/AcUi 基类（对话框、停靠栏、选项板等），并可选是否向工程插入对话框资源；
- 自定义对象：选择 ObjectDBX 基类与需要实现的协议扩展；
- 反应器：在列表中勾选要继承的反应器基类（30 余种）。

> 📷 **【图 16 · 截图占位】类向导选项页（以自定义对象为例）**
> 应截取：任选一个类向导的选项页（建议 ArxWizCustomObject：基类下拉/协议扩展复选框区域），让读者看到类向导同样是多页英文 HTML 界面、底部有上一步/下一步/完成按钮。
> 文件名：`images/wizard-item-customobject.png`

### 7.2 重要提示：请在工程就绪后再添加类

工程刚创建完的半分钟内，Visual Studio 正在后台解析/索引大量头文件。此期间打开“添加新项/添加类”对话框并选中会预填名称的模板，可能触发 VS 自身缺陷导致对话框消失甚至 VS 退出（错误码 `0x80040201`）。

**请等 Visual Studio 状态栏显示“就绪”后再执行添加类操作。** 详见[第 11.4 节](#114-新建工程后立即添加类时-vs-闪退或对话框消失)。

### 7.3 七个类向导分别做什么

| 向导 | 生成的内容 | 典型用途 |
|---|---|---|
| **ObjectDBX Custom Object Class** | 从 ObjectDBX 基类派生的自定义类（自定义实体） | 需要在 DWG 中保存自己的图形对象 |
| **ObjectARX/DBX Transient Reactors Class** | 瞬态反应器类（编辑器、文档、数据库、事件等 30 余种基类可选） | 响应 AutoCAD 的各类事件（图形修改、命令开始、打印等） |
| **ObjectARX Jig Class** | `AcEdJig` 派生类 | 实体绘制过程中的鼠标交互/动态拖拽预览 |
| **ObjectARX MFC Class** | AdUi/AcUi MFC 派生类（对话框、停靠栏、选项板、字段对话框、文件对话框等），可选注入对话框资源 | 做模态/非模态对话框、停靠面板、属性选项板界面 |
| **ObjectARX .NET Wrapper Class** | C++ 类的 C++/CLI 托管包装 | 让 ObjectARX .NET (C#/VB.NET) 代码调用原生 ObjectARX 类 |
| **AutoCAD COM Wrapper Object** | ATL COM coclass 包装 | 向 VBA、.NET COM 互操作等自动化客户端暴露对象 |
| **AutoCAD Object Dynamic Property** | 实现 `IDynamicProperty2` 的 ATL COM 对象 | 在 AutoCAD“特性”选项板中为对象增加自定义动态特性 |

---

## 8. 编译、加载与调试

### 8.1 选择配置并编译

1. 在配置工具栏选择目标年份：**`2024`** 为 Release、**`2024d`** 为 Debug（年份换成您需要的）；
2. 2020 年起平台固定为 **x64**；
3. **生成 → 生成解决方案**（`Ctrl+Shift+B`）。

输出文件位于工程的 `x64\<年份>\`（或 `x64\<年份>d\`）目录，文件名自动带 RDS 前缀和年份，例如 `ADSKMyArxApp2024.arx`。

> 📷 **【图 17 · 截图占位】在配置工具栏切换年份配置**
> 应截取：VS 顶部标准工具栏区域，左侧“解决方案配置”下拉展开后可见各年份配置（2020、2020d、2024、2024d、2026、2026d、2027、2027d …），旁边“解决方案平台”为 x64。
> 文件名：`images/vs-config-year.png`

### 8.2 在 AutoCAD 中加载

- `.arx / .dbx / .crx`：在 AutoCAD 中输入命令 **APPLOAD**（或“管理 → 加载应用程序”），选择生成的文件；也可以把文件拖入 AutoCAD 窗口；
- .NET 混合模块（`.dll`）：使用 **NETLOAD** 命令加载；
- 样板工程已带一个测试命令（通常是工程名相关命令），加载后在命令行输入即可验证。

> 📷 **【图 18 · 截图占位】AutoCAD 的“加载/卸载应用程序”对话框**
> 应截取：AutoCAD 中执行 APPLOAD 后弹出的“加载/卸载应用程序”对话框，文件类型下拉可显示 `*.arx;*.dbx;*.crx;*.lsp` 等类型；底部“历史记录列表”区域可见。
> 文件名：`images/acad-appload.png`

### 8.3 调试设置

右键工程 → 属性 → 调试 → 命令，填写您的 `acad.exe` 完整路径（例如 `C:\Program Files\Autodesk\AutoCAD 2024\acad.exe`），然后按 **F5**：会启动 AutoCAD，APPLOAD 加载生成的模块后，命中断点。

### 8.4 编译报“未找到 ObjectARX <年份> 的属性表”怎么办

报错信息类似：

> 未找到 ObjectARX 2026 的属性表（…Autodesk.arx-2026.props）。请重新运行安装程序勾选该年份，或改用已安装的年份配置。

说明当前选的年份配置在安装时没有勾选。处理方式见[第 10 节](#10-修复增减目标年份与卸载)。

### 8.5 OMF 工程提示缺少 `AecCore.h`

这是本机缺少 **AEC OMF SDK**（建筑垂直产品 SDK）导致的，与向导无关。安装对应版本的 AEC OMF SDK 后再编译；不做垂直产品开发请使用第一个（非 OMF）项目向导。

---

## 9. 年份 / 工具集 / 平台速查表

| AutoCAD 年份 | 平台工具集 | ObjectARX SDK | 可用平台 | .NET 混合模块目标 |
|---|---|---|---|---|
| 2010 | v90 | 18.2 | Win32 / x64 | .NET Framework 3.5 |
| 2012 | v90 | 18.2 | Win32 / x64 | .NET Framework 3.5 |
| 2013 | v100 | 19.1 | Win32 / x64 | .NET Framework 4.0 |
| 2014 | v100 | 19.1 | Win32 / x64 | .NET Framework 4.0 |
| 2015 | v110 | 21.0 | Win32 / x64 | .NET Framework 4.5 |
| 2016 | v110 | 21.0 | Win32 / x64 | .NET Framework 4.5 |
| 2018 | v140 | 22.0 | Win32 / x64 | .NET Framework 4.6 |
| 2019 | v141 | 23.0 | Win32 / x64 | .NET Framework 4.7 |
| 2020 | v141 | 23.1 | **仅 x64** | .NET Framework 4.7 |
| 2021 | v142 | 24.0 | 仅 x64 | .NET Framework 4.8 |
| 2022 | v142 | 24.1 | 仅 x64 | .NET Framework 4.8 |
| 2023 | v142 | 24.2 | 仅 x64 | .NET Framework 4.8 |
| 2024 | v143 | 24.3 | 仅 x64 | .NET Framework 4.8 |
| 2025 | v143 | 25.0 | 仅 x64 | **.NET 8** |
| 2026 | v143 | 25.1 | 仅 x64 | **.NET 8** |
| 2027 | v145 | 26.0 | 仅 x64 | **.NET 10** |

---

## 10. 修复、增减目标年份与卸载

**增减目标年份（推荐方式）：**

1. 在 **设置 → 应用 → 已安装的应用** 中卸载 **ObjectARX Multi-Version Wizards**（卸载不会删除 ObjectARX SDK，也不会影响您的工程文件）；
2. 重新运行 `ObjectARXMultiVersionWizards-classic.exe`，在“目标 AutoCAD 版本”页重新勾选需要的年份组合（首次进入时会自动回显上次的选择）；
3. 安装器会为新勾年份补生成属性表，并清理已取消年份的属性表。

也可以用静默方式一次完成，例如只保留 2024/2026/2027：

```bat
ObjectARXMultiVersionWizards-classic.exe /VERYSILENT /SUPPRESSMSGBOXES /YEARS=2024,2026,2027
```

**修复安装：**

重新双击运行安装包，在弹出的维护界面选择“修复安装”，会重新写入向导文件、重新注册 VS 向导并刷新 VS 向导列表。Visual Studio 更新后向导“消失”时，用它恢复。

> 📷 **【图 19 · 截图占位】安装包维护界面（修复 / 卸载）**
> 应截取：已安装状态下再次运行安装包后弹出的维护页面，可见“修复安装”与“卸载安装”两个选项。
> 文件名：`images/setup-maintenance.png`

**卸载：**

通过“设置 → 应用 → 已安装的应用”，或重新运行安装包选择“卸载安装”。卸载会移除向导文件、两个版本 Visual Studio 中的向导读写、扩展组件以及已生成的年份属性表；写入 ObjectARX SDK `inc` 目录的 3 个公共文件会保留（Autodesk SDK 可能共用它们）。

---

## 11. 常见问题

### 11.1 安装程序一启动就提示“本机必须安装 Visual Studio 2022 或 2026”

安装器未在本机检测到受支持的 VS。请确认：

1. 已安装 Visual Studio 2022（17.x）或 2026（18.x），Community/Professional/Enterprise/BuildTools 均可；
2. 如果 VS 装在非标准位置，可在命令行用 `/VSROOT=<VS 安装根目录>` 指定，例如：
   `ObjectARXMultiVersionWizards-classic.exe /VSROOT="D:\MyTools\VS2026\Enterprise"`。

### 11.2 “新建项目”里搜不到 ObjectARX 向导

1. 确认安装时所有 Visual Studio 都已关闭，并在安装完成后重启过 VS（安装后第一次启动 VS 会花一些时间重建向导列表，请耐心等待）；
2. 重新运行安装包选择“修复安装”，完成后再次重启 VS；
3. 仍不显示：在 PowerShell 中执行一次 VS 配置刷新（按你的实际 VS 版本与版本号调整路径），完成后再启动 VS：

   ```powershell
   & "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\devenv.exe" /updateconfiguration
   ```

   VS2026 对应路径为 `${env:ProgramFiles}\Microsoft Visual Studio\18\Enterprise\Common7\IDE\devenv.exe`；
4. 也可以删除 VS 的模板缓存目录后重启 VS：`%LOCALAPPDATA%\Microsoft\VisualStudio\<版本实例目录>\VTC`（例如 `18.0_xxxxxxxx`），把该目录改名或删除；
5. 以日志启动 VS 定位：命令行执行 `devenv /log`，复现后查看 `%APPDATA%\Microsoft\VisualStudio\<版本>\ActivityLog.xml`。

### 11.3 安装结束提示“扩展（VSIX）没有装完”

最常见原因是 VS 或 Visual Studio Installer 仍在后台运行。关闭它们后**重新运行安装包并选择修复**即可（已复制的文件不受影响）。

### 11.4 新建工程后立即“添加类”时 VS 闪退或对话框消失

这是 Visual Studio 2022/2026 自身的已知缺陷（工程刚创建时后台正在解析/索引大量头文件，此时在“添加新项/添加类”对话框中选中会预填名称的模板可能触发，错误码 `0x80040201`）。规避方法：

- **等 VS 状态栏显示“就绪”后再添加类**（约半分钟，工程越大越久）；
- 如仍然出现，重新启动一次 VS 后再添加。

### 11.5 版本页里某些年份是灰色、勾不上

灰色表示该年份的属性表尚未在本机生成。重新运行安装包（卸载重装或修复时通过 `/YEARS` 指定），在“目标 AutoCAD 版本”页勾上所需年份即可。

### 11.6 安装刚开始就提示“请先关闭 Visual Studio 再安装”

安装器检测到 `devenv.exe`（或 Visual Studio Installer）在运行。请保存工作、关闭所有 VS 窗口后重新运行；此时尚未写入任何内容，直接取消也不会留下残余。

### 11.7 部分年份配置编译失败，提示找不到工具集（如 v90/v100/v140）

旧年份需要旧版平台工具集：打开 Visual Studio Installer → 修改 → 单个组件，勾选对应的 **MSVC vxxx 生成工具**（v140 = VS2015 工具集，v100/v90 等为更早版本）与旧版 Windows SDK。只做 2020 以后版本则无需安装。

### 11.8 编译报“未找到 ObjectARX <年份> 的属性表”

该年份安装时未勾选。按[第 10 节](#10-修复增减目标年份与卸载)重新安装并勾选该年份，或在配置工具栏切换到已安装的年份。

### 11.9 在哪里看安装日志

| 用途 | 位置 |
|---|---|
| 安装/卸载完整日志 | `%TEMP%\ObjectARXWizards-Inno.log` |
| VSIX 扩展安装日志 | `%TEMP%\vsixinstall.log` |

---

*ObjectARX 官方文档：<https://help.autodesk.com/view/OARX/2024/ENU/> · AutoCAD 开发者中心：<https://www.autodesk.com/developer-network/platform-technologies/autocad>*
