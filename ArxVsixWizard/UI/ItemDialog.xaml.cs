// Generic, declarative dialog for the ObjectARX class/item wizards. The control
// tree is built from ItemModel.Fields so the seven wizards only need descriptors.
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ArxVsixWizard.Items;

namespace ArxVsixWizard.UI
{
    public partial class ItemDialog : Window
    {
        readonly ItemModel _model;
        readonly Dictionary<string, FrameworkElement> _controls = new Dictionary<string, FrameworkElement>();
        bool _syncing;

        public ItemDialog(ItemModel model)
        {
            InitializeComponent();
            _model = model;

            Title = model.Title;
            DescriptionText.Text = model.Description;

            if (model.Fields.Count > 0)
                BuildFields();

            // Fill the dependent fields (file names follow the class name) and let the model
            // derive whatever else it needs before the dialog is shown.
            model.ApplyDerivations();
            model.OnFieldsChanged();
            RefreshDerivedFields();
        }

        void BuildFields()
        {
            TextBox firstTextBox = null;
            for (int i = 0; i < _model.Fields.Count; i++)
            {
                var field = _model.Fields[i];
                if (i > 0) FieldsPanel.Children.Add(new Border { Height = 8 });

                switch (field.Kind)
                {
                    case ItemFieldKind.Bool:
                    {
                        var box = new CheckBox
                        {
                            Content = field.Label,
                            IsChecked = _model.Symbols.GetBool(field.Symbol),
                            ToolTip = field.ToolTip,
                        };
                        box.Checked += OnValueChanged;
                        box.Unchecked += OnValueChanged;
                        _controls[field.Symbol] = box;
                        FieldsPanel.Children.Add(box);
                        break;
                    }
                    case ItemFieldKind.Combo:
                    {
                        var panel = NewFieldPanel(field);
                        var combo = new ComboBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
                        if (field.Choices != null)
                            foreach (var choice in field.Choices) combo.Items.Add(choice);
                        combo.SelectedItem = _model.Symbols.GetString(field.Symbol);
                        combo.SelectionChanged += OnValueChanged;
                        _controls[field.Symbol] = combo;
                        panel.Children.Add(combo);
                        FieldsPanel.Children.Add(panel);
                        break;
                    }
                    default:
                    {
                        var panel = NewFieldPanel(field);
                        var text = new TextBox { Width = 300, HorizontalAlignment = HorizontalAlignment.Left };
                        text.Text = _model.Symbols.GetString(field.Symbol);
                        text.TextChanged += OnValueChanged;
                        _controls[field.Symbol] = text;
                        panel.Children.Add(text);
                        FieldsPanel.Children.Add(panel);
                        if (firstTextBox == null) firstTextBox = text;
                        break;
                    }
                }
            }

            if (firstTextBox != null)
                Loaded += (s, e) => { firstTextBox.Focus(); firstTextBox.SelectAll(); };
        }

        static StackPanel NewFieldPanel(ItemField field)
        {
            var panel = new StackPanel();
            panel.Children.Add(new TextBlock
            {
                Text = field.Required ? field.Label + " *" : field.Label,
                Margin = new Thickness(0, 0, 0, 4),
                ToolTip = field.ToolTip,
            });
            return panel;
        }

        void OnValueChanged(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            ErrorText.Text = "";
            // Publish the current values first: models with choice-driven symbols (base class
            // selects a template, for example) need them to recompute.
            string changed = ChangedSymbol(sender);
            CollectValues();
            _syncing = true;
            try
            {
                // Only the field that actually changed re-derives its dependants, so a manually
                // typed file name is not overwritten when another field is edited.
                _model.ApplyDerivations(changed);
                _model.OnFieldsChanged();
            }
            finally { _syncing = false; }
            RefreshDependentChoices(changed);
            RefreshDerivedFields();
        }

        /// <summary>
        /// Rebuilds the combos whose item list depends on <paramref name="changedSymbol"/>
        /// (the MFC wizard's base class list follows its filter).
        /// </summary>
        void RefreshDependentChoices(string changedSymbol)
        {
            bool rebuilt = false;
            foreach (var field in _model.Fields)
            {
                if (string.IsNullOrEmpty(field.ChoicesFrom)) continue;
                if (changedSymbol != null &&
                    !string.Equals(field.ChoicesFrom, changedSymbol, StringComparison.Ordinal))
                    continue;
                if (!_controls.TryGetValue(field.Symbol, out var control) || !(control is ComboBox combo))
                    continue;

                var choices = _model.GetChoices(field);
                if (choices.Count == 0) continue;
                string previous = combo.SelectedItem as string;

                _syncing = true;
                try
                {
                    combo.Items.Clear();
                    foreach (var choice in choices) combo.Items.Add(choice);
                    // Keep the selection when it survives the new filter, otherwise take the first.
                    combo.SelectedItem = choices.Contains(previous) ? previous : choices[0];
                }
                finally { _syncing = false; }

                _model.Symbols.Set(field.Symbol, combo.SelectedItem as string ?? "");
                rebuilt = true;
            }
            if (rebuilt)
                _model.OnFieldsChanged();
        }

        /// <summary>Copies every control's value into the model's symbol table.</summary>
        void CollectValues()
        {
            foreach (var field in _model.Fields)
            {
                if (!_controls.TryGetValue(field.Symbol, out var control)) continue;
                if (control is CheckBox check)
                    _model.Symbols.Set(field.Symbol, check.IsChecked == true);
                else if (control is TextBox text)
                    _model.Symbols.Set(field.Symbol, text.Text.Trim());
                else if (control is ComboBox combo)
                    _model.Symbols.Set(field.Symbol, combo.SelectedItem as string ?? "");
            }
        }

        /// <summary>Shows the model's (possibly re-derived) value in every dependent text box.</summary>
        void RefreshDerivedFields()
        {
            foreach (var field in _model.Fields)
            {
                if (string.IsNullOrEmpty(field.DerivedFrom)) continue;
                SetText(field.Symbol, _model.Symbols.GetString(field.Symbol));
            }
        }

        /// <summary>Symbol of the field whose control raised the event, or null.</summary>
        string ChangedSymbol(object sender)
        {
            foreach (var pair in _controls)
                if (ReferenceEquals(pair.Value, sender))
                    return pair.Key;
            return null;
        }

        void SetText(string symbol, string value)
        {
            if (_controls.TryGetValue(symbol, out var c) && c is TextBox box && box.Text != value)
                box.Text = value;
        }

        void OnOk(object sender, RoutedEventArgs e)
        {
            CollectValues();
            _model.OnFieldsChanged();

            string error = _model.Validate();
            if (!string.IsNullOrEmpty(error))
            {
                ErrorText.Text = error;
                return;
            }

            DialogResult = true;
        }
    }
}