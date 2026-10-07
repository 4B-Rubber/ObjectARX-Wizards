# ObjectARX 多年度安装器：Inno Setup 方案（分支 `dev`）

> **状态：仅方案与接口约定，尚无实现。** MSI 版（`main`，tag `v26.2.0-msi`）真机验收通过后，再按本文落地。
> 基线：本分支 `dev` 从 tag `v26.2.0-msi`（`faf8c73`）拉出，不修改任何既有安装器代码。

## 1. 目的

两条分发线并行：

- **`main`**：WiX 3.14 MSI + Burn 引导程序（`ObjectARXWizardsInstaller\`），已打包验证 → tag `v26.2.0-msi`。
- **`dev`**：本文，用 [Inno Setup](https://jrsoftware.org/isinfo.php) 复刻同一套安装行为 → 目标 tag `v26.2.0-inno`。

两版都做完后横向对比，再决定对外发哪一版。

要复刻的行为（全部来自已完成的 MSI 版）：

| # | 行为 | MSI 版出处 |
|---|---|---|
| 1 | 载荷：`_Installs\` 下的 VCAddClass / `*.vsz` / HTML 向导 / 共享 props | `directory.wxi` |
| 2 | 年份可勾选（2010–2027，默认勾 2018–2027），按根目录探测 SDK 并预勾选/置灰 | `UI.wxi` 的 `SdkForm`、`YEAR_*` 属性 |
| 3 | props **安装时生成**（不随包携带），按用户给的 SDK 根/AutoCAD 根产出 33 个文件 | `ArxWizCustomAction\CustomAction.cs` 的 `ArxProps` 类 |
| 4 | props 目录可选（默认 `C:\Program Files\Autodesk\ObjectARX Props`），并写注册表 | `CA_DefaultPropsDir` + `property.wxi` |
| 5 | 老向导路径修补：`*.vsz` 的 `[TARGETDIR]`、`default.htm` 的 `ADSK→RDS`、`arxCommon.js` 的 `ARX_PROPS_DIR` | `CustomAction.cs` 的 `PatchVSFiles` / `PatchHTMLWizFiles` / `PatchArxCommonJsFiles` |
| 6 | 顺带静默安装 VSIX（VS 必须已关闭） | `Bundle.wxs` |
| 7 | 卸载：删掉自己生成的 props（含年份通配）与注册表项，不留残渣 | `CA_REMOVEARXPROPS` / `CA_CLEANUPUNSELECTEDARXPROPS` |

## 2. 为什么值得保留 Inno Setup 这条路

| 维度 | WiX 3.14 MSI + Burn（现状） | Inno Setup |
|---|---|---|
| 构建工具 | 便携 WiX 3.14（116 MB，`.tools\`，需另行下载，本机不在 PATH） | ISCC（本机**未安装**，见 §7） |
| 自定义页/勾选页 | MSI 对话框集全部自绘，`*_NextArgs` 串链 | `[Code]` 里 `CreateCustomPage` + Pascal，迭代更快 |
| 通配删除 | 需走 CA（`Deferred`，`Impersonate="no"`） | `[UninstallDelete]` 原生支持通配 |
| 单文件分发 | 需 MSI + EXE 两个产物 | 单个 EXE |
| 部署/运维 | 支持 GPO / Intune / `msiexec` 修复与组件引用计数 | 无 MSI 语义，企业静默分发能力弱 |
| 已踩过的坑 | 255 字符 `CustomAction.Target` 上限（已用 `PrepArxPropsData` 绕过） | 不存在该类限制 |

结论：**行为一致的前提下，Inno 版开发与自测成本更低、产物更简单；代价是丢掉 MSI 的企业分发语义。** 这正是要对比的两点。

## 3. 架构：共享一个 props 生成器

「年份表 + 骨架 = 唯一真相」这条在 Inno 版**不变**：

```
tools\arx-props\arx-props-table.json        # 16 行年份表
tools\arx-props\props-template.props        # 普通 props 骨架
tools\arx-props\props-net-fx-template.props
tools\arx-props\props-net-core-template.props
```

现在这套逻辑以 C# 形式内嵌在 `ArxWizCustomAction` 里（`ArxProps` 类，资源经 `csproj` 的
`EmbeddedResource` 从 `tools\arx-props\` 取），只能被 MSI 的 CA 调用。

**计划：抽成独立的 `arx-genprops.exe`，两个分支共用。**

- 出处：`ArxWizCustomAction\CustomAction.cs` 的 `ArxProps` 类（生成/清理/删除三段），
  去掉 `Microsoft.Deployment.WindowsInstaller` 依赖，改为命令行入口。
- 子命令（对应现有 CA）：
  - `generate --props-dir <dir> --sdk-root <dir> --acad-root <dir> --years 2018,2019,...`
  - `cleanup --props-dir <dir> --keep 2018,...`（= `CleanupUnselectedArxProps`）
  - `remove --props-dir <dir>`（= `RemoveArxProps`，含共享 props 清理）
- MSI 版的 CA 改为**调用同一个 EXE**（内嵌为 Binary 释放到 temp），避免两处实现漂移；
  若为省事，MSI 侧也可暂不动，等 Inno 版验证完再统一。
- Inno 侧用 `[Run]` 或 `[Code]` 里的 `Exec()` 调用，**Pascal 不重复实现生成逻辑**。

## 4. 逻辑映射清单（逐条对照）

| MSI 元素 | Inno 对应物 | 备注 |
|---|---|---|
| `SdkForm` 年份勾选 + 探测/置灰 | `[Code]` 自定义页 + `TCheckListBox`；探测逻辑复用生成器或 Pascal 查目录 | 需保留「未找到 SDK 仍可勾选」的提示文案 |
| `All / None / Invert` 三个 CA | 页面上的三个按钮，直接改勾选状态 | Inno 里无需重进对话框刷新 |
| `CA_DefaultPropsDir`（默认 props 目录） | `[Code]` 初始化默认值 | 默认 `C:\Program Files\Autodesk\ObjectARX Props` |
| 用户选 props 目录 | 自定义页的目录选择控件 | 老向导要读同一路径（第 5 条） |
| `HKLM\SOFTWARE\Autodesk\ObjectARX Wizards\PropsDir` | `[Registry]` | perMachine，卸载时删除 |
| `CA_CREATEARXPROPS`（deferred） | `[Run]` 调 `arx-genprops.exe generate` | 用 `Flags: runhidden waituntilterminated` |
| `CA_CLEANUPUNSELECTEDARXPROPS` | 紧接着调 `cleanup` | |
| `CA_REMOVEARXPROPS` | `[UninstallRun]` 调 `remove` + `[UninstallDelete]` 兜底通配 `Autodesk.arx-*.props` | |
| `PatchVSFiles` / `PatchHTMLWizFiles` / `PatchArxCommonJsFiles` | `[Code]` 里直接读写文件（Inno 装完文件再执行） | 逻辑简单，Pascal 实现比再塞一个 EXE 更划算；若想零重复也可并入生成器做成 `patch-wizards` 子命令 |
| `CA_PatchPropsWizFiles` | **不需要** | 生成器直接产出最终内容，无 post-patch |
| Bundle 装 VSIX | `[Run]` 调 `VSIXInstaller.exe /quiet <vsix>` | 需先检测 VS 是否在运行并阻断；注意返回码与回滚 |
| `PreventDowngrading` / `UpgradeVersion` | `[Code]` 里的 `AppId` + 版本比较 | Inno 无 MSI 的双产品并存问题 |
| `InstallScope=perMachine` / `InstallPrivileges=elevated` | `PrivilegesRequired=admin` + `DefaultDirName={autopf}` | 单次 UAC |
| 卸载残留清理 | `[UninstallDelete]` + `[UninstallRun]` | 重点：`ObjectARX.User.props` 与共享 props |

## 5. 目录与产物约定

- 新增目录 **`InnoSetupInstaller\`**：
  - `ObjectARXMultiYearWizards.iss`（主脚本）
  - `payload\`（或直接引用仓库既有 `_Installs\`、`ObjectARXMultiYearWizards.vsix`）
  - `WizardImages\`（Banner / 小图标，复用 MSI 的 `Installerbitmaps\`）
- 产物名 **`ObjectARXMultiYearWizardsSetup-Inno.exe`**，与 MSI/Burn 的 `...Setup.exe` 区分，避免混淆。
- 构建：`ISCC.exe InnoSetupInstaller\ObjectARXMultiYearWizards.iss`

## 6. 与 MSI 版的对比方式

```powershell
git tag v26.2.0-inno            # dev 上打 tag
git diff --stat v26.2.0-msi v26.2.0-inno
git diff v26.2.0-msi v26.2.0-inno -- ObjectARXWizardsInstaller tools/arx-props
```

对比维度（两版都装一次，记录实测值）：

- 安装包体积、安装耗时、安装后磁盘占用
- 是否依赖额外工具链 / 是否需联网
- 静默安装参数（`msiexec /qn` vs `/VERYSILENT /SUPPRESSMSGBOXES`）
- 卸载残留（`ObjectARX Props` 目录、注册表、两处 `Program Files`/`Program Files (x86)`）
- 企业分发能力（GPO / Intune）

## 7. 未决问题与风险

| # | 问题 | 初步意见 |
|---|---|---|
| R1 | 本机**未安装 Inno Setup**（ISCC 不在 PATH，常见安装目录也不存在） | 落地前先装 Inno Setup 6；是否把编译器放进 `.tools\`（像 WiX 那样便携化）待定 |
| R2 | `VSIXInstaller.exe` 返回码语义、失败后是否回滚 | 先按「失败即报错并提示」处理，不做回滚 |
| R3 | 年份探测在 Inno 侧是复用 `arx-genprops.exe` 还是 Pascal 自实现 | 倾向复用，保证与 MSI 判断一致 |
| R4 | 是否必须保留 MSI（GPO/Intune 分发场景） | 若目标用户只用双击安装，Inno 版即可；否则 MSI 继续保留 |
| R5 | 代码签名 | 两版目前都没有，暂不在本分支范围 |
| R6 | 生成器抽取方式：独立 EXE 还是 VSIX 向导里的 C# 直接复用 | 倾向独立 EXE（见 §3） |

## 8. 落地顺序

1. **（当前）** 建 `dev` 分支 + 本文档。✅ 分支已建，实现暂缓。
2. 等 `v26.2.0-msi` 真机验收结论（见附录）。
3. 抽 `arx-genprops.exe`，用 `tools\arx-props\gen-arx-props.ps1` 的零 diff 基线做回归。
4. 写 `.iss` 骨架，先打通「装文件 → 生成 props → 装 VSIX」主链路。
5. 补年份页、目录选择、路径修补、卸载清理。
6. 打 tag `v26.2.0-inno`，跑 §6 对比。

---

## 附录：`v26.2.0-msi` 待验收清单

产物：仓库根 `ObjectARXMultiYearWizardsSetup.exe`（引导程序）、`ObjectARXMultiYearWizards.msi`、`ObjectARXMultiYearWizards.vsix`（22:05 版）。

- [ ] 引导程序**单次 UAC**（Bootstrapper 不应弹两次提权）
- [ ] **VS 已关闭**时才能装 VSIX；VSIX 包为 `Vital="no"` + `Permanent="yes"`
- [ ] `SdkForm` 的 **All / None / Invert** 三个按钮生效（点后对话框会重进一次，预期有轻微闪动）
- [ ] props 目录选择生效，且 `HKLM\SOFTWARE\Autodesk\ObjectARX Wizards\PropsDir` 写入正确值
- [ ] 未勾选年份的 props **被清理**（不残留旧年份文件）
- [ ] 卸载后 props 目录与注册表干净
- [ ] 生成的文件与 `_Installs\ObjectARX Props\` 对照一致（除刻意三处差异：
      Import `ObjectARX.User.props`、`ArxSdkRoot`/`AcadRoot` 默认值、判空条件）
- [ ] 注意：本机 `C:\Program Files\Autodesk\ObjectARX Props` 与 `C:\Program Files (x86)\Autodesk\ObjectARX Props`
      各有 62 个残留 props，会让 16 个年份全被勾上 —— 属预期行为，非缺陷