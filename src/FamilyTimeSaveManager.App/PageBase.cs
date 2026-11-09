using FamilyTimeSaveManager.Core.Detection;
using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.App;

internal sealed record Snapshot(
    IReadOnlyList<GameInstall> Installs,
    IReadOnlyList<SaveLocation> Locations,
    IReadOnlyList<SaveFileInfo> Saves,
    IReadOnlyList<BackupRecord> Backups,
    IReadOnlyList<Profile> Profiles)
{
    public static Snapshot Empty { get; } = new(
        Array.Empty<GameInstall>(),
        Array.Empty<SaveLocation>(),
        Array.Empty<SaveFileInfo>(),
        Array.Empty<BackupRecord>(),
        Array.Empty<Profile>());
}

internal abstract class PageBase : UserControl
{
    protected PageBase(MainForm main)
    {
        Main = main;
        Dock = DockStyle.Fill;
        BackColor = Theme.Window;
        DoubleBuffered = true;
        Margin = new Padding(0);
    }

    protected MainForm Main { get; }

    public abstract string Key { get; }

    public abstract string Title { get; }

    public abstract string Subtitle { get; }

    public virtual void Apply(Snapshot snapshot)
    {
    }

    /// <summary>Selects representative data for documentation screenshots.</summary>
    public virtual void PrepareScreenshot()
    {
    }

    protected static (Panel Host, TableLayoutPanel Stack) CreateScrollArea()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Theme.Window
        };

        var stack = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Theme.Window,
            Padding = new Padding(0, 0, 0, 8)
        };
        stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        host.Controls.Add(stack);
        host.Resize += (_, _) =>
        {
            var width = host.ClientSize.Width - (host.VerticalScroll.Visible ? 20 : 4);
            if (width > 320)
            {
                stack.Width = width;
            }
        };

        return (host, stack);
    }

    protected static CardPanel Card(string? title = null, string? subtitle = null)
    {
        var card = new CardPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };

        if (title is not null)
        {
            var heading = Ui.SectionLabel(title);
            heading.Location = new Point(18, 14);
            card.Controls.Add(heading);
        }

        if (subtitle is not null)
        {
            var sub = new Label
            {
                Text = subtitle,
                Font = Theme.BodySmall,
                ForeColor = Theme.Muted,
                AutoSize = false,
                Location = new Point(18, 40),
                Width = 600,
                Height = 34,
                BackColor = Color.Transparent
            };
            card.Controls.Add(sub);
        }

        return card;
    }

    protected static Label Body(string text, int x, int y, int width, int height = 20, Color? color = null)
    {
        return new Label
        {
            Text = text,
            Font = Theme.Body,
            ForeColor = color ?? Theme.Text,
            AutoSize = false,
            Location = new Point(x, y),
            Width = width,
            Height = height,
            BackColor = Color.Transparent
        };
    }

    protected static Label Small(string text, int x, int y, int width, int height = 18, Color? color = null)
    {
        return new Label
        {
            Text = text,
            Font = Theme.BodySmall,
            ForeColor = color ?? Theme.Muted,
            AutoSize = false,
            Location = new Point(x, y),
            Width = width,
            Height = height,
            BackColor = Color.Transparent
        };
    }
}
