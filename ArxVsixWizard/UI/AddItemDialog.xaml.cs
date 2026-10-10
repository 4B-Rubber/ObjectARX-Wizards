// Picker for the "Add ObjectARX Class..." command: the item templates the extension
// ships, plus a Name box whose suggestion the dialog invents itself (uniqueness against
// the project directory). That is precisely the step Visual Studio's Add New Item dialog
// crashes in, so doing it here is what makes the command safe during the warm-up window.
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ArxVsixWizard.Commands;

namespace ArxVsixWizard.UI
{
    public partial class AddItemDialog : Window
    {
        // Extensions the seven wizards emit for a class of this name; used both for the
        // uniqueness suggestion and the OK-time collision check.
        static readonly string[] GeneratedExtensions = { ".h", ".cpp", ".rc", ".idl", ".rgs" };
        static readonly char[] InvalidChars = { '\\', '/', ':', '*', '?', '"', '<', '>', '|' };

        readonly string _projectDir;
        readonly List<TemplateRow> _rows;
        string _lastSuggestion = "";

        public ArxItemTemplateInfo SelectedTemplate { get; private set; }
        public string ItemName { get; private set; } = "";

        public AddItemDialog(List<ArxItemTemplateInfo> templates, string projectDir)
        {
            InitializeComponent();
            _projectDir = projectDir ?? "";
            _rows = new List<TemplateRow>();
            foreach (var t in templates)
                _rows.Add(new TemplateRow(t));
            TemplateList.ItemsSource = _rows;
            if (_rows.Count > 0) TemplateList.SelectedIndex = 0;
            Loaded += (s, e) => { NameBox.Focus(); NameBox.SelectAll(); };
        }

        void OnTemplateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Refill the name only when the user has not replaced the suggestion yet.
            if (NameBox.Text.Length == 0 || NameBox.Text == _lastSuggestion)
            {
                _lastSuggestion = SuggestName(SelectedInfo());
                NameBox.Text = _lastSuggestion;
                NameBox.SelectAll();
            }
            ErrorText.Text = "";
        }

        void OnAdd(object sender, RoutedEventArgs e)
        {
            string error = Validate(NameBox.Text);
            if (error != null)
            {
                ErrorText.Text = error;
                return;
            }
            SelectedTemplate = SelectedInfo();
            ItemName = NameBox.Text.Trim();
            DialogResult = true;
        }

        ArxItemTemplateInfo SelectedInfo()
            => (TemplateList.SelectedItem as TemplateRow)?.Info ?? (_rows.Count > 0 ? _rows[0].Info : null);

        /// <summary>DefaultName, then DefaultName1, DefaultName2... until nothing collides.</summary>
        string SuggestName(ArxItemTemplateInfo info)
        {
            string baseName = info?.DefaultName ?? "";
            if (baseName.Length == 0) baseName = "MyClass";
            if (!NameTaken(baseName)) return baseName;
            for (int i = 1; i < 100; i++)
            {
                string candidate = baseName + i;
                if (!NameTaken(candidate)) return candidate;
            }
            return baseName;
        }

        bool NameTaken(string stem)
        {
            if (_projectDir.Length == 0) return false;
            foreach (var ext in GeneratedExtensions)
                if (File.Exists(Path.Combine(_projectDir, stem + ext)))
                    return true;
            return false;
        }

        string Validate(string name)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) return "A name is required.";
            if (name.IndexOfAny(InvalidChars) >= 0) return "The name is not a valid file name.";
            if (NameTaken(name)) return "Files with this name already exist in the project directory.";
            if (SelectedInfo() == null) return "Choose a class wizard first.";
            return null;
        }

        sealed class TemplateRow
        {
            public TemplateRow(ArxItemTemplateInfo info)
            {
                Info = info;
                Name = info.Name;
                Description = info.Description;
                Icon = LoadIcon(info.IconPath);
            }

            public ArxItemTemplateInfo Info { get; }
            public string Name { get; }
            public string Description { get; }
            public ImageSource Icon { get; }

            static ImageSource LoadIcon(string path)
            {
                if (string.IsNullOrEmpty(path)) return null;
                try
                {
                    var frame = BitmapFrame.Create(new Uri(path, UriKind.Absolute));
                    frame.Freeze();
                    return frame;
                }
                catch { return null; }
            }
        }
    }
}
