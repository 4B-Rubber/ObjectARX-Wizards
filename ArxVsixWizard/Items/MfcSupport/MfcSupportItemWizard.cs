namespace ArxVsixWizard.Items
{
    public sealed class MfcSupportItemWizard : ArxItemWizardBase
    {
        protected override ItemModel CreateModel(string suggestedName)
            => new MfcSupportItemModel(suggestedName);

        protected override void RunPostActions()
        {
            MfcResourceEditor.Apply(Model, Context);
        }
    }
}