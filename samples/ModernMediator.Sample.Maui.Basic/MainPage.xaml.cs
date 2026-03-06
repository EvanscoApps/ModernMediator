namespace ModernMediator.Sample.Maui.Basic;

public partial class MainPage : ContentPage
{
    public MainPage(AnimalsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
