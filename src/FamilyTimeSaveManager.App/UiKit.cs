using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace FamilyTimeSaveManager.App;

internal static class Theme
{
    public static readonly Color Window = FromHex("#0D1117");
    public static readonly Color Surface = FromHex("#131820");
    public static readonly Color Sidebar = FromHex("#0F141C");
    public static readonly Color Card = FromHex("#19202B");
    public static readonly Color CardAlt = FromHex("#212A38");
    public static readonly Color Border = FromHex("#2A3444");
    public static readonly Color Accent = FromHex("#5B8DFF");
    public static readonly Color AccentHover = FromHex("#7BA6FF");
    public static readonly Color AccentSoft = FromHex("#26344F");
    public static readonly Color Text = FromHex("#E9EEF7");
    public static readonly Color Muted = FromHex("#96A3B7");
    public static readonly Color Faint = FromHex("#6D7889");
    public static readonly Color Good = FromHex("#4ADE80");
    public static readonly Color Warn = FromHex("#FBBF24");
    public static readonly Color Bad = FromHex("#F87171");

    public static readonly Font Body = new("Segoe UI", 9.75f);
    public static readonly Font BodySmall = new("Segoe UI", 8.75f);
    public static readonly Font Bold = new("Segoe UI Semibold", 9.75f);
    public static readonly Font H1 = new("Segoe UI Semibold", 19f);
    public static readonly Font H2 = new("Segoe UI Semibold", 13f);
    public static readonly Font H3 = new("Segoe UI Semibold", 10.5f);
    public static readonly Font Mono = new("Cascadia Mono", 8.75f);
    public static readonly Font Icon = new("Segoe MDL2 Assets", 11.5f);
    public static readonly Font IconLarge = new("Segoe MDL2 Assets", 17f);

    public static Color FromHex(string hex) => ColorTranslator.FromHtml(hex);

    public static GraphicsPath Round(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (radius <= 0)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static Color StatusColor(Core.Models.SaveStatus status) => status switch
    {
        Core.Models.SaveStatus.Valid => Good,
        Core.Models.SaveStatus.Suspicious => Warn,
        Core.Models.SaveStatus.Corrupt => Bad,
        Core.Models.SaveStatus.Unreadable => Bad,
        Core.Models.SaveStatus.Unsupported => Warn,
        Core.Models.SaveStatus.Empty => Warn,
        _ => Muted
    };
}

internal sealed class RoundedButton : Button
{
    private bool _hover;

    public RoundedButton()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Color.Transparent;
        ForeColor = Theme.Text;
        Font = Theme.Bold;
        Height = 34;
        Cursor = Cursors.Hand;
        Padding = new Padding(14, 0, 14, 0);
    }

    public Color FillColor { get; set; } = Theme.Accent;
    public Color HoverColor { get; set; } = Theme.AccentHover;
    public Color BorderColor { get; set; } = Color.Transparent;
    public int Radius { get; set; } = 8;
    public bool Quiet { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(bounds, Radius);

        var fill = !Enabled
            ? Theme.CardAlt
            : Quiet
                ? (_hover ? Theme.CardAlt : Theme.Card)
                : (_hover ? HoverColor : FillColor);

        using var brush = new SolidBrush(fill);
        graphics.FillPath(brush, path);

        if (BorderColor != Color.Transparent)
        {
            using var pen = new Pen(BorderColor);
            graphics.DrawPath(pen, path);
        }

        var textColor = Enabled ? ForeColor : Theme.Faint;
        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            bounds,
            textColor,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (Focused && Enabled)
        {
            using var pen = new Pen(Color.FromArgb(120, Theme.Accent));
            graphics.DrawPath(pen, Theme.Round(new Rectangle(2, 2, Width - 5, Height - 5), Math.Max(2, Radius - 2)));
        }
    }
}

internal sealed class NavButton : Button
{
    private bool _hover;

    public NavButton(string glyph, string label)
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        Glyph = glyph;
        Text = label;
        Height = 44;
        Dock = DockStyle.Top;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Sidebar;
        ForeColor = Theme.Muted;
        Font = Theme.Body;
        Cursor = Cursors.Hand;
        TextAlign = ContentAlignment.MiddleLeft;
        Padding = new Padding(48, 0, 8, 0);
    }

    public string Glyph { get; }

    public bool Selected { get; set; }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width, Height);

        if (Selected || _hover)
        {
            using var brush = new SolidBrush(Selected ? Theme.Card : Theme.Surface);
            graphics.FillRectangle(brush, new Rectangle(10, 3, Width - 20, Height - 6));
        }

        if (Selected)
        {
            using var accent = new SolidBrush(Theme.Accent);
            graphics.FillRectangle(accent, new Rectangle(10, 10, 3, Height - 20));
        }

        var glyphColor = Selected ? Theme.Accent : Theme.Faint;
        TextRenderer.DrawText(
            graphics,
            Glyph,
            Theme.Icon,
            new Rectangle(22, 0, 26, Height),
            glyphColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter);

        TextRenderer.DrawText(
            graphics,
            Text,
            Font,
            new Rectangle(46, 0, Width - 56, Height),
            Selected ? Theme.Text : Theme.Muted,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
    }
}

internal class CardPanel : Panel
{
    public CardPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Window;
        Padding = new Padding(16);
    }

    public Color FillColor { get; set; } = Theme.Card;

    public Color BorderColor { get; set; } = Theme.Border;

    public int Radius { get; set; } = 12;

    protected override void OnPaint(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.Round(bounds, Radius);
        using var brush = new SolidBrush(FillColor);
        graphics.FillPath(brush, path);
        using var pen = new Pen(BorderColor);
        graphics.DrawPath(pen, path);
        base.OnPaint(e);
    }
}

internal sealed class StatCard : CardPanel
{
    private readonly Label _value;
    private readonly Label _caption;
    private readonly Label _hint;
    private readonly Label _icon;

    public StatCard(string icon, string caption)
    {
        Padding = new Padding(18, 14, 18, 14);
        Height = 108;

        _icon = new Label
        {
            Text = icon,
            Font = Theme.IconLarge,
            ForeColor = Theme.Accent,
            AutoSize = true,
            Location = new Point(16, 14),
            BackColor = Color.Transparent
        };

        _caption = new Label
        {
            Text = caption,
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(18, 44),
            BackColor = Color.Transparent
        };

        _value = new Label
        {
            Text = "0",
            Font = new Font("Segoe UI Semibold", 22f),
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(16, 60),
            BackColor = Color.Transparent
        };

        _hint = new Label
        {
            Text = string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = true,
            Location = new Point(18, 140),
            BackColor = Color.Transparent
        };

        Controls.Add(_icon);
        Controls.Add(_caption);
        Controls.Add(_value);
    }

    public void SetData(string value, string hint, Color? accent = null)
    {
        _value.Text = value;
        _hint.Text = hint;
        _icon.ForeColor = accent ?? Theme.Accent;
        Invalidate();
    }
}

internal sealed class DarkTextBox : Panel
{
    private readonly TextBox _inner;
    private string _placeholder = string.Empty;

    public DarkTextBox(bool multiline = false, bool password = false)
    {
        BackColor = Theme.CardAlt;
        Padding = new Padding(10, 7, 10, 7);
        Height = multiline ? 80 : 36;

        _inner = new TextBox
        {
            BorderStyle = BorderStyle.None,
            BackColor = Theme.CardAlt,
            ForeColor = Theme.Text,
            Font = Theme.Body,
            Dock = DockStyle.Fill,
            Multiline = multiline,
            UseSystemPasswordChar = password,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
        };

        _inner.TextChanged += (_, _) => OnTextChanged(EventArgs.Empty);
        Controls.Add(_inner);
    }

    public TextBox Inner => _inner;

    public string Placeholder
    {
        get => _placeholder;
        set
        {
            _placeholder = value ?? string.Empty;
            _inner.PlaceholderText = _placeholder;
        }
    }

    public override string Text
    {
        get => _inner.Text;
        set => _inner.Text = value;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Invalidate();
    }
}

internal static class DarkTitleBar
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void Apply(Form form)
    {
        try
        {
            var enabled = 1;
            DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref enabled, sizeof(int));
        }
        catch (DllNotFoundException)
        {
        }
        catch (EntryPointNotFoundException)
        {
        }
    }
}

internal static class Grids
{
    public static void Style(DataGridView grid)
    {
        grid.BackgroundColor = Theme.Window;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Surface,
            ForeColor = Theme.Muted,
            Font = Theme.BodySmall,
            SelectionBackColor = Theme.Surface,
            SelectionForeColor = Theme.Muted,
            Padding = new Padding(10, 0, 0, 0),
            Alignment = DataGridViewContentAlignment.MiddleLeft
        };
        grid.ColumnHeadersHeight = 36;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.Window,
            ForeColor = Theme.Text,
            SelectionBackColor = Theme.AccentSoft,
            SelectionForeColor = Theme.Text,
            Font = Theme.Body,
            Padding = new Padding(10, 0, 0, 0)
        };
        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Theme.FromHex("#11161F")
        };
        grid.RowTemplate.Height = 36;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.AllowUserToOrderColumns = false;
        grid.ReadOnly = true;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowHeadersVisible = false;
        grid.GridColor = Theme.FromHex("#1E2631");
        grid.Dock = DockStyle.Fill;
        grid.Margin = new Padding(0);
    }
}

internal sealed class BusyBar : Control
{
    private readonly System.Windows.Forms.Timer _timer;
    private int _offset;

    public BusyBar()
    {
        Height = 4;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        _timer = new System.Windows.Forms.Timer { Interval = 28 };
        _timer.Tick += (_, _) =>
        {
            _offset = (_offset + 8) % Math.Max(1, Width + 120);
            Invalidate();
        };
    }

    public bool Running
    {
        get => _timer.Enabled;
        set
        {
            _timer.Enabled = value;
            Visible = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(Theme.Surface);
        using var brush = new SolidBrush(Theme.Accent);
        e.Graphics.FillRectangle(brush, _offset - 120, 0, 120, Height);
    }
}

internal static class Ui
{
    public static Label Heading(string text) => new()
    {
        Text = text,
        Font = Theme.H1,
        ForeColor = Theme.Text,
        AutoSize = true,
        BackColor = Color.Transparent
    };

    public static Label Sub(string text) => new()
    {
        Text = text,
        Font = Theme.Body,
        ForeColor = Theme.Muted,
        AutoSize = true,
        BackColor = Color.Transparent
    };

    public static Label SectionLabel(string text) => new()
    {
        Text = text,
        Font = Theme.H3,
        ForeColor = Theme.Text,
        AutoSize = true,
        BackColor = Color.Transparent
    };

    public static Label FieldLabel(string text) => new()
    {
        Text = text,
        Font = Theme.BodySmall,
        ForeColor = Theme.Muted,
        AutoSize = true,
        BackColor = Color.Transparent
    };

    public static Label Value(string text, Color? color = null) => new()
    {
        Text = text,
        Font = Theme.Body,
        ForeColor = color ?? Theme.Text,
        AutoSize = false,
        BackColor = Color.Transparent,
        Height = 22
    };

    public static RoundedButton Button(string text, bool primary = false, int width = 110)
    {
        var button = new RoundedButton
        {
            Text = text,
            Width = width,
            Quiet = !primary,
            FillColor = Theme.Accent,
            HoverColor = Theme.AccentHover,
            ForeColor = primary ? Color.White : Theme.Text
        };

        if (!primary)
        {
            button.ForeColor = Theme.Text;
        }

        return button;
    }

    public static DarkTextBox Search(string placeholder, int width = 260)
    {
        return new DarkTextBox { Placeholder = placeholder, Width = width };
    }

    public static string Shorten(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..Math.Max(0, max - 1)] + "…";
    }
}
