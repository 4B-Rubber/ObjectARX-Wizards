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

> **Status (0.2.4).** The VSIX, the MSI/Burn bundle and the Inno single-file installer are all built
> from this tree. Known VS-side issue: in VS 2022 (17.x) and 2026 (18.x), selecting an item template
> that pre-fills the name can take `devenv` down while a freshly created project is still being
> parsed (UI-Automation `ElementNotAvailableException`, `0x80040201`). The VSIX ships
> **Add ObjectARX Class...** on the Solution Explorer project context menu, which bypasses the
> defective dialog entirely (introduced in 0.2.0, usable since 0.2.2). The classic rule - wait for
> the status bar to show **Ready** / 「就绪」 before using Add → New Item - still applies. Analysis
> and measurements: [docs/VS-AddNewItem-Crash.md](docs/VS-AddNewItem-Crash.md).

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

There are three buildable lines. The VSIX and Inno lines ship version **0.2.4**; the MSI/Burn bundle remains **0.1.7**.

| Line | Command | Output |
|---|---|---|
| VSIX (project + item wizards) | `msbuild ArxVsixWizard\ArxVsixWizard.csproj -t:Restore,Build -p:Configuration=Release` | `ArxVsixWizard\bin\Release\ArxVsixWizard.vsix` |

| Inno Setup (single file) | `tools\build-and-pack.ps1 -BuildRoot <build root>` | `<build root>\inno\ObjectARXMultiVersionWizards-<branch>.exe` |

Build into a **separate build directory**, never into the checkout: mirror the tree, build the copy, keep the source tree read-only. The local convention (build root, exclusions, per-line commands) is in `AGENTS.local.md`; `tools\build-and-pack.ps1` does the whole Inno line in one go - mirror, VSIX, props generator, ISCC.

Details: [ArxVsixWizard/README.md](ArxVsixWizard/README.md) for the VSIX, [StepsToBuild.md](ObjectARXWizardsInstaller/StepsToBuild.md) for the WiX/MSI project, and `InnoSetupInstaller\ObjectARXMultiVersionWizards.iss` for the Inno line (its original plan document was removed; the crash analysis summary is in [docs/VS-AddNewItem-Crash.md](docs/VS-AddNewItem-Crash.md)).


### 💾 Installation

This is the classic line: the .vsz project and item wizards plus the props, and no VSIX - dev and
main ship the VSIX instead. Any of the three lines installs the same wizard payload and the same VSIX - pick one:



3. **`<build root>\inno\ObjectARXMultiVersionWizards-<branch>.exe`** - single file, Chinese-first UI, user-selectable install folder (build-directory convention: `AGENTS.local.md`).

Restart Visual Studio afterwards. The wizards then appear in **File → New → Project** (Visual C++ → *ObjectARX/DBX/CRX Application (Multi-Version)*) and, for the item wizards, in **Add → New Item** under the **ArxWizard** category.

Note: the item templates pre-fill the **Name** box (`ProvideDefaultName=true`). After a project has just been created, either add classes through the project context menu's **Add ObjectARX Class...** (safe at any time - it does not use Visual Studio's template dialog) or wait for the status bar to show **Ready** / 「就绪」 before using Add → New Item. The underlying VS-side defect and the full analysis are in [docs/VS-AddNewItem-Crash.md](docs/VS-AddNewItem-Crash.md).

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
