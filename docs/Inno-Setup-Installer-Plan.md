# ObjectARX 多年度安装器：Inno Setup 方案（分支 dev）

> **状态**：本分支已合入 MSI 线的全部修复（merge `8ecde18`）。MSI 版定版 **0.1.1 / tag `v0.1.1-msi`**；Inno 侧**尚未开始编码**。
> 版本号已从 **0.1.1** 重新起算，旧的 26.x 线作废。

## 1. 两条线

| 分支 | 方案 | 状态 |
|---|---|---|
| `main` | WiX 3.14 MSI + Burn 引导程序 + VSIX | 真机验收通过 -> `v0.1.1-msi` |
| `dev` | Inno Setup 复刻同一套安装行为 | 本文，待实现 -> 目标 `v0.1.1-inno` |

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
    PropsDir   REG_SZ   <属性表目录，默认 C:\Program Files\Autodesk\ObjectARX Props\>
    ArxRoot    REG_SZ   <ObjectARX SDK 根目录>
    AcadRoot   REG_SZ   <AutoCAD 根目录>
```

- `PropsDir` 由 VSIX 向导（`ArxVsixWizard/Models/ArxVersion.cs` 的 `PropsDir`）读取，读不到退回默认值
- `ArxRoot` / `AcadRoot` 用于**重装时预填**上次的值（MSI 用 `RegistrySearch` 回填）
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
- MSI 的升级判定**不再比 ProductVersion 大小**：旧线到过 26.x，数值上比 0.1.1 高，直接比会判成"更新的产品"而阻断安装。现在按**产品线**划界：低于旧线 RTM（18.0.0）属新线，其余属旧线，都会先被 `RemoveExistingProducts` 移除再装
- Inno 侧注意同样别让"降级安装"被拒

## 7. 目录、产物与对比

- 新增 **`InnoSetupInstaller/`**：`ObjectARXMultiYearWizards.iss`、payload、图标
- 产物名 **`ObjectARXMultiYearWizardsSetup-Inno.exe`**，与 MSI/Burn 的 `...Setup.exe` 区分
- 构建（ISCC 不在 PATH，写全路径）：`& "C:\Program Files\Inno Setup 7\ISCC.exe" InnoSetupInstaller\ObjectARXMultiYearWizards.iss`
- 对比：`git diff --stat v0.1.1-msi v0.1.1-inno`，并实测体积 / 耗时 / 静默参数 / 卸载残留（两处 `Autodesk\` 目录 + 注册表三项）/ 企业分发

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
5. 打 tag `v0.1.1-inno`，跑第 7 节对比

