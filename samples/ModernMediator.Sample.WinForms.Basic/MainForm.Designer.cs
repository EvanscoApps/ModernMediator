#nullable enable

namespace ModernMediator.Sample.WinForms.Basic;

public partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    private System.Windows.Forms.Button _reloadButton = null!;
    private System.Windows.Forms.Button _searchButton = null!;
    private System.Windows.Forms.Button _backgroundWorkButton = null!;
    private System.Windows.Forms.TextBox _searchTextBox = null!;
    private System.Windows.Forms.Label _searchResultLabel = null!;
    private System.Windows.Forms.Label _statusLabel = null!;
    private System.Windows.Forms.ListView _animalListView = null!;

    private void InitializeComponent()
    {
        SuspendLayout();

        _animalListView = new System.Windows.Forms.ListView
        {
            Location = new System.Drawing.Point(12, 12),
            Size = new System.Drawing.Size(560, 200),
            View = System.Windows.Forms.View.Details,
            FullRowSelect = true,
        };
        _animalListView.Columns.Add("Name", 280);
        _animalListView.Columns.Add("Type", 280);

        _reloadButton = new System.Windows.Forms.Button
        {
            Location = new System.Drawing.Point(12, 220),
            Size = new System.Drawing.Size(120, 28),
            Text = "Reload",
        };
        _reloadButton.Click += _reloadButton_Click;

        _searchTextBox = new System.Windows.Forms.TextBox
        {
            Location = new System.Drawing.Point(12, 260),
            Size = new System.Drawing.Size(280, 23),
        };

        _searchButton = new System.Windows.Forms.Button
        {
            Location = new System.Drawing.Point(298, 258),
            Size = new System.Drawing.Size(120, 28),
            Text = "Search",
        };
        _searchButton.Click += _searchButton_Click;

        _searchResultLabel = new System.Windows.Forms.Label
        {
            Location = new System.Drawing.Point(12, 295),
            Size = new System.Drawing.Size(560, 22),
            Text = "",
        };

        _backgroundWorkButton = new System.Windows.Forms.Button
        {
            Location = new System.Drawing.Point(12, 325),
            Size = new System.Drawing.Size(180, 28),
            Text = "Run Background Work",
        };
        _backgroundWorkButton.Click += _backgroundWorkButton_Click;

        _statusLabel = new System.Windows.Forms.Label
        {
            Location = new System.Drawing.Point(12, 365),
            Size = new System.Drawing.Size(560, 40),
            Text = "Ready.",
            BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
        };

        ClientSize = new System.Drawing.Size(584, 421);
        Controls.Add(_animalListView);
        Controls.Add(_reloadButton);
        Controls.Add(_searchTextBox);
        Controls.Add(_searchButton);
        Controls.Add(_searchResultLabel);
        Controls.Add(_backgroundWorkButton);
        Controls.Add(_statusLabel);
        Text = "ModernMediator WinForms.Basic Sample";

        ResumeLayout(false);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && components != null)
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }
}
