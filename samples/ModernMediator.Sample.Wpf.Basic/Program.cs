namespace ModernMediator.Sample.Wpf.Basic;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new global::ModernMediator.Sample.Wpf.Basic.App();
        app.InitializeComponent();
        app.Run();
    }
}
