namespace ModernMediator.Sample.Wpf.PubSub;

public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new global::ModernMediator.Sample.Wpf.PubSub.App();
        app.InitializeComponent();
        app.Run();
    }
}
