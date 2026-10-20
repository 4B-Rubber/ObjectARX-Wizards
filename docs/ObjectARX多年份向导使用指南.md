# ObjectARX 多年份向导 · 使用指南

**适用安装包：`ObjectARXMultiVersionWizards-dev.exe`（单文件安装包）**

本指南面向拿到安装包的最终用户。读完本文，您可以独立完成：安装 → 新建 ObjectARX 工程 → 添加类 → 编译与在 AutoCAD 中加载调试 → 日后修改年份/修复/卸载。

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

安装后，Visual Studio 中会多出两类模板，用于 AutoCAD ObjectARX 二次开发：

**2 个项目模板（文件 → 新建 → 项目）**

| 模板名称 | 用途 |
|---|---|
| ObjectARX/DBX/CRX Application (Multi-Version) | 创建 ObjectARX（`.arx`）、ObjectDBX（`.dbx`）或 AutoCAD 控制台（`.crx`）应用 |
| ObjectARX/DBX Application with OMF Support (Multi-Version) | 同上，并支持 OMF / MAP 垂直 SDK（面向 AutoCAD Architecture 等建筑类产品），会额外生成一个 Enu 资源 DLL 子项目 |

**7 个类向导（向已有工程添加文件）**

自定义实体、反应器、Jig、MFC 界面类、.NET 包装、ATL COM 包装、动态特性（详见[第 7 节](#7-向工程中添加-objectarx-类)）。

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
| .NET（按需） | 勾选“.NET 混合模块”时：2025 及以后配置需要 .NET 8（2027 需要 .NET 10）托管工具链；更早年份需要对应 .NET Framework 开发包（3.5 / 4.x） |
| 权限 | 需要管理员权限，启动时会弹出 UAC，请点击“是” |

**安装前请关闭所有 Visual Studio 窗口**（以及 Visual Studio Installer）。安装程序检测到 VS 正在运行会拒绝继续，这是为了避免扩展注册到一半失败——此时尚未写入任何内容，关闭 VS 后重新运行即可。

---

## 3. 图形化安装（逐页说明）

双击 `ObjectARXMultiVersionWizards-dev.exe`，在 UAC 提示中点击“是”。安装包为简体中文界面（非中文系统自动显示英文），依次出现以下页面：

### 第 1 页：欢迎

直接点击“下一步”。

### 第 2 页：选择安装位置

- 安装目录，默认 `C:\Program Files\Autodesk\ObjectARX Multi-Version Wizards\`，可点击“浏览…”自行选择；
- **属性表目录默认自动跟随安装目录**；
- 如果本机以前安装过，这里会自动回填上次的目录。

### 第 3 页：注册开发者符号（RDS）

- 新建工程时使用的默认开发者前缀（最长 4 个字符），生成代码和输出文件名都会用到它；
- **可以留空**，留空则使用内置的 `ADSK`；
- RDS 的官方含义见 ObjectARX 文档的 *Symbols Registration*，不确定就直接点“下一步”。

### 第 4 页：ObjectARX SDK 与目标位置

两个目录，**AutoCAD 和 SDK 装在默认位置时整页都不用改**。本版安装器**不会向 SDK 安装目录写入或修改任何文件**：

| 字段 | 含义 | 默认值 |
|---|---|---|
| Autodesk CAD 目录 | AutoCAD 所在的根目录，年份由属性表自动拼接（`$(AcadRoot)AutoCAD <年份>\`） | `C:\Program Files\Autodesk\` |
| ObjectARX SDK 目录 | ObjectARX SDK 所在的根目录，年份由属性表自动拼接（`$(ArxSdkRoot)ObjectARX <年份>\`） | `C:\Program Files\Autodesk\` |

> 属性表目录不在这里选：它恒等于安装目录。

### 第 5 页：目标 AutoCAD 版本

- 年份分两列显示，勾选您需要支持的 AutoCAD 年份。**每勾选一个年份，就生成一份对应的属性表**；未勾选的不生成；
- 勾选了本机暂时没装 SDK 的年份也没有任何副作用，以后装好 SDK 直接就能编译；
- 首次安装默认勾选 **2020、2024、2026、2027**；如果是覆盖安装，会自动回显上次已生成属性表的年份；
- 四个快捷按钮：**全部**（勾上 16 个年份）、**全不选**、**反选**、**兼容**（只勾各二进制兼容代系的代表年份：2014、2016、2018、2020、2024、2026、2027，同代系共用一套属性表）。

### 第 6 页：准备安装

本页以清单形式汇总前面的全部选择（安装目录、RDS、Autodesk CAD 目录、ObjectARX SDK 目录、目标年份），确认无误后点击“安装”。

### 安装过程中

1. 复制向导文件、生成勾选年份的属性表；
2. 自动为当前用户把 Visual Studio 扩展注册到本机所有受支持的 VS 实例（Visual Studio 2022 与 2026）。此步骤可能弹出 Visual Studio 自带的扩展安装窗口并持续 1–2 分钟，属正常现象，**请勿手动关闭它**；
3. 如果扩展安装返回错误，安装结束时会弹出提示框（处理方法见 [11.3](#113-安装结束提示扩展vsix没有装完)）。

看到“安装完成”后，**重新启动 Visual Studio**。

---

## 4. 静默安装（企业/批量部署）

以管理员身份运行，例如：

```bat
ObjectARXMultiVersionWizards-dev.exe /VERYSILENT /SUPPRESSMSGBOXES /YEARS=2020,2024,2026,2027 /RDS=ACME
```

| 参数 | 作用 |
|---|---|
| `/VERYSILENT` | 完全静默，不显示向导界面与进度框 |
| `/SUPPRESSMSGBOXES` | 抑制提示框（建议与 `/VERYSILENT` 一起用） |
| `/YEARS=2020,2024` | 预设勾选的年份（逗号分隔，不要加空格） |
| `/RDS=符号` | RDS 前缀；省略则使用 ADSK |
| `/DIR=路径` | 安装目录 |
| `/PROPSDIR=路径` | 属性表目录；省略则跟随安装目录 |
| `/ACADROOT=路径` | Autodesk CAD 根目录（AutoCAD 所在根目录） |
| `/ARXROOT=路径` | ObjectARX SDK 根目录（SDK 所在根目录） |
| `/VSROOT=路径` | 手工指定 Visual Studio 安装根目录（自动检测不到时使用） |
| `/SKIPVSIX=1` | 只装文件和属性表，不安装 VS 扩展 |
| `/SKIPVSCHECK=1` | Visual Studio 正在运行也继续安装（不推荐） |

---

## 5. 安装后检查

打开 Visual Studio 2022 或 2026：

1. **文件 → 新建 → 项目**（或按 `Ctrl+Shift+N`）；
2. 搜索框输入 **ObjectARX**；
3. 应看到两个模板：**ObjectARX/DBX/CRX Application (Multi-Version)** 与 **ObjectARX/DBX Application with OMF Support (Multi-Version)**，语言标记为 C++。

能看到即表示安装成功。若看不到，见[第 11 节](#11-常见问题)。

---

## 6. 新建第一个 ObjectARX 工程

### 6.1 操作步骤

1. **文件 → 新建 → 项目**，搜索 ObjectARX，选择 **ObjectARX/DBX/CRX Application (Multi-Version)**，点击“下一步”；
2. 输入工程名（例如 `MyArxApp`）和保存位置，点击“创建”；
3. 弹出选项窗口 **ObjectARX/DBX Application Wizard**（英文界面，单页）。

### 6.2 选项窗口逐栏说明

| 分组 | 选项 | 什么时候选 |
|---|---|---|
| **Registered Developer Symbol (RDS)** | 最多 4 个字符 | 输出文件与样板代码的前缀，默认 `ADSK`，一般保持默认 |
| **Application Type** | ObjectARX Application (`.arx`) | 标准 AutoCAD 扩展（最常用） |
| | ObjectDBX Application (`.dbx`) | 宿主无关的数据库应用（可被 RealDWG/DBX 宿主加载，不依赖 AutoCAD 界面） |
| | AutoCAD Console Application (`.crx`) | AutoCAD 控制台程序 |
| **Additional SDK Support**（仅 OMF 模板显示） | OMF application support | OMF 垂直应用支持，会强制 MFC Extension DLL |
| | MAP API support | MAP API 支持，会强制 ObjectARX 应用类型 |
| **Target AutoCAD Versions** | 年份复选框 + Select all / Clear | 要纳入工程的年份。**只有本机已装属性表的年份可以勾选**；需要其他年份时重新运行安装包补勾（见[第 10 节](#10-修复增减目标年份与卸载)） |
| **MFC Support** | No MFC support | 不用 MFC（纯 ObjectARX 命令/实体通常选这个） |
| | Regular DLL with MFC statically linked | 静态链接 MFC 的规则 DLL |
| | Regular DLL using MFC shared DLL | 动态 MFC 规则 DLL |
| | Extension DLL using MFC shared DLL | MFC 扩展 DLL；要使用 AutoCAD 的 AdUi/AcUi 界面类时选它 |
| | with AutoCAD MFC Extension support | 同时链接 AutoCAD MFC 扩展 |
| **COM Options** | Not a COM server | 不做 COM（多数人选这个） |
| | Implement a COM server / …using ATL | 向 VBA、.NET 等 COM 客户端暴露对象时选择 |
| | use ObjectARX ATL Extensions for Custom Objects | ATL 方式下对自定义实体使用 ObjectARX ATL 扩展 |
| | Type library to `#import` | 是否自动 `#import` ObjectDBX 或 AutoCAD 类型库 |
| **.NET Options** | .NET mixed managed module | 生成 C++/CLI 混合模块（供 .NET 代码调用；2025 起为 .NET 8/10，更早为 .NET Framework） |
| | AutoCAD Architecture (ACA) / AutoCAD MEP support | 对应垂直产品的 .NET 支持 |
| **Debug** | Implement `_DEBUG` preprocessor symbol | 需要样板预置 `_DEBUG` 宏时勾选 |

### 6.3 完成创建

窗口底部红字会提示阻止创建的问题（例如一个年份都没选）。确认无误后点 **Finish**。

生成内容包括：

- `acrxEntryPoint.cpp`（命令入口）、`StdAfx.h/.cpp`、`DocData.h/.cpp`、`Resource.h`、`root.cpp/.idl/.rgs/.rc` 等样板文件（随选项增删）；
- 每个选中年份两套配置 `<年份>|x64` 和 `<年份>d|x64`；2010–2019 年另含 Win32 平台，**2020 年起只有 x64**；
- 选择 OMF 模板时，解决方案中还会出现一个 **Enu 资源 DLL 子项目**，主项目自动依赖它。

> 建议：第一次使用时保持所有默认选项（`.arx` + 默认勾选的年份），先跑通“创建 → 编译 → 加载”全流程，再尝试其他选项。

---

## 7. 向工程中添加 ObjectARX 类

### 7.1 推荐方式：右键菜单“Add ObjectARX Class...”

在“解决方案资源管理器”中**右键工程节点 → Add ObjectARX Class...**：

1. 在弹出的列表中选择需要的类模板；
2. 类名已自动填好默认值，可自行修改；
3. 点击确认，部分模板还会再弹一个选项窗口（例如 MFC 类要选基类），按提示选择即可。

**为什么推荐这种方式**：这个入口不经过 Visual Studio 自带的“添加新项”对话框，在任何时候都稳定可用（它绕开了 VS 自身的一个已知缺陷，见[第 11.4 节](#114-新建工程后立即添加新项时-vs-闪退或对话框消失)）。该菜单项只在 ObjectARX 工程上出现，普通工程上自动隐藏。

### 7.2 另一种方式：添加 → 新建项

**项目 → 添加新项**（`Ctrl+Shift+A`），在 **ArxWizard** 分类下选择同样的 7 个模板。注意：**工程刚创建完的半分钟内请等状态栏显示“就绪”后再操作**，原因同上。

### 7.3 七个类模板分别做什么

| 模板 | 生成的内容 | 典型用途 |
|---|---|---|
| **ObjectDBX Custom Object Class** | 从 ObjectDBX 基类派生的自定义类（自定义实体） | 需要在 DWG 中保存自己的图形对象 |
| **ObjectARX/DBX Transient Reactors Class** | 瞬态反应器类（编辑器、文档、数据库、事件等 30 余种基类可选） | 响应 AutoCAD 的各类事件（图形修改、命令开始、打印等） |
| **ObjectARX Jig Class** | `AcEdJig` 派生类 | 实体绘制过程中的鼠标交互/动态拖拽预览 |
| **ObjectARX MFC Class** | AdUi/AcUi MFC 派生类（对话框、停靠栏、选项板、字段对话框、文件对话框等），自动注入对话框资源（工程里已有该 id 时不重复添加） | 做模态/非模态对话框、停靠面板、属性选项板界面 |
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

### 8.2 在 AutoCAD 中加载

- `.arx / .dbx / .crx`：在 AutoCAD 中输入命令 **APPLOAD**（或“管理 → 加载应用程序”），选择生成的文件；也可以把文件拖入 AutoCAD 窗口；
- .NET 混合模块（`.dll`）：使用 **NETLOAD** 命令加载；
- 样板工程已带一个测试命令（通常是工程名相关命令），加载后在命令行输入即可验证。

### 8.3 调试设置

右键工程 → 属性 → 调试 → 命令，填写您的 `acad.exe` 完整路径（例如 `C:\Program Files\Autodesk\AutoCAD 2024\acad.exe`），然后按 **F5**：会启动 AutoCAD，APPLOAD 加载生成的模块后，命中断点。

### 8.4 编译报“未找到 ObjectARX <年份> 的属性表”怎么办

报错信息类似：

> 未找到 ObjectARX 2026 的属性表（…Autodesk.arx-2026.props）。请重新运行安装程序勾选该年份，或改用已安装的年份配置。

说明当前选的年份配置在安装时没有勾选。处理方式见[第 10 节](#10-修复增减目标年份与卸载)。

### 8.5 OMF 工程提示缺少 `AecCore.h`

这是本机缺少 **AEC OMF SDK**（建筑垂直产品 SDK）导致的，与向导无关。安装对应版本的 AEC OMF SDK 后再编译；不做垂直产品开发请使用第一个（非 OMF）项目模板。

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
2. 重新运行 `ObjectARXMultiVersionWizards-dev.exe`，在“目标 AutoCAD 版本”页重新勾选需要的年份组合（首次进入时会自动回显上次的选择）；
3. 安装器会为新勾年份补生成属性表，并清理已取消年份的属性表。

也可以用静默方式一次完成，例如只保留 2024/2026/2027：

```bat
ObjectARXMultiVersionWizards-dev.exe /VERYSILENT /SUPPRESSMSGBOXES /YEARS=2024,2026,2027
```

**修复安装：**

重新双击运行安装包，在弹出的维护界面选择“修复安装”，会重新生成年份属性表并重新注册扩展。Visual Studio 大版本更新后模板“消失”时，用它恢复。

**卸载：**

通过“设置 → 应用 → 已安装的应用”，或重新运行安装包选择“卸载安装”。卸载会移除 VS 扩展、注册表记录以及已生成的年份属性表；本版安装器不会向 ObjectARX SDK 目录写入任何文件。

---

## 11. 常见问题

### 11.1 安装程序一启动就提示“本机必须安装 Visual Studio 2022 或 2026”

安装器未在本机检测到受支持的 VS。请确认：

1. 已安装 Visual Studio 2022（17.x）或 2026（18.x），Community/Professional/Enterprise/BuildTools 均可；
2. 如果 VS 装在非标准位置，可在命令行用 `/VSROOT=<VS 安装根目录>` 指定，例如：
   `ObjectARXMultiVersionWizards-dev.exe /VSROOT="D:\MyTools\VS2026\Enterprise"`。

### 11.2 “新建项目”里搜不到 ObjectARX 模板

1. 确认安装时所有 Visual Studio 都已关闭，并在安装完成后重启过 VS；
2. 重新运行安装包选择“修复安装”，完成后再次重启 VS；
3. 仍不显示：删除 VS 的模板缓存目录后重启 VS——
   `%LOCALAPPDATA%\Microsoft\VisualStudio\<版本实例目录>\VTC`（例如 `18.0_xxxxxxxx`），把该目录改名或删除，再启动 VS；
4. 以日志启动 VS 定位：命令行执行 `devenv /log`，复现后查看 `%APPDATA%\Microsoft\VisualStudio\<版本>\ActivityLog.xml`。

### 11.3 安装结束提示“扩展（VSIX）没有装完”

最常见原因是 VS 或 Visual Studio Installer 仍在后台运行。关闭它们后**重新运行安装包并选择修复**即可（已复制的文件不受影响）。如暂时只想安装文件，可用 `/SKIPVSIX=1` 跳过扩展步骤，之后再手动安装扩展。

### 11.4 新建工程后立即“添加新项”时 VS 闪退或对话框消失

这是 Visual Studio 2022/2026 自身的已知缺陷（工程刚创建时后台正在解析/索引大量头文件，此时在“添加新项”对话框中选中会预填名称的模板可能触发，错误码 `0x80040201`）。规避方法：

- **始终优先使用右键工程 → Add ObjectARX Class...**（见 [7.1](#71-推荐方式右键菜单add-objectarx-class)），它完全不走有缺陷的对话框；
- 或等待 VS 状态栏显示“**就绪**”后再使用“添加新项”。

### 11.5 安装刚开始就提示“请先关闭 Visual Studio 再安装”

安装器检测到 `devenv.exe`（或 Visual Studio Installer）在运行。请保存工作、关闭所有 VS 窗口后重新运行；此时尚未写入任何内容，直接取消也不会留下残余。

### 11.6 部分年份配置编译失败，提示找不到工具集（如 v90/v100/v140）

旧年份需要旧版平台工具集：打开 Visual Studio Installer → 修改 → 单个组件，勾选对应的 **MSVC vxxx 生成工具**（v140 = VS2015 工具集，v100/v90 等为更早版本）与旧版 Windows SDK。只做 2020 以后版本则无需安装。

### 11.7 编译报“未找到 ObjectARX <年份> 的属性表”

该年份安装时未勾选。按[第 10 节](#10-修复增减目标年份与卸载)重新安装并勾选该年份，或在配置工具栏切换到已安装的年份。

### 11.8 在哪里看安装日志

| 用途 | 位置 |
|---|---|
| 安装/卸载完整日志 | `%TEMP%\ObjectARXWizards-Inno.log` |
| 向导/扩展运行日志 | `%TEMP%\ArxVsixWizard\wizard.log` |
| VSIX 扩展安装日志 | `%TEMP%\vsixinstall.log` |

---

*ObjectARX 官方文档：<https://help.autodesk.com/view/OARX/2024/ENU/> · AutoCAD 开发者中心：<https://www.autodesk.com/developer-network/platform-technologies/autocad>*
