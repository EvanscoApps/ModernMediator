using global::Avalonia.Controls;

namespace ModernMediator.Sample.Avalonia.PubSub;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
