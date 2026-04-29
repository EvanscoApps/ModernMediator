using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ModernMediator.Sample.WinForms.Basic.Notifications;
using ModernMediator.Sample.WinForms.Basic.Requests;

namespace ModernMediator.Sample.WinForms.Basic;

public partial class MainForm : Form
{
    private readonly IMediator _mediator;

    public MainForm(IMediator mediator)
    {
        _mediator = mediator;
        InitializeComponent();

        Load += MainForm_Load;
    }

    private async void MainForm_Load(object? sender, EventArgs e)
    {
        _mediator.SubscribeOnMainThread<StatusUpdatedNotification>(notification =>
        {
            _statusLabel.Text = notification.Message;
        }, weak: false);

        await ReloadAnimals();
    }

    private async void _reloadButton_Click(object? sender, EventArgs e)
    {
        await ReloadAnimals();
    }

    private async void _searchButton_Click(object? sender, EventArgs e)
    {
        var name = _searchTextBox.Text.Trim();
        if (string.IsNullOrEmpty(name))
            return;

        var result = await _mediator.Send(new FindAnimalByNameQuery(name));
        _searchResultLabel.Text = result.IsSuccess
            ? $"Found: {result.Value!.Name} ({result.Value.GetType().Name})"
            : $"Not found: {name}";
    }

    private async void _backgroundWorkButton_Click(object? sender, EventArgs e)
    {
        _backgroundWorkButton.Enabled = false;
        _statusLabel.Text = "Background work starting...";

        await Task.Run(() =>
        {
            for (int i = 1; i <= 3; i++)
            {
                Thread.Sleep(500);
                _mediator.Publish(new StatusUpdatedNotification(
                    $"Background work step {i}/3 (from thread {Thread.CurrentThread.ManagedThreadId})"));
            }
        });

        _statusLabel.Text = "Background work complete.";
        _backgroundWorkButton.Enabled = true;
    }

    private async Task ReloadAnimals()
    {
        var animals = await _mediator.Send(new GetAnimalsQuery());
        _animalListView.Items.Clear();
        foreach (var animal in animals)
        {
            var item = new ListViewItem(animal.Name);
            item.SubItems.Add(animal.GetType().Name);
            _animalListView.Items.Add(item);
        }
    }
}
