# AGENTS.md — 本仓库的修改规范

本文件面向所有在本仓库里工作的 AI agent（也供人类贡献者参考）。**动代码或文档前先读这份**。
这里只放"规矩"；具体方案与背景见仓库内 `docs/`，以及本机（不上传）的 `.trae/documents/`。

> 本机专用补充约定见 `AGENTS.local.md`（若存在；只在本机、不随仓库上传）。

---

## 1. 文档与路径写法

- **禁止**在文档/注释里写个人本机绝对路径（例如 `D:\<你的目录>\...`、`C:\Users\<某人>\...`）。
- 仓库内文件引用一律用**相对路径 / 相对链接**（GitHub 可点、别人 clone 后有效）：
  - 同目录：`[ArxVersion.cs](Models/ArxVersion.cs)`
  - 跨目录：`[directory.wxi](../ObjectARXWizardsInstaller/directory.wxi#L528-L593)`
  - **不要**再用 `file:///D:/...` 形式——那种链接只有作者本机能打开。
- 构建 / 工具命令里的路径用**变量或环境变量**，不要写死：
  - VS 工具：先设 `$vs = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Enterprise"`，再用 `"$vs\MSBuild\Current\Bin\MSBuild.exe"`；并就近注明"按你的版本号/版本调整"。
  - Inno / WiX 等：用 `${env:ProgramFiles}` / `${env:ProgramFiles(x86)}`。
  - 命令默认在**仓库根**执行；仓内文件用相对路径。
- 以下路径**保留原样**（机器无关的公开事实，不算"本地路径"）：
  - 产品安装位置：`C:\Program Files\Autodesk\ObjectARX Props\`、`...\ObjectARX 2027\inc\`
  - 环境变量：`%TEMP%`、`%LOCALAPPDATA%`、`%USERPROFILE%`、`%ProgramFiles(x86)%`

## 2. 构建与产物

- 三条分发线（VSIX / MSI+Burn / Inno）都从本树构建，入口与产物见根 `README.md`。
- 构建产物**不要提交**（`.gitignore` 已覆盖）：`bin/`、`obj/`、`*.wixobj`、`*.wixpdb`、`*.binlog`、`_E2EGen/`、`.tools/`、`InnoSetupInstaller/Output/`、根目录的 `*.msi` 与 `...Setup.exe`、`.trae/`。
- 以下二进制是**有意提交**的，别当垃圾删掉、也别改了不还原（它们被安装器 `.wxs` 当输入引用）：
  - 根 `ObjectARXMultiVersionWizards.vsix` is NOT in this list any more: it is a build output, written into the build root by tools\build-and-pack.ps1.
  - `ObjectARXWizardsInstaller/Bundle/ArxWizardsMsiSetup.exe`、`ArxWizardsVsixSetup.exe`
  - `ObjectARXWizardsInstaller/Binary/ArxWizCustomAction.dll`、`ArxWizCustomAction.CA.dll`

## 3. 改完必须自检

- **版本号一致性**：改动任何版本号来源（`ArxVsixWizard.csproj` 的 `<Version>`、`source.extension.vsixmanifest` 的 `Identity/@Version` 与 `Asset/@AssemblyName`、`Packaging\**\*.vstemplate` 的 `<Assembly>`）后，必须跑
  `ArxVsixWizard\test-version-consistency.ps1`（断言四处 + 已构建的 VSIX）。
- **模板回归**：改渲染器 / 模板后跑 `TemplateSmokeTest`，要求零 diff 通过。
- 各线自带一致性测试：`tools\arx-props\test-years-consistency.ps1`、`tools\arx-genprops\test-arx-genprops.ps1`、`InnoSetupInstaller\test-payload-parity.ps1`、`InnoSetupInstaller\test-inno-sandbox.ps1`。

## 4. 语言与文风

- 根 `README.md` 为英文；`ArxVsixWizard/README.md`、`docs/`、`.trae/documents/`（本机）为中文。
- **改哪个文件就沿用该文件的语言**；代码注释同理。

## 5. 提交前

- 只 `git add` 明确的文件，别用 `git add -A`。
- 确认没有把本机路径、密钥、构建产物带进提交。