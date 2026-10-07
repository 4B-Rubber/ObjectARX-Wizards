namespace ArxVsixWizard.Items
{
    public sealed class ReactorsItemWizard : ArxItemWizardBase
    {
        protected override ItemModel CreateModel(string suggestedName)
            => new ReactorsItemModel(suggestedName);
    }
}