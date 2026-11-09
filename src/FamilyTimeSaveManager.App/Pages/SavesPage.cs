using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.App;

internal sealed class SavesPage : PageBase
{
    private readonly DataGridView _grid = new();
    private readonly DataGridView _gridPlaceholder = new();
    private readonly DarkTextBox _search;
    private readonly FlowLayoutPanel _pillHost;
    private readonly Label _empty;
    private readonly Label _name;
    private readonly Label _status;
    private readonly Label _path;
    private readonly Label _meta;
    private readonly Label _message;
    private readonly RoundedButton _backupButton;
    private readonly RoundedButton _compareButton;
    private readonly RoundedButton _openButton;

    private Snapshot _snapshot = Snapshot.Empty;
    private Episode? _episodeFilter;
    private bool _sharedOnly;

    public SavesPage(MainForm main) : base(main)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Window,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Window,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Theme.Window,
            Padding = new Padding(0, 4, 0, 0),
            Margin = new Padding(0)
        };

        _pillHost = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            BackColor = Theme.Window,
            Margin = new Padding(0, 0, 12, 0)
        };
        left.Controls.Add(_pillHost);

        _search = Ui.Search("Search saves…", 240);
        _search.Height = 32;
        _search.Margin = new Padding(0, 2, 0, 0);
        _search.Inner.TextChanged += (_, _) => Rebuild();
        left.Controls.Add(_search);

        var right = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            BackColor = Theme.Window,
            Padding = new Padding(0, 4, 0, 0),
            Margin = new Padding(0)
        };

        var refresh = Ui.Button("Rescan", false, 96);
        refresh.Height = 32;
        refresh.Click += async (_, _) => await Main.RescanAsync();

        var open = Ui.Button("Open save folder", false, 132);
        open.Height = 32;
        open.Click += (_, _) => OpenSelectedFolder();

        right.Controls.Add(refresh);
        right.Controls.Add(open);

        toolbar.Controls.Add(left, 0, 0);
        toolbar.Controls.Add(right, 1, 0);
        root.Controls.Add(toolbar, 0, 0);

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Window,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 344));

        var gridCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(4), Margin = new Padding(0, 0, 14, 0) };
        Grids.Style(_grid);
        _grid.MultiSelect = true;
        _grid.SelectionChanged += (_, _) => UpdateDetails();
        _grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0 && _grid.Rows[e.RowIndex].Tag is SaveFileInfo save)
            {
                await Main.CreateBackupAsync(save);
            }
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "file", HeaderText = "Save", FillWeight = 34 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "episode", HeaderText = "Episode", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "format", HeaderText = "Format", FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "size", HeaderText = "Size", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "modified", HeaderText = "Modified", FillWeight = 20 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "Status", FillWeight = 18 });

        _empty = new Label
        {
            Text = "No saves found yet.\r\n\r\nPlay Family Time once and save a game, or point the manager at a save folder in Settings.",
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

        body.Controls.Add(gridCard, 0, 0);

        var detailCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(18) };

        _name = new Label
        {
            Text = "Select a save",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = false,
            Height = 54,
            Width = 300,
            Location = new Point(18, 16),
            BackColor = Color.Transparent
        };
        _status = new Label
        {
            Text = string.Empty,
            Font = Theme.Bold,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Height = 22,
            Width = 300,
            Location = new Point(18, 74),
            BackColor = Color.Transparent
        };
        _path = new Label
        {
            Text = string.Empty,
            Font = Theme.Mono,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Height = 54,
            Width = 300,
            Location = new Point(18, 100),
            BackColor = Color.Transparent
        };
        _meta = new Label
        {
            Text = string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Height = 120,
            Width = 300,
            Location = new Point(18, 160),
            BackColor = Color.Transparent
        };
        _message = new Label
        {
            Text = string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = false,
            Height = 70,
            Width = 300,
            Location = new Point(18, 284),
            BackColor = Color.Transparent
        };

        _backupButton = Ui.Button("Back up now", primary: true, width: 140);
        _backupButton.Location = new Point(18, 368);
        _backupButton.Click += async (_, _) =>
        {
            if (SelectedSaves().FirstOrDefault() is { } save)
            {
                await Main.CreateBackupAsync(save);
            }
        };

        _compareButton = Ui.Button("Compare with…", false, 140);
        _compareButton.Location = new Point(166, 368);
        _compareButton.Click += (_, _) => CompareSelection();

        _openButton = Ui.Button("Open folder", false, 140);
        _openButton.Location = new Point(18, 408);
        _openButton.Click += (_, _) => OpenSelectedFolder();

        detailCard.Controls.AddRange(new Control[]
        {
            _name, _status, _path, _meta, _message, _backupButton, _compareButton, _openButton
        });

        body.Controls.Add(detailCard, 1, 0);
        root.Controls.Add(body, 0, 1);

        Controls.Add(root);

        BuildPills();
        UpdateDetails();
    }

    public override string Key => "saves";

    public override string Title => "Saves";

    public override string Subtitle => "Every save on this PC, read-only until you choose to back one up.";

    public override void Apply(Snapshot snapshot)
    {
        _snapshot = snapshot;
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

    private void BuildPills()
    {
        _pillHost.Controls.Clear();
        AddPill("All", null, false);
        AddPill("EP1", Episode.Episode1, false);
        AddPill("EP2", Episode.Episode2, false);
        AddPill("EP3", Episode.Episode3, false);
        AddPill("Shared", null, true);
    }

    private void AddPill(string label, Episode? episode, bool shared)
    {
        var selected = _episodeFilter == episode && _sharedOnly == shared;
        var pill = new RoundedButton
        {
            Text = label,
            Width = 66,
            Height = 30,
            Font = Theme.BodySmall,
            Quiet = !selected,
            FillColor = Theme.AccentSoft,
            HoverColor = Theme.CardAlt,
            BorderColor = selected ? Theme.Accent : Theme.Border,
            Radius = 15,
            Margin = new Padding(0, 2, 6, 0)
        };

        pill.Click += (_, _) =>
        {
            _episodeFilter = episode;
            _sharedOnly = shared;
            BuildPills();
            Rebuild();
        };

        _pillHost.Controls.Add(pill);
    }

    private IEnumerable<SaveFileInfo> Filtered()
    {
        var query = _snapshot.Saves.AsEnumerable();

        if (_sharedOnly)
        {
            query = query.Where(s => s.Episode == Episode.Unknown);
        }
        else if (_episodeFilter is not null)
        {
            query = query.Where(s => s.Episode == _episodeFilter);
        }

        var text = _search.Text.Trim();
        if (text.Length > 0)
        {
            query = query.Where(s =>
                s.FileName.Contains(text, StringComparison.OrdinalIgnoreCase)
                || s.Path.Contains(text, StringComparison.OrdinalIgnoreCase)
                || s.Episode.ToDisplayName().Contains(text, StringComparison.OrdinalIgnoreCase)
                || s.StatusDisplay.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        return query;
    }

    private void Rebuild()
    {
        var selection = SelectedSaves().Select(s => s.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _grid.Rows.Clear();
        foreach (var save in Filtered())
        {
            var index = _grid.Rows.Add(
                save.FileName,
                save.Episode == Episode.Unknown ? "shared" : save.Episode.ToShortLabel(),
                save.Metadata.Format,
                save.SizeDisplay,
                save.ModifiedUtc.ToLocalTime().ToString("g"),
                save.StatusDisplay);

            var row = _grid.Rows[index];
            row.Tag = save;
            row.Cells["status"].Style.ForeColor = Theme.StatusColor(save.Status);

            if (selection.Contains(save.Path))
            {
                row.Selected = true;
            }
        }

        _empty.Visible = _grid.Rows.Count == 0;
        _empty.BringToFront();
        UpdateDetails();
    }

    private IReadOnlyList<SaveFileInfo> SelectedSaves() =>
        _grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(r => r.Tag)
            .OfType<SaveFileInfo>()
            .ToList();

    private void UpdateDetails()
    {
        var selected = SelectedSaves();
        var save = selected.FirstOrDefault();

        _backupButton.Enabled = save is not null;
        _compareButton.Enabled = selected.Count == 2;
        _openButton.Enabled = save is not null;

        if (save is null)
        {
            _name.Text = "Select a save";
            _status.Text = string.Empty;
            _path.Text = string.Empty;
            _meta.Text = string.Empty;
            _message.Text = string.Empty;
            return;
        }

        _name.Text = save.FileName;
        _status.Text = save.StatusDisplay;
        _status.ForeColor = Theme.StatusColor(save.Status);
        _path.Text = save.Path;
        _meta.Text =
            $"Episode: {save.Episode.ToDisplayName()}\r\n" +
            $"Format: {save.Metadata.FormatDisplay}\r\n" +
            $"Size: {save.SizeDisplay}  ·  {save.Metadata.SectionCount} section(s)\r\n" +
            $"Embedded release: {save.Metadata.ReleaseLabelDisplay}\r\n" +
            $"Modified: {save.ModifiedUtc.ToLocalTime():g}\r\n" +
            $"SHA-256: {Ui.Shorten(save.Sha256 ?? "unavailable", 34)}\r\n" +
            $"Build-part sidecar: {(save.Metadata.HasBuildParts ? "found" : "none")}";
        _message.Text = save.StatusMessage ?? string.Empty;
        _message.ForeColor = save.Status == SaveStatus.Valid ? Theme.Faint : Theme.StatusColor(save.Status);
    }

    private void CompareSelection()
    {
        var selected = SelectedSaves();
        if (selected.Count != 2)
        {
            MessageBox.Show(this, "Select exactly two saves to compare. Hold Ctrl while clicking rows.", Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Main.ShowCompare(selected[0], selected[1]);
    }

    private void OpenSelectedFolder()
    {
        var save = SelectedSaves().FirstOrDefault();
        if (save?.Folder is not null)
        {
            Main.OpenFolder(save.Folder);
        }
    }
}
