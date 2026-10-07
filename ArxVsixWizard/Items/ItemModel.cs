// Declarative model of one class/item wizard: the dialog fields, the files to
// generate and the symbol table shared with TemplateRenderer.
using System;
using System.Collections.Generic;
using ArxVsixWizard.Engine;
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public class ItemModel
    {
        public string Title = "ObjectARX Wizard";
        public string Description = "";

        public readonly List<ItemField> Fields = new List<ItemField>();
        public readonly List<ItemFile> Files = new List<ItemFile>();
        public readonly SymbolTable Symbols = new SymbolTable();

        protected ItemModel(string suggestedName)
        {
            SuggestedName = suggestedName ?? "";
        }

        /// <summary>Name VS derived from the file the user typed ("Add New Item" name box).</summary>
        public string SuggestedName { get; }

        /// <summary>Name of the project the item is added to; set through <see cref="SetProjectName"/>.</summary>
        public string ProjectName { get; private set; } = "";

        public ItemField AddField(ItemField field)
        {
            Fields.Add(field);
            Symbols.Set(field.Symbol, field.Default ?? "");
            return field;
        }

        public ItemFile AddFile(string wrapper, string resource, string defaultFileName,
            string fileNameSymbol = null, string skipSymbol = null)
        {
            var f = new ItemFile
            {
                WrapperName = wrapper,
                ResourceName = resource,
                DefaultFileName = defaultFileName,
                FileNameSymbol = fileNameSymbol,
                SkipSymbol = skipSymbol,
            };
            Files.Add(f);
            return f;
        }

        /// <summary>Like <see cref="AddFile"/> but the template is chosen at run time through a symbol.</summary>
        public ItemFile AddSymbolicFile(string wrapper, string resourceSymbol, string defaultFileName,
            string fileNameSymbol = null, string skipSymbol = null)
        {
            var f = AddFile(wrapper, null, defaultFileName, fileNameSymbol, skipSymbol);
            f.ResourceSymbol = resourceSymbol;
            return f;
        }

        /// <summary>Embedded resource to render for <paramref name="file"/>.</summary>
        public string ResourceNameOf(ItemFile file)
            => string.IsNullOrEmpty(file.ResourceSymbol)
                ? file.ResourceName
                : Symbols.GetString(file.ResourceSymbol);

        public string FileNameOf(ItemFile file)
            => string.IsNullOrEmpty(file.FileNameSymbol)
                ? file.DefaultFileName
                : Symbols.GetString(file.FileNameSymbol);

        /// <summary>False for optional files whose controlling symbol is empty.</summary>
        public bool ShouldGenerate(ItemFile file)
            => string.IsNullOrEmpty(file.SkipSymbol)
               || !string.IsNullOrWhiteSpace(Symbols.GetString(file.SkipSymbol));

        public ItemFile FindByWrapper(string wrapperName)
        {
            foreach (var f in Files)
                if (string.Equals(f.WrapperName, wrapperName, StringComparison.OrdinalIgnoreCase))
                    return f;
            return null;
        }

        /// <summary>
        /// Call at the end of a subclass constructor so the derived symbols are valid straight
        /// away, without waiting for the dialog (offline rendering, smoke tests).
        /// </summary>
        protected void Initialize()
        {
            ApplyDerivations();
            OnFieldsChanged();
        }

        /// <summary>Symbol derivation that needs the whole model; called after the dialog closes.</summary>
        public virtual void OnFieldsChanged() => ApplyDerivations();

        /// <summary>
        /// Fills the project-wide symbols the templates expect. Item templates do not receive
        /// $projectname$ reliably, so the wizard detects the name and pushes it in here.
        /// </summary>
        public void SetProjectName(string projectName)
        {
            ProjectName = projectName ?? "";
            string safe = Names.SafeName(ProjectName);
            Symbols.Set("PROJECT_NAME", ProjectName);
            Symbols.Set("UPPER_CASE_PROJECT_NAME", ProjectName.ToUpperInvariant());
            Symbols.Set("SAFE_PROJECT_NAME", safe);
            Symbols.Set("UPPER_CASE_SAFE_PROJECT_NAME", safe.ToUpperInvariant());
        }

        /// <summary>
        /// Recomputes the fields that follow another field (the file names follow the class name
        /// in most wizards). With <paramref name="changedSymbol"/> only that field's dependants are
        /// refreshed, so a manually typed file name survives edits to unrelated fields. Without it
        /// only blank fields are filled, which is what the offline renderer needs.
        /// </summary>
        public void ApplyDerivations(string changedSymbol = null)
        {
            foreach (var field in Fields)
            {
                if (string.IsNullOrEmpty(field.DerivedFrom)) continue;
                if (changedSymbol == null)
                {
                    if (!string.IsNullOrEmpty(Symbols.GetString(field.Symbol))) continue;
                }
                else if (!string.Equals(field.DerivedFrom, changedSymbol, StringComparison.Ordinal))
                {
                    continue;
                }
                Symbols.Set(field.Symbol, Derive(field, Symbols.GetString(field.DerivedFrom)));
            }
        }

        /// <summary>Port of OnClass() from the old wizards' default.htm.</summary>
        public static string Derive(ItemField field, string source)
        {
            source = (source ?? "").Trim();
            if (source.Length == 0) return "";
            bool leadingC = source[0] == 'C' || source[0] == 'c';
            if (field.StripLeadingC)
            {
                if (source.Length == 1 && leadingC) return "";
                if (leadingC) source = source.Substring(1);
            }
            return field.Uppercase ? source.ToUpperInvariant() + field.DerivedSuffix
                                   : source + field.DerivedSuffix;
        }

        /// <summary>
        /// Items to show for a Combo field. Override to make the list depend on another field
        /// (see <see cref="ItemField.ChoicesFrom"/>).
        /// </summary>
        public virtual List<string> GetChoices(ItemField field)
            => field.Choices ?? new List<string>();

        /// <summary>Returns an error message, or null when the input is acceptable.</summary>
        public virtual string Validate()
        {
            var fileNames = new List<string>();
            foreach (var f in Fields)
            {
                string value = Symbols.GetString(f.Symbol);
                if (f.Required && string.IsNullOrWhiteSpace(value))
                    return f.Label + " is required.";
                if (!f.ValidateAsFileName)
                    continue;
                if (string.IsNullOrWhiteSpace(value))
                    return f.Label + " is required.";
                if (value.IndexOfAny(new[] { '\\', '/', ':', '*', '?', '"', '<', '>', '|' }) >= 0)
                    return f.Label + " is not a valid file name.";
                foreach (var other in fileNames)
                    if (string.Equals(other, value, StringComparison.OrdinalIgnoreCase))
                        return "Header and implementation files cannot have the same name.";
                fileNames.Add(value);
            }
            return null;
        }
    }
}