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

The same wizards also exist as a modern **VSIX** (`ArxVsixWizard`) for Visual Studio 2022 (17.x) and 2026 (18.x). This branch installs it without its template definitions - the install is what makes Visual Studio rebuild the list the classic entries live in - so the wizards listed above stay the only ones on offer; see [ArxVsixWizard/README.md](ArxVsixWizard/README.md).

> **Status (0.2.4).** Two lines are buildable here - the VSIX and the Inno single-file installer, both
> at 0.2.4 (the MSI/Burn tree lives on `main`) - and **this branch ships the classic line**: New
> Project lists the two `.vsz/.vsdir` entries the installer copies into `vcprojects\Autodesk`
> (*ObjectARX/DBX/CRX Application (Multi-Version)* and *ObjectARX/DBX Application with OMF Support
> (Multi-Version)* - the same names the VSIX line's templates carry), and **Add → Class** lists
> the item wizards it copies into
> `VCAddClass`. The installer still deploys the VSIX, because that install is what makes Visual
> Studio rebuild the wizard list those `.vsdir` entries live in - but the VSIX is built without any
> template definition, so nothing from the VSIX line is listed (dev and main ship the VSIX line
> instead). This line has no **Add ObjectARX Class...** command either: classes are added with the
> classic **Add → Class**, and the extension itself is only the wizard-list trigger.
>
> Known VS-side issue: in VS 2022 (17.x) and 2026 (18.x), selecting an item template that pre-fills
> the name can take `devenv` down while a freshly created project is still being parsed
> (UI-Automation `ElementNotAvailableException`, `0x80040201`); the workaround on the VSIX line is
> **Add ObjectARX Class...** on the Solution Explorer project context menu. Waiting for the status
> bar to show **Ready** / 「就绪」 before adding an item helps on either line. Analysis and
> measurements: [docs/VS-AddNewItem-Crash.md](docs/VS-AddNewItem-Crash.md).

---

## 🚀 Getting Started

### Prerequisites

- **Visual Studio 2022 (17.x) or 2026 (18.x)**
- **ObjectARX SDK** (corresponding to your AutoCAD version)
- **WiX Toolset v3.14** (only for the MSI/Burn line, which lives on `main`)
- **Inno Setup 6 or 7** (for building the Inno line - optional)
- **AutoCAD** (for testing your ObjectARX applications)

### 📥 Clone the Repository

```bash
git clone https://github.com/ADN-DevTech/ObjectARX-Wizards.git
cd ObjectARX-Wizards
```

### 🔨 Build Instructions

Two lines are buildable here, both at version **0.2.4**; the MSI/Burn bundle lives on `main`.

| Line | Command | Output |
|---|---|---|
| VSIX (installed as the wizard-list trigger on this line: no template definitions inside) | `msbuild ArxVsixWizard\ArxVsixWizard.csproj -t:Restore,Build -p:Configuration=Release` | `ArxVsixWizard\bin\Release\ArxVsixWizard.vsix` |
| Inno Setup (single file, the package to ship here) | `tools\build-and-pack.ps1 -BuildRoot <build root>` | `<build root>\inno\ObjectARXMultiVersionWizards-classic.exe` |

Build into a **separate build directory**, never into the checkout: mirror the tree, build the copy, keep the source tree read-only. The local convention (build root, exclusions, per-line commands) is in `AGENTS.local.md`; `tools\build-and-pack.ps1` does the whole Inno line in one go - mirror, VSIX, props generator, ISCC.

Details: [ArxVsixWizard/README.md](ArxVsixWizard/README.md) for the VSIX and `InnoSetupInstaller\ObjectARXMultiVersionWizards.iss` for the Inno line (its original plan document was removed; the crash analysis summary is in [docs/VS-AddNewItem-Crash.md](docs/VS-AddNewItem-Crash.md)). The WiX/MSI project is not part of this branch.


### 💾 Installation

This is the classic line: the `.vsz` project and item wizards plus the props. The Inno single-file
installer is the only package built here:

1. **`<build root>\inno\ObjectARXMultiVersionWizards-classic.exe`** - single file, Chinese-first UI, user-selectable install folder (build-directory convention: `AGENTS.local.md`).

Restart Visual Studio afterwards. The project wizards then appear in **File → New → Project** as *ObjectARX/DBX/CRX Application (Multi-Version)* and *ObjectARX/DBX Application with OMF Support (Multi-Version)* - the same two names the VSIX line uses - and the item wizards in **Add → Class**. The installer registers them for every Visual Studio on the machine (VS 2022 and 2026).

The installer deploys the VSIX as well, because installing the extension is what makes Visual Studio rebuild the wizard list the `.vsdir` entries live in; the container is built without any template definition, so the classic entries above are the only ones listed. The VS-side defect around pre-filled template names, and the full analysis, are in [docs/VS-AddNewItem-Crash.md](docs/VS-AddNewItem-Crash.md).

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
