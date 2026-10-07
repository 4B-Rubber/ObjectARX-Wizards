// Copyright (c) Autodesk, Inc. All rights reserved.
// C# port of the multi-year version table from ArxWizCommon/arxCommon.js
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;

namespace ArxVsixWizard.Models
{
    public sealed class ArxVersion
    {
        public string Year { get; }
        public string Toolset { get; }
        public string Sdk { get; }
        public bool X64Only { get; }
        public bool ClrCore { get; }

        public ArxVersion(string year, string toolset, string sdk, bool x64Only, bool clrCore)
        {
            Year = year;
            Toolset = toolset;
            Sdk = sdk;
            X64Only = x64Only;
            ClrCore = clrCore;
        }
    }

    public static class ArxVersionTable
    {
        /// <summary>Where the installer puts the props when the user does not change it.</summary>
        public const string PropsDirDefault = @"C:\Program Files\Autodesk\ObjectARX Props\";

        const string PropsDirRegKey = @"SOFTWARE\Autodesk\ObjectARX Wizards";
        const string PropsDirRegValue = "PropsDir";

        /// <summary>
        /// The installer lets the user choose the props folder and records the result in HKLM, so read
        /// that first and fall back to the default. Generated projects then import the real folder.
        /// </summary>
        public static string PropsDir
        {
            get
            {
                try
                {
                    using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                    using (var key = hklm.OpenSubKey(PropsDirRegKey))
                    {
                        var value = key == null ? null : key.GetValue(PropsDirRegValue) as string;
                        if (!string.IsNullOrEmpty(value))
                            return value.EndsWith("\\") ? value : value + "\\";
                    }
                }
                catch { }
                return PropsDirDefault;
            }
        }

        public static readonly IReadOnlyList<ArxVersion> Versions = new List<ArxVersion>
        {
            new ArxVersion("2010", "v90",  "18.2", false, false),
            new ArxVersion("2012", "v90",  "18.2", false, false),
            new ArxVersion("2013", "v100", "19.1", false, false),
            new ArxVersion("2014", "v100", "19.1", false, false),
            new ArxVersion("2015", "v110", "21.0", false, false),
            new ArxVersion("2016", "v110", "21.0", false, false),
            new ArxVersion("2018", "v140", "22.0", false, false),
            new ArxVersion("2019", "v141", "23.0", false, false),
            new ArxVersion("2020", "v141", "23.1", true,  false),
            new ArxVersion("2021", "v142", "24",   true,  false),
            new ArxVersion("2022", "v142", "24.1", true,  false),
            new ArxVersion("2023", "v142", "24.2", true,  false),
            new ArxVersion("2024", "v143", "24.3", true,  false),
            new ArxVersion("2025", "v143", "25",   true,  true),
            new ArxVersion("2026", "v143", "25.1", true,  true),
            new ArxVersion("2027", "v145", "26.0", true,  true),
        };

        // 预勾选年份：仅在本机确实装了该年份的 props 时才生效（见 WizardDialog）。
        // 一个二进制兼容代系勾最新那一年就够——同 SDK 主版本的其它年份共用同一套 props。
        public static readonly HashSet<string> DefaultYears = new HashSet<string>
        {
            "2020", "2024", "2026", "2027"
        };

        /// <summary>某年份 props 的完整路径。</summary>
        public static string PropsPath(string year)
            => Path.Combine(PropsDir, "Autodesk.arx-" + year + ".props");

        /// <summary>安装器是否已为本机生成该年份的 props（未勾选安装的年份没有该文件）。</summary>
        public static bool IsInstalled(string year) => File.Exists(PropsPath(year));
    }
}
