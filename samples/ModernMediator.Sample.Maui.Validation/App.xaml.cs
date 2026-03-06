namespace ModernMediator.Sample.Maui.Validation;
public partial class App : Application
{
    public App(AdoptionFormPage formPage)
    {
        try
        {
            InitializeComponent();
            MainPage = new NavigationPage(formPage);
        }
        catch (Exception ex)
        {
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "maui-error.txt");
            File.WriteAllText(path, ex.ToString());
            MainPage = new ContentPage
            {
                Content = new ScrollView
                {
                    Content = new Label
                    {
                        Text = ex.ToString(),
                        Margin = new Thickness(16)
                    }
                }
            };
        }
    }
}