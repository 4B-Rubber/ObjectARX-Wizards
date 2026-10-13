# ObjectARX Wizards for Visual Studio

**AutoCAD/ObjectARX Wizards for Visual Studio 2022 and 2026**  
*by Cyrille Fauvel, Madhukar Moogala - Autodesk Developer Network (ADN)*

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## 📋 Overview

This repository contains Visual Studio project wizards for **ObjectARX** development, making it easier to create AutoCAD plugins and applications. The wizards provide templates and boilerplate code for various ObjectARX development scenarios.

### Available Wizards (shipped by the VSIX)

- **ObjectARX/DBX/CRX Application (Multi-Version)** - main project wizard (AutoCAD 2010-2027)
- **ObjectARX/DBX Application with OMF Support (Multi-Version)** - OMF/MAP vertical SDK project wizard
- **ObjectDBX Custom Object Class** - custom AutoCAD entity creation
- **ObjectARX/DBX Transient Reactors Class** - event-driven reactor implementations
- **ObjectARX Jig Class** - interactive drawing jigs
- **ObjectARX MFC Class** - MFC-based UI components
- **ObjectARX .NET Wrapper Class** - .NET wrapper classes
- **AutoCAD COM Wrapper Object** - COM component wrappers
- **AutoCAD Object Dynamic Property** - dynamic property implementations

On this branch the modern **VSIX** (`ArxVsixWizard`) *is* the product, in Visual Studio 2022 (17.x) and 2026 (18.x) - see [ArxVsixWizard/README.md](ArxVsixWizard/README.md). The classic `.vsz` wizard sources and the `_Installs` payload live on the `classic-wizards` branch only.

> **Status (0.2.10).** The VSIX and the Inno single-file installer are built from this tree (the
> MSI/Burn bundle lives on `main`). The setup installs nothing into Visual Studio's
> `vcprojects`/`vcprojectitems` folders itself - the VSIX is the whole Visual Studio integration;
> registrations this product's own 0.2.4-and-earlier builds left under the legacy names shared with
> the official yearly ObjectARX wizard are only reclaimed when their content identifies this
> product, so installing or uninstalling this line never removes the official wizard.
> Known VS-side issue: in VS 2022 (17.x) and 2026 (18.x), selecting an item template
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

Two lines are buildable here, both at version **0.2.10**; the MSI/Burn bundle lives on `main`.

| Line | Command | Output |
|---|---|---|
| VSIX (project + item wizards) | `msbuild ArxVsixWizard\ArxVsixWizard.csproj -t:Restore,Build -p:Configuration=Release` | `ArxVsixWizard\bin\Release\ArxVsixWizard.vsix` |
| Inno Setup (single file, the package to ship here) | `tools\build-and-pack.ps1 -BuildRoot <build root>` | `<build root>\inno\ObjectARXMultiVersionWizards-dev.exe` |

Build into a **separate build directory**, never into the checkout: mirror the tree, build the copy, keep the source tree read-only. The local convention (build root, exclusions, per-line commands) is in `AGENTS.local.md`; `tools\build-and-pack.ps1` does the whole Inno line in one go - mirror, VSIX, props generator, ISCC.

Details: [ArxVsixWizard/README.md](ArxVsixWizard/README.md) for the VSIX and `InnoSetupInstaller\ObjectARXMultiVersionWizards.iss` for the Inno line (the WiX/MSI project lives on `main`; the crash analysis summary is in [docs/VS-AddNewItem-Crash.md](docs/VS-AddNewItem-Crash.md)).


### 💾 Installation

The only package built here is the Inno single-file installer:

1. **`<build root>\inno\ObjectARXMultiVersionWizards-dev.exe`** - single file, Chinese-first UI, user-selectable install folder (build-directory convention: `AGENTS.local.md`). It installs the VSIX (the whole Visual Studio integration) and the generated multi-year property sheets; it writes nothing into Visual Studio's own wizard folders.

Restart Visual Studio afterwards. The wizards then appear in **File → New → Project** (*ObjectARX/DBX/CRX Application (Multi-Version)* and *ObjectARX/DBX Application with OMF Support (Multi-Version)*) and the item wizards in **Add → New Item** / the project context menu's **Add ObjectARX Class...**. An upgrade still takes away the files this product's own 0.2.4-and-earlier builds copied into the Visual Studio folders, but only when they identify this product - the official yearly ObjectARX wizard, which uses the same legacy names, is left untouched.

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
├── ArxVsixWizard/          # VSIX wizards (project + item) for VS 2022/2026 - the product
├── InnoSetupInstaller/     # Inno Setup line (single-file installer), carries its own SetupWizard.ico
├── tools/                  # arx-props / arx-genprops helpers
└── docs/                   # usage guide + crash analysis
```

The classic `.vsz` wizard trees (`ArxAppWiz`, `ArxWiz*`, `ArxAtlWiz*`) and the `_Installs` payload live on the `classic-wizards` branch only; the MSI/Burn project lives on `main`.

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
