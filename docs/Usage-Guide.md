# ObjectARX 多年份向导使用说明（main 分支）

> 适用分支：`main`（原始线：**VSIX + MSI/Burn**）
> 组件版本：VSIX 0.2.6、MSI 0.2.6、Burn 引导程序 0.2.8
> 最后更新：2026-10-12
>
> 本分支同时交付 2 个**项目模板**与 7 个**项模板（类向导）**，一个 Visual Studio 扩展同时支持
> **Visual Studio 2022 (17.x)** 与 **Visual Studio 2026 (18.x)**，单个工程即可覆盖
> **AutoCAD 2010–2027** 共 16 个年份。
>
> 其它两条分支不要与本文混淆：`dev` 是 Inno 单文件安装器线，`classic-wizards` 是经典 `.vsz`
> 项目向导线（见文末[附录 B](#附录-b三条分支的区别)）。

---

## 目录

1. [这套向导能做什么](#1-这套向导能做什么)
2. [环境要求](#2-环境要求)
3. [安装与卸载](#3-安装与卸载)
4. [新建 ObjectARX 工程](#4-新建-objectarx-工程)
5. [向工程中添加 ObjectARX 类](#5-向工程中添加-objectarx-类)
6. [编译与调试](#6-编译与调试)
7. [年份、工具集与平台速查](#7-年份工具集与平台速查)
8. [已知问题与注意事项](#8-已知问题与注意事项)
9. [从源码构建](#9-从源码构建)
10. [故障排查](#10-故障排查)
11. [附录 A：安装器在机器上写入的内容](#附录-a安装器在机器上写入的内容)
12. [附录 B：三条分支的区别](#附录-b三条分支的区别)

---

## 1. 这套向导能做什么

### 1.1 两个项目模板（File → New → Project）

| 模板 | 说明 |
|---|---|
| **ObjectARX/DBX/CRX Application (Multi-Version)** | 创建 ObjectARX (`.arx`)、ObjectDBX (`.dbx`) 或 AutoCAD 控制台 (`.crx`) 应用，目标年份 2010–2027 |
| **ObjectARX/DBX Application with OMF Support (Multi-Version)** | 同上，额外支持 OMF / MAP 垂直 SDK（AEC 建筑类产品），含独立的 Enu 资源 DLL 子项目；目标年份 2014–2027 |

### 1.2 七个项模板（向已有工程添加类）

自定义对象、反应器、Jig、MFC 界面类、.NET 包装、ATL COM 包装、动态特性——详见[第 5 节](#5-向工程中添加-objectarx-类)。

### 1.3 “多年份单工程”是什么意思

向导生成的**一个** `.vcxproj` 工程里，每个勾选年份各有两套配置：

- `2024|x64`：该年份的 Release 配置
- `2024d|x64`：该年份的 Debug 配置

切换配置即可用对应年份的工具集（v90–v145）、SDK 与引用编译，不必为每个 AutoCAD 版本维护一份工程。
各年份的差异（工具集、SDK 路径、.NET 框架等）不在工程里写死，而是由安装时生成的
**属性表** `Autodesk.arx-<年份>.props` 提供（见 [6.2](#62-属性表props机制)）。

---

## 2. 环境要求

**使用向导（最终用户）：**

- Windows x64；
- **Visual Studio 2022（17.x）或 Visual Studio 2026（18.x）**，Community / Professional / Enterprise 均可，需含 **使用 C++ 的桌面开发**工作负荷；
- 对应年份的 **ObjectARX SDK**（从 Autodesk Developer Network 获取），编译前请确认 SDK 与第 7 节的版本对应关系；
- 用于加载调试的 **AutoCAD**（或 ObjectDBX/CRX 宿主）；
- 编译 .NET mixed 目标时：2025 及以后年份需要 **.NET 8/10** 托管工具链，更早年份使用 .NET Framework 3.5–4.8。

**从源码构建安装器（开发者）额外需要：**

- **WiX Toolset v3.14**（构建 MSI/Burn 用；详见 [ObjectARXWizardsInstaller/StepsToBuild.md](../ObjectARXWizardsInstaller/StepsToBuild.md)）；
- 能访问 NuGet.org（还原 VSIX 工程的 NuGet 包，可离线还原）。

---

## 3. 安装与卸载

### 3.1 三种安装包，任选其一

| 包 | 形态 | 适用场景 |
|---|---|---|
| **`ObjectARXMultiVersionWizards-main.exe`**（Burn 引导程序，推荐） | 单文件、中文界面、一次 UAC 提示 | 普通安装：依次装 MSI（文件与属性表）和 VSIX（项目/项模板） |
| **`ObjectARXWizard.msi`** | Windows Installer 包，**必须以管理员身份运行** | 只要 MSI 部分（向导文件、经典项向导、属性表）；项目模板来自 VSIX，可另行单独安装 |
| **`ObjectARXMultiVersionWizards.vsix`** | VS 扩展，每用户安装 | 已有 props 与向导文件、只想装/升级扩展；或需要装到某个特定 VS 实例 |

> Burn 引导程序自身保持不提升权限：MSI 的提权由其内置启动器完成，而 VSIX 必须以当前用户
> 非提升令牌安装，否则会注册到错误的用户配置单元。不要用“以管理员身份运行”去启动整个 bundle。

### 3.2 图形化安装步骤（MSI 向导页）

安装界面为简体中文，顺序如下：

1. **安装文件夹页**：选择向导文件的安装目录（默认 `%ProgramFiles%\Autodesk\ObjectARX Multi-Version Wizards\`）。属性表目录默认跟随它，下一页可单独修改。再次安装时会自动回填上次的选择。
2. **注册开发者符号（RDS）页**：填写新建工程时使用的默认 RDS 前缀（最长 4 个字符）。可留空，留空则保留内置默认（`ADSK`）。RDS 的含义见 Autodesk ObjectARX 文档的 *Symbols Registration*。
3. **ObjectARX SDK 与目标位置页**：
   - **Autodesk 根目录**：ObjectARX SDK 与 AutoCAD 的共同上级目录（默认 `%ProgramFiles%\Autodesk\`）；
   - **ObjectARX SDK 位置**：安装器会向其 `inc` 目录写入 `rxsdk_common.props`、`crx.props`、`arxEntryPoint.h`；
   - **属性表目录**：生成的年份属性表存放处，默认跟随安装目录。
   
   SDK/AutoCAD 装在默认位置时本页通常无需修改。
4. **目标年份页**：勾选需要支持的 AutoCAD 年份。安装器会为**每个勾选年份各生成一份属性表**，未勾选的不生成；勾选了本机没装 SDK 的年份也没有副作用（等装好 SDK 再用即可）。
   - **全选 / 全不选 / 反选**：字面含义；
   - **兼容**：勾选各二进制兼容代系的代表年份（2014、2016、2018、2020、2024、2026、2027），同一 SDK 主版本的其它年份共用一套属性表；
   - 首次安装默认勾选 **2020、2024、2026、2027**。
5. **确认页 → 进度页 → 完成**。

随后引导程序自动为当前用户把 VSIX 装进所有受支持的 VS 实例（每个实例 VSIXInstaller 可能需要一两分钟）。

**安装完成后重启所有 Visual Studio 实例。** 重启后：

- **文件 → 新建 → 项目**，搜索 “ObjectARX” 即可看到两个项目模板；
- 项目内 **添加 → 新建项** 的 **ArxWizard** 分类下可见 7 个项模板；
- VS2022 中还能看到 MSI 部署的经典 `.vsz` 项向导入口（与 VSIX 模板同源，见[第 8 节](#8-已知问题与注意事项)）。

### 3.3 无人值守 / 命令行安装

**Burn 引导程序**接受以下变量（均为可选项，写法为 `变量=值`）：

| 变量 | 含义 |
|---|---|
| `ArxQuiet=1` | MSI 静默安装（`msiexec /qn`），不显示 MSI 界面 |
| `ArxSkipVsix=1` | 完全不安装 VSIX（只装文件与属性表） |
| `ArxSkipVsCheck=1` | Visual Studio 正在运行时也继续安装（默认会拒绝运行） |
| `ArxRds=<符号>` | RDS 前缀；留空保持 `ADSK` |
| `ArxRoot=<路径>` | Autodesk 根目录 |
| `ArxSdkPath=<路径>` | ObjectARX SDK 路径（其 `inc` 目录会被写入文件） |
| `ArxPropsDir=<路径>` | 属性表目录；留空跟随安装目录 |
| `ArxYears=<列表>` | 初始年份选择，如 `ArxYears=2020,2024,2026,2027` |

示例：

```powershell
.\ObjectARXMultiVersionWizards-main.exe ArxQuiet=1 ArxSkipVsix=1 ArxRds=ACME ArxRoot=D:\Autodesk
```

**单独运行 MSI**（管理员）时，引导程序内置的启动器 `ArxWizardsMsiSetup.exe` 接受等价参数：
`--quiet=1`、`--skipvscheck=1`、`--rds=`、`--arxroot=`、`--arxsdk=`、`--propsdir=`、`--years=`，
卸载用 `--uninstall --quiet=1`。需要详细日志时可自行用 `msiexec /i ObjectARXWizard.msi /l*v <日志文件>`。

### 3.4 修复、更改目标年份、卸载

**再次运行同一个安装包**即进入维护模式，提供三个选项：

- **修复**：重新写入全部文件并重装 VSIX（VS 大版本更新后模板“消失”时也用它重新挂接）；
- **更改目标 AutoCAD 版本并重新安装**：重新勾选年份，安装器会**补生成**新勾年份的属性表，并**清理**取消年份的属性表；
- **删除**：移除产品与 VSIX（属性表文件夹与写入 SDK `inc` 的 3 个文件按永久组件保留，属预期行为）。

也可以在 **设置 → 应用 → 已安装的应用**（程序和功能）中卸载 “ObjectARX Multi-Version Wizards”。

安装器在系统中写入内容的完整清单见[附录 A](#附录-a安装器在机器上写入的内容)。

---

## 4. 新建 ObjectARX 工程

### 4.1 操作入口

1. Visual Studio 中 **文件 → 新建 → 项目**；
2. 搜索框输入 **ObjectARX**（或在语言里选 C++），选择：
   - *ObjectARX/DBX/CRX Application (Multi-Version)*，或
   - *ObjectARX/DBX Application with OMF Support (Multi-Version)*；
3. 输入工程名与位置，确定后会弹出本扩展自绘的 WPF 选项窗口 **ObjectARX/DBX Application Wizard**。

### 4.2 选项页字段说明

| 分组 | 选项 | 说明 |
|---|---|---|
| **Registered Developer Symbol (RDS)** | 最多 4 个字符 | 生成代码与输出文件名使用的开发者前缀，默认 `ADSK` |
| **Application Type** | ObjectARX Application (`.arx`) | 标准 AutoCAD 扩展应用 |
| | ObjectDBX Application (`.dbx`) | 宿主无关的数据库应用（可被 RealDWG/DBX 宿主加载） |
| | AutoCAD Console Application (`.crx`) | AutoCAD 控制台应用 |
| **Additional SDK Support**（仅 OMF 模板显示） | OMF application support | OMF 垂直应用支持，**会强制 MFC Extension DLL** |
| | MAP API support | MAP API 支持，**会强制 ObjectARX 应用类型** |
| **Target AutoCAD Versions** | 年份复选框 + Select all / Clear | 勾选要纳入工程的年份；默认勾选项仅对本机**已生成属性表**的年份生效，因此这里通常只显示装过的年份 |
| **MFC Support** | No MFC support | 不使用 MFC |
| | Regular DLL with MFC statically linked | 静态链接 MFC 的规则 DLL |
| | Regular DLL using MFC shared DLL | 动态 MFC 规则 DLL |
| | Extension DLL using MFC shared DLL | MFC 扩展 DLL（AutoCAD MFC/AcUi 支持所需） |
| | with AutoCAD MFC Extension support | 链接 AutoCAD 的 MFC 扩展（AdUi/AcUi） |
| **COM Options** | Not a COM server / Implement a COM server / …using ATL | COM 服务器三选一 |
| | use ObjectARX ATL Extensions for Custom Objects | ATL 方式下使用 ObjectARX ATL 扩展 |
| | Type library to `#import` | 不导入 / 导入 ObjectDBX 类型库 / 导入 AutoCAD 类型库 |
| **.NET Options** | .NET mixed managed module | 生成 C++/CLI 混合模式托管模块 |
| | AutoCAD Architecture (ACA) / AutoCAD MEP support | 对应垂直产品的 .NET 支持 |
| | —— | 2025 及以后目标编译为 .NET (Core) 混合模块（net8/net10）；更早年份使用 .NET Framework |
| **Debug** | Implement `_DEBUG` preprocessor symbol | 是否在工程中预置 `_DEBUG` 宏 |

窗口底部红字会提示阻止完成的问题（例如一个年份都没选）。点击 **Finish** 后扩展渲染全部源文件、
生成 `.vcxproj` / `.filters`；OMF 工程还会额外创建 **Enu 资源 DLL 子项目**（`<名>Enu.vcxproj`），
自动加入解决方案并把主项目设为依赖它。

> 无界面自动化提示：存在 `%TEMP%\ArxVsixWizard\no-ui.flag` 文件时，向导窗口不弹出、全部使用默认值。
> 仅用于自动化测试/批量生成，日常使用不需要。

### 4.3 生成结果

- 一套与经典 ObjectARX 向导一致的样板：`acrxEntryPoint.cpp`、`StdAfx.h/.cpp`、`DocData.h/.cpp`、
  `Resource.h`、`root.cpp/.idl/.rgs/.rc`、`AssemblyInfo.cpp` 等（随选项增删）；
- 每个选中年份两套配置 `<年份>|x64` 与 `<年份>d|x64`（2010–2019 年另含 Win32 平台，2020 年起仅 x64）；
- 工程内带 `<ArxAppType>` 标记，右键菜单的 **Add ObjectARX Class...** 命令靠它识别本工程；
- OMF 模板额外生成 `OmfApp.cpp/.h`、`OmfHeaders.h`、`mapHeaders.h` 与 Enu 资源项目。

---

## 5. 向工程中添加 ObjectARX 类

有两个入口，**优先使用第一个**。

### 5.1 推荐：右键菜单 “Add ObjectARX Class...”

在解决方案资源管理器中**右键工程节点 → Add ObjectARX Class...**。

- 该命令由扩展自绘选择器，列出扩展自带的 7 个项模板，默认类名也由扩展自己生成；
- 确认后通过 `ProjectItems.AddFromTemplate` 直接驱动原有向导，**完全不经过 VS 的“添加新项”对话框**，
  因此在工程刚创建的预热期内也安全（绕过的 VS 缺陷见[第 8.1 节](#81-vs添加新项对话框可能导致-devenv-退出0x80040201)）；
- 命令只在 ObjectARX 工程（vcxproj 且含 `<ArxAppType>` 标记）上显示，其它工程上自动隐藏。

### 5.2 常规入口：Add → New Item

**项目 → 添加新项**，在 **ArxWizard** 分类下选择模板。注意：模板会预填名称（`ProvideDefaultName=true`），
工程刚建好的约 0–30 秒内使用可能触发 VS 自身缺陷。**请等状态栏显示“就绪”后再操作**，或直接改用 5.1。

### 5.3 七个项模板一览

| 模板（界面英文名） | 生成内容 |
|---|---|
| **ObjectDBX Custom Object Class** | 从 ObjectDBX 类派生自定义类（自定义实体），协议级别随所选基类 |
| **ObjectARX/DBX Transient Reactors Class** | 从 ObjectARX/DBX 瞬态反应器类派生（编辑器、文档、数据库、事件等 30 余种反应器可选） |
| **ObjectARX Jig Class** | `AcEdJig` 派生类，用于实体交互式拖拽 |
| **ObjectARX MFC Class** | 从 ObjectARX AdUi/AcUi MFC 类派生（对话框、停靠栏、选项板、文件对话框等），可选向工程注入对话框资源 |
| **ObjectARX .NET Wrapper Class** | ObjectARX C++ 类的托管包装，供 ObjectARX .NET 代码调用 |
| **AutoCAD COM Wrapper Object** | 用 ATL COM coclass 包装 ObjectARX/DBX 类，供 VBA/.NET/COM 自动化客户端使用 |
| **AutoCAD Object Dynamic Property** | 实现 `IDynamicProperty2` 的 ATL COM 对象，让 AutoCAD 特性选项板显示自定义动态特性 |

---

## 6. 编译与调试

### 6.1 选择配置并编译

1. 配置工具栏选择目标年份配置，例如 **`2024`**（Release）或 **`2024d`**（Debug）；
2. 2020 年起平台只有 **x64**；更早年份可选 Win32/x64；
3. 生成解决方案。首次编译请确认对应版本的 ObjectARX SDK 已在安装时填写的目录中就位。

产物按类型与年份输出，文件名带 RDS 前缀与年份（默认 RDS 为 `ADSK`），例如
`<RDS><工程名>2024.arx`、`<RDS><工程名>2024d.crx`、混合模式目标为 `.dll`。

调试通常把工程的调试命令设为 `acad.exe` 并传入需求图纸；也可在 AutoCAD 内用 **APPLOAD**
加载 `.arx/.dbx/.crx`，混合托管模块用 **NETLOAD** 加载。

### 6.2 属性表（props）机制

- 安装器为每个勾选年份在**属性表目录**生成 `Autodesk.arx-<年份>.props`（及 .NET 相关表），
  年份→工具集→SDK 的全部映射都在其中；
- 生成的工程通过 `$(ArxPropsDir)` 导入对应表，属性表目录记录在注册表
  `HKLM\SOFTWARE\Autodesk\ObjectARX Wizards\PropsDir`，VSIX 向导与经典 HTML 向导都读它；
- 若选择了**未安装属性表**的年份配置，工程会直接报错并给出中文提示：

  > 未找到 ObjectARX \<年份\> 的属性表（…Autodesk.arx-\<年份\>.props）。请重新运行安装程序勾选该年份，或改用已安装的年份配置。

  按提示重新运行安装器，选择“更改目标 AutoCAD 版本并重新安装”补齐即可。
- OMF 真模式编译还需要本机安装 **AEC OMF SDK**（提供 `AecCore.h` 等头文件）；未安装时 OMF 工程会停在缺头文件，这与向导无关。

---

## 7. 年份、工具集与平台速查

VSIX 向导使用的年份表（源码：[ArxVsixWizard/Models/ArxVersion.cs](../ArxVsixWizard/Models/ArxVersion.cs)；
安装器侧的完整属性表（含 .NET 框架版本等）在 [tools/arx-props/arx-props-table.json](../tools/arx-props/arx-props-table.json)）：

| 年份 | 平台工具集 | ObjectARX SDK | 平台 | .NET mixed 目标 |
|---|---|---|---|---|
| 2010 | v90 | 18.2 | Win32 / x64 | .NET Framework 3.5 |
| 2012 | v90 | 18.2 | Win32 / x64 | .NET Framework 3.5 |
| 2013 | v100 | 19.1 | Win32 / x64，支持 CRX | .NET Framework 4.0 |
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
| 2025 | v143 | 25.0 | 仅 x64 | **.NET 8 (Core)** |
| 2026 | v143 | 25.1 | 仅 x64 | **.NET 8 (Core)** |
| 2027 | v145 | 26.0 | 仅 x64 | **.NET 10 (Core)** |

> 新增年份时，VSIX 侧只需在 [ArxVersion.cs](../ArxVsixWizard/Models/ArxVersion.cs) 表中加一行，
> 安装器侧同步 [arx-props-table.json](../tools/arx-props/arx-props-table.json) 并重新生成属性表。

---

## 8. 已知问题与注意事项

### 8.1 VS“添加新项”对话框可能导致 devenv 退出（`0x80040201`）

在 VS 2022 与 2026 上，**刚用本扩展创建完工程的预热窗口（约 0–30 秒，VS 正在索引头文件）内**，
在“添加新项”对话框中选中任何会预填名称的模板（包括微软自带模板），可能触发 VS 自身的
UI Automation 缺陷（`ElementNotAvailableException`，`0x80040201`）；安装了 Visual Assist 时
会表现为 VS 直接消失。调用栈无一帧属于本扩展。

规避：

1. **始终优先使用右键 Add ObjectARX Class...**（见 [5.1](#51-推荐右键菜单-add-objectarx-class)）；
2. 或等待状态栏显示 **就绪 / Ready** 后再用“添加新项”。

完整证据链与量化分析见 [docs/VS-AddNewItem-Crash.md](VS-AddNewItem-Crash.md)。

### 8.2 VS2022 中会看到两套项入口

MSI 部署了 7 个经典 `.vsz` HTML 项向导（并镜像到本机其它受支持 VS），VSIX 又自带 7 个同名项模板，
因此 VS2022 中经典入口与 ArxWizard 分类入口会并存——两者模板同源。VS2026 已移除项目级
`.vsz` 落点（项目模板只能来自 VSIX），项级 `.vsz` 仍保留。

### 8.3 安装/重装 VSIX 前先关闭所有 Visual Studio

同版本号重复执行 `VSIXInstaller /quiet` 会被判定“已安装”而静默跳过。需要强制重装时先卸载：

```powershell
# $vs 按你的安装调整，例如 ${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise
& "$vs\Common7\IDE\VSIXInstaller.exe" /uninstall:ObjectARX.MultiYear.Wizard
& "$vs\Common7\IDE\VSIXInstaller.exe" /quiet ObjectARXMultiVersionWizards.vsix
```

### 8.4 版本号一致性

VSIX 版本号同时写在四处：工程文件 `<Version>`、vsixmanifest 的 `Identity/@Version` 与
`Asset/@AssemblyName`、以及 9 个 `.vstemplate` 中的 `<Assembly>`。改动后必须运行
`ArxVsixWizard\test-version-consistency.ps1` 断言，否则 VS 会以“此模板尝试加载组件程序集…”
拒绝加载向导。

---

## 9. 从源码构建

> 约定：**不要在 Git 工作区内直接构建**。先把源码树镜像（同步）到独立的构建根目录，
> 在副本中构建，源码树保持只读。本机约定（构建根路径等）见 `AGENTS.local.md`（若存在，不随仓库上传）。
> 下文以 `<构建根目录>` 表示该目录。

### 9.1 一键构建 VSIX 与生成器

```powershell
# 在仓库根执行；PowerShell 执行策略受限时用 powershell -ExecutionPolicy Bypass -File ...
powershell -ExecutionPolicy Bypass -File tools\build-and-pack.ps1 -BuildRoot <构建根目录>
```

脚本依次完成：镜像仓库到 `<构建根目录>\src`（排除 `.git/bin/obj/Output` 等）→ 构建 VSIX →
构建 props 离线生成器 `arx-genprops`。产物：

- `<构建根目录>\vsix\ObjectARXMultiVersionWizards.vsix`（分发用副本）；
- `<构建根目录>\src\ObjectARXMultiVersionWizards.vsix`（bundle 打包时取这一份）。

### 9.2 构建 MSI 与 Burn bundle

在**镜像副本**中执行：

```powershell
# $msbuild 可用 vswhere 定位，通常在 "$vs\MSBuild\Current\Bin\MSBuild.exe"
& $msbuild ObjectARXWizardsInstaller\ObjectARXWizardsBundle.wixproj -p:Configuration=Release -p:Platform=x86
```

该工程会先调用 `ObjectARXWizardsInstaller\make.bat`（`candle -arch x64` + `light`）生成 MSI，
再链接 Burn 引导程序。产物位于镜像的 `ObjectARXWizardsInstaller\bin\Release\`：

- `ObjectARXWizard.msi`
- `ObjectARXMultiVersionWizards-main.exe`

WiX 工程的更多细节（自定义操作调试、x86/x64 区别）见
[ObjectARXWizardsInstaller/StepsToBuild.md](../ObjectARXWizardsInstaller/StepsToBuild.md)。

### 9.3 只构建 VSIX

```powershell
# $vs = VS 安装根，按版本号与版本（Enterprise/Professional/Community）调整
$vs = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise"
& "$vs\MSBuild\Current\Bin\MSBuild.exe" `
  ArxVsixWizard\ArxVsixWizard.csproj -t:Restore,Build -p:Configuration=Release -v:m -nologo
# 产物：ArxVsixWizard\bin\Release\ArxVsixWizard.vsix
```

要点与坑：

- 工程为 net472 + WPF、SDK 风格，依赖 `Microsoft.VisualStudio.SDK`、
  `Microsoft.VisualStudio.TemplateWizardInterface`、`Microsoft.VSSDK.BuildTools`（18.x，含 VS18 清单架构）；
- 直接 Build 报 `NETSDK1004 project.assets.json 找不到` 时，先跑 `-t:Restore`；
- VSSDK 18 下 `ProductArchitecture` 必须写成 `<InstallationTarget>` 的**子元素**
  `<ProductArchitecture>amd64</ProductArchitecture>`，写成属性会报 VSSDK1311。

### 9.4 自测清单

改动后按改动内容运行：

| 检查 | 命令/工程 |
|---|---|
| 版本号四处 + VSIX 一致性 | `ArxVsixWizard\test-version-consistency.ps1` |
| 年份表一致性（MSI 枚举 vs 表） | `tools\arx-props\test-years-consistency.ps1` |
| 生成器 vs 离线规则一致性 | 先构建 `tools\arx-genprops`，再跑 `tools\arx-genprops\test-arx-genprops.ps1` |
| 模板渲染回归（60 组合 × 33 模板，要求零异常） | 构建并运行 `ArxVsixWizard\TemplateSmokeTest` |

### 9.5 单独安装/卸载 VSIX

```powershell
# 安装到 VS2022；装到 VS2026 时把 $vs 换成 ${env:ProgramFiles}\Microsoft Visual Studio\18\Enterprise
& "$vs\Common7\IDE\VSIXInstaller.exe" /quiet ObjectARXMultiVersionWizards.vsix
# 卸载
& "$vs\Common7\IDE\VSIXInstaller.exe" /uninstall:ObjectARX.MultiYear.Wizard
```

扩展每用户落盘在 `%LOCALAPPDATA%\Microsoft\VisualStudio\<版本>_<实例>\Extensions\<随机名>\`。

---

## 10. 故障排查

| 现象 | 处理 |
|---|---|
| 新建项目里看不到模板 | 确认扩展已对该 VS 实例安装；用“修复”重装一次；仍不行则清 MEF/VTC 缓存，见 [ObjectARX Wizard Troubleshooting Guide.md](../ObjectARX%20Wizard%20Troubleshooting%20Guide.md) |
| 右键没有 Add ObjectARX Class... | 该命令只在含 `<ArxAppType>` 的 vcxproj 上出现；确认工程由本向导生成；包加载情况看 `%TEMP%\ArxVsixWizard\wizard.log` |
| 编译报“未找到 ObjectARX \<年份\> 的属性表” | 重新运行安装器，维护模式中“更改目标 AutoCAD 版本”补勾年份 |
| 选模板时 VS 闪退/对话框消失 | 见 [8.1](#81-vs添加新项对话框可能导致-devenv-退出0x80040201)，改用右键 Add ObjectARX Class... |
| OMF 工程缺 `AecCore.h` | 安装 AEC OMF SDK；非模板问题 |
| 同版本 VSIX 重装无效 | 先 `/uninstall:ObjectARX.MultiYear.Wizard` 再装（见 [8.3](#83-安装重装-vsix-前先关闭所有-visual-studio)） |

关键日志位置：

| 日志 | 位置 | 内容 |
|---|---|---|
| 向导/包日志 | `%TEMP%\ArxVsixWizard\wizard.log` | 包加载、模板目录扫描、工程创建各阶段耗时 |
| VSIX 安装日志 | `%TEMP%\vsixinstall.log` 及 bundle 启动器旁的 `%TEMP%\ObjectARXWizards-vsix.log` | VSIXInstaller 每个 VS 实例的结果 |
| VS 活动日志 | 以 `devenv /log` 启动后查看 ActivityLog.xml | 扩展加载失败、模板扫描问题 |
| MSI 详细日志 | `msiexec /i ObjectARXWizard.msi /l*v <日志路径>` | 安装/自定义操作排错 |

---

## 附录 A：安装器在机器上写入的内容

| 位置 | 内容 | 卸载行为 |
|---|---|---|
| 安装目录（默认 `%ProgramFiles%\Autodesk\ObjectARX Multi-Version Wizards\`，可改） | 9 个经典向导目录（`ArxWizCommon`、`ArxAppWiz`、`ArxAppWiz182` 及 7 个项向导）的 HTML/脚本/模板/图片 | 随产品删除 |
| ObjectARX SDK 的 `inc` 目录（默认 `C:\ObjectARX\inc`，可在路径页改） | `rxsdk_common.props`、`crx.props`、`arxEntryPoint.h` | 永久保留 |
| VS2022 的 `Common7\IDE\VC\vcprojectitems\ObjectARX\`（并镜像到本机其它受支持 VS） | 7 个经典项向导的 `.vsz` + `.ico` | 随产品移除 |
| 属性表目录（默认跟随安装目录；仅装 VSIX 时兜底为 `%ProgramFiles%\Autodesk\ObjectARX Props\`） | 安装时按勾选年份生成的 `Autodesk.arx-<年份>.props`（及 .NET 表） | 文件夹永久保留；“更改年份”会增删其中的表 |
| 注册表 `HKLM\SOFTWARE\Autodesk\ObjectARX Wizards` | `InstallDir`、`PropsDir`、`ArxRoot`、`AcadRoot`（供两套向导与下次安装读取；bundle 也据此判定安装状态） | 随产品删除 |
| 每用户 VS 扩展目录 | VSIX（2 个项目模板 + 7 个项模板 + 右键命令），装入所有版本号落在 `[17.0,19.0)` 的 VS 实例 | bundle 卸载时调用 VSIXInstaller 卸载并清理残留目录 |

---

## 附录 B：三条分支的区别

| 分支 | 分发形态 | 项目模板来源 | 适用 |
|---|---|---|---|
| **`main`**（本文） | VSIX + MSI + Burn 单文件引导程序 | VSIX 模板（新建项目对话框） | VS2022/2026 的现代体验，推荐 |
| `dev` | VSIX + **Inno Setup** 单文件安装器（中文界面、可选安装目录） | VSIX 模板 | 偏好 Inno 安装体验时 |
| `classic-wizards` | Inno 单文件；VSIX 仅作为“重建向导列表”的触发器，不含模板定义 | 经典 `.vsz/.vsdir` 两个项目向导 + 经典项向导 | 需要经典 HTML 向导外形时 |

三条线共用同一套模板源、年份表与属性表规则；7 个项向导在各线上功能一致。

---

## 相关文档

- 根说明与英文入口：[README.md](../README.md)
- VSIX 开发交接文档：[ArxVsixWizard/README.md](../ArxVsixWizard/README.md)
- MSI/Burn 构建：[ObjectARXWizardsInstaller/StepsToBuild.md](../ObjectARXWizardsInstaller/StepsToBuild.md)
- “添加新项”崩溃分析：[docs/VS-AddNewItem-Crash.md](VS-AddNewItem-Crash.md)
- 通用安装故障排查：[ObjectARX Wizard Troubleshooting Guide.md](../ObjectARX%20Wizard%20Troubleshooting%20Guide.md)
- ObjectARX 官方文档：<https://help.autodesk.com/view/OARX/2024/ENU/>
