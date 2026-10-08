# ObjectARX Wizards for Visual Studio

**AutoCAD/ObjectARX Wizards for Visual Studio 2022 and 2026**  
*by Cyrille Fauvel, Madhukar Moogala - Autodesk Developer Network (ADN)*

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## 📋 Overview

This repository contains Visual Studio project wizards for **ObjectARX** development, making it easier to create AutoCAD plugins and applications. The wizards provide templates and boilerplate code for various ObjectARX development scenarios.

### Available Wizards

- **ArxAppWiz** - Main ObjectARX application wizard
- **ArxWizCustomObject** - Custom AutoCAD entity creation
- **ArxWizReactors** - Event-driven reactor implementations  
- **ArxWizJig** - Interactive drawing jigs
- **ArxWizMFCSupport** - MFC-based UI components
- **ArxWizNETWrapper** - .NET wrapper classes
- **ArxAtlWizComWrapper** - COM component wrappers
- **ArxAtlWizDynProp** - Dynamic property implementations

The same wizards are also shipped as a modern **VSIX** (`ArxVsixWizard`) that installs into Visual Studio 2022 (17.x) and 2026 (18.x) - see [ArxVsixWizard/README.md](ArxVsixWizard/README.md).

> **Status (0.1.7).** The VSIX, the MSI/Burn bundle and the Inno single-file installer are all built
> from this tree. Known open issue: on VS 2026 (18.x) selecting our *project* template can take
> `devenv` down with a UI-Automation `ElementNotAvailableException`; the analysis, dumps and repro
> steps are in [docs/Inno-Setup-Installer-Plan.md](docs/Inno-Setup-Installer-Plan.md) section 18.

---

## 🚀 Getting Started

### Prerequisites

- **Visual Studio 2022 (17.x) or 2026 (18.x)**
- **ObjectARX SDK** (corresponding to your AutoCAD version)
- **WiX Toolset v3.14** (for building the MSI/Burn line)
- **Inno Setup 6 or 7** (for building the Inno line - optional)
- **AutoCAD** (for testing your ObjectARX applications)

### 📥 Clone the Repository

```bash
git clone https://github.com/ADN-DevTech/ObjectARX-Wizards.git
cd ObjectARX-Wizards
```

### 🔨 Build Instructions

There are three buildable lines. All of them ship version **0.1.7** today.

| Line | Command | Output |
|---|---|---|
| VSIX (project + item wizards) | `msbuild ArxVsixWizard\ArxVsixWizard.csproj -t:Restore,Build -p:Configuration=Release` | `ObjectARXMultiVersionWizards.vsix` |
| MSI + Burn bundle | `ObjectARXWizardsInstaller\make.bat`, then `msbuild ObjectARXWizardsInstaller\ObjectARXWizardsBundle.wixproj -p:Configuration=Release -p:Platform=x86` | `ObjectARXMultiVersionWizards.msi`, `ObjectARXMultiVersionWizardsSetup.exe` |
| Inno Setup (single file) | `& "${env:ProgramFiles}\Inno Setup 7\ISCC.exe" InnoSetupInstaller\ObjectARXMultiVersionWizards.iss` | `InnoSetupInstaller\Output\ObjectARXMultiVersionWizardsSetup-Inno.exe` |

Details: [ArxVsixWizard/README.md](ArxVsixWizard/README.md) for the VSIX, [StepsToBuild.md](ObjectARXWizardsInstaller/StepsToBuild.md) for the WiX/MSI project, and [docs/Inno-Setup-Installer-Plan.md](docs/Inno-Setup-Installer-Plan.md) for the Inno line plus the full behaviour matrix.

Each line carries its own consistency test: `ArxVsixWizard\test-version-consistency.ps1`, `tools\arx-props\test-years-consistency.ps1`, `tools\arx-genprops\test-arx-genprops.ps1`, `InnoSetupInstaller\test-payload-parity.ps1` and `InnoSetupInstaller\test-inno-sandbox.ps1`.

### 💾 Installation

Any of the three lines installs the same wizard payload and the same VSIX - pick one:

1. **`ObjectARXMultiVersionWizardsSetup.exe`** (Burn bundle) - a single UAC prompt; installs the MSI and then the VSIX for the current user. Simplest option.
2. **`ObjectARXMultiVersionWizards.msi`** - run as **Administrator** if you want the MSI on its own (it also installs the classic `.vsz` wizards).
3. **`InnoSetupInstaller\Output\ObjectARXMultiVersionWizardsSetup-Inno.exe`** - single file, Chinese-first UI, user-selectable install folder.

Restart Visual Studio afterwards. The wizards then appear in **File → New → Project** (Visual C++ → *ObjectARX/DBX/CRX Application (Multi-Version)*) and, for the item wizards, in **Add → New Item** under the **ArxWizard** category.

Note: the item templates deliberately set `ProvideDefaultName=false`, so VS leaves the **Name** box empty; type the class name and **Add** becomes enabled. Why it is pinned that way is in [docs/Inno-Setup-Installer-Plan.md](docs/Inno-Setup-Installer-Plan.md) section 18.

---

## 🔧 Troubleshooting

**Wizards not appearing in Visual Studio?** 

See our comprehensive **[Troubleshooting Guide](ObjectARX%20Wizard%20Troubleshooting%20Guide.md)** for solutions to common issues including:

- MEF cache clearing
- Visual Studio configuration reset
- Permission and compatibility issues

---

## 📁 Project Structure

```
ObjectARX-Wizards/
├── ArxVsixWizard/          # VSIX wizards (project + item) for VS 2022/2026
├── InnoSetupInstaller/     # Inno Setup line (single-file installer)
├── ArxAppWiz/              # Main application wizard (classic .vsz)
├── ArxWizCustomObject/     # Custom object wizard  
├── ArxWizReactors/         # Reactor wizard templates
├── ArxWizJig/              # Jig wizard templates
├── ArxWizMFCSupport/       # MFC support templates
├── ArxWizNETWrapper/       # .NET wrapper templates
├── ArxAtlWizComWrapper/    # COM wrapper templates
├── ArxAtlWizDynProp/       # Dynamic property templates
├── ObjectARXWizardsInstaller/ # MSI + Burn bundle (WiX)
├── ArxAppWiz182/           # OMF/MAP vertical SDK wizard (classic .vsz)
├── tools/                  # arx-props / arx-genprops helpers shared by both installer lines
├── docs/                   # installer plan + crash analysis
└── _Installs/              # Installation files and props
```

---

## 🤝 Contributing

Contributions are welcome! Please feel free to submit issues and pull requests.

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Submit a pull request

---

## 📄 License

Copyright (c) Autodesk, Inc. All rights reserved 

Permission is hereby granted, free of charge, to any person obtaining a copy of
this software and associated documentation files (the "Software"), to deal in
the Software without restriction, including without limitation the rights to
use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
the Software, and to permit persons to whom the Software is furnished to do so,
subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER
IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN
CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

## 🔗 Resources

- [ObjectARX Documentation](https://help.autodesk.com/view/OARX/2024/ENU/)
- [AutoCAD Developer Center](https://www.autodesk.com/developer-network/platform-technologies/autocad)
- [Autodesk Developer Network](https://www.autodesk.com/developer-network)

---

**Happy ObjectARX Development! 🎯**
