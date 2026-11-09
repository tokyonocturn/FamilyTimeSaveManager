using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.App;

internal sealed class HistoryPage : PageBase
{
    private readonly DataGridView _grid = new();
    private readonly DarkTextBox _search;
    private readonly ComboBox _actionFilter;
    private readonly Label _detail;
    private readonly Label _empty;

    public HistoryPage(MainForm main) : base(main)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Window,
            Margin = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));

        var toolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = false,
            BackColor = Theme.Window,
            Padding = new Padding(0, 6, 0, 0),
            Margin = new Padding(0)
        };

        _search = Ui.Search("Search history…", 280);
        _search.Height = 32;
        _search.Inner.TextChanged += (_, _) => Rebuild();
        toolbar.Controls.Add(_search);

        _actionFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.CardAlt,
            ForeColor = Theme.Text,
            Font = Theme.Body,
            Width = 150,
            Margin = new Padding(8, 4, 0, 0)
        };
        _actionFilter.Items.AddRange(new object[] { "All actions", "Backup", "Restore", "Export", "Import", "Delete", "Verify", "Schedule" });
        _actionFilter.SelectedIndex = 0;
        _actionFilter.SelectedIndexChanged += (_, _) => Rebuild();
        toolbar.Controls.Add(_actionFilter);

        var refresh = Ui.Button("Refresh", false, 96);
        refresh.Height = 32;
        refresh.Margin = new Padding(12, 4, 0, 0);
        refresh.Click += (_, _) => Rebuild();
        toolbar.Controls.Add(refresh);

        root.Controls.Add(toolbar, 0, 0);

        var gridCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(4), Margin = new Padding(0, 0, 0, 10) };
        Grids.Style(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "time", HeaderText = "When", FillWeight = 18 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "action", HeaderText = "Action", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "Status", FillWeight = 11 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "summary", HeaderText = "Summary", FillWeight = 59 });
        _empty = new Label
        {
            Text = "No history yet. Backups, restores and archive operations appear here.",
            Font = Theme.Body,
            ForeColor = Theme.Faint,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Visible = false
        };

        gridCard.Controls.Add(_grid);
        gridCard.Controls.Add(_empty);
        _empty.BringToFront();
        root.Controls.Add(gridCard, 0, 1);

        var detailCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(16), Margin = new Padding(0) };
        _detail = new Label
        {
            Text = "Select an entry to see its full detail.",
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        detailCard.Controls.Add(_detail);
        root.Controls.Add(detailCard, 0, 2);

        _grid.SelectionChanged += (_, _) =>
        {
            var entry = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<HistoryEntry>().FirstOrDefault();
            _detail.Text = entry?.Detail ?? "Select an entry to see its full detail.";
        };

        Controls.Add(root);
        Rebuild();
    }

    public override string Key => "history";

    public override string Title => "History";

    public override string Subtitle => "Every backup, restore and archive operation, searchable and stored locally.";

    public override void Apply(Snapshot snapshot)
    {
        Rebuild();
    }

    public override void PrepareScreenshot()
    {
        if (_grid.Rows.Count == 0)
        {
            return;
        }

        _grid.ClearSelection();
        _grid.Rows[0].Selected = true;
        _grid.CurrentCell = _grid.Rows[0].Cells[0];
    }

    private void Rebuild()
    {
        HistoryAction? action = _actionFilter.SelectedIndex switch
        {
            1 => HistoryAction.Backup,
            2 => HistoryAction.Restore,
            3 => HistoryAction.Export,
            4 => HistoryAction.Import,
            5 => HistoryAction.Delete,
            6 => HistoryAction.Verify,
            7 => HistoryAction.Schedule,
            _ => null
        };

        var entries = Main.Services.History.Search(_search.Text, action);
        _grid.Rows.Clear();
        foreach (var entry in entries)
        {
            var index = _grid.Rows.Add(
                entry.TimestampUtc.ToLocalTime().ToString("g"),
                entry.ActionDisplay,
                entry.StatusDisplay,
                entry.Summary);

            var row = _grid.Rows[index];
            row.Tag = entry;
            row.Cells["status"].Style.ForeColor = entry.Status switch
            {
                HistoryStatus.Success => Theme.Good,
                HistoryStatus.Warning => Theme.Warn,
                _ => Theme.Bad
            };
        }

        _empty.Visible = _grid.Rows.Count == 0;
        _empty.BringToFront();
    }
}
