<!--
排版说明（发布前删除本块）：
1. 本文为微信公众号草稿，配图直接引用 docs/images/dev/ 下的截图，正文用 ![]() 嵌入；封面图单独拍，位以「📷 封面占位」引用块标出；
2. 文中命令/路径在公众号排版时建议用浅灰底纹代码块；表格在公众号编辑器里需重排为图片或窄表；
3. 内容基准：多年份向导 0.2.15（VS 2022/2026，AutoCAD 2010–2027），以 dev 单文件安装包（Inno）为主线；
4. 另两条分发线的差异见第十一节，完整说明见 docs 下的三份使用指南。
-->

# 一个工程编译 AutoCAD 2010–2027：ObjectARX 多年份向导上手指南

> 备选标题：
> 1.《VS2026 也能用了：ObjectARX 多年份向导安装与实战（全流程图解）》
> 2.《告别"一个年份一个工程"：ObjectARX 多年份向导从安装到调试》

> 📷 **【封面图 · 占位】**
> **拍摄画面**：新建项目对话框中的 ObjectARX 模板 + 配置工具栏年份下拉的拼版，可加标题字"一个工程，通吃 2010–2027"；可直接拿 [图 1](images/dev/13-config-bar.png) 与 [图 4](images/dev/07-new-project.png) 拼一张
> **建议文件名**：wechat-cover.png（建议 900×500）

做 ObjectARX 二次开发的同学，大概都被两件事折磨过：

- **新版 Visual Studio 用不了老向导。** 经典的 ObjectARX 向导依赖 `.vsz` 机制，而 VS2026 已经移除了项目级向导入口——装完在"新建项目"里什么都找不到。
- **一个年份一个工程。** 客户手里 AutoCAD 2014 到 2027 都有，为了多版本兼容，只能复制一堆工程、分别配 SDK 路径、改输出名，切一次版本像脱一层皮。

**ObjectARX 多年份向导（Multi-Version Wizards）** 就是冲着这两个痛点来的：

- 一个 VSIX 扩展，同时支持 **Visual Studio 2022（17.x）和 2026（18.x）**；
- **一个工程内置 AutoCAD 2010–2027 全部年份的编译配置**，工具栏切换即可，输出文件自动带年份后缀；
- 中文图形安装界面，勾选目标年份，一键生成属性表、注册扩展。

今天这篇就用 **dev 单文件安装包**（`ObjectARXMultiVersionWizards-dev.exe`）走完整条链路：**安装 → 新建工程 → 添加类 → 编译加载 → 调试 → 日后增减年份**。

---

## 一、它到底装了什么

安装完成后，Visual Studio 里会多出 **2 个项目模板** 和 **7 个类向导**。

**项目模板（文件 → 新建 → 项目）**

| 模板 | 用途 |
|---|---|
| ObjectARX/DBX/CRX Application (Multi-Version) | 创建 `.arx` / `.dbx` / `.crx` 应用，绝大多数人选它 |
| ObjectARX/DBX Application with OMF Support (Multi-Version) | 同上，额外支持 OMF/MAP 垂直 SDK（面向 AutoCAD Architecture 等建筑类产品），会多生成一个资源 DLL 子项目 |

**七个类向导（往已有工程里加文件）**

自定义实体（Custom Object）、瞬态反应器（Reactors）、交互拖拽（Jig）、MFC 界面类、.NET 包装类、COM 包装对象、动态特性——后面第六节细讲。

那"多年份单工程"是什么概念？看一眼生成工程的配置列表就明白了：

![图 1：一个工程里的年份配置](images/dev/13-config-bar.png)

每个年份两套配置：`2024` 是 Release，`2024d` 是 Debug。**切年份 = 在下拉框里换一个配置**，输出文件名自动区分，比如 `ADSKMyApp2024.arx`、`ADSKMyApp2024d.arx`，再也不用维护十几个工程。

---

## 二、安装前，先备齐这几样

| 项目 | 要求 |
|---|---|
| 系统 | 64 位 Windows |
| Visual Studio | **2022（17.x）或 2026（18.x）**，Community/Professional/Enterprise 均可；VS Installer 中须勾选 **"使用 C++ 的桌面开发"** 工作负荷 |
| ObjectARX SDK | 从 Autodesk 开发者网络下载你要支持的各年份 SDK（编译时才需要，安装向导本身不强制） |
| AutoCAD | 用来加载、调试生成的模块 |
| 旧版生成工具（按需） | 只编译 2020 以后版本可忽略；要编译 2010–2019，需在 VS Installer 加装 **MSVC v140/v110/v100/v90 生成工具** 和旧版 Windows SDK |
| .NET（按需） | 勾选 .NET 混合模块时：2025 起需 .NET 8（2027 需 .NET 10），更早年份需对应 .NET Framework 开发包 |

> ⚠️ **安装前请关闭所有 Visual Studio 窗口**（连同 Visual Studio Installer）。安装器检测到 VS 在运行会主动"罢工"——这是在保护扩展注册不写一半，关掉重跑即可。

---

## 三、安装：六页点下来，全程中文

双击 `ObjectARXMultiVersionWizards-dev.exe`，UAC 弹窗点"是"，只需授权这一次。

**第 1 页：欢迎**

直接"下一步"。

**第 2 页：选择安装位置**

向导文件装到哪，默认 `C:\Program Files\Autodesk\ObjectARX Multi-Version Wizards\`；属性表目录默认跟着它走。

![图 2：选择安装位置](images/dev/01-install-dir.png)

> 💡 **覆盖安装不会再问这一页**：安装器直接沿用上次的目录。要换目录请先卸载再装，或静默参数 `/DIR=` 指定。

**第 3 页：注册开发者符号（RDS）**

最长 4 个字符的开发者前缀，生成的代码和输出文件名都会带上它。**不确定就留空**，自动使用内置的 `ADSK`。

![图 3：注册开发者符号（RDS）](images/dev/02-rds.png)

**第 4 页：SDK 与目标位置**

两个路径：Autodesk CAD 根目录、ObjectARX SDK 根目录，默认都是 `C:\Program Files\Autodesk\`。**SDK 和 AutoCAD 都装在默认位置的话，这一页直接下一步**——年份由属性表自动拼接，安装器不会往你的 SDK 目录写任何文件。

![图 4：ObjectARX SDK 与目标位置](images/dev/03-sdk-paths.png)

**第 5 页：勾选目标 AutoCAD 版本**

这是最关键的一页：**勾哪年，就生成哪年的属性表**；没勾的年份以后想补，重跑安装包即可。

- 首次安装默认勾选 **2020、2024、2026、2027**；
- 勾了暂时没装 SDK 的年份也没副作用，以后装好 SDK 直接能编；
- 四个快捷按钮：**全部 / 全不选 / 反选 / 兼容**。"兼容"只勾每个二进制兼容代系的代表年份（2014、2016、2018、2020、2024、2026、2027），最省心。

![图 5：勾选目标 AutoCAD 版本](images/dev/04-target-years.png)

**第 6 页：准备安装**

清单里再核对一遍安装目录、RDS、两个根目录和目标年份，点"安装"。

![图 6：准备安装（设置汇总）](images/dev/05-ready.png)

安装过程中会做两件事：① 复制向导文件、生成勾选年份的属性表；② 给本机所有受支持的 VS 实例（2022 和 2026）注册扩展，**每个实例大约 1–2 分钟，耐心等它跑完**。

![图 7：正在安装](images/dev/06-progress.png)

![图 8：安装完成](images/dev/06-progress-finish.png)

看到"安装完成"后，**重新启动 Visual Studio**。

> 💡 **企业批量部署**：安装包支持静默参数，例如
> `ObjectARXMultiVersionWizards-dev.exe /VERYSILENT /SUPPRESSMSGBOXES /YEARS=2020,2024,2026,2027 /RDS=ACME`
> （另有 `/DIR= `、`/ACADROOT=`、`/ARXROOT=`、`/SKIPVSIX=1` 等参数，年份之间用逗号、不要加空格）。

---

## 四、十秒钟验证安装是否成功

打开 Visual Studio：

1. **文件 → 新建 → 项目**（或 `Ctrl+Shift+N`）；
2. 搜索框输入 **ObjectARX**；
3. 看到那两个 C++ 模板，就说明成了。

![图 9：新建项目中搜索 ObjectARX](images/dev/07-new-project.png)

搜不到？翻到第十节看排查方法，绝大多数情况重装一遍就能解决。

---

## 五、新建第一个工程：选项窗口逐栏看

1. 选择 **ObjectARX/DBX/CRX Application (Multi-Version)**，下一步；
2. 输入工程名（比如 `MyArxApp`）和保存位置，点"创建"；
3. 弹出一个英文单页选项窗 **ObjectARX/DBX Application Wizard**。

![图 10：项目向导选项窗口](images/dev/08-wizard.png)

选项虽然不少，但日常只需关注前三组：

| 分组 | 怎么选 |
|---|---|
| **RDS** | 保持默认即可（安装时填的前缀会自动带过来） |
| **Application Type** | `.arx` 标准扩展（最常用）；`.dbx` 是宿主无关的数据库应用；`.crx` 是控制台程序 |
| **Target Versions** | 勾选要纳入工程的年份。**只有安装时生成了属性表的年份能勾**；想加年份重跑安装包 |
| **MFC Support** | 纯命令/实体开发选 No MFC；要做对话框、停靠面板选对应 MFC 选项；要用 AutoCAD 的 AdUi/AcUi 界面类选 Extension DLL |
| **COM Options** | 不向 VBA/.NET 暴露 COM 对象就选 Not a COM server（多数人选这个） |
| **.NET Options** | 需要 C++/CLI 混合模块供 C# 调用时勾选；2025 起是 .NET 8/10，更早年份是 .NET Framework |
| **Debug** | 需要预置 `_DEBUG` 宏时再勾 |

> 🚀 **新手建议**：第一次全部保持默认（`.arx` + 默认年份），先把"创建 → 编译 → 加载"跑通一遍，回头再研究 MFC、COM、.NET 这些选项。

窗口底部会用红字提示阻止创建的问题（比如一个年份都没选）。生成后你会得到 `acrxEntryPoint.cpp`（命令入口）、`StdAfx`、`DocData` 等一整套样板文件；2010–2019 年配置含 Win32/x64 两个平台，**2020 年起只有 x64**。

![图 11：生成后的工程结构](images/dev/09-solution.png)

---

## 六、给工程"加类"：请认准这个右键菜单

工程有了，接下来要加自定义实体、反应器、对话框……怎么加？

**推荐方式：在解决方案资源管理器里右键工程节点 → Add ObjectARX Class...**

![图 12：右键菜单入口](images/dev/10-add-class.png)

在弹出的选择器里挑模板、确认类名即可；部分模板（比如 MFC 类）还会让你再选基类。

![图 13：类模板选择器](images/dev/11-class-picker.png)

**为什么专门推荐它？** 因为 VS2022/2026 自身有个已知缺陷：工程刚建完的半分钟内，后台正在解析大量头文件，这时走"添加 → 新建项"选中会**预填名称**的模板，可能触发 devenv 闪退（错误码 `0x80040201`）——这是 Visual Studio 的锅，不是向导的。而右键菜单这条路完全绕开了有缺陷的对话框，**任何时候点都稳**。

当然，等状态栏显示"**就绪**"之后，用 `Ctrl+Shift+A` 在 **ArxWizard** 分类下选模板也是可以的：

![图 14：“添加新项”对话框中的 ArxWizard 分类](images/dev/12-add-new-item.png)

七个类模板分别能干什么：

| 模板 | 用途 |
|---|---|
| ObjectDBX Custom Object Class | 自定义实体，把自己的图形对象存进 DWG |
| ObjectARX/DBX Transient Reactors Class | 30 余种反应器基类，响应图形修改、命令开始、打印等事件 |
| ObjectARX Jig Class | `AcEdJig` 派生类，绘制时鼠标动态拖拽预览 |
| ObjectARX MFC Class | AdUi/AcUi 对话框、停靠栏、选项板、文件对话框等 MFC 界面，自动注入对话框资源 |
| ObjectARX .NET Wrapper Class | C++/CLI 托管包装，让 C# 调用你的原生类 |
| AutoCAD COM Wrapper Object | ATL COM 对象，给 VBA、COM 互操作客户端用 |
| AutoCAD Object Dynamic Property | 在"特性"选项板里给对象挂自定义动态特性 |

---

## 七、编译、在 AutoCAD 里加载、断点调试

**1）选配置，编译**

配置栏选 `2024`（Release）或 `2024d`（Debug），平台 x64，**生成 → 生成解决方案**（`Ctrl+Shift+B`）。

产物在工程的 `x64\2024\`（或 `x64\2024d\`）目录下，文件名自带前缀和年份，例如 `ADSKMyArxApp2024.arx`。

![图 15：编译成功](images/dev/14-build-output.png)

**2）加载进 AutoCAD**

- `.arx / .dbx / .crx`：命令行输入 **APPLOAD**（或"管理 → 加载应用程序"）选文件，**或者直接把文件拖进 AutoCAD 窗口**；
- .NET 混合模块 `.dll`：用 **NETLOAD** 命令；
- 样板工程自带一个测试命令，加载后在命令行敲命令名就能验证。

**3）F5 断点调试**

右键工程 → 属性 → 调试 → 命令，填你的 `acad.exe` 完整路径（例如 `C:\Program Files\Autodesk\AutoCAD 2024\acad.exe`）。按 F5 启动 AutoCAD，APPLOAD 加载模块后，断点就能命中。

**两个高频编译报错，先说解法：**

> ⚠️ 报"**未找到 ObjectARX 2026 的属性表（Autodesk.arx-2026.props）**"——说明这个年份安装时没勾。**重跑安装包，在年份页补勾**，不用重装 VS。
>
> ⚠️ OMF 工程报"**找不到 AecCore.h**"——本机缺 AEC OMF SDK（建筑垂直产品 SDK），与向导无关；不做垂直产品开发就用第一个（非 OMF）模板。

---

## 八、年份 / 工具集 / .NET 速查表

建议收藏，选年份、装工具集时对一眼：

| AutoCAD | 平台工具集 | 平台 | .NET 混合模块 |
|---|---|---|---|
| 2010 / 2012 | v90 | Win32/x64 | .NET Framework 3.5 |
| 2013 / 2014 | v100 | Win32/x64 | 4.0 |
| 2015 / 2016 | v110 | Win32/x64 | 4.5 |
| 2018 | v140 | Win32/x64 | 4.6 |
| 2019 | v141 | Win32/x64 | 4.7 |
| 2020 | v141 | **仅 x64** | 4.7 |
| 2021–2023 | v142 | 仅 x64 | 4.8 |
| 2024 | v143 | 仅 x64 | 4.8 |
| 2025 / 2026 | v143 | 仅 x64 | **.NET 8** |
| 2027 | v145 | 仅 x64 | **.NET 10** |

只做 2020 以后版本的话，装完 VS 就能开工，任何旧工具集都不用额外打。

---

## 九、以后想加年份、重装、卸载

**加年份（或减年份）：重新双击同一个安装包**即可——它会沿用上次的安装目录直接覆盖安装，跳过"选择安装位置"页，其余步骤和第一次完全一样。在"目标 AutoCAD 版本"页重新勾选：

- 新勾的年份 → 补生成属性表；
- 取消的年份 → 清掉对应的属性表、不再生成配置。

> ⚠️ **重装会先清掉上一次装的向导模板文件再重写**，所以别在安装目录里手工改模板；你自己创建的工程完全不受影响。

**重装（等同于修复）**：安装包没有单独的"修复/卸载"维护页。VS 大版本更新后模板"消失"、或向导文件被误删，直接重跑安装包即可恢复。

**卸载**：设置 → 应用 → 已安装的应用 → **ObjectARX Multi-Version Wizards** → 卸载；或到安装目录运行 `unins000.exe`。卸载会移除向导文件、VS 扩展、注册表记录和已生成的年份属性表，不会动 ObjectARX SDK，也不会动你的工程。

---

## 十、高频问题排雷

**Q1：新建项目里搜不到 ObjectARX 模板？**
先确认安装时 VS 全部关闭、装完重启过；不行就重跑安装包重装一遍；仍不显示，把模板缓存目录改名后重启 VS：`%LOCALAPPDATA%\Microsoft\VisualStudio\<版本实例目录>\VTC`；还可以用 `devenv /log` 启动，查 `%APPDATA%\Microsoft\VisualStudio\<版本>\ActivityLog.xml`。

**Q2：刚建完工程，"添加新项"时 VS 闪退/对话框消失？**
VS2022/2026 的已知缺陷（`0x80040201`）。**一律右键工程 → Add ObjectARX Class...**，或者等状态栏"就绪"后再添加新项。

**Q3：编译提示找不到 v90/v100/v140 工具集？**
旧年份需要旧生成工具：Visual Studio Installer → 修改 → 单个组件，勾上对应的 **MSVC vxxx 生成工具** 和旧版 Windows SDK。

**Q4：装到一半提示关闭 Visual Studio？**
说明后台还有 `devenv.exe` 或 Installer 在跑。保存、全部关掉后重试即可，此时没有写入任何文件，取消也不留残余。

**Q5：出问题去哪看日志？**
安装日志：`%TEMP%\ObjectARXWizards-Inno.log`；向导运行日志：`%TEMP%\ArxVsixWizard\wizard.log`；VSIX 安装日志：`%TEMP%\vsixinstall.log`。

---

## 十一、三条分发线，我该装哪个？

同一套模板、同一份年份表，打包成了三个安装包，功能一致，区别在"安装体验"：

| 安装包 | 形态 | 适合谁 |
|---|---|---|
| `ObjectARXMultiVersionWizards-dev.exe` | Inno 单文件，中文六页向导，可自选安装目录 | **本文用的就是它**，喜欢轻量、直给的安装流程 |
| `ObjectARXMultiVersionWizards-main.exe` | MSI + Burn 引导，五页向导，带"修改安装"维护页（修复 / 卸载） | 企业环境已有 MSI 分发习惯、需要"修复"入口 |
| `ObjectARXMultiVersionWizards-classic.exe` | Inno 单文件，装的是经典 `.vsz` 多页 HTML 向导 | 团队习惯 Autodesk 历年那套经典向导外形 |

三者装出来的 VS 模板是同一套，**装一个就够了，不要混装**。每条的逐页图解、静默参数、故障排查见仓库 `docs\` 下的三份使用指南（`-dev` / `-main` / `-classic`）。

---

## 写在最后

这套向导的价值，说穿了就是把两件最消耗热情的事自动化了：**让新 Visual Studio 能用上 ObjectARX 工程向导，让一个工程覆盖 AutoCAD 2010–2027（16 个年份版本）**。装好它之后，你的精力终于可以留在命令逻辑、自定义实体和业务本身。

整个项目是开源的（基于 Autodesk ADN 的 ObjectARX-Wizards 仓库，MIT 协议），想研究实现或自行构建的同学可以到这里看源码：

**https://github.com/ADN-DevTech/ObjectARX-Wizards**

官方 ObjectARX 文档：https://help.autodesk.com/view/OARX/2024/ENU/

如果这篇教程帮你省下了配环境的时间，欢迎**点赞、在看、转发**给身边做 CAD 二次开发的同事——我们下次聊聊怎么用它写第一个自定义实体。

---

*配图清单（共 15 图 + 封面）：封面（拼版）、年份配置、安装页 5 张（安装位置/RDS/SDK/年份/准备安装）、正在安装、安装完成、新建项目搜索、向导选项窗、生成工程、右键菜单、类选择器、ArxWizard 分类、编译产物。*
