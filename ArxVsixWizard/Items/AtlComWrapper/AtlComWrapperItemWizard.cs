// ArxAtlWizComWrapper item wizard: the declarative model plus the project level side
// effects (insert the coclass into the main .idl and register the .rgs resource).
using ArxVsixWizard.Engine;

namespace ArxVsixWizard.Items
{
    public sealed class AtlComWrapperItemWizard : ArxItemWizardBase
    {
        protected override ItemModel CreateModel(string suggestedName)
            => new AtlComWrapperItemModel(suggestedName);

        protected override void RunPostActions()
        {
            // Attributed projects declare the coclass in the header and let the compiler generate
            // the type library, so the old wizard skipped the .idl/.rgs edits too (default.js:40).
            if (Model.Symbols.GetBool("ATTRIBUTED")) return;

            string idl = TemplateRenderer.Render(
                ReadResource(AtlComWrapperItemModel.CoclassIdlResource), Model.Symbols);
            AtlItemSupport.InsertIdl(Context, Model.ProjectName, idl,
                Model.Symbols.GetString("COCLASS"), Model.Symbols.GetString("INTERFACE_NAME"));

            string rgs = TemplateRenderer.Render(
                ReadResource(AtlComWrapperItemModel.RgsResource), Model.Symbols);
            AtlItemSupport.AttachRgs(Context, Model.Symbols.GetString("RGS_FILE"),
                Model.Symbols.GetString("RGS_ID"), rgs);
        }
    }
}