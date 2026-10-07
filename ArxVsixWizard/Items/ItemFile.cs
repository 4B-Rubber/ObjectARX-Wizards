// One file produced by a class/item wizard.
//
// VS copies the thin wrapper file (<see cref="WrapperName"/>, shipped in the VSIX
// under ItemTemplates) and renames it to <see cref="FileNameSymbol"/>. The wrapper
// itself contains only $ArxWrapNContent$, which RunStarted injects with the text
// rendered by TemplateRenderer.
namespace ArxVsixWizard.Items
{
    public sealed class ItemFile
    {
        /// <summary>Thin file shipped next to the .vstemplate, e.g. "Wrap0.txt".</summary>
        public string WrapperName;

        /// <summary>Embedded template resource, e.g. "ArxWizNetWrapper.managedWrapper.h".</summary>
        public string ResourceName;

        /// <summary>Symbol holding the resource name, for wizards whose template depends on a choice.</summary>
        public string ResourceSymbol;

        /// <summary>Used when <see cref="FileNameSymbol"/> is not set.</summary>
        public string DefaultFileName;

        /// <summary>Symbol holding the final file name (also the vstemplate TargetFileName key).</summary>
        public string FileNameSymbol;

        /// <summary>When set, the file is only produced if that symbol is non-empty.</summary>
        public string SkipSymbol;
    }
}