using System.Windows;

namespace ModernMediator.Sample.Wpf.PubSub;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
