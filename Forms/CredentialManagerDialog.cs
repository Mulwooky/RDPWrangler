using RDPWrangler.Models;

namespace RDPWrangler.Forms;

public class CredentialManagerDialog : Form
{
    private readonly List<CredentialProfile> _profiles;
    private readonly ListView _lstProfiles;
    private readonly Button _btnAdd;
    private readonly Button _btnEdit;
    private readonly Button _btnDelete;
    private readonly Button _btnClose;

    public bool HasChanges { get; private set; }
    public List<CredentialProfile> Profiles => _profiles;

    public CredentialManagerDialog(List<CredentialProfile> profiles)
    {
        _profiles = profiles.Select(p => p.Clone()).ToList();

        Text = "Manage Credential Vault";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(580, 420);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Color.FromArgb(245, 247, 250);
        ForeColor = Color.FromArgb(33, 37, 41);

        var lblHeader = new Label
        {
            Text = "🔑 Reusable Credential Profiles",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            Location = new Point(20, 16),
            AutoSize = true
        };

        var lblSub = new Label
        {
            Text = "Profiles are encrypted using Windows DPAPI. Assign them to any server connection.",
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(100, 110, 125),
            Location = new Point(20, 44),
            AutoSize = true
        };

        _lstProfiles = new ListView
        {
            Location = new Point(20, 75),
            Size = new Size(420, 320),
            View = View.Details,
            FullRowSelect = true,
            MultiSelect = false,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f)
        };
        _lstProfiles.Columns.Add("Profile Name", 160);
        _lstProfiles.Columns.Add("Username", 120);
        _lstProfiles.Columns.Add("Domain", 110);
        _lstProfiles.SelectedIndexChanged += (_, _) => UpdateButtonStates();
        _lstProfiles.DoubleClick += (_, _) => EditSelectedProfile();

        int btnX = 455;
        _btnAdd = CreateButton("➕ Add...", btnX, 75, Color.FromArgb(0, 120, 215), Color.White);
        _btnAdd.Click += (_, _) => AddProfile();

        _btnEdit = CreateButton("✏ Edit...", btnX, 115, Color.FromArgb(225, 230, 238), Color.FromArgb(30, 30, 30));
        _btnEdit.Enabled = false;
        _btnEdit.Click += (_, _) => EditSelectedProfile();

        _btnDelete = CreateButton("🗑 Delete", btnX, 155, Color.FromArgb(225, 230, 238), Color.FromArgb(180, 40, 40));
        _btnDelete.Enabled = false;
        _btnDelete.Click += (_, _) => DeleteProfile();

        _btnClose = CreateButton("Close", btnX, 360, Color.FromArgb(210, 215, 225), Color.FromArgb(30, 30, 30));
        _btnClose.DialogResult = DialogResult.OK;

        Controls.AddRange(new Control[]
        {
            lblHeader, lblSub, _lstProfiles, _btnAdd, _btnEdit, _btnDelete, _btnClose
        });

        RefreshListView();
    }

    private Button CreateButton(string text, int x, int y, Color backColor, Color foreColor)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(105, 34),
            BackColor = backColor,
            ForeColor = foreColor,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void RefreshListView()
    {
        _lstProfiles.BeginUpdate();
        _lstProfiles.Items.Clear();

        foreach (var p in _profiles)
        {
            var item = new ListViewItem(p.Title);
            item.SubItems.Add(p.Username);
            item.SubItems.Add(p.Domain);
            item.Tag = p;
            _lstProfiles.Items.Add(item);
        }

        _lstProfiles.EndUpdate();
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        bool hasSelection = _lstProfiles.SelectedItems.Count > 0;
        _btnEdit.Enabled = hasSelection;
        _btnDelete.Enabled = hasSelection;
    }

    private void AddProfile()
    {
        var newProfile = new CredentialProfile();
        using var dlg = new CredentialEditDialog(newProfile);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _profiles.Add(dlg.Profile);
            HasChanges = true;
            RefreshListView();
        }
    }

    private void EditSelectedProfile()
    {
        if (_lstProfiles.SelectedItems.Count == 0) return;
        if (_lstProfiles.SelectedItems[0].Tag is not CredentialProfile selected) return;

        using var dlg = new CredentialEditDialog(selected);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            int idx = _profiles.FindIndex(p => p.Id == selected.Id);
            if (idx >= 0)
            {
                _profiles[idx] = dlg.Profile;
                HasChanges = true;
                RefreshListView();
            }
        }
    }

    private void InitializeComponent()
    {

    }

    private void DeleteProfile()
    {
        if (_lstProfiles.SelectedItems.Count == 0) return;
        if (_lstProfiles.SelectedItems[0].Tag is not CredentialProfile selected) return;

        var res = MessageBox.Show(this,
            $"Are you sure you want to delete profile '{selected.Title}'?",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (res == DialogResult.Yes)
        {
            _profiles.RemoveAll(p => p.Id == selected.Id);
            HasChanges = true;
            RefreshListView();
        }
    }
}

internal class CredentialEditDialog : Form
{
    private readonly TextBox _txtTitle;
    private readonly TextBox _txtUsername;
    private readonly TextBox _txtDomain;
    private readonly TextBox _txtPassword;
    private readonly CheckBox _chkShowPassword;

    public CredentialProfile Profile { get; }

    public CredentialEditDialog(CredentialProfile profileToEdit)
    {
        Profile = profileToEdit.Clone();
        bool isEdit = !string.IsNullOrWhiteSpace(profileToEdit.Title);

        Text = isEdit ? "Edit Credential Profile" : "New Credential Profile";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(400, 310);
        Font = new Font("Segoe UI", 9.5f);
        BackColor = Color.FromArgb(245, 247, 250);

        int y = 20;
        const int labelX = 20;
        const int inputX = 120;
        const int inputW = 250;
        const int rowH = 38;

        var lblTitle = new Label { Text = "Profile Name:*", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _txtTitle = new TextBox { Location = new Point(inputX, y), Size = new Size(inputW, 28), Text = Profile.Title, PlaceholderText = "e.g. Domain Admin" };

        y += rowH;
        var lblUser = new Label { Text = "Username:*", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _txtUsername = new TextBox { Location = new Point(inputX, y), Size = new Size(inputW, 28), Text = Profile.Username, PlaceholderText = "e.g. Administrator" };

        y += rowH;
        var lblDomain = new Label { Text = "Domain:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtDomain = new TextBox { Location = new Point(inputX, y), Size = new Size(inputW, 28), Text = Profile.Domain, PlaceholderText = "e.g. CORP" };

        y += rowH;
        var lblPass = new Label { Text = "Password:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtPassword = new TextBox { Location = new Point(inputX, y), Size = new Size(inputW - 70, 28), Text = Profile.Password, UseSystemPasswordChar = true };
        _chkShowPassword = new CheckBox { Text = "Show", Location = new Point(inputX + inputW - 65, y + 3), AutoSize = true };
        _chkShowPassword.CheckedChanged += (_, _) => _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;

        y += 55;
        var btnOk = new Button
        {
            Text = "Save",
            Location = new Point(190, y),
            Size = new Size(95, 34),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(_txtTitle.Text))
            {
                MessageBox.Show(this, "Profile name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtTitle.Focus();
                return;
            }
            if (string.IsNullOrWhiteSpace(_txtUsername.Text))
            {
                MessageBox.Show(this, "Username is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                _txtUsername.Focus();
                return;
            }

            Profile.Title = _txtTitle.Text.Trim();
            Profile.Username = _txtUsername.Text.Trim();
            Profile.Domain = _txtDomain.Text.Trim();
            Profile.Password = _txtPassword.Text;

            DialogResult = DialogResult.OK;
            Close();
        };

        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(295, y),
            Size = new Size(75, 34),
            BackColor = Color.FromArgb(220, 225, 235),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel,
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderSize = 0;

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange(new Control[]
        {
            lblTitle, _txtTitle,
            lblUser, _txtUsername,
            lblDomain, _txtDomain,
            lblPass, _txtPassword, _chkShowPassword,
            btnOk, btnCancel
        });
    }
}
