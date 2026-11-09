using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.App;

internal sealed class ProfilesPage : PageBase
{
    private readonly DataGridView _grid = new();
    private readonly DarkTextBox _name;
    private readonly Label _preview;
    private readonly Label _counts;
    private Snapshot _snapshot = Snapshot.Empty;

    public ProfilesPage(MainForm main) : base(main)
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.Window,
            Margin = new Padding(0)
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));

        var gridCard = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(4), Margin = new Padding(0, 0, 14, 0) };
        Grids.Style(_grid);
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "name", HeaderText = "Profile", FillWeight = 50 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "backups", HeaderText = "Backups", FillWeight = 20 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "created", HeaderText = "Created", FillWeight = 30 });
        gridCard.Controls.Add(_grid);
        root.Controls.Add(gridCard, 0, 0);

        var editor = new CardPanel { Dock = DockStyle.Fill, Padding = new Padding(18) };
        var heading = Ui.SectionLabel("Profile name");
        heading.Location = new Point(18, 16);

        _name = new DarkTextBox { Location = new Point(18, 44), Width = 290 };
        _name.Placeholder = "e.g. Before Episode 3 update";

        _preview = new Label
        {
            Text = "Pick a profile on the left, or create a new one.",
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = false,
            Height = 70,
            Width = 290,
            Location = new Point(18, 92),
            BackColor = Color.Transparent
        };

        var create = Ui.Button("Create profile", primary: true, width: 140);
        create.Location = new Point(18, 172);
        create.Click += (_, _) => CreateProfile();

        var rename = Ui.Button("Rename", false, 100);
        rename.Location = new Point(168, 172);
        rename.Click += (_, _) => RenameProfile();

        var delete = Ui.Button("Delete", false, 100);
        delete.Location = new Point(18, 212);
        delete.FillColor = Theme.FromHex("#3A2328");
        delete.ForeColor = Theme.Bad;
        delete.Click += (_, _) => DeleteProfile();

        var info = new CardPanel
        {
            FillColor = Theme.Surface,
            Location = new Point(18, 264),
            Size = new Size(290, 150),
            Padding = new Padding(14)
        };
        _counts = new Label
        {
            Text = string.Empty,
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        info.Controls.Add(_counts);

        editor.Controls.AddRange(new Control[] { heading, _name, _preview, create, rename, delete, info });
        root.Controls.Add(editor, 1, 0);

        _grid.SelectionChanged += (_, _) =>
        {
            var profile = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<Profile>().FirstOrDefault();
            if (profile is not null)
            {
                _name.Text = profile.Name;
                _preview.Text = profile.IsDefault
                    ? "The default profile cannot be renamed or deleted."
                    : "Rename or delete this profile. Backups are moved to Default, never deleted.";
            }
        };

        Controls.Add(root);
    }

    public override string Key => "profiles";

    public override string Title => "Profiles";

    public override string Subtitle => "Group backups by playthrough, patch or experiment.";

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

    private void Rebuild()
    {
        _grid.Rows.Clear();
        foreach (var profile in _snapshot.Profiles)
        {
            var count = _snapshot.Backups.Count(b => b.ProfileId == profile.Id);
            var index = _grid.Rows.Add(profile.Name, count, profile.CreatedUtc.ToLocalTime().ToString("d"));
            _grid.Rows[index].Tag = profile;
        }

        _counts.Text =
            $"{_snapshot.Profiles.Count} profile(s)\r\n" +
            $"{_snapshot.Backups.Count} backup(s) total\r\n\r\n" +
            "Profiles only organise backups. They never change save files.";
    }

    private void CreateProfile()
    {
        var name = _name.Text.Trim();
        if (name.Length == 0)
        {
            MessageBox.Show(this, "Enter a profile name.", Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        try
        {
            Main.Services.Profiles.Create(name, "#5B8DFF");
            Main.Services.Log.Info("profiles", $"Created profile '{name}'.");
            _name.Text = string.Empty;
            _ = Main.ReloadBackupsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void RenameProfile()
    {
        var profile = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<Profile>().FirstOrDefault();
        if (profile is null)
        {
            MessageBox.Show(this, "Select a profile first.", Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var prompt = new PromptDialog("Rename profile", "New name", profile.Name);
        if (prompt.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            Main.Services.Profiles.Rename(profile.Id, prompt.Value);
            _ = Main.ReloadBackupsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void DeleteProfile()
    {
        var profile = _grid.SelectedRows.Cast<DataGridViewRow>().Select(r => r.Tag).OfType<Profile>().FirstOrDefault();
        if (profile is null)
        {
            MessageBox.Show(this, "Select a profile first.", Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (profile.IsDefault)
        {
            MessageBox.Show(this, "The default profile cannot be deleted.", Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var confirm = new ConfirmDialog(
            "Delete this profile?",
            $"'{profile.Name}' will be removed. Its backups move to the Default profile and are not deleted.",
            "Delete profile",
            dangerous: true);

        if (confirm.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var backups = Main.Services.Backups.List().Where(b => b.ProfileId == profile.Id).ToList();
            foreach (var backup in backups)
            {
                backup.ProfileId = Profile.DefaultId;
                Main.Services.Backups.Save(backup);
            }

            Main.Services.Profiles.Delete(profile.Id);
            Main.Services.Log.Info("profiles", $"Deleted profile '{profile.Name}' and moved {backups.Count} backup(s) to Default.");
            _ = Main.ReloadBackupsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Program.ApplicationName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
