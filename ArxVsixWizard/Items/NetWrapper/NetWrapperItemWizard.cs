namespace ArxVsixWizard.Items
{
    // Entry point for "Add New Item" (ObjectARX category).
    public sealed class NetWrapperItemWizard : ArxItemWizardBase
    {
        protected override ItemModel CreateModel(string suggestedName)
            => new NetWrapperItemModel(suggestedName);
    }
}