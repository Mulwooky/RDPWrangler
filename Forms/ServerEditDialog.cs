using RDPWrangler.Models;

namespace RDPWrangler.Forms;

public class ServerEditDialog : Form
{
    private readonly ComboBox _cboGroup;
    private readonly TextBox _txtDisplayName;
    private readonly TextBox _txtHost;
    private readonly NumericUpDown _numPort;
    private readonly ComboBox _cboCredentialProfile;
    private readonly TextBox _txtUsername;
    private readonly TextBox _txtDomain;
    private readonly TextBox _txtPassword;
    private readonly CheckBox _chkShowPassword;
    private readonly CheckBox _chkSmartSizing;
    private readonly TextBox _txtNotes;
    private readonly Button _btnOk;
    private readonly Button _btnCancel;

    private readonly List<CredentialProfile> _profiles;

    public RdpServerConnection Server { get; private set; }

    public ServerEditDialog(
        RdpServerConnection? serverToEdit = null,
        IEnumerable<string>? existingGroups = null,
        IEnumerable<CredentialProfile>? credentialProfiles = null)
    {
        Server = serverToEdit != null ? serverToEdit.Clone() : new RdpServerConnection();
        _profiles = credentialProfiles?.ToList() ?? new List<CredentialProfile>();
        bool isEdit = serverToEdit != null;

        Text = isEdit ? "Edit RDP Connection" : "Add RDP Connection";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(490, 600);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Color.FromArgb(245, 247, 250);
        ForeColor = Color.FromArgb(33, 37, 41);

        var lblHeader = new Label
        {
            Text = isEdit ? "Modify Connection Settings" : "Configure New Connection",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(24, 43, 73),
            Location = new Point(24, 16),
            AutoSize = true
        };

        int y = 52;
        const int labelX = 24;
        const int inputX = 145;
        const int inputWidth = 315;
        const int rowHeight = 36;

        // Group
        var lblGroup = new Label { Text = "Group / Category:", Location = new Point(labelX, y + 3), AutoSize = true };
        _cboGroup = new ComboBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputWidth, 28),
            DropDownStyle = ComboBoxStyle.DropDown
        };
        if (existingGroups != null)
        {
            foreach (var g in existingGroups.Where(g => !string.IsNullOrWhiteSpace(g)).Distinct())
            {
                _cboGroup.Items.Add(g);
            }
        }
        if (_cboGroup.Items.Count == 0)
        {
            _cboGroup.Items.Add("General");
            _cboGroup.Items.Add("Production");
            _cboGroup.Items.Add("Development");
        }
        _cboGroup.Text = string.IsNullOrWhiteSpace(Server.Group) ? "General" : Server.Group;

        // Display Name
        y += rowHeight;
        var lblDisplayName = new Label { Text = "Display Name:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtDisplayName = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputWidth, 28),
            Text = Server.DisplayName,
            PlaceholderText = "e.g., Main Web Server"
        };

        // Host
        y += rowHeight;
        var lblHost = new Label { Text = "Host / IP:*", Location = new Point(labelX, y + 3), AutoSize = true, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold) };
        _txtHost = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputWidth, 28),
            Text = Server.Host,
            PlaceholderText = "e.g., 192.168.1.50 or rdp.domain.com"
        };

        // Port
        y += rowHeight;
        var lblPort = new Label { Text = "Port:", Location = new Point(labelX, y + 3), AutoSize = true };
        _numPort = new NumericUpDown
        {
            Location = new Point(inputX, y),
            Size = new Size(120, 28),
            Minimum = 1,
            Maximum = 65535,
            Value = Server.Port > 0 ? Server.Port : 3389
        };

        // Credential Profile Dropdown
        y += rowHeight;
        var lblProfile = new Label { Text = "Saved Profile:", Location = new Point(labelX, y + 3), AutoSize = true };
        _cboCredentialProfile = new ComboBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputWidth, 28),
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cboCredentialProfile.Items.Add("<Custom / Standalone Credentials>");
        foreach (var p in _profiles)
        {
            _cboCredentialProfile.Items.Add(p.DisplayText);
        }

        int selectedProfileIdx = 0;
        if (!string.IsNullOrEmpty(Server.CredentialProfileId))
        {
            int foundIdx = _profiles.FindIndex(p => p.Id == Server.CredentialProfileId);
            if (foundIdx >= 0)
            {
                selectedProfileIdx = foundIdx + 1;
            }
        }
        _cboCredentialProfile.SelectedIndex = selectedProfileIdx;
        _cboCredentialProfile.SelectedIndexChanged += CboCredentialProfile_SelectedIndexChanged;

        // Username
        y += rowHeight;
        var lblUsername = new Label { Text = "Username:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtUsername = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputWidth, 28),
            Text = Server.Username,
            PlaceholderText = "e.g., Administrator"
        };

        // Domain
        y += rowHeight;
        var lblDomain = new Label { Text = "Domain (optional):", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtDomain = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputWidth, 28),
            Text = Server.Domain,
            PlaceholderText = "e.g., CORP"
        };

        // Password
        y += rowHeight;
        var lblPassword = new Label { Text = "Password:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtPassword = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputWidth - 75, 28),
            Text = Server.Password,
            UseSystemPasswordChar = true
        };
        _chkShowPassword = new CheckBox
        {
            Text = "Show",
            Location = new Point(inputX + inputWidth - 70, y + 3),
            AutoSize = true
        };
        _chkShowPassword.CheckedChanged += (_, _) =>
        {
            _txtPassword.UseSystemPasswordChar = !_chkShowPassword.Checked;
        };

        // Smart Sizing Checkbox
        y += rowHeight;
        _chkSmartSizing = new CheckBox
        {
            Text = "Enable Smart Sizing (Auto-fit desktop to panel)",
            Location = new Point(inputX, y),
            Size = new Size(inputWidth, 28),
            Checked = Server.SmartSizing
        };

        // Notes
        y += rowHeight;
        var lblNotes = new Label { Text = "Notes:", Location = new Point(labelX, y + 3), AutoSize = true };
        _txtNotes = new TextBox
        {
            Location = new Point(inputX, y),
            Size = new Size(inputWidth, 55),
            Text = Server.Notes,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical
        };

        // Buttons
        y += 75;
        _btnOk = new Button
        {
            Text = isEdit ? "Save Changes" : "Add Connection",
            Location = new Point(230, y),
            Size = new Size(130, 36),
            BackColor = Color.FromArgb(0, 120, 215),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _btnOk.FlatAppearance.BorderSize = 0;
        _btnOk.Click += BtnOk_Click;

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(370, y),
            Size = new Size(90, 36),
            BackColor = Color.FromArgb(225, 230, 235),
            ForeColor = Color.FromArgb(40, 40, 40),
            FlatStyle = FlatStyle.Flat,
            DialogResult = DialogResult.Cancel,
            Cursor = Cursors.Hand
        };
        _btnCancel.FlatAppearance.BorderSize = 0;

        AcceptButton = _btnOk;
        CancelButton = _btnCancel;

        Controls.AddRange(new Control[]
        {
            lblHeader,
            lblGroup, _cboGroup,
            lblDisplayName, _txtDisplayName,
            lblHost, _txtHost,
            lblPort, _numPort,
            lblProfile, _cboCredentialProfile,
            lblUsername, _txtUsername,
            lblDomain, _txtDomain,
            lblPassword, _txtPassword, _chkShowPassword,
            _chkSmartSizing,
            lblNotes, _txtNotes,
            _btnOk, _btnCancel
        });
    }

    private void CboCredentialProfile_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cboCredentialProfile.SelectedIndex > 0 && _cboCredentialProfile.SelectedIndex - 1 < _profiles.Count)
        {
            var p = _profiles[_cboCredentialProfile.SelectedIndex - 1];
            _txtUsername.Text = p.Username;
            _txtDomain.Text = p.Domain;
            _txtPassword.Text = p.Password;
            Server.CredentialProfileId = p.Id;
        }
        else
        {
            Server.CredentialProfileId = null;
        }
    }

    private void InitializeComponent()
    {

    }

    private void BtnOk_Click(object? sender, EventArgs e)
    {
        string host = _txtHost.Text.Trim();
        if (string.IsNullOrWhiteSpace(host))
        {
            MessageBox.Show(this, "Host / IP Address is required.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtHost.Focus();
            return;
        }

        string group = _cboGroup.Text.Trim();
        if (string.IsNullOrWhiteSpace(group))
        {
            group = "General";
        }

        Server.Group = group;
        Server.DisplayName = _txtDisplayName.Text.Trim();
        Server.Host = host;
        Server.Port = (int)_numPort.Value;
        Server.Username = _txtUsername.Text.Trim();
        Server.Domain = _txtDomain.Text.Trim();
        Server.Password = _txtPassword.Text;
        Server.SmartSizing = _chkSmartSizing.Checked;
        Server.Notes = _txtNotes.Text.Trim();

        if (_cboCredentialProfile.SelectedIndex > 0 && _cboCredentialProfile.SelectedIndex - 1 < _profiles.Count)
        {
            Server.CredentialProfileId = _profiles[_cboCredentialProfile.SelectedIndex - 1].Id;
        }
        else
        {
            Server.CredentialProfileId = null;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
