namespace ArxVsixWizard.Items
{
    public sealed class CustomObjectItemWizard : ArxItemWizardBase
    {
        protected override ItemModel CreateModel(string suggestedName)
            => new CustomObjectItemModel(suggestedName);
    }
}