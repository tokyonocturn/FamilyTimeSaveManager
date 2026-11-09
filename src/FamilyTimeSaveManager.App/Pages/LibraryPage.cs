using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.App;

internal sealed class LibraryPage : PageBase
{
    private readonly DataGridView _grid = new();
    private readonly DarkTextBox _search;
    private readonly ComboBox _profileFilter;
    private readonly ComboBox _kindFilter;
    private readonly Label _empty;
    private readonly Label _name;
    private readonly Label _meta;
    private readonly Label _description;
    private readonly Label _files;
    private readonly Label _source;
    private readonly RoundedButton _restoreButton;
    private readonly RoundedButton _verifyButton;
    private readonly RoundedButton _exportButton;
    private readonly RoundedButton _deleteButton;
    private Snapshot _snapshot = Snapshot.Empty;
    private bool _loadingFilters;

    public LibraryPage(MainForm main) : base(main)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Window,
            Margin = new Padding(0)
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var toolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Window, Margin = new Padding(0) };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = Theme.Window, Padding = new Padding(0, 4, 0, 0), Margin = new Padding(0) };
        _search = Ui.Search("Search backups…", 220);
        _search.Height = 32;
        _search.Inner.TextChanged += (_, _) => Rebuild();
        left.Controls.Add(_search);

        _profileFilter = DarkCombo(150);
        _profileFilter.SelectedIndexChanged += (_, _) => { if (!_loadingFilters) Rebuild(); };
        left.Controls.Add(_profileFilter);

        _kindFilter = DarkCombo(140);
        _kindFilter.Items.AddRange(new object[] { "All kinds", "Manual", "Scheduled", "Pre-restore", "Imported" });
        _kindFilter.SelectedIndex = 0;
        _kindFilter.SelectedIndexChanged += (_, _) => { if (!_loadingFilters) Rebuild(); };
        left.Controls.Add(_kindFilter);

        var right = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, AutoSize = true, BackColor = Theme.Window, Padding = new Padding(0, 4, 0, 0), Margin = new Padding(0) };
        var import = Ui.Button("Import archive", false, 124);
        import.Height = 32;
        import.Click += async (_, _) => await Main.ImportArchiveAsync();

        var exportAll = Ui.Button("Export all", false, 100);
        exportAll.Height = 32;
        exportAll.Click += async (_, _) => await Main.ExportBackupsAsync(_snapshot.Backups);

        right.Controls.Add(import);
        right.Controls.Add(exportAll);

        toolbar.Controls.Add(left, 0, 0);
        toolbar.Controls.Add(right, 1, 0);
        root.Controls.Add(toolbar, 0, 0);

        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Theme.Window, Margin = new Padding(0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360));

        var gridCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(4), Margin = new Padding(0, 0, 14, 0) };
        Grids.Style(_grid);
        _grid.MultiSelect = true;
        _grid.SelectionChanged += (_, _) => UpdateDetails();

        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Backup", FillWeight = 38 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "kind", HeaderText = "Kind", FillWeight = 16 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "episode", HeaderText = "Episode", FillWeight = 13 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "profile", HeaderText = "Profile", FillWeight = 16 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "created", HeaderText = "Created", FillWeight = 22 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "size", HeaderText = "Size", FillWeight = 12 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "verified", HeaderText = "Integrity", FillWeight = 13 });

        _empty = new Label
        {
            Text = "No backups yet.\r\n\r\nOpen Saves and choose Back up now, or import an archive you made earlier.",
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

        var detail = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(18) };
        _name = new Label
        {
            Text = "Select a backup",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = false,
            Height = 52,
            Width = 320,
            Location = new Point(18, 16),
            BackColor = Color.Transparent
        };
        _meta = new Label
        {
            Text = string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Height = 96,
            Width = 320,
            Location = new Point(18, 72),
            BackColor = Color.Transparent
        };
        _description = new Label
        {
            Text = string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = false,
            Height = 56,
            Width = 320,
            Location = new Point(18, 170),
            BackColor = Color.Transparent
        };
        _files = new Label
        {
            Text = string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Height = 90,
            Width = 320,
            Location = new Point(18, 230),
            BackColor = Color.Transparent
        };
        _source = new Label
        {
            Text = string.Empty,
            Font = Theme.Mono,
            ForeColor = Theme.Faint,
            AutoSize = false,
            Height = 48,
            Width = 320,
            Location = new Point(18, 324),
            BackColor = Color.Transparent
        };

        _restoreButton = Ui.Button("Restore…", primary: true, width: 150);
        _restoreButton.Location = new Point(18, 392);
        _restoreButton.Click += async (_, _) =>
        {
            var record = SelectedRecords().FirstOrDefault();
            if (record is not null)
            {
                await Main.RestoreBackupAsync(record);
            }
        };

        _verifyButton = Ui.Button("Verify", false, 74);
        _verifyButton.Location = new Point(176, 392);
        _verifyButton.Click += async (_, _) =>
        {
            var record = SelectedRecords().FirstOrDefault();
            if (record is not null)
            {
                await Main.VerifyBackupAsync(record);
            }
        };

        _exportButton = Ui.Button("Export selected", false, 136);
        _exportButton.Location = new Point(18, 432);
        _exportButton.Click += async (_, _) => await Main.ExportBackupsAsync(SelectedRecords());

        _deleteButton = Ui.Button("Move to trash", false, 136);
        _deleteButton.Location = new Point(160, 432);
        _deleteButton.FillColor = Theme.FromHex("#3A2328");
        _deleteButton.ForeColor = Theme.Bad;
        _deleteButton.Click += async (_, _) =>
        {
            var record = SelectedRecords().FirstOrDefault();
            if (record is not null)
            {
                await Main.DeleteBackupAsync(record);
            }
        };

        detail.Controls.AddRange(new Control[]
        {
            _name, _meta, _description, _files, _source, _restoreButton, _verifyButton, _exportButton, _deleteButton
        });
        body.Controls.Add(detail, 1, 0);

        root.Controls.Add(body, 0, 1);
        Controls.Add(root);

        UpdateDetails();
    }

    public override string Key => "library";

    public override string Title => "Backup library";

    public override string Subtitle => "Verified copies with SHA-256 manifests. Restoring always snapshots the current save first.";

    public override void Apply(Snapshot snapshot)
    {
        _snapshot = snapshot;
        RefreshProfileFilter(snapshot);
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

    private static ComboBox DarkCombo(int width) => new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        FlatStyle = FlatStyle.Flat,
        BackColor = Theme.CardAlt,
        ForeColor = Theme.Text,
        Font = Theme.Body,
        Width = width,
        Height = 32,
        Margin = new Padding(8, 4, 0, 0)
    };

    private void RefreshProfileFilter(Snapshot snapshot)
    {
        _loadingFilters = true;
        var selected = _profileFilter.SelectedIndex;

        _profileFilter.Items.Clear();
        _profileFilter.Items.Add("All profiles");
        foreach (var profile in snapshot.Profiles)
        {
            _profileFilter.Items.Add(profile.Name);
        }

        _profileFilter.SelectedIndex = Math.Clamp(selected, 0, _profileFilter.Items.Count - 1);
        _loadingFilters = false;
    }

    private IEnumerable<BackupRecord> Filtered()
    {
        var query = _snapshot.Backups.AsEnumerable();

        var text = _search.Text.Trim();
        if (text.Length > 0)
        {
            query = query.Where(b =>
                b.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || (b.Description?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || (b.SourcePath?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false)
                || b.EpisodeDisplay.Contains(text, StringComparison.OrdinalIgnoreCase));
        }

        if (_profileFilter.SelectedIndex > 0 && _profileFilter.SelectedItem is string profileName)
        {
            var profile = _snapshot.Profiles.FirstOrDefault(p => string.Equals(p.Name, profileName, StringComparison.OrdinalIgnoreCase));
            if (profile is not null)
            {
                query = query.Where(b => b.ProfileId == profile.Id);
            }
        }

        var kind = _kindFilter.SelectedItem as string;
        if (kind is not null && kind != "All kinds")
        {
            query = query.Where(b => b.KindDisplay == kind);
        }

        return query;
    }

    private void Rebuild()
    {
        _grid.Rows.Clear();
        foreach (var record in Filtered())
        {
            var profile = _snapshot.Profiles.FirstOrDefault(p => p.Id == record.ProfileId);
            var index = _grid.Rows.Add(
                record.Name,
                record.KindDisplay,
                record.Episode == Episode.Unknown ? "shared" : record.Episode.ToShortLabel(),
                profile?.Name ?? "Default",
                record.CreatedUtc.ToLocalTime().ToString("g"),
                record.SizeDisplay,
                record.Verified ? "verified" : "check");

            _grid.Rows[index].Tag = record;
            _grid.Rows[index].Cells["verified"].Style.ForeColor = record.Verified ? Theme.Good : Theme.Warn;
        }

        _empty.Visible = _grid.Rows.Count == 0;
        _empty.BringToFront();
        UpdateDetails();
    }

    private IReadOnlyList<BackupRecord> SelectedRecords() =>
        _grid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(r => r.Tag)
            .OfType<BackupRecord>()
            .ToList();

    private void UpdateDetails()
    {
        var selected = SelectedRecords();
        var record = selected.FirstOrDefault();

        _restoreButton.Enabled = record is not null;
        _verifyButton.Enabled = record is not null;
        _deleteButton.Enabled = record is not null;
        _exportButton.Enabled = selected.Count > 0;

        if (record is null)
        {
            _name.Text = "Select a backup";
            _meta.Text = string.Empty;
            _description.Text = string.Empty;
            _files.Text = string.Empty;
            _source.Text = string.Empty;
            return;
        }

        _name.Text = record.Name;
        _meta.Text =
            $"{record.KindDisplay} backup · {record.CreatedUtc.ToLocalTime():f}\r\n" +
            $"{record.EpisodeDisplay}" +
            (string.IsNullOrWhiteSpace(record.BuildLabel) ? string.Empty : $" · build {record.BuildLabel}") + "\r\n" +
            $"{record.Files.Count} file(s) · {record.SizeDisplay}\r\n" +
            $"Integrity: {(record.Verified ? "verified" : "not verified")}";
        _description.Text = string.IsNullOrWhiteSpace(record.Description) ? "No description." : record.Description;
        _files.Text = "Files:\r\n" + string.Join("\r\n", record.Files.Select(f =>
            $"  {f.StoredName}  ·  {f.Length:N0} B  ·  {(f.Role == "primary" ? "save" : "build parts")}"));
        _source.Text = record.SourcePath ?? string.Empty;
    }
}
