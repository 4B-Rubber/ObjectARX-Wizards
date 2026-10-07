// ArxWizJig: generates an AcEdJig derived class. The only non-declarative part is
// the set of multi-line symbols built from NUMBER_OF_INPUTS (the old default.js
// assembled the same strings in OnFinish).
using System.Globalization;
using System.Text;
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public sealed class JigItemModel : ItemModel
    {
        public JigItemModel(string suggestedName) : base(suggestedName)
        {
            Title = "ObjectARX Jig Class";
            Description = "Creates an AcEdJig derived class used to drag an entity interactively.";

            AddField(new ItemField
            {
                Symbol = "CLASS_NAME",
                Label = "Class name",
                Default = suggestedName,
                Required = true,
                ToolTip = "Name of the new jig class.",
            });
            AddField(new ItemField
            {
                Symbol = "ARX_OBJECTNAME",
                Label = "ObjectARX object to jig",
                Default = "AcDbEntity",
                Required = true,
                ToolTip = "The ObjectARX class being dragged by the jig.",
            });
            AddField(new ItemField
            {
                Symbol = "NUMBER_OF_INPUTS",
                Label = "Number of inputs",
                Default = "1",
                Required = true,
                ToolTip = "How many inputs the jig acquires, from 1 to 20.",
            });
            AddField(new ItemField
            {
                Symbol = "HEADER_FILE",
                Label = ".h file",
                Required = true,
                ValidateAsFileName = true,
                DerivedFrom = "CLASS_NAME",
                DerivedSuffix = ".h",
                StripLeadingC = true,
                ToolTip = "Header file where the class is declared.",
            });
            AddField(new ItemField
            {
                Symbol = "IMPL_FILE",
                Label = ".cpp file",
                Required = true,
                ValidateAsFileName = true,
                DerivedFrom = "CLASS_NAME",
                DerivedSuffix = ".cpp",
                StripLeadingC = true,
                ToolTip = "Implementation file for the class.",
            });

            AddFile("Wrap0.txt", "ArxWizJig.Jig.h", "Jig.h", "HEADER_FILE");
            AddFile("Wrap1.txt", "ArxWizJig.Jig.cpp", "Jig.cpp", "IMPL_FILE");

            Initialize();
        }

        public override void OnFieldsChanged()
        {
            base.OnFieldsChanged();
            BuildInputSymbols();
        }

        public override string Validate()
        {
            string error = base.Validate();
            if (error != null) return error;
            if (!int.TryParse(Symbols.GetString("NUMBER_OF_INPUTS"), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int nb) || nb < 1 || nb > 20)
                return "Number of inputs must be a whole number between 1 and 20.";
            return null;
        }

        /// <summary>Port of the INPUT_* / *_SWITCH assembly in ArxWizJig\Scripts\1033\default.js:18-44.</summary>
        void BuildInputSymbols()
        {
            int nb = 1;
            int.TryParse(Symbols.GetString("NUMBER_OF_INPUTS"), NumberStyles.Integer,
                CultureInfo.InvariantCulture, out nb);
            if (nb < 1) nb = 1;
            if (nb > 20) nb = 20;
            Symbols.Set("NUMBER_OF_INPUTS", nb.ToString(CultureInfo.InvariantCulture));

            var prompts = new StringBuilder("\"\\nPick point\"");
            var keywords = new StringBuilder("\"\"");
            var userCtrls = new StringBuilder("/*AcEdJig::UserInputControls::*/(AcEdJig::UserInputControls)0");
            var cursorTypes = new StringBuilder("/*AcEdJig::CursorType::*/(AcEdJig::CursorType)0");
            var sampler = new StringBuilder("case 1:\n\t\t\t// TODO : get an input here\n\t\t\t//status =GetStartPoint () ;\n\t\t\tbreak ;\n");
            var update = new StringBuilder("case 1:\n\t\t\t// TODO : update your entity for this input\n\t\t\t//mpEntity->setCenter (mInputPoints [mCurrentInputLevel]) ;\n\t\t\tbreak ;\n");

            for (int j = 1; j < nb; j++)
            {
                prompts.Append(",\n\t\t\"\\nPick point\"");
                keywords.Append(",\n\t\t\"\"");
                userCtrls.Append(",\n\t\t/*AcEdJig::UserInputControls::*/(AcEdJig::UserInputControls)0");
                cursorTypes.Append(",\n\t\t/*AcEdJig::CursorType::*/(AcEdJig::CursorType)0");
                sampler.Append("\t\tcase ").Append(j + 1)
                       .Append(":\n\t\t\t// TODO : get an input here\n\t\t\t//status =GetStartPoint () ;\n\t\t\tbreak ;\n");
                update.Append("\t\tcase ").Append(j + 1)
                      .Append(":\n\t\t\t// TODO : update your entity for this input\n\t\t\t//mpEntity->setCenter (mInputPoints [mCurrentInputLevel]) ;\n\t\t\tbreak ;\n");
            }

            Symbols.Set("INPUT_PROMPTS", prompts.ToString());
            Symbols.Set("INPUT_KEYWORDS", keywords.ToString());
            Symbols.Set("INPUT_USERCTRLS", userCtrls.ToString());
            Symbols.Set("INPUT_CURSORTYPES", cursorTypes.ToString());
            Symbols.Set("SAMPLER_SWITCH", sampler.ToString());
            Symbols.Set("UPDATE_SWITCH", update.ToString());
        }
    }
}