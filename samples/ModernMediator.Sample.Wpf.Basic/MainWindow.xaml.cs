using System.Windows;

namespace ModernMediator.Sample.Wpf.Basic;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
