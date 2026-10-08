@echo off
setlocal enabledelayedexpansion

:: Set WiX tool paths - adjust if you installed WiX elsewhere
set WIX_BIN="%ProgramFiles(x86)%\WiX Toolset v3.14\bin"
set CANDLE=%WIX_BIN%\candle.exe
set LIGHT=%WIX_BIN%\light.exe
set EXT_PATH=%WIX_BIN%\WixVSExtension.dll

:: Input and output files. The MSI name is deliberately year-neutral: this is a multi-year product
:: (2010-2027), so nothing here may be tied to a single release.
set WXS=ObjectARXWizards.wxs
set WIXOBJ=ObjectARXWizards.wixobj
set MSI=ObjectARXMultiYearWizards.msi

:: Clean old outputs
if exist %WIXOBJ% del /f %WIXOBJ%
if exist %MSI% del /f %MSI%

echo =====================================
echo Building installer for ObjectARX...
echo =====================================

:: Compile .wxs to .wixobj. -arch x64 marks every component 64-bit, which is what the payload is:
:: it installs under the 64-bit Program Files now, and leaving the components 32-bit would put their
:: component registration (and any registry they write) in the 32-bit view. The custom action stays
:: x86 on purpose - it is an SfxCA stub and runs in the 32-bit server, which is unrelated to this.
%CANDLE% -arch x64 -I. -ext WixVSExtension %WXS%
if errorlevel 1 (
    echo candle.exe failed. Check for syntax or missing .wxi includes.
    exit /b 1
)

:: Link .wixobj to .msi
%LIGHT% -ext WixVSExtension -out %MSI% %WIXOBJ%
if errorlevel 1 (
    echo light.exe failed. Check for unresolved symbols or bad paths.
    exit /b 2
)

echo  Build succeeded: %MSI%
exit /b 0
