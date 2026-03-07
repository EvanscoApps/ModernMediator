using global::Avalonia.Controls;

namespace ModernMediator.Sample.Avalonia.Basic;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
