// Copyright (c) Autodesk, Inc. All rights reserved.
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ArxVsixWizard.Models;

namespace ArxVsixWizard.UI
{
    public partial class WizardDialog : Window
    {
        readonly Dictionary<string, CheckBox> _yearBoxes = new Dictionary<string, CheckBox>();
        readonly WizardKind _kind;

        public WizardOptions Options { get; } = new WizardOptions();

        public WizardDialog(WizardKind kind)
        {
            _kind = kind;
            Options.Kind = kind;
            Options.Rds = kind == WizardKind.OmfApp ? "asdk" : "ADSK";

            InitializeComponent();
            Title = kind == WizardKind.OmfApp
                ? "ObjectARX/DBX/OMF Application Wizard"
                : "ObjectARX/DBX Application Wizard";

            RdsBox.Text = Options.Rds;
            if (kind == WizardKind.OmfApp)
            {
                VerticalsBox.Visibility = Visibility.Visible;
                TypeCrx.IsEnabled = false; // OMF wizard supports ARX/DBX only
            }

            BuildYearCheckboxes();
            TypeArx.IsChecked = true;
            MfcNone.IsChecked = true;
            ComNone.IsChecked = true;
            ImportNone.IsChecked = true;
            UpdateMfcDependentState();
            UpdateComDependentState();
        }

        void BuildYearCheckboxes()
        {
            var defaults = new List<string>();
            foreach (var v in ArxVersionTable.Versions)
            {
                // 只列出本机装了的年份；没装的置灰并给提示（不直接消失，否则用户以为向导不支持）。
                bool installed = ArxVersionTable.IsInstalled(v.Year);
                bool isDefault = installed && ArxVersionTable.DefaultYears.Contains(v.Year);
                var cb = new CheckBox
                {
                    Content = v.Year,
                    Width = 78,
                    Margin = new Thickness(0, 0, 4, 4),
                    IsChecked = isDefault,
                    IsEnabled = installed
                };
                if (!installed)
                    cb.ToolTip = "未检测到该年份的 ObjectARX 属性表（" + ArxVersionTable.PropsPath(v.Year) +
                                 "）。请先运行安装程序勾选该年份。";
                if (isDefault) defaults.Add(v.Year);
                _yearBoxes[v.Year] = cb;
                YearsPanel.Children.Add(cb);
            }

            DefaultYearsLabel.Text = defaults.Count > 0
                ? "Defaults: " + string.Join(", ", defaults)
                : "未检测到任何已安装的 ObjectARX 属性表，请先运行安装程序勾选需要的年份。";
        }

        void OnSelectAllYears(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _yearBoxes.Values) if (cb.IsEnabled) cb.IsChecked = true;
        }

        void OnClearYears(object sender, RoutedEventArgs e)
        {
            foreach (var cb in _yearBoxes.Values) cb.IsChecked = false;
        }

        void OnTypeChanged(object sender, RoutedEventArgs e) { }

        void OnOmfChanged(object sender, RoutedEventArgs e)
        {
            bool omf = OmfBox.IsChecked == true;
            MfcPanel.IsEnabled = !omf;
            if (omf)
            {
                // Match the HTML wizard: OMF forces an MFC extension DLL with the AutoCAD extension
                MfcExt.IsChecked = true;
                AcadExtBox.IsChecked = true;
            }
        }

        void OnMapChanged(object sender, RoutedEventArgs e)
        {
            bool map = MapBox.IsChecked == true;
            if (map)
            {
                // MAP support forces an ObjectARX application
                TypeArx.IsChecked = true;
                TypeDbx.IsEnabled = false;
                if (_kind == WizardKind.ArxApp) TypeCrx.IsEnabled = false;
            }
            else
            {
                TypeDbx.IsEnabled = true;
                if (_kind == WizardKind.ArxApp) TypeCrx.IsEnabled = true;
            }
        }

        void OnMfcChanged(object sender, RoutedEventArgs e) => UpdateMfcDependentState();

        void UpdateMfcDependentState()
        {
            bool sharedOrExt = MfcShared.IsChecked == true || MfcExt.IsChecked == true;
            AcadExtBox.IsEnabled = sharedOrExt;
            if (!sharedOrExt) AcadExtBox.IsChecked = false;
            else AcadExtBox.IsChecked = true;
        }

        void OnComChanged(object sender, RoutedEventArgs e) => UpdateComDependentState();

        void UpdateComDependentState()
        {
            bool atl = ComAtl.IsChecked == true;
            AcadAtlExtBox.IsEnabled = atl;
            if (!atl) AcadAtlExtBox.IsChecked = false;
            else AcadAtlExtBox.IsChecked = true;
        }

        void OnOk(object sender, RoutedEventArgs e)
        {
            var years = _yearBoxes.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
            if (years.Count == 0)
            {
                ErrorText.Text = "Select at least one target AutoCAD version on the Target Versions page.";
                return;
            }

            Options.Rds = (RdsBox.Text ?? "").Trim();
            Options.AppType = TypeArx.IsChecked == true ? AppType.Arx
                          : TypeDbx.IsChecked == true ? AppType.Dbx : AppType.Crx;

            Options.Mfc = MfcStatic.IsChecked == true ? MfcSupport.RegularStatic
                        : MfcShared.IsChecked == true ? MfcSupport.RegularShared
                        : MfcExt.IsChecked == true ? MfcSupport.ExtensionShared
                        : MfcSupport.None;
            Options.AcadMfcExtension = AcadExtBox.IsChecked == true;

            Options.ComServer = ComStd.IsChecked == true ? ComServer.Standard
                              : ComAtl.IsChecked == true ? ComServer.Atl : ComServer.None;
            Options.AcadAtlExtension = AcadAtlExtBox.IsChecked == true;
            Options.ComImport = ImportDbx.IsChecked == true ? ComImport.Dbx
                              : ImportAcad.IsChecked == true ? ComImport.Acad : ComImport.None;

            Options.DotNetModule = DotNetBox.IsChecked == true;
            Options.DotNetAca = DotNetAcaBox.IsChecked == true;
            Options.DotNetMep = DotNetMepBox.IsChecked == true;

            if (_kind == WizardKind.OmfApp)
            {
                Options.OmfApp = OmfBox.IsChecked == true;
                Options.MapApi = MapBox.IsChecked == true;
            }

            Options.ImplementDebug = ImplDebugBox.IsChecked == true;
            Options.SelectedYears.Clear();
            foreach (var y in years) Options.SelectedYears.Add(y);

            DialogResult = true;
        }
    }
}
