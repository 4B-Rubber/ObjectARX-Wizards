// ArxWizNETWrapper: the simplest of the seven class wizards - two files, only
// [!output] directives, no GUIDs and no project level side effects. It is the
// end-to-end probe for the whole ItemTemplates pipeline.
using ArxVsixWizard.UI;

namespace ArxVsixWizard.Items
{
    public sealed class NetWrapperItemModel : ItemModel
    {
        public NetWrapperItemModel(string suggestedName) : base(suggestedName)
        {
            Title = "ObjectARX .NET Wrapper";
            Description = "Wraps an ObjectARX C++ class so that managed (ObjectARX .NET) code can use it. "
                        + "The project must be built as a .NET mixed managed module (/clr).";

            AddField(new ItemField
            {
                Symbol = "CUSTOM_OBJECTNAME",
                Label = "ObjectARX class to be wrapped",
                Required = true,
                ToolTip = "The unmanaged ObjectARX class that the managed wrapper exposes.",
            });
            AddField(new ItemField
            {
                Symbol = "MANAGED_WRAPPER_NAME",
                Label = "Managed wrapper class name",
                Default = suggestedName,
                Required = true,
                ToolTip = "Name of the managed (ref) class that will wrap your object.",
            });
            AddField(new ItemField
            {
                Symbol = "MANAGED_DERIVATION",
                Label = "Managed base class",
                Default = "Entity",
                Required = true,
                ToolTip = "Managed class from Autodesk.AutoCAD.DatabaseServices that the wrapper derives from.",
            });
            AddField(new ItemField
            {
                Symbol = "COMPANY_NAMESPACE",
                Label = "Company namespace",
                Default = "ObjectARX",
                Required = true,
                ToolTip = "Your company name, used as the outer namespace.",
            });
            AddField(new ItemField
            {
                Symbol = "OBJECT_NAMESPACE",
                Label = "Object namespace",
                Default = "Samples",
                Required = true,
                ToolTip = "Inner namespace the object resides in.",
            });
            AddField(new ItemField
            {
                Symbol = "HEADER_FILE",
                Label = ".h file",
                Required = true,
                ValidateAsFileName = true,
                DerivedFrom = "MANAGED_WRAPPER_NAME",
                DerivedSuffix = ".h",
                StripLeadingC = true,
                ToolTip = "Header file where the wrapper class is declared.",
            });
            AddField(new ItemField
            {
                Symbol = "IMPL_FILE",
                Label = ".cpp file",
                Required = true,
                ValidateAsFileName = true,
                DerivedFrom = "MANAGED_WRAPPER_NAME",
                DerivedSuffix = ".cpp",
                StripLeadingC = true,
                ToolTip = "Implementation file for the wrapper class.",
            });

            AddFile("Wrap0.txt", "ArxWizNetWrapper.managedWrapper.h", "Wrapper.h", "HEADER_FILE");
            AddFile("Wrap1.txt", "ArxWizNetWrapper.managedWrapper.cpp", "Wrapper.cpp", "IMPL_FILE");

            Initialize();
        }
    }
}