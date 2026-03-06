namespace ModernMediator.Sample.Maui.Validation;

public partial class AdoptionFormPage : ContentPage
{
    public AdoptionFormPage(AdoptionFormViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
