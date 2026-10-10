# VS「添加新项」崩溃 / 静默退出：结论摘要

> 2026-10-10 汇总（本文是这次问题的结论与证据摘要，供以后查阅，不必再从日志里重新总结）。
> 完整过程记录见本文末尾附录（原 `Inno-Setup-Installer-Plan.md` §18）；
> 详细日志见 `%TEMP%\ArxVsixWizard\wizard.log`（另有一份未精简的 `wizard-full.log`）。

## 一句话

缺陷在 **Visual Studio 自己的模板对话框**：选中**任何带默认名的模板**（Name 框会预填的那些，
包括微软内置模板）时，它会调用 `Microsoft.VisualStudio.Dialogs.ServiceHelper.GenerateItemName`；
在"刚建完工程、工程系统 / IntelliSense 还在预热"的窗口里，这条调用抛 UIA
`ElementNotAvailableException`（`0x80040201`，17.14 上同栈抛 `E_FAIL`）。装了 Visual Assist 时
它升级为进程级未处理异常（**devenv 直接消失**），没装时被对话框自身的兜底接住（**对话框静默关闭**）。

**与本扩展的项模板无关**：微软内置模板同样触发（E8 实测）。必备前提是**用本扩展的项目模板建工程**
——它的预热窗口足够长（见下），VS 自带的轻量工程只有一两秒，碰不到。

## 复现（必须真实鼠标；脚本复现无效）

1. 用本扩展的项目模板建工程（默认选项，Finish）；
2. 在窗口内（约 0–30 秒）右键项目 → 添加 → 新建项 → 点分类或任一预填模板；
3. 结果：VA 开 → 崩溃；VA 关 → 对话框自己关掉。**等状态栏回到「就绪」之后再操作则一切正常。**

脚本/UI Automation 复现无效：程序化选中走同步路径，到不了 `TemplateSelectionChangedDelayed`。

## 硬证据

- 栈（8 份 18.10 转储 + 1 份 17.14 转储逐帧一致；并用 12:09「添加新项」会话的转储复核过）：

  ```
  Marshal.ThrowExceptionForHRInternal
    ← Microsoft.VisualStudio.Dialogs.ServiceHelper.GenerateItemName(IVsProject, UInt32, IVsTemplate)
    ← NewProjectDialog.UpdateNameField
    ← ApplyTemplateSelection
    ← TemplateSelectionChangedDelayed
  ```

  → 「添加新项」与「新建项目」两个对话框由 `Microsoft.VisualStudio.Dialogs` **同一套实现**；
  「添加新项」以项模式复用 `NewProjectDialog`（共用 `UpdateNameField` / `GenerateItemName`）。
- 我们的代码不在崩溃路径上：崩溃线程栈上没有任何本扩展的帧；全进程唯一的 `ArxVsixWizard` 帧是
  诊断采样线程在 `Thread.Sleep` 里（也正是日志在崩溃处中断的原因）。
- 日志时间线：崩溃都落在"建完工程 10–20 秒内"；**失败边界不是 CPU**——+21 s 与 +26.8 s 两次静默退出时
  CPU 已降到 28–91%，+34.6 s 才成功；边界是"工程系统 / IntelliSense 预热完成"，与状态栏「就绪」一致。
- 预热负载量化：本扩展生成的工程把 ObjectARX SDK 的包含路径全部加入
  —— **802 个头文件 / 16.6 MB**（`inc` 636 个 6.1 MB、`inc-x64` 7 个 9.1 MB、utils 约 160 个），
  新建后 VS 建立 C++ 代码模型时 CPU 冲到 500–810% 持续十几秒；UI 线程延迟全程仅 0–20 ms
  （是后台负载，不是界面卡死）。属性表与模板元数据已逐文件排查，**没有问题**。
- 触发面（E8，2026-10-10 实测）：用本扩展项目模板建的工程里，**点微软自带的预填模板同样崩溃**；
  反之，VS 自带模板建的工程里点本扩展的项模板不崩。
- 第 10 份转储（2026-10-10 15:13 复现，`devenv.exe.14908.dmp`）：日志时间线为 ArxApp8 建完 +5.3 s
  主窗口被模态对话框禁用、下一拍之前进程死亡；转储内层异常同为 `ElementNotAvailableException`
  （`0x80040201`），抛出栈与上表逐帧一致。

## 根因：为什么 `ProvideDefaultName=false` 就不崩

选中任何模板走的都是同一条路：`TemplateSelectionChangedDelayed → ApplyTemplateSelection →
UpdateNameField(模板)`。差别在 `UpdateNameField` **内部**：

- 模板声明 `ProvideDefaultName=true`（Name 框要预填）→ 它才去调
  `ServiceHelper.GenerateItemName(IVsProject, …)`，问工程系统"造一个不重名的默认名"；
- 声明 `false` → **这次调用整体跳过**，Name 框留空。不崩不是对话框变健康了，而是唯一会抛异常的
  那行代码根本没被执行——全部转储的栈都终止在 `GenerateItemName`，没有第二嫌疑人。

`GenerateItemName` 造的名字必须在工程内不重名（MyJig、MyJig1……），所以要枚举工程现有的项；
VS 这段实现走到了自己的 **UI Automation 层**（解决方案资源管理器的自动化树）取这些信息。刚建完的
工程还在后台建 C++ 代码模型，对应的 UIA 元素处于"已虚拟化"状态，UIA 对虚拟化元素的回答就是
`0x80040201`（*Element does not exist or it is virtualized…*）；COM 把这个 HResult 返回给托管包装，
`Marshal.ThrowExceptionForHRInternal` 将它抛成 `ElementNotAvailableException`。

证据边界：

- **实测钉死**：`false` 后同一对话框、同一模板、同一窗口期不崩，唯一变量就是那次调用没发生
  （0.1.7 的 A/B）；`true` 时微软内置模板同样崩——扳机是"预填"这个动作，与模板内容无关；
  `Microsoft.VisualStudio.Dialogs.dll` 内 `ProvideDefaultName` 与 `GenerateItemName` 两个符号
  同时存在，与"标志门控调用"的行为吻合。
- **推断**（与所有观测一致，但只有微软源码能证实）：`GenerateItemName` 内部为何走 UIA 而不是
  直接问 `IVsProject` 接口。

**推论**：在 VS 自己的对话框里，"预填"与"不崩"互斥——预填的唯一入口就是那个标志，而那个标志
就是扳机；向导也帮不上（`RunStarted` 在用户点"添加"后才跑，崩溃发生在选中模板那一刻）。两者
兼得只能绕开这个对话框，由扩展自造默认名（见下「Add ObjectARX Class...」一节）。

## 已知规避（当前出货建议）

| 方案 | 效果 | 代价 |
|---|---|---|
| 项模板 `ProvideDefaultName=false` | 稳定不崩（该调用根本不发生） | Name 框为空、「添加」需先输入名字 |
| 保留 `true` + 等状态栏「就绪」再加项 | 保留预填体验 | 用户必须遵守；VA 开着时不等会崩 |
| 关闭 Visual Assist | 只是从"崩"变"对话框自关" | 不解决问题 |

元数据层面**没有**"保留预填"的可行开关：`<LocationField>` 已被反证（项目模板一直是 `Enabled` 却照样崩）；
`<SortOrder>` 只改"谁先被自动选中"，显式点击照样触发；进程内保活 UIA 元素等于自己扮演 VA，会让情况更糟。
**唯一稳妥解法是绕开 VS 的模板对话框**（自建入口，例如
`ProjectItems.AddFromTemplate(<.vstemplate>, <默认名>)`，复用现有 7 个向导与后处理）。
另注：项目模板侧有同型风险——建完一个工程后 30 秒内在同一解决方案里再建第二个，会走同一条路径。

## 解决：「Add ObjectARX Class...」命令（0.2.0 引入，0.2.2 起可用，当前 0.2.3）

按上面的思路把唯一稳妥解法落地了：解决方案资源管理器里**右键 ObjectARX 工程 →
"Add ObjectARX Class..."**。它在预热窗口内同样安全——整条链路不经过 `NewProjectDialog`：

- 命令挂在项目节点上下文菜单（`IDM_VS_CTXT_PROJNODE`），`DefaultInvisible + DynamicVisibility`，
  只在选中**带 `<ArxAppType>` 标记的 VC++ 工程**时出现（标记检测有缓存，老 .vsz 向导建的工程也算）；
- 弹出扩展自己的选择器（`UI\AddItemDialog`）：列出扩展安装目录下扫描到的全部项模板（名称/描述/图标，
  按 `SortOrder` 排序），名称框用模板 `<DefaultName>` 加序号**自己造默认名**——正是替掉
  `GenerateItemName` 的那一步，重名与非法字符在对话框内校验；
- 确认后调 `ProjectItems.AddFromTemplate(<.vstemplate>, <名字>)`：模板引擎照常跑原有 7 个
  `IWizard`（选项页照弹、`$ArxWrapNFile$/$ArxWrapNContent$` 照常注入、资源/.idl 后处理照跑），
  只是**跳过了 VS 的模板对话框**。向导选项页里点取消表现为 `E_ABORT`，命令按正常退出处理。

实现要点（都是实测踩出来的，重做时照抄）：

- 包能加载的三要素缺一不可：csproj `<GeneratePkgDefFile>true</GeneratePkgDefFile>`（把注册特性
  变成 `.pkgdef`）、vsixmanifest 里 `<Asset Type="Microsoft.VisualStudio.VsPackage" Path="%CurrentProject%.pkgdef" />`
  （让 VS 采纳这份 pkgdef）、VSCT 经 `VSCTCompile` 编译并由 `MergeCtoResource` 合并进程序集
  （`Menus.ctmenu` 落在 `_EmptyResource.resources` 里，构建后已核验 636 字节在位）；
- `InitializeAsync` 第一行就写 `%TEMP%\ArxVsixWizard\wizard.log`（`PackageInit enter/done`），
  包不加载时先看这里，再用 `devenv /log` 查 `ActivityLog.xml`；
- **必须配 `ProvideAutoLoad`**（0.2.1 修）：`DefaultInvisible + DynamicVisibility` 的命令在包未加载时
  不可见，而不可见的命令点不到、包也就永远不会加载——死锁，表现就是菜单里什么都没有。实测日志里
  零条 `PackageInit` 即为此现象。现用
  `[ProvideAutoLoad("f1536ef8-92ec-443c-9ed7-fdadf150da82" /* SolutionExists */, PackageAutoLoadFlags.BackgroundLoad)]`；
- **模板引擎的收尾步要自愈**（0.2.2 修）：引擎在"文件已生成之后、把项写进工程并保存"这一步会失败，
  抛出 `OLE_E_PROMPTSAVECANCELLED`（0x8004000C，保存对话框被取消）或 `E_FAIL`（0x80004005），
  结果是文件躺在磁盘上、`.vcxproj` 里却没有该项（实测：`MyJig.h/.cpp` 在、`ArxApp11.vcxproj` 里无 `MyJig`）。
  现在调用前先静默 `project.Save()` / `Solution.SaveAs(自己)`，失败后按向导登记的生成项名单
  `ProjectItems.AddFromFile` 补进去再保存，不再把原始 HRESULT 弹给用户。

VS 自带「添加新项」对话框继续可用（等「就绪」后），两条入口并存；`ProvideDefaultName=true` 维持不变。
安装器不提供"是否安装该命令"的勾选项（用户 2026-10-10 决定）。

## 自建入口的包加载排查（首次尝试时命令未出现）

日志里**没有任何包加载记录**（连 `InitializeAsync` 的第一行都没有），说明问题在 VsPackage 注册/加载环节，
而不是菜单位置。重做时应：在 `InitializeAsync` **第一行**就写日志，并用 `devenv /log` 查看
`ActivityLog.xml`；仍未出现则优先怀疑 VSIX 的 `Microsoft.VisualStudio.VsPackage` 资源是否被 VS 采纳。

> 2026-10-10 复查确认了根因：当时 csproj 里 `GeneratePkgDefFile=false` 且清单中没有
> `Microsoft.VisualStudio.VsPackage` 资产——包根本没有注册入口。0.2.0 已按上节修正。

## 未做 / 可选

- 缩短预热窗口：判定**不可行**（头文件闭包就是产品本体，且失败边界不是 CPU），该方向已放弃。
- 以 VA 为唯一变量的对照复测（12:16 会话已观测到该对照，未专门复测）。
- 上报 Microsoft / Whole Tomato：**决定不做**（相关草稿已随本轮清理删除）。

---

## 附录：过程记录（原 `docs\Inno-Setup-Installer-Plan.md` §18）

> 原文档（Inno Setup 安装器方案与版本史）已删除，第 1–17 章保留在 git 提交版本中
> （`git show HEAD:docs/Inno-Setup-Installer-Plan.md` 可取回）。下面是 §18 的完整历史记录，
> 取自提交版本；本次会话后期补充的 §18.8–18.10 结论已并入本文正文，不再重复。

### 18. VS 崩溃分析：0x80040201 / ElementNotAvailable（不是本向导的代码）

#### 18.1 现象

在 VS 18 Insiders（18.10.12224.181）里创建项目后，继续创建下一个项目 / 添加项时，VS 直接**进程消失**（不是普通卡死），可复现；打开已有项目再加就没有。

#### 18.2 证据（来自本机留下的崩溃转储）

`%LOCALAPPDATA%\CrashDumps` 里有 3 份 devenv 转储（20:59、21:20、21:21，WER bucket 三次完全相同 `1925513046915056743`）。用 `dotnet-dump` 读出：

| 项 | 值 |
|---|---|
| 事件 | `CLR20r3`，`Exception code 0xe0434352`（未处理的托管异常），faulting module `PresentationFramework 10.0.108.26451` |
| 异常 | `System.Reflection.TargetInvocationException` → 内层 **`System.Windows.Automation.ElementNotAvailableException`**，`HResult 0x80040201`，消息 *"Element does not exist or it is virtualized; use VirtualizedItem Pattern if it is supported."* |
| 抛出点 | `Marshal.ThrowExceptionForHRInternal` ← `Microsoft.VisualStudio.Dialogs.ServiceHelper.GenerateItemName(IVsProject, UInt32, IVsTemplate)` ← `NewProjectDialog.UpdateNameField` ← `ApplyTemplateSelection` ← `TemplateSelectionChangedDelayed` |
| 触发路径 | 资源管理器右键 → 上下文菜单命令 → `SVsDialogService.InvokeDialog(VSNEWPROJECTDLGINFO)` → `DialogWindow.ShowModal`（即**在已有解决方案里"添加 → 新建项目"**） |
| 关键否定证据 | 94 个线程的托管栈里 **没有一帧属于 `ArxVsixWizard`**（`clrstack -all` 全表检索） |

#### 18.3 结论

0x80040201 是 UIA 的 `UIA_E_ELEMENTNOTAVAILABLE`：VS 的「新建项目」对话框在**为选中的模板生成默认项目名**时，去问已有项目/解决方案要一个不重名的名字，这一步走到了自己的 UI Automation 提供程序，而那个元素已经被虚拟化掉了；异常从 VS 的对话框代码里逃出来，没人接 → 进程直接没了。

**这是 VS 自己的 Bug，不在本扩展的调用路径上**，两条硬证据：① 抛异常的调用链全是 `Microsoft.VisualStudio.Dialogs.*`；② 崩溃瞬间所有托管线程栈里都没有我们的程序集。

对照 VS 自带的 VC 项目模板（`Common7\IDE\ProjectTemplates\VC\MFCATL\*`）：它们同样是 `<DefaultName>` + `<ProvideDefaultName>true</ProvideDefaultName>`，所以这条 `GenerateItemName` 调用**不是我们模板独有的**——但用户当前环境里每次都在我们的模板上触发，所以 0.1.7 先按 18.5 把我们的模板从这条路径上摘出去（能摘掉就说明触点在调用侧而不是模板内容）。

#### 18.4 第二轮复现（21:47 转储）追加的事实

用户装完 0.1.6 后再次复现，新增第 4 份转储，结论修正两点：

- 崩溃点与前三份**完全一致**（同一 `UpdateNameField` → `GenerateItemName` → UIA `0x80040201`），外层路径也一致（资源管理器右键 → 上下文菜单命令 → `SVsDialogService.InvokeDialog`）。所以「先建项目再选模板」这条路径每次都崩，换版本号并不影响。
- `ArxVsixWizard.dll` **已加载**（来自 `18.0_436ebb14` 那个扩展目录），但崩溃瞬间**没有任何一帧在跑我们的代码**，项目目录里也**没有任何项文件**产生 —— 崩在"选中模板 → 自动填名字"这一步，早于向导的 `RunStarted`。
- 安装是干净的：17.0 与 18.0 两个 VS 实例各只有**一个**扩展目录，dll = 0.1.6.0，9/9 模板 `<Assembly>` = 0.1.6.0，**不存在版本错配**（这条假设可以排除）。

#### 18.5 0.1.7 的规避改动

我们改不了 VS，但能改**自己模板的元数据**，让 VS 不去走那条崩掉的调用：

- **项目模板（`ArxApp` / `OmfApp`）**：`<ProvideDefaultName>true</ProvideDefaultName>` 改成 **false**（`<DefaultName>` 保留）。依据：`GenerateItemName` 唯一的用途就是自动填 Name 框，而 `Microsoft.VisualStudio.Dialogs.dll` 里 `ProvideDefaultName` 与 `GenerateItemName` 两个字符串同时存在，说明这段逻辑确实认这个标志；设为 false 后 VS 不应再向项目系统要默认名，也就不会碰那个虚拟化元素。4 份崩溃转储的触发点全在 `NewProjectDialog`（18.2 / 18.4），所以这 2 个模板才是要在意的地方。
- **项模板（7 个）**：**保持 `true`**。起初一刀切全改成 false，结果 `false` 的语义（微软文档明确写着：Name 框会被填成占位值而非真实名字）让「添加新项」的名称框留空、**「添加」按钮一直是灰的**，用户表现为"选完模板却加不进去"——比崩溃更直接地把功能废掉。项模板不在崩溃路径上，没有理由付这个代价。

代价（仅剩项目模板）：新建项目时 Name 框不再自动预填。**这是规避不是定论**——如果 0.1.7 还崩，说明这条 UIA 调用另有触发点，需要按 18.4 的对照实验定位。

#### 18.6 定位用的对照实验（按顺序做，做一步就能少一半可能）

1. **换成 VS 自带的 VC 模板**（如「空项目」「控制台应用」），在同样的"已有解决方案里添加"路径下点选 → 若同样崩，就是 VS 侧的问题（本扩展无关），报 Microsoft / 换 Release 通道。
2. **换到 VS 2022（17.x）**试同一套操作：本扩展在 `17.0_c051676b` 那个实例里也装了同一份 0.1.6。17.x 不崩、18.x 崩 → 确认是 VS 18 Insiders 的问题，演示时走 17.x。
3. 资源管理器树**收起来**（减少被虚拟化的节点）后再点选模板，看是否还崩 —— 崩溃消息本身就是"元素被虚拟化"。
4. 装 0.1.7 复测：Name 框不再预填，若不再崩 → 就是那条 `GenerateItemName` 调用；若照崩 → 触点在别处，把新转储给我继续挖。

#### 18.7 其他

- 本扩展侧够不着这条异常：它发生在 WPF 的 dispatcher 里、属于 VS 的对话框，向导拿到控制权**之前**就崩了。
- 转储文件 4×约 350-360 MB，在 `%LOCALAPPDATA%\CrashDumps\devenv.exe.*.dmp`，确认无用后可删。
- 读转储的方法：`dotnet tool install dotnet-dump --tool-path <dir>` → `dotnet-dump analyze <dump> -c "pe" -c "printexception <内层异常地址>" -c "clrstack"`（`pe` 拿到内外层异常类型，`printexception` 拿到抛出点的托管栈，`clrstack -all` 可确认某个程序集在不在栈上）。
