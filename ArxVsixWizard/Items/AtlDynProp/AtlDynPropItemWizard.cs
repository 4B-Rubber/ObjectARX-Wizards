// ArxAtlWizDynProp item wizard: the declarative model plus the project level side
// effects (insert the interface + coclass into the main .idl, register the .rgs resource).
using ArxVsixWizard.Engine;

namespace ArxVsixWizard.Items
{
    public sealed class AtlDynPropItemWizard : ArxItemWizardBase
    {
        protected override ItemModel CreateModel(string suggestedName)
            => new AtlDynPropItemModel(suggestedName);

        protected override void RunPostActions()
        {
            // Attributed projects declare the coclass in the header (default.js:41).
            if (Model.Symbols.GetBool("ATTRIBUTED")) return;

            // Interface section first, then the coclass that implements it (default.js:82-84).
            string idl = TemplateRenderer.Render(
                    ReadResource(AtlDynPropItemModel.InterfaceIdlResource), Model.Symbols)
                + "\r\n"
                + TemplateRenderer.Render(
                    ReadResource(AtlDynPropItemModel.CoclassIdlResource), Model.Symbols);
            AtlItemSupport.InsertIdl(Context, Model.ProjectName, idl,
                Model.Symbols.GetString("COCLASS"), Model.Symbols.GetString("INTERFACE_NAME"));

            string rgs = TemplateRenderer.Render(
                ReadResource(AtlDynPropItemModel.RgsResource), Model.Symbols);
            AtlItemSupport.AttachRgs(Context, Model.Symbols.GetString("RGS_FILE"),
                Model.Symbols.GetString("RGS_ID"), rgs);
        }
    }
}