// Windows platform entry point.
// Namespace deliberately avoids the "ModernMediator" root to prevent the
// namespace / class name collision in WinUI XAML-generated code (CS0426).
namespace MauiSample.Basic.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() =>
        global::ModernMediator.Sample.Maui.Basic.MauiProgram.CreateMauiApp();
}
