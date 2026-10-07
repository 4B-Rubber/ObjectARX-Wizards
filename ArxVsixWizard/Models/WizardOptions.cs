// Copyright (c) Autodesk, Inc. All rights reserved.
// All user choices collected by the WPF wizard page.
using System.Collections.Generic;

namespace ArxVsixWizard.Models
{
    public sealed class WizardOptions
    {
        public WizardKind Kind { get; set; } = WizardKind.ArxApp;
        public string Rds { get; set; } = "ADSK";
        public bool ImplementDebug { get; set; } = false;

        public AppType AppType { get; set; } = AppType.Arx;
        public MfcSupport Mfc { get; set; } = MfcSupport.None;
        public bool AcadMfcExtension { get; set; } = true;

        public ComServer ComServer { get; set; } = ComServer.None;
        public bool AcadAtlExtension { get; set; } = true;
        public ComImport ComImport { get; set; } = ComImport.None;

        public bool DotNetModule { get; set; } = false;
        public bool DotNetAca { get; set; } = false;
        public bool DotNetMep { get; set; } = false;

        // OMF verticals
        public bool OmfApp { get; set; } = false;
        public bool MapApi { get; set; } = false;

        public HashSet<string> SelectedYears { get; } =
            new HashSet<string>(ArxVersionTable.DefaultYears);
    }
}
