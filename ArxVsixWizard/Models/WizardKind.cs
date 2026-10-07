namespace ArxVsixWizard.Models
{
    public enum WizardKind
    {
        /// <summary>ObjectARX/DBX/CRX application wizard (ArxAppWiz).</summary>
        ArxApp,

        /// <summary>ObjectARX/DBX with optional OMF/MAP support (ArxAppWiz182).</summary>
        OmfApp
    }

    public enum AppType
    {
        Arx,
        Dbx,
        Crx
    }

    public enum MfcSupport
    {
        None,
        RegularStatic,
        RegularShared,
        ExtensionShared
    }

    public enum ComServer
    {
        None,
        Standard,
        Atl
    }

    public enum ComImport
    {
        None,
        Dbx,
        Acad
    }
}
