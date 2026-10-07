// Declarative description of one field shown by UI\ItemDialog. The seven class
// wizards differ only in these descriptors plus their templates, so the dialog
// itself stays generic.
using System.Collections.Generic;

namespace ArxVsixWizard.UI
{
    public enum ItemFieldKind { Text, Bool, Combo }

    public sealed class ItemField
    {
        /// <summary>Symbol the value is published under (also the template [!output] name).</summary>
        public string Symbol;

        public string Label = "";

        public ItemFieldKind Kind = ItemFieldKind.Text;

        public string Default = "";

        public string ToolTip = "";

        public bool Required;

        /// <summary>Choices for <see cref="ItemFieldKind.Combo"/>.</summary>
        public List<string> Choices;

        /// <summary>
        /// Symbol whose value selects which choices apply (the MFC wizard filters its base class
        /// list). When set, the dialog asks <c>ItemModel.GetChoices</c> again on every change.
        /// </summary>
        public string ChoicesFrom;

        /// <summary>Recomputed live from another field's text (the old OnClass() behaviour).</summary>
        public string DerivedFrom;

        /// <summary>Extension appended by the derivation, e.g. ".h".</summary>
        public string DerivedSuffix = "";

        /// <summary>"CFoo" derives "Foo.h" - drop the leading C/c like the old wizard did.</summary>
        public bool StripLeadingC;

        /// <summary>Upper-case the derived value (the DXF name is the upper-cased class name).</summary>
        public bool Uppercase;

        /// <summary>Validated as a file name; duplicates between such fields are rejected.</summary>
        public bool ValidateAsFileName;
    }
}