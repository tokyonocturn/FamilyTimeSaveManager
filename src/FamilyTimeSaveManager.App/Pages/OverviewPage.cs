using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.App;

internal sealed class OverviewPage : PageBase
{
    private readonly StatCard _installCard;
    private readonly StatCard _saveCard;
    private readonly StatCard _backupCard;
    private readonly StatCard _issueCard;
    private readonly FlowLayoutPanel _installList;
    private readonly FlowLayoutPanel _warningList;
    private readonly Label _installEmpty;
    private Snapshot _snapshot = Snapshot.Empty;

    public OverviewPage(MainForm main) : base(main)
    {
        var (host, stack) = CreateScrollArea();
        Controls.Add(host);

        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            RowCount = 1,
            Height = 112,
            BackColor = Theme.Window,
            Margin = new Padding(0, 0, 0, 14),
            Padding = new Padding(0)
        };
        for (var index = 0; index < 4; index++)
        {
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        }

        _installCard = new StatCard("\uE7F8", "Installations") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
        _saveCard = new StatCard("\uE74E", "Saves found") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
        _backupCard = new StatCard("\uE8F1", "Backups") { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0) };
        _issueCard = new StatCard("\uE7BA", "Need attention") { Dock = DockStyle.Fill, Margin = new Padding(0) };

        stats.Controls.Add(_installCard, 0, 0);
        stats.Controls.Add(_saveCard, 1, 0);
        stats.Controls.Add(_backupCard, 2, 0);
        stats.Controls.Add(_issueCard, 3, 0);
        stack.Controls.Add(stats, 0, 0);

        var installCard = new CardPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(18, 14, 18, 14) };
        var installStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Card,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        installStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 4; row++)
        {
            installStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        var installTitle = Ui.SectionLabel("Detected installations");
        installTitle.Dock = DockStyle.Fill;
        var installHint = Small("Family Time is detected from FamilyTime.exe and FamilyTime_Data. Nothing in the install is modified.", 0, 0, 900, 32);
        installHint.Dock = DockStyle.Fill;
        installHint.Margin = new Padding(0, 4, 0, 6);

        _installList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Card,
            Padding = new Padding(0, 2, 0, 0),
            Margin = new Padding(0)
        };
        _installList.Resize += (_, _) =>
        {
            foreach (Control child in _installList.Controls)
            {
                child.Width = Math.Max(240, _installList.ClientSize.Width - 10);
            }
        };

        _installEmpty = Small("No installation detected yet. Add the folder that contains FamilyTime.exe in Settings.", 0, 0, 760, 24, Theme.Faint);
        _installEmpty.Dock = DockStyle.Fill;

        installStack.Controls.Add(installTitle, 0, 0);
        installStack.Controls.Add(installHint, 0, 1);
        installStack.Controls.Add(_installEmpty, 0, 2);
        installStack.Controls.Add(_installList, 0, 3);
        installCard.Controls.Add(installStack);
        stack.Controls.Add(installCard, 0, 1);

        var warningCard = new CardPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(18, 14, 18, 14) };
        var warningStack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.Card,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        warningStack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        warningStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        warningStack.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var warningTitle = Ui.SectionLabel("Things to check");
        warningTitle.Dock = DockStyle.Fill;
        _warningList = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = Theme.Card,
            Padding = new Padding(0, 6, 0, 0),
            Margin = new Padding(0)
        };
        _warningList.Resize += (_, _) =>
        {
            foreach (Control child in _warningList.Controls)
            {
                child.Width = Math.Max(240, _warningList.ClientSize.Width - 20);
            }
        };
        warningStack.Controls.Add(warningTitle, 0, 0);
        warningStack.Controls.Add(_warningList, 0, 1);
        warningCard.Controls.Add(warningStack);
        stack.Controls.Add(warningCard, 0, 2);

        var actions = new CardPanel { Dock = DockStyle.Top, Height = 104, Margin = new Padding(0) };
        var actionTitle = Ui.SectionLabel("Quick actions");
        actionTitle.Location = new Point(18, 14);

        var actionFlow = new FlowLayoutPanel
        {
            Location = new Point(14, 48),
            AutoSize = true,
            BackColor = Theme.Card,
            WrapContents = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var backup = Ui.Button("Back up a save", primary: true, width: 132);
        backup.Click += (_, _) => Main.NavigateTo("saves");

        var library = Ui.Button("Open backup library", false, 156);
        library.Click += (_, _) => Main.NavigateTo("library");

        var import = Ui.Button("Import archive", false, 126);
        import.Click += async (_, _) => await Main.ImportArchiveAsync();

        var rescan = Ui.Button("Rescan", false, 96);
        rescan.Click += async (_, _) => await Main.RescanAsync();

        actionFlow.Controls.AddRange(new Control[] { backup, library, import, rescan });
        actions.Controls.Add(actionTitle);
        actions.Controls.Add(actionFlow);
        stack.Controls.Add(actions, 0, 3);
    }

    public override string Key => "overview";

    public override string Title => "Overview";

    public override string Subtitle => "Detected installations, saves and protection status.";

    public override void Apply(Snapshot snapshot)
    {
        _snapshot = snapshot;

        var issues = snapshot.Saves.Count(s => s.Status != SaveStatus.Valid)
                     + snapshot.Locations.Count(l => l.Episode == Episode.Unknown)
                     + snapshot.Installs.Count(i => i.Episode == Episode.Unknown);

        _installCard.SetData(snapshot.Installs.Count.ToString(), snapshot.Installs.Count == 1 ? "installation" : "installations");
        _saveCard.SetData(snapshot.Saves.Count.ToString(), snapshot.Saves.Count == 1 ? "save file" : "save files");
        _backupCard.SetData(snapshot.Backups.Count.ToString(), $"{FormatBytes(snapshot.Backups.Sum(b => b.TotalBytes))} stored");
        _issueCard.SetData(issues.ToString(), issues == 0 ? "all clear" : "review the list below", issues == 0 ? Theme.Good : Theme.Warn);

        _installList.Controls.Clear();
        foreach (var install in snapshot.Installs)
        {
            _installList.Controls.Add(BuildInstallRow(install));
        }

        _installEmpty.Visible = snapshot.Installs.Count == 0;
        _installList.Visible = snapshot.Installs.Count > 0;

        _warningList.Controls.Clear();
        if (issues == 0)
        {
            _warningList.Controls.Add(BuildNote("No problems detected. Saves parse cleanly and every detected folder is attributed.", Theme.Good));
        }
        else
        {
            foreach (var install in snapshot.Installs.Where(i => i.Episode == Episode.Unknown))
            {
                _warningList.Controls.Add(BuildNote($"Installation '{install.FolderName}' could not be identified. Assign its episode in Settings.", Theme.Warn));
            }

            foreach (var location in snapshot.Locations.Where(l => l.Episode == Episode.Unknown))
            {
                _warningList.Controls.Add(BuildNote($"Save folder '{location.Path}' is shared by Episodes 1 and 2, so saved files there cannot be attributed to one episode.", Theme.Warn));
            }

            foreach (var save in snapshot.Saves.Where(s => s.Status != SaveStatus.Valid))
            {
                _warningList.Controls.Add(BuildNote(
                    $"{save.FileName}: {save.StatusDisplay}. {save.StatusMessage}".Trim(),
                    Theme.StatusColor(save.Status)));
            }
        }

        var firstFolder = snapshot.Saves.FirstOrDefault()?.Folder ?? snapshot.Locations.FirstOrDefault(l => l.Exists)?.Path;
        _ = firstFolder;
    }

    private Control BuildInstallRow(GameInstall install)
    {
        var row = new CardPanel
        {
            FillColor = Theme.Surface,
            Height = 96,
            Margin = new Padding(0, 0, 0, 8),
            Padding = new Padding(16)
        };

        var title = new Label
        {
            Text = install.DisplayName,
            Font = Theme.H3,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(16, 12),
            BackColor = Color.Transparent
        };

        var path = new Label
        {
            Text = install.InstallPath,
            Font = Theme.Mono,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Height = 20,
            Location = new Point(16, 38),
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        var meta = new Label
        {
            Text = BuildMeta(install),
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = false,
            Height = 20,
            Location = new Point(16, 60),
            BackColor = Color.Transparent,
            AutoEllipsis = true
        };

        var open = Ui.Button("Open folder", false, 104);
        open.Height = 30;
        open.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        open.Location = new Point(Math.Max(0, row.Width - 122), 14);
        open.Click += (_, _) => Main.OpenFolder(install.InstallPath);

        row.Controls.Add(title);
        row.Controls.Add(path);
        row.Controls.Add(meta);
        row.Controls.Add(open);
        row.Resize += (_, _) =>
        {
            open.Location = new Point(Math.Max(0, row.ClientSize.Width - open.Width - 14), 14);
            path.Width = Math.Max(120, row.ClientSize.Width - 160);
            meta.Width = Math.Max(120, row.ClientSize.Width - 32);
        };

        return row;
    }

    private static string BuildMeta(GameInstall install)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(install.UnityVersion))
        {
            parts.Add($"Unity {install.UnityVersion}");
        }

        if (!string.IsNullOrWhiteSpace(install.CompanyName) || !string.IsNullOrWhiteSpace(install.ProductName))
        {
            parts.Add($"{install.CompanyName ?? "?"} / {install.ProductName ?? "?"}");
        }

        if (install.SessionsFolder is not null)
        {
            parts.Add("Sessions inside install");
        }
        else if (install.Episode is Episode.Episode1 or Episode.Episode2)
        {
            parts.Add("saves in LocalLow");
        }

        if (install.EpisodeIsGuessed)
        {
            parts.Add("episode inferred");
        }

        return string.Join("  ·  ", parts);
    }

    private static Control BuildNote(string text, Color color)
    {
        var label = new Label
        {
            Text = "●  " + text,
            Font = Theme.Body,
            ForeColor = color,
            AutoSize = false,
            Height = 40,
            Margin = new Padding(0, 0, 0, 4),
            BackColor = Color.Transparent
        };

        return label;
    }

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.0} MB"
    };
}
