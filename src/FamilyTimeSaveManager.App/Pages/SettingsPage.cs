using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.App;

internal sealed class SettingsPage : PageBase
{
    private readonly TableLayoutPanel _stack;
    private readonly FlowLayoutPanel _installRows;
    private readonly Label _installEmpty;
    private readonly Dictionary<Episode, DarkTextBox> _savePaths = new();
    private readonly CheckBox _autoEnabled;
    private readonly NumericUpDown _interval;
    private readonly NumericUpDown _retentionCount;
    private readonly NumericUpDown _retentionDays;
    private readonly CheckBox _backupOnChange;
    private readonly CheckBox _warnGameRunning;
    private readonly CheckBox _verifyOnRestore;
    private readonly Label _dataPath;
    private bool _loading;
    private Snapshot _snapshot = Snapshot.Empty;
    private bool _built;

    public SettingsPage(MainForm main) : base(main)
    {
        var (host, stack) = CreateScrollArea();
        Controls.Add(host);
        _stack = stack;

        var installCard = new CardPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(18, 14, 18, 14) };
        var installStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Theme.Card,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        installStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 5; row++)
        {
            installStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var installTitle = Ui.SectionLabel("Installations");
        installTitle.Dock = DockStyle.Fill;
        var installHint = Small("Detected automatically and kept read-only. Assign an episode manually when detection is ambiguous.", 0, 0, 900, 32);
        installHint.Dock = DockStyle.Fill;
        installHint.Margin = new Padding(0, 4, 0, 6);

        _installRows = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Card,
            Margin = new Padding(0, 2, 0, 6)
        };
        _installRows.Resize += (_, _) =>
        {
            foreach (Control child in _installRows.Controls)
            {
                child.Width = Math.Max(520, _installRows.ClientSize.Width - 6);
            }
        };
        _installEmpty = Small("No installations yet. Add the folder that contains FamilyTime.exe.", 0, 0, 700, 24, Theme.Faint);
        _installEmpty.Dock = DockStyle.Fill;

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            BackColor = Theme.Card,
            Margin = new Padding(0, 4, 0, 0),
            WrapContents = false
        };
        var addFolder = Ui.Button("Add install folder", false, 148);
        addFolder.Click += (_, _) => AddInstallFolder();
        var rescan = Ui.Button("Rescan now", primary: true, 116);
        rescan.Click += async (_, _) => await Main.RescanAsync();
        buttons.Controls.Add(addFolder);
        buttons.Controls.Add(rescan);

        installStack.Controls.Add(installTitle, 0, 0);
        installStack.Controls.Add(installHint, 0, 1);
        installStack.Controls.Add(_installRows, 0, 2);
        installStack.Controls.Add(_installEmpty, 0, 3);
        installStack.Controls.Add(buttons, 0, 4);
        installCard.Controls.Add(installStack);
        _stack.Controls.Add(installCard, 0, 0);

        var saveCard = BuildSaveFolderCard();
        _stack.Controls.Add(saveCard, 0, 1);

        var scheduleCard = new CardPanel { Dock = DockStyle.Top, Height = 300, Margin = new Padding(0, 0, 0, 14) };
        var scheduleTitle = Ui.SectionLabel("Scheduled backups");
        scheduleTitle.Location = new Point(18, 16);

        _autoEnabled = Check("Enable scheduled backups while this app is running", 18, 50);
        _interval = Number(18, 90, 5, 1440, 30);
        var intervalLabel = Small("Minutes between checks", 86, 92, 220);
        _retentionCount = Number(300, 90, 1, 500, 20);
        var retentionLabel = Small("Scheduled backups to keep per profile", 368, 92, 300);
        _retentionDays = Number(18, 150, 0, 3650, 0);
        var daysLabel = Small("Delete scheduled backups older than this many days (0 = never)", 86, 152, 420);
        _backupOnChange = Check("Only create a scheduled backup when a save changes", 18, 196);

        var note = Small("Scheduled backups run while this window is open. Manual, pre-restore and imported backups are never pruned.", 18, 232, 820, 34, Theme.Faint);
        var runNow = Ui.Button("Run backup check now", false, 176);
        runNow.Location = new Point(18, 258);
        runNow.Click += (_, _) =>
        {
            var created = Main.Services.AutoBackup.RunOnce(manual: true);
            Main.SetStatus(created == 0
                ? "Scheduled check finished: nothing changed."
                : $"Scheduled check created {created} backup(s).");
            _ = Main.ReloadBackupsAsync();
        };

        scheduleCard.Controls.AddRange(new Control[] { scheduleTitle, _autoEnabled, _interval, intervalLabel, _retentionCount, retentionLabel, _retentionDays, daysLabel, _backupOnChange, note, runNow });
        _stack.Controls.Add(scheduleCard, 0, 2);

        var safetyCard = new CardPanel { Dock = DockStyle.Top, Height = 186, Margin = new Padding(0, 0, 0, 14) };
        var safetyTitle = Ui.SectionLabel("Safety");
        safetyTitle.Location = new Point(18, 16);
        _warnGameRunning = Check("Warn and block restores while Family Time is running", 18, 50);
        _verifyOnRestore = Check("Verify backup checksums before and after every restore", 18, 84);
        var safetyNote = Small("A recovery backup is always created before a restore overwrites a save. Archive imports reject parent traversal, absolute paths and hash mismatches.", 18, 122, 820, 48, Theme.Faint);
        safetyCard.Controls.AddRange(new Control[] { safetyTitle, _warnGameRunning, _verifyOnRestore, safetyNote });
        _stack.Controls.Add(safetyCard, 0, 3);

        var dataCard = new CardPanel { Dock = DockStyle.Top, Height = 210, Margin = new Padding(0, 0, 0, 14) };
        var dataTitle = Ui.SectionLabel("Data, logs and privacy");
        dataTitle.Location = new Point(18, 16);
        _dataPath = new Label
        {
            Text = Main.Services.Paths.DataRoot,
            Font = Theme.Mono,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Height = 44,
            Width = 820,
            Location = new Point(18, 48),
            BackColor = Color.Transparent
        };
        var privacy = Small("Backups, history and logs live only in this folder. The app makes no network connections and never uploads saves.", 18, 96, 820, 40, Theme.Faint);

        var openData = Ui.Button("Open data folder", false, 150);
        openData.Location = new Point(18, 146);
        openData.Click += (_, _) => Main.OpenFolder(Main.Services.Paths.DataRoot);

        var openLogs = Ui.Button("Open logs", false, 110);
        openLogs.Location = new Point(176, 146);
        openLogs.Click += (_, _) => Main.OpenFolder(Main.Services.Paths.LogsRoot);

        dataCard.Controls.AddRange(new Control[] { dataTitle, _dataPath, privacy, openData, openLogs });
        _stack.Controls.Add(dataCard, 0, 4);

        var footer = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 58, BackColor = Theme.Window, Padding = new Padding(0, 6, 0, 0) };
        var save = Ui.Button("Save settings", primary: true, 140);
        save.Click += async (_, _) => await SaveSettingsAsync();
        var reload = Ui.Button("Discard changes", false, 130);
        reload.Click += (_, _) =>
        {
            LoadFromSettings();
            Main.SetStatus("Settings reloaded from disk.");
        };
        footer.Controls.Add(save);
        footer.Controls.Add(reload);
        _stack.Controls.Add(footer, 0, 5);
    }

    public override string Key => "settings";

    public override string Title => "Settings";

    public override string Subtitle => "Folders, schedule, retention and safety guards.";

    public override void Apply(Snapshot snapshot)
    {
        _snapshot = snapshot;
        RebuildInstalls();
        if (!_built)
        {
            LoadFromSettings();
            _built = true;
        }
    }

    private Control _installHintSpacer()
    {
        return new Panel { Height = 66, Dock = DockStyle.Top, BackColor = Theme.Card };
    }

    private CardPanel BuildSaveFolderCard()
    {
        var card = new CardPanel { Dock = DockStyle.Top, Height = 232, Margin = new Padding(0, 0, 0, 14) };
        var title = Ui.SectionLabel("Save folders");
        title.Location = new Point(18, 16);
        var hint = Small("Leave a field empty to use automatic detection. Episode 3 stores saves inside the install; Episodes 1 and 2 share the LocalLow folder.", 18, 42, 880, 34);
        card.Controls.Add(title);
        card.Controls.Add(hint);

        var y = 84;
        foreach (var episode in new[] { Episode.Episode1, Episode.Episode2, Episode.Episode3 })
        {
            var label = Ui.FieldLabel(episode.ToDisplayName());
            label.Location = new Point(18, y);

            var box = new DarkTextBox { Location = new Point(18, y + 20), Width = 560 };
            box.Placeholder = "Automatic";
            _savePaths[episode] = box;

            var browse = Ui.Button("Browse", false, 84);
            browse.Location = new Point(588, y + 20);
            var browseEpisode = episode;
            browse.Click += (_, _) => BrowseSaveFolder(browseEpisode);

            var clear = Ui.Button("Clear", false, 70);
            clear.Location = new Point(680, y + 20);
            clear.Click += (_, _) => _savePaths[browseEpisode].Text = string.Empty;

            card.Controls.Add(label);
            card.Controls.Add(box);
            card.Controls.Add(browse);
            card.Controls.Add(clear);

            y += 50;
        }

        return card;
    }

    private CheckBox Check(string text, int x, int y) => new()
    {
        Text = text,
        Font = Theme.Body,
        ForeColor = Theme.Text,
        BackColor = Color.Transparent,
        AutoSize = false,
        Width = 760,
        Height = 26,
        Location = new Point(x, y),
        FlatStyle = FlatStyle.Flat
    };

    private static NumericUpDown Number(int x, int y, int min, int max, int value) => new()
    {
        Location = new Point(x, y),
        Width = 62,
        Minimum = min,
        Maximum = max,
        Value = value,
        BackColor = Theme.CardAlt,
        ForeColor = Theme.Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = Theme.Body,
        TextAlign = HorizontalAlignment.Right
    };

    private void RebuildInstalls()
    {
        _installRows.Controls.Clear();

        foreach (var install in _snapshot.Installs)
        {
            var row = new CardPanel
            {
                FillColor = Theme.Surface,
                Height = 86,
                Margin = new Padding(0, 0, 0, 8),
                Padding = new Padding(14)
            };

            var name = new Label
            {
                Text = install.DisplayName,
                Font = Theme.H3,
                ForeColor = Theme.Text,
                AutoSize = true,
                Location = new Point(14, 10),
                BackColor = Color.Transparent
            };
            var path = new Label
            {
                Text = install.InstallPath,
                Font = Theme.Mono,
                ForeColor = Theme.Muted,
                AutoSize = false,
                Height = 20,
                Width = 520,
                Location = new Point(14, 34),
                BackColor = Color.Transparent,
                AutoEllipsis = true
            };

            var episode = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.CardAlt,
                ForeColor = Theme.Text,
                Font = Theme.BodySmall,
                Width = 124,
                Location = new Point(14, 58)
            };
            episode.Items.AddRange(new object[] { "Auto episode", "Episode 1", "Episode 2", "Episode 3" });
            episode.SelectedIndex = install.Episode switch
            {
                Episode.Episode1 => 1,
                Episode.Episode2 => 2,
                Episode.Episode3 => 3,
                _ => 0
            };

            var renderer = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                FlatStyle = FlatStyle.Flat,
                BackColor = Theme.CardAlt,
                ForeColor = Theme.Text,
                Font = Theme.BodySmall,
                Width = 116,
                Location = new Point(146, 58)
            };
            renderer.Items.AddRange(new object[] { "Auto renderer", "Direct3D 11", "Direct3D 12" });
            renderer.SelectedIndex = install.GraphicsApi switch
            {
                GraphicsApi.Dx11 => 1,
                GraphicsApi.Dx12 => 2,
                _ => 0
            };

            var installPath = install.InstallPath;
            var apply = Ui.Button("Apply", false, 70);
            apply.Height = 28;
            apply.Location = new Point(270, 57);
            apply.Click += (_, _) =>
            {
                Main.Services.Settings.Update(settings =>
                {
                    if (episode.SelectedIndex == 0)
                    {
                        settings.InstallEpisodeOverrides.Remove(installPath);
                    }
                    else
                    {
                        settings.InstallEpisodeOverrides[installPath] = ((Episode)episode.SelectedIndex).ToString();
                    }

                    if (renderer.SelectedIndex == 0)
                    {
                        settings.InstallGraphicsOverrides.Remove(installPath);
                    }
                    else
                    {
                        settings.InstallGraphicsOverrides[installPath] = ((GraphicsApi)(renderer.SelectedIndex == 1 ? 11 : 12)).ToString();
                    }
                });

                Main.SetStatus("Install override saved. Rescanning…");
                _ = Main.RescanAsync();
            };

            var open = Ui.Button("Open", false, 64);
            open.Height = 28;
            open.Location = new Point(348, 57);
            open.Click += (_, _) => Main.OpenFolder(installPath);

            row.Controls.Add(name);
            row.Controls.Add(path);
            row.Controls.Add(episode);
            row.Controls.Add(renderer);
            row.Controls.Add(apply);
            row.Controls.Add(open);

            _installRows.Controls.Add(row);
        }

        _installEmpty.Visible = _snapshot.Installs.Count == 0;
    }

    private void LoadFromSettings()
    {
        _loading = true;
        var settings = Main.Services.Settings.Current;

        foreach (var episode in _savePaths.Keys)
        {
            _savePaths[episode].Text = settings.GetSavePathOverride(episode) ?? string.Empty;
        }

        _autoEnabled.Checked = settings.AutoBackupEnabled;
        _interval.Value = Math.Clamp(settings.AutoBackupIntervalMinutes, (int)_interval.Minimum, (int)_interval.Maximum);
        _retentionCount.Value = Math.Clamp(settings.RetentionAutoBackups, (int)_retentionCount.Minimum, (int)_retentionCount.Maximum);
        _retentionDays.Value = Math.Clamp(settings.RetentionDays, (int)_retentionDays.Minimum, (int)_retentionDays.Maximum);
        _backupOnChange.Checked = settings.AutoBackupOnSaveChange;
        _warnGameRunning.Checked = settings.WarnWhenGameRunning;
        _verifyOnRestore.Checked = settings.VerifyChecksumsOnRestore;
        _dataPath.Text = Main.Services.Paths.DataRoot;
        _loading = false;
    }

    private async Task SaveSettingsAsync()
    {
        if (_loading)
        {
            return;
        }

        Main.Services.Settings.Update(settings =>
        {
            foreach (var episode in _savePaths.Keys)
            {
                var value = _savePaths[episode].Text.Trim();
                settings.SetSavePathOverride(episode, value.Length == 0 ? null : value);
            }

            settings.AutoBackupEnabled = _autoEnabled.Checked;
            settings.AutoBackupIntervalMinutes = (int)_interval.Value;
            settings.RetentionAutoBackups = (int)_retentionCount.Value;
            settings.RetentionDays = (int)_retentionDays.Value;
            settings.AutoBackupOnSaveChange = _backupOnChange.Checked;
            settings.WarnWhenGameRunning = _warnGameRunning.Checked;
            settings.VerifyChecksumsOnRestore = _verifyOnRestore.Checked;

            Main.Services.History.Add(new HistoryEntry
            {
                Action = HistoryAction.Settings,
                Status = HistoryStatus.Success,
                Summary = "Settings saved",
                Detail = $"Scheduled backups {(settings.AutoBackupEnabled ? "on" : "off")}, every {settings.AutoBackupIntervalMinutes} minute(s), keeping {settings.RetentionAutoBackups}."
            });
        });

        Main.Services.AutoBackup.Start();
        Main.SetStatus("Settings saved.", Theme.Good);
        await Main.RescanAsync();
    }

    private void AddInstallFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Choose the folder that contains FamilyTime.exe",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        Main.Services.Settings.Update(settings =>
        {
            if (!settings.ExtraInstallPaths.Contains(dialog.SelectedPath, StringComparer.OrdinalIgnoreCase))
            {
                settings.ExtraInstallPaths.Add(dialog.SelectedPath);
            }
        });

        Main.SetStatus("Folder added. Rescanning…");
        _ = Main.RescanAsync();
    }

    private void BrowseSaveFolder(Episode episode)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = $"Choose the save folder for {episode.ToDisplayName()}",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _savePaths[episode].Text = dialog.SelectedPath;
        }
    }
}
