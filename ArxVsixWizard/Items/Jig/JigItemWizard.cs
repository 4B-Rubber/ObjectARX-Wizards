namespace ArxVsixWizard.Items
{
    public sealed class JigItemWizard : ArxItemWizardBase
    {
        protected override ItemModel CreateModel(string suggestedName)
            => new JigItemModel(suggestedName);
    }
}