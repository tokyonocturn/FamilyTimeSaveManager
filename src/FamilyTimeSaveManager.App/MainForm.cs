using System.Diagnostics;
using FamilyTimeSaveManager.Core;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Services;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.App;

internal sealed class MainForm : Form
{
    private readonly AppServices _services;
    private readonly Dictionary<string, PageBase> _pages = new(StringComparer.Ordinal);
    private readonly List<NavButton> _navButtons = new();

    private readonly Panel _contentHost;
    private readonly Label _pageTitle;
    private readonly Label _pageSubtitle;
    private readonly Label _statusText;
    private readonly Label _statusRight;
    private readonly BusyBar _busyBar;
    private readonly RoundedButton _rescanButton;
    private readonly RichTextBox _logBox;
    private readonly TableLayoutPanel _rightLayout;
    private readonly RowStyle _logRow;
    private readonly RoundedButton _logToggle;

    private Snapshot _snapshot = Snapshot.Empty;
    private string _currentKey = "overview";
    private bool _working;
    private bool _logVisible = true;

    public MainForm(AppServices services)
    {
        _services = services;

        Text = Program.ApplicationName;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(1120, 720);
        ClientSize = new Size(1320, 840);
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        DoubleBuffered = true;
        KeyPreview = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Window,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 238));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _rightLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Window,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 92));
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _logRow = new RowStyle(SizeType.Absolute, 172);
        _rightLayout.RowStyles.Add(_logRow);
        _rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));

        var sidebar = BuildSidebar();
        root.Controls.Add(sidebar, 0, 0);
        root.Controls.Add(_rightLayout, 1, 0);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Window,
            Padding = new Padding(26, 12, 26, 6),
            Margin = new Padding(0)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titlePanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window };
        _pageTitle = new Label
        {
            Text = "Overview",
            Font = Theme.H1,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(0, 6),
            BackColor = Color.Transparent
        };
        _pageSubtitle = new Label
        {
            Text = string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(2, 42),
            BackColor = Color.Transparent
        };
        titlePanel.Controls.Add(_pageTitle);
        titlePanel.Controls.Add(_pageSubtitle);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            AutoSize = true,
            BackColor = Theme.Window,
            Padding = new Padding(0, 18, 0, 0),
            Margin = new Padding(0)
        };

        _rescanButton = Ui.Button("Rescan", primary: true, width: 104);
        _rescanButton.Click += async (_, _) => await RescanAsync();

        var openData = Ui.Button("Open data folder", primary: false, width: 150);
        openData.Click += (_, _) => OpenFolder(_services.Paths.DataRoot);

        actions.Controls.Add(_rescanButton);
        actions.Controls.Add(openData);

        header.Controls.Add(titlePanel, 0, 0);
        header.Controls.Add(actions, 1, 0);
        _rightLayout.Controls.Add(header, 0, 0);

        _contentHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 4, 24, 10),
            BackColor = Theme.Window
        };
        _rightLayout.Controls.Add(_contentHost, 0, 1);

        var logPanel = BuildLogPanel(out var logBoxField, out var logToggleField);
        _logBox = logBoxField;
        _logToggle = logToggleField;
        _rightLayout.Controls.Add(logPanel, 0, 2);

        var statusPanel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface };
        _busyBar = new BusyBar { Dock = DockStyle.Top, Running = false };
        _statusText = new Label
        {
            Text = "Ready.",
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Location = new Point(16, 9),
            Height = 18,
            Width = 760,
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };
        _statusRight = new Label
        {
            Text = "Local only · no network",
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleRight,
            Height = 18,
            Width = 240,
            BackColor = Color.Transparent,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        statusPanel.Controls.Add(_busyBar);
        statusPanel.Controls.Add(_statusText);
        statusPanel.Controls.Add(_statusRight);
        statusPanel.Resize += (_, _) =>
        {
            _statusRight.Location = new Point(Math.Max(0, statusPanel.ClientSize.Width - _statusRight.Width - 16), 9);
            _statusText.Width = Math.Max(160, statusPanel.ClientSize.Width - _statusRight.Width - 40);
        };
        _rightLayout.Controls.Add(statusPanel, 0, 3);

        Controls.Add(root);

        RegisterPages();
        NavigateTo("overview");

        _services.Log.EntryWritten += OnLogEntry;
        _services.AutoBackup.StatusChanged += OnScheduleStatus;

        Shown += async (_, _) =>
        {
            DarkTitleBar.Apply(this);
            InitialScan = RescanAsync();
            await InitialScan;
            _services.AutoBackup.Start();
        };

        FormClosing += (_, _) => _services.AutoBackup.Stop();
    }

    public Snapshot Data => _snapshot;

    public AppServices Services => _services;

    /// <summary>Completes when the scan started by the first Shown event finishes.</summary>
    public Task InitialScan { get; private set; } = Task.CompletedTask;

    /// <summary>Selects a representative row so documentation captures show populated details.</summary>
    public void PrepareScreenshot()
    {
        if (_pages.TryGetValue(_currentKey, out var page))
        {
            page.PrepareScreenshot();
        }
    }

    private Panel BuildSidebar()
    {
        var sidebar = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Sidebar, Margin = new Padding(0) };

        var logo = new Panel { Dock = DockStyle.Top, Height = 104, BackColor = Theme.Sidebar };
        var mark = new Label
        {
            Text = "FT",
            Font = new Font("Segoe UI Semibold", 15f),
            ForeColor = Color.White,
            BackColor = Theme.Accent,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(42, 42),
            Location = new Point(20, 26)
        };
        var name = new Label
        {
            Text = "FamilyTime",
            Font = new Font("Segoe UI Semibold", 14f),
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(72, 26),
            BackColor = Color.Transparent
        };
        var caption = new Label
        {
            Text = "SAVE MANAGER",
            Font = new Font("Segoe UI Semibold", 7.5f),
            ForeColor = Theme.Faint,
            AutoSize = true,
            Location = new Point(74, 52),
            BackColor = Color.Transparent
        };
        logo.Controls.Add(mark);
        logo.Controls.Add(name);
        logo.Controls.Add(caption);
        sidebar.Controls.Add(logo);

        var footer = new Panel { Dock = DockStyle.Bottom, Height = 122, BackColor = Theme.Sidebar, Padding = new Padding(20, 0, 20, 12) };
        var localOnly = new Label
        {
            Text = "●  Everything stays on this PC",
            Font = Theme.BodySmall,
            ForeColor = Theme.Good,
            AutoSize = false,
            Height = 20,
            Width = 196,
            Location = new Point(20, 10),
            BackColor = Color.Transparent
        };
        var version = new Label
        {
            Text = $"Version {typeof(MainForm).Assembly.GetName().Version?.ToString(3) ?? "1.0.0"}",
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = false,
            Height = 20,
            Width = 196,
            Location = new Point(20, 34),
            BackColor = Color.Transparent
        };
        var dataButton = Ui.Button("Data folder", primary: false, width: 150);
        dataButton.Location = new Point(20, 64);
        dataButton.Height = 30;
        dataButton.Click += (_, _) => OpenFolder(_services.Paths.DataRoot);

        footer.Controls.Add(localOnly);
        footer.Controls.Add(version);
        footer.Controls.Add(dataButton);
        sidebar.Controls.Add(footer);

        var nav = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Sidebar,
            Padding = new Padding(0, 0, 0, 0),
            Margin = new Padding(0)
        };

        AddNav(nav, "overview", "\uE80F", "Overview");
        AddNav(nav, "saves", "\uE74E", "Saves");
        AddNav(nav, "library", "\uE8F1", "Backup library");
        AddNav(nav, "history", "\uE81C", "History");
        AddNav(nav, "profiles", "\uE716", "Profiles");
        AddNav(nav, "settings", "\uE713", "Settings");

        sidebar.Controls.Add(nav);
        sidebar.Controls.SetChildIndex(nav, 1);
        return sidebar;
    }

    private void AddNav(FlowLayoutPanel host, string key, string glyph, string label)
    {
        var button = new NavButton(glyph, label)
        {
            Dock = DockStyle.None,
            Width = 238,
            Height = 44,
            Margin = new Padding(0, 0, 0, 2)
        };
        button.Click += (_, _) => NavigateTo(key);
        host.Controls.Add(button);
        _navButtons.Add(button);
        button.Tag = key;
    }

    private Panel BuildLogPanel(out RichTextBox logBox, out RoundedButton toggle)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Surface, Padding = new Padding(0) };

        var head = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Theme.Surface };
        var title = new Label
        {
            Text = "Activity log",
            Font = Theme.H3,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(26, 8),
            BackColor = Color.Transparent
        };

        var clear = new RoundedButton
        {
            Text = "Clear",
            Quiet = true,
            Width = 64,
            Height = 24,
            Font = Theme.BodySmall,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        clear.Location = new Point(Width - 220, 5);

        var openLog = new RoundedButton
        {
            Text = "Open log file",
            Quiet = true,
            Width = 108,
            Height = 24,
            Font = Theme.BodySmall,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        openLog.Location = new Point(Width - 300, 5);
        openLog.Click += (_, _) => OpenFolder(_services.Paths.LogsRoot);

        var toggleButton = new RoundedButton
        {
            Text = "Hide",
            Quiet = true,
            Width = 64,
            Height = 24,
            Font = Theme.BodySmall,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        toggleButton.Location = new Point(Width - 150, 5);

        var box = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Surface,
            ForeColor = Theme.Muted,
            BorderStyle = BorderStyle.None,
            Font = Theme.Mono,
            ReadOnly = true,
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            WordWrap = false,
            Margin = new Padding(0)
        };

        toggleButton.Click += (_, _) => ToggleLog();
        clear.Click += (_, _) =>
        {
            box.Clear();
            SetStatus("Activity log cleared.");
        };

        head.Controls.Add(title);
        head.Controls.Add(clear);
        head.Controls.Add(openLog);
        head.Controls.Add(toggleButton);
        head.Resize += (_, _) =>
        {
            toggleButton.Location = new Point(Math.Max(0, head.ClientSize.Width - 74), 5);
            openLog.Location = new Point(Math.Max(0, head.ClientSize.Width - 194), 5);
            clear.Location = new Point(Math.Max(0, head.ClientSize.Width - 264), 5);
        };

        logBox = box;
        toggle = toggleButton;

        var logHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(26, 2, 18, 10), BackColor = Theme.Surface };
        logHost.Controls.Add(box);

        panel.Controls.Add(logHost);
        panel.Controls.Add(head);
        return panel;
    }

    private void RegisterPages()
    {
        foreach (var page in new PageBase[]
                 {
                     new OverviewPage(this),
                     new SavesPage(this),
                     new LibraryPage(this),
                     new HistoryPage(this),
                     new ProfilesPage(this),
                     new SettingsPage(this)
                 })
        {
            _pages[page.Key] = page;
            page.Visible = false;
        }
    }

    public void NavigateTo(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            return;
        }

        _currentKey = key;
        _contentHost.SuspendLayout();
        _contentHost.Controls.Clear();
        _contentHost.Controls.Add(page);
        page.Dock = DockStyle.Fill;
        page.Visible = true;
        page.BringToFront();
        _contentHost.ResumeLayout();

        _pageTitle.Text = page.Title;
        _pageSubtitle.Text = page.Subtitle;

        foreach (var button in _navButtons)
        {
            button.Selected = string.Equals(button.Tag as string, key, StringComparison.Ordinal);
            button.Invalidate();
        }

        page.Apply(_snapshot);
    }

    public void SetStatus(string message, Color? color = null)
    {
        _statusText.Text = message;
        _statusText.ForeColor = color ?? Theme.Muted;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busyBar.Running = busy;
        _rescanButton.Enabled = !busy;
        if (message is not null)
        {
            SetStatus(message);
        }
    }

    private void ToggleLog()
    {
        _logVisible = !_logVisible;
        _logRow.Height = _logVisible ? 172 : 34;
        _logToggle.Text = _logVisible ? "Hide" : "Show";
    }

    private void OnLogEntry(LogEntry entry)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        try
        {
            BeginInvoke(() =>
            {
                if (_logBox.IsDisposed)
                {
                    return;
                }

                if (_logBox.TextLength > 160_000)
                {
                    _logBox.Clear();
                }

                var color = entry.Level switch
                {
                    "error" => Theme.Bad,
                    "warn" => Theme.Warn,
                    _ => Theme.Muted
                };

                _logBox.SelectionStart = _logBox.TextLength;
                _logBox.SelectionColor = color;
                _logBox.AppendText(entry.Display + Environment.NewLine);
                _logBox.SelectionStart = _logBox.TextLength;
                _logBox.ScrollToCaret();
            });
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void OnScheduleStatus(string message)
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        try
        {
            BeginInvoke(() => SetStatus(message, Theme.Faint));
        }
        catch (InvalidOperationException)
        {
        }
    }

    public async Task RescanAsync(bool verifyChecksums = true)
    {
        if (_working)
        {
            return;
        }

        _working = true;
        SetBusy(true, "Scanning installations and save folders…");

        try
        {
            var installs = await Task.Run(() => _services.DetectInstalls());
            var locations = await Task.Run(() => _services.ResolveLocations(installs));
            var saves = await Task.Run(() => _services.Scanner.Scan(locations, verifyChecksums));
            var backups = await Task.Run(() => _services.Backups.List());

            _snapshot = new Snapshot(installs, locations, saves, backups, _services.Profiles.All);

            foreach (var page in _pages.Values)
            {
                page.Apply(_snapshot);
            }

            var problems = saves.Count(s => s.Status != SaveStatus.Valid);
            SetStatus(
                $"Scan complete · {installs.Count} installation(s) · {saves.Count} save(s)" +
                (problems > 0 ? $" · {problems} need attention" : string.Empty) +
                $" · {backups.Count} backup(s)",
                problems > 0 ? Theme.Warn : Theme.Muted);
        }
        catch (Exception ex)
        {
            _services.Log.Error("app", "Scan failed: " + ex);
            SetStatus("Scan failed: " + ex.Message, Theme.Bad);
            MessageBox.Show(this, "The scan failed:\r\n\r\n" + ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _working = false;
            SetBusy(false);
        }
    }

    public async Task ReloadBackupsAsync()
    {
        try
        {
            var backups = await Task.Run(() => _services.Backups.List());
            _snapshot = _snapshot with { Backups = backups, Profiles = _services.Profiles.All };
            foreach (var page in _pages.Values)
            {
                page.Apply(_snapshot);
            }

            SetStatus($"{_snapshot.Backups.Count} backup(s) in the library.");
        }
        catch (Exception ex)
        {
            _services.Log.Error("app", "Could not reload backups: " + ex.Message);
            SetStatus("Could not reload the backup library.", Theme.Bad);
        }
    }

    public async Task CreateBackupAsync(SaveFileInfo save)
    {
        using var dialog = new BackupDialog(save, _services.Profiles.All);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true, "Copying and hashing the save…");
        try
        {
            var record = await Task.Run(() => _services.BackupService.Create(
                save,
                dialog.BackupName,
                dialog.Description,
                dialog.ProfileId,
                BackupKind.Manual));

            await ReloadBackupsAsync();
            MessageBox.Show(this,
                $"Backup created and verified.\r\n\r\n{record.Name}\r\n{record.Files.Count} file(s), {record.SizeDisplay}",
                Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _services.Log.Error("app", "Backup failed: " + ex.Message);
            MessageBox.Show(this, "The backup failed:\r\n\r\n" + ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            SetStatus("Ready.");
        }
    }

    public async Task RestoreBackupAsync(BackupRecord record)
    {
        var destination = record.PrimaryFile is null
            ? "(unknown)"
            : Path.GetDirectoryName(record.PrimaryFile.OriginalPath) ?? record.PrimaryFile.OriginalPath;

        using var dialog = new RestoreDialog(record, destination, AppServices.IsGameRunning(), BuildRestoreWarning(record));
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true, "Restoring backup and verifying checksums…");
        try
        {
            var result = await Task.Run(() => _services.RestoreService.Restore(record));
            await RescanAsync();
            await ReloadBackupsAsync();

            if (result.Success)
            {
                MessageBox.Show(this,
                    result.Message + "\r\n\r\nA snapshot of the previous save was kept" +
                    (result.PreRestoreBackupId is null ? " (the destination was empty)." : " as a pre-restore backup."),
                    Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(this,
                    result.Message + "\r\n\r\n" + string.Join("\r\n", result.Errors),
                    Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            _services.Log.Error("app", "Restore failed: " + ex.Message);
            MessageBox.Show(this, "The restore failed:\r\n\r\n" + ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            SetStatus("Ready.");
        }
    }

    private string? BuildRestoreWarning(BackupRecord record)
    {
        var install = _snapshot.Installs.FirstOrDefault(i => i.Episode == record.Episode
                                                            && (record.Episode != Episode.Episode3 || i.SessionsFolder is not null));

        if (record.Episode != Episode.Unknown && install is null)
        {
            return $"This backup belongs to {record.Episode.ToDisplayName()}, which is not currently detected. " +
                   "Restoring into its original folder is allowed, but the game may ignore a save from another episode.";
        }

        if (install is not null
            && !string.IsNullOrWhiteSpace(record.BuildLabel)
            && !string.IsNullOrWhiteSpace(install.BuildLabel)
            && !string.Equals(record.BuildLabel, install.BuildLabel, StringComparison.OrdinalIgnoreCase))
        {
            return $"This backup was written by build {record.BuildLabel}, but the detected {install.Episode.ToDisplayName()} installation is build {install.BuildLabel}.";
        }

        if (record.Episode == Episode.Unknown)
        {
            return "This backup's episode could not be determined from the file. Check that it belongs to the game you are about to load.";
        }

        return null;
    }

    public async Task VerifyBackupAsync(BackupRecord record)
    {
        SetBusy(true, "Checking backup integrity…");
        try
        {
            var verification = await Task.Run(() => _services.BackupService.Verify(record));
            if (verification.Ok)
            {
                _services.History.Add(new HistoryEntry
                {
                    Action = HistoryAction.Verify,
                    Status = HistoryStatus.Success,
                    Summary = $"Backup '{record.Name}' verified",
                    Detail = $"{record.Files.Count} file(s) match their SHA-256 values.",
                    BackupId = record.Id
                });
                MessageBox.Show(this, "All files match the SHA-256 values recorded in the manifest.", Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                _services.History.Add(new HistoryEntry
                {
                    Action = HistoryAction.Verify,
                    Status = HistoryStatus.Failed,
                    Summary = $"Backup '{record.Name}' failed verification",
                    Detail = string.Join(" ", verification.Errors),
                    BackupId = record.Id
                });
                MessageBox.Show(this,
                    "This backup no longer matches its manifest:\r\n\r\n" + string.Join("\r\n", verification.Errors),
                    Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            await ReloadBackupsAsync();
        }
        finally
        {
            SetBusy(false);
            SetStatus("Ready.");
        }
    }

    public async Task DeleteBackupAsync(BackupRecord record)
    {
        using var confirm = new ConfirmDialog(
            "Remove this backup?",
            $"'{record.Name}' will be moved to the local backup trash folder so it can still be recovered.\r\n\r\n" +
            "Save files in the game folder are not touched.",
            "Move to trash");

        if (confirm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            await Task.Run(() => _services.Backups.MoveToTrash(record.Id));
            _services.History.Add(new HistoryEntry
            {
                Action = HistoryAction.Delete,
                Status = HistoryStatus.Success,
                Summary = $"Backup '{record.Name}' moved to trash",
                BackupId = record.Id,
                Bytes = record.TotalBytes
            });
            await ReloadBackupsAsync();
            SetStatus("Backup moved to the local trash folder.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not remove the backup:\r\n\r\n" + ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public async Task ExportBackupsAsync(IReadOnlyList<BackupRecord> records)
    {
        if (records.Count == 0)
        {
            MessageBox.Show(this, "Select at least one backup first.", Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var save = new SaveFileDialog
        {
            Title = "Export backups",
            Filter = "Backup archive (*.zip)|*.zip",
            FileName = $"FamilyTime-backups-{DateTime.Now:yyyyMMdd-HHmm}.zip",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            OverwritePrompt = false
        };

        if (save.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var overwrite = false;
        if (File.Exists(save.FileName))
        {
            using var confirm = new ConfirmDialog(
                "Replace the existing archive?",
                "An archive already exists at that path. Replacing it keeps only the new export.",
                "Replace", dangerous: true);
            if (confirm.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            overwrite = true;
        }

        SetBusy(true, "Writing and verifying the archive…");
        try
        {
            var result = await Task.Run(() => _services.ArchiveService.Export(records, save.FileName, overwrite));
            await ReloadBackupsAsync();
            MessageBox.Show(this,
                $"Exported {result.BackupCount} backup(s).\r\n\r\n{result.ArchivePath}\r\n{result.ArchiveBytes:N0} bytes",
                Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "The export failed:\r\n\r\n" + ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            SetBusy(false);
            SetStatus("Ready.");
        }
    }

    public async Task ImportArchiveAsync()
    {
        using var open = new OpenFileDialog
        {
            Title = "Import backup archive",
            Filter = "Backup archive (*.zip)|*.zip",
            CheckFileExists = true
        };

        if (open.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        SetBusy(true, "Validating archive contents…");
        try
        {
            var result = await Task.Run(() => _services.ArchiveService.Import(open.FileName));
            await ReloadBackupsAsync();
            MessageBox.Show(this, result.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this,
                "The archive was rejected:\r\n\r\n" + ex.Message,
                Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            SetBusy(false);
            SetStatus("Ready.");
        }
    }

    public void ShowCompare(SaveFileInfo left, SaveFileInfo right)
    {
        var comparison = SaveComparer.Compare(left, right);
        using var dialog = new CompareDialog(comparison);
        dialog.ShowDialog(this);
    }

    public void OpenFolder(string path)
    {
        try
        {
            var target = path;
            if (!Directory.Exists(target) && !File.Exists(target))
            {
                target = Path.GetDirectoryName(path) ?? path;
            }

            if (!Directory.Exists(target) && !File.Exists(target))
            {
                MessageBox.Show(this, "That folder does not exist:\r\n\r\n" + path, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{target}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not open the folder:\r\n\r\n" + ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        DarkTitleBar.Apply(this);
    }
}
