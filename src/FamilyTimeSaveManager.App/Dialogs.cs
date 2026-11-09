using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.App;

internal class DarkDialog : Form
{
    protected DarkDialog() : this(string.Empty, new Size(520, 320))
    {
    }

    public DarkDialog(string title, Size size)
    {
        Text = title;
        ClientSize = size;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Window;
        ForeColor = Theme.Text;
        Font = Theme.Body;
        Padding = new Padding(22);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        DarkTitleBar.Apply(this);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        using var pen = new Pen(Theme.Border);
        e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        base.OnPaint(e);
    }
}

internal sealed class BackupDialog : DarkDialog
{
    private readonly DarkTextBox _name;
    private readonly DarkTextBox _description;
    private readonly ComboBox _profile;

    public BackupDialog(SaveFileInfo save, IReadOnlyList<Profile> profiles, string? suggestedName = null)
    {
        var dialog = this;
        dialog.Text = "Create backup";

        var heading = new Label
        {
            Text = "Back up this save",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(22, 18),
            BackColor = Color.Transparent
        };

        var subtitle = new Label
        {
            Text = $"{save.FileName}  ·  {save.Episode.ToDisplayName()}  ·  {save.SizeDisplay}",
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(24, 48),
            BackColor = Color.Transparent
        };

        var nameLabel = Ui.FieldLabel("Backup name");
        nameLabel.Location = new Point(24, 84);

        _name = new DarkTextBox { Location = new Point(24, 104), Width = 500 };
        _name.Text = suggestedName ?? $"{save.BaseName} {DateTime.Now:yyyy-MM-dd HH-mm}";

        var descriptionLabel = Ui.FieldLabel("Description (optional)");
        descriptionLabel.Location = new Point(24, 150);

        _description = new DarkTextBox(multiline: true) { Location = new Point(24, 170), Width = 500, Height = 84 };
        _description.Placeholder = "What changed? Why are you keeping this one?";

        var profileLabel = Ui.FieldLabel("Profile");
        profileLabel.Location = new Point(24, 268);

        _profile = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            BackColor = Theme.CardAlt,
            ForeColor = Theme.Text,
            Font = Theme.Body,
            Location = new Point(24, 288),
            Width = 300
        };
        foreach (var profile in profiles)
        {
            _profile.Items.Add(new ProfileItem(profile));
        }

        _profile.SelectedIndex = 0;

        var info = new Label
        {
            Text = "The save is copied and hashed with SHA-256. Original files are not touched.",
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = false,
            Location = new Point(24, 330),
            Width = 500,
            Height = 32,
            BackColor = Color.Transparent
        };

        var cancel = Ui.Button("Cancel", primary: false, width: 100);
        cancel.Location = new Point(318, 378);
        cancel.DialogResult = DialogResult.Cancel;

        var create = Ui.Button("Create backup", primary: true, width: 120);
        create.Location = new Point(424, 378);
        create.Click += (_, _) =>
        {
            BackupName = _name.Text;
            Description = _description.Text;
            ProfileId = (_profile.SelectedItem as ProfileItem)?.Profile.Id ?? Profile.DefaultId;
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[]
        {
            heading, subtitle, nameLabel, _name, descriptionLabel, _description,
            profileLabel, _profile, info, cancel, create
        });

        AcceptButton = create;
        CancelButton = cancel;
    }

    public string BackupName { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string ProfileId { get; private set; } = Profile.DefaultId;

    private sealed record ProfileItem(Profile Profile)
    {
        public override string ToString() => Profile.Name;
    }
}

internal sealed class RestoreDialog : DarkDialog
{
    private readonly CheckBox _confirm;

    public RestoreDialog(BackupRecord record, string destination, bool gameRunning, string? warning)
    {
        Text = "Restore backup";

        var heading = new Label
        {
            Text = "Restore this backup?",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(22, 18),
            BackColor = Color.Transparent
        };

        var summary = new Label
        {
            Text = $"{record.Name}\r\n{record.KindDisplay} backup from {record.CreatedUtc.ToLocalTime():f}\r\n{record.Files.Count} file(s), {record.SizeDisplay}",
            Font = Theme.Body,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Location = new Point(24, 52),
            Width = 580,
            Height = 62,
            BackColor = Color.Transparent
        };

        var destinationLabel = Ui.FieldLabel("Files will be written to");
        destinationLabel.Location = new Point(24, 122);

        var destinationBox = new Label
        {
            Text = destination,
            Font = Theme.Mono,
            ForeColor = Theme.Text,
            AutoSize = false,
            Location = new Point(24, 142),
            Width = 580,
            Height = 40,
            BackColor = Color.Transparent
        };

        var safety = new CardPanel
        {
            Location = new Point(24, 194),
            Size = new Size(580, 92),
            FillColor = Theme.Surface,
            Padding = new Padding(14)
        };

        var safetyText = new Label
        {
            Text = "A snapshot of the current save is created first, and every restored file is verified against the backup's SHA-256.\r\nThe game must be closed. Nothing is uploaded.",
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        safety.Controls.Add(safetyText);

        var warningLabel = new Label
        {
            Text = warning ?? string.Empty,
            Font = Theme.Bold,
            ForeColor = Theme.Warn,
            AutoSize = false,
            Location = new Point(24, 296),
            Width = 580,
            Height = 44,
            BackColor = Color.Transparent,
            Visible = !string.IsNullOrWhiteSpace(warning)
        };

        var runningLabel = new Label
        {
            Text = "Family Time is running. Close the game before restoring so it cannot overwrite the file again.",
            Font = Theme.Bold,
            ForeColor = Theme.Bad,
            AutoSize = false,
            Location = new Point(24, 296),
            Width = 580,
            Height = 44,
            BackColor = Color.Transparent,
            Visible = gameRunning
        };

        if (gameRunning)
        {
            warningLabel.Visible = false;
        }

        _confirm = new CheckBox
        {
            Text = "I understand this replaces the current save and I want to continue.",
            Font = Theme.Body,
            ForeColor = Theme.Text,
            AutoSize = false,
            Location = new Point(24, 348),
            Width = 580,
            Height = 30,
            BackColor = Color.Transparent,
            FlatStyle = FlatStyle.Flat
        };

        var cancel = Ui.Button("Cancel", false, 100);
        cancel.Location = new Point(396, 392);
        cancel.DialogResult = DialogResult.Cancel;

        var restore = Ui.Button("Restore now", true, 120);
        restore.Location = new Point(504, 392);
        restore.Enabled = false;
        restore.FillColor = Theme.Bad;
        restore.HoverColor = Theme.FromHex("#FCA5A5");
        restore.ForeColor = Color.White;

        _confirm.CheckedChanged += (_, _) => restore.Enabled = _confirm.Checked && !gameRunning;
        restore.Click += (_, _) =>
        {
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[]
        {
            heading, summary, destinationLabel, destinationBox, safety, warningLabel, runningLabel, _confirm, cancel, restore
        });

        CancelButton = cancel;
    }
}

internal sealed class CompareDialog : DarkDialog
{
    public CompareDialog(Core.Services.SaveComparison comparison)
    {
        Text = "Compare saves";
        ClientSize = new Size(720, 560);

        var heading = new Label
        {
            Text = comparison.Identical ? "These saves are identical" : "Save comparison",
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(22, 18),
            BackColor = Color.Transparent
        };

        var left = new CardPanel
        {
            Location = new Point(24, 54),
            Size = new Size(330, 110),
            FillColor = Theme.Surface
        };
        left.Controls.Add(new Label
        {
            Text = $"{comparison.Left.FileName}\r\n{comparison.Left.Episode.ToDisplayName()}  ·  {comparison.Left.SizeDisplay}\r\n{comparison.Left.Metadata.FormatDisplay}\r\n{comparison.Left.Metadata.ReleaseLabelDisplay}",
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        });

        var right = new CardPanel
        {
            Location = new Point(370, 54),
            Size = new Size(326, 110),
            FillColor = Theme.Surface
        };
        right.Controls.Add(new Label
        {
            Text = $"{comparison.Right.FileName}\r\n{comparison.Right.Episode.ToDisplayName()}  ·  {comparison.Right.SizeDisplay}\r\n{comparison.Right.Metadata.FormatDisplay}\r\n{comparison.Right.Metadata.ReleaseLabelDisplay}",
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        });

        var summary = new Label
        {
            Text = comparison.Summary,
            Font = Theme.Body,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Location = new Point(24, 180),
            Width = 672,
            Height = 46,
            BackColor = Color.Transparent
        };

        var list = new ListBox
        {
            Location = new Point(24, 234),
            Size = new Size(672, 244),
            BackColor = Theme.Surface,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            Font = Theme.BodySmall,
            IntegralHeight = false
        };
        foreach (var difference in comparison.Differences)
        {
            list.Items.Add("•  " + difference);
        }

        var note = new Label
        {
            Text = "Comparison uses file metadata and section metadata only. Save contents are never decoded or modified.",
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = false,
            Location = new Point(24, 486),
            Width = 672,
            Height = 24,
            BackColor = Color.Transparent
        };

        var close = Ui.Button("Close", true, 100);
        close.Location = new Point(596, 514);
        close.DialogResult = DialogResult.OK;

        Controls.AddRange(new Control[] { heading, left, right, summary, list, note, close });
        AcceptButton = close;
    }
}

internal sealed class PromptDialog : DarkDialog
{
    private readonly DarkTextBox _input;

    public PromptDialog(string title, string label, string initialValue = "", string? hint = null)
    {
        Text = title;
        ClientSize = new Size(460, 220);

        var heading = new Label
        {
            Text = title,
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(22, 18),
            BackColor = Color.Transparent
        };

        var field = Ui.FieldLabel(label);
        field.Location = new Point(24, 58);

        _input = new DarkTextBox { Location = new Point(24, 78), Width = 410 };
        _input.Text = initialValue;

        var hintLabel = new Label
        {
            Text = hint ?? string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Faint,
            AutoSize = false,
            Location = new Point(24, 120),
            Width = 410,
            Height = 32,
            BackColor = Color.Transparent
        };

        var cancel = Ui.Button("Cancel", false, 90);
        cancel.Location = new Point(232, 166);
        cancel.DialogResult = DialogResult.Cancel;

        var ok = Ui.Button("Save", true, 100);
        ok.Location = new Point(334, 166);
        ok.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_input.Text))
            {
                MessageBox.Show(this, "Enter a name first.", title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Value = _input.Text.Trim();
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[] { heading, field, _input, hintLabel, cancel, ok });
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public string Value { get; private set; } = string.Empty;
}

internal sealed class ConfirmDialog : DarkDialog
{
    public ConfirmDialog(string title, string message, string confirmText = "Continue", bool dangerous = false)
    {
        Text = title;
        ClientSize = new Size(520, 260);

        var heading = new Label
        {
            Text = title,
            Font = Theme.H2,
            ForeColor = Theme.Text,
            AutoSize = true,
            Location = new Point(22, 18),
            BackColor = Color.Transparent
        };

        var body = new Label
        {
            Text = message,
            Font = Theme.Body,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Location = new Point(24, 60),
            Width = 470,
            Height = 120,
            BackColor = Color.Transparent
        };

        var cancel = Ui.Button("Cancel", false, 90);
        cancel.Location = new Point(292, 202);
        cancel.DialogResult = DialogResult.Cancel;

        var confirm = Ui.Button(confirmText, true, 120);
        confirm.Location = new Point(388, 202);
        if (dangerous)
        {
            confirm.FillColor = Theme.Bad;
            confirm.HoverColor = Theme.FromHex("#FCA5A5");
            confirm.ForeColor = Color.White;
        }

        confirm.DialogResult = DialogResult.OK;

        Controls.AddRange(new Control[] { heading, body, cancel, confirm });
        CancelButton = cancel;
        AcceptButton = cancel;
    }
}
