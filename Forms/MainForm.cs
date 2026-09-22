using System.Drawing.Drawing2D;
using RDPWrangler.Controls;
using RDPWrangler.Models;
using RDPWrangler.Services;

namespace RDPWrangler.Forms;

public class MainForm : Form
{
    private readonly IniManager _iniManager;
    private AppSettings _settings;
    private List<RdpServerConnection> _servers;
    private List<CredentialProfile> _credentialProfiles;

    // Layout Controls
    private SplitContainer _splitContainer = null!;
    private Panel _sidebarPanel = null!;
    private Panel _sidebarHeader = null!;
    private Label _lblSidebarTitle = null!;
    private Button _btnCollapseSidebar = null!;
    private TextBox _txtSearch = null!;
    private FlowLayoutPanel _sidebarToolbar = null!;
    private Button _btnAddServer = null!;
    private Button _btnEditServer = null!;
    private Button _btnDeleteServer = null!;
    private Button _btnCredentials = null!;
    private TreeView _treeServers = null!;
    private ImageList _imageList = null!;
    private ContextMenuStrip _treeContextMenu = null!;

    // Remote Desktop Panel Controls
    private Panel _rdpContainer = null!;
    private Panel _sessionToolbar = null!;
    private Button _btnExpandSidebar = null!;
    private Label _lblSessionTitle = null!;
    private Label _lblStatusBadge = null!;
    private Button _btnConnect = null!;
    private Button _btnDisconnect = null!;
    private Button _btnReconnect = null!;
    private Button _btnToggleSidebar = null!;

    private RdpSessionManager _sessionManager = null!;
    private RdpServerConnection? _selectedServer;
    private int _savedSplitterDistance = 280;

    public MainForm()
    {
        _iniManager = new IniManager();
        (_settings, _servers, _credentialProfiles) = _iniManager.Load();

        InitializeAppWindow();
        InitializeComponents();
        LoadServerTree();

        // Wire up keyboard shortcuts
        KeyPreview = true;
        KeyDown += MainForm_KeyDown;
        FormClosing += MainForm_FormClosing;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MainForm.OnLoad: Bounds={Bounds}, WindowState={WindowState}\n");
        _splitContainer.Panel1MinSize = 150;
        _splitContainer.Panel2MinSize = 250;
        RestoreLayoutState();
        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MainForm.OnLoad finished: Bounds={Bounds}, WindowState={WindowState}\n");
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try
        {
            Activate();
            BringToFront();
        }
        catch { }
    }

    private void InitializeAppWindow()
    {
        Text = "RDPWrangler - Remote Desktop Connection Manager";
        Size = new Size(Math.Max(1000, _settings.WindowWidth), Math.Max(650, _settings.WindowHeight));
        MinimumSize = new Size(800, 550);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
        BackColor = Color.FromArgb(240, 242, 245);
        ForeColor = Color.FromArgb(30, 30, 30);
    }

    private void InitializeComponents()
    {
        // 1. ImageList for TreeView icons
        _imageList = CreateImageList();

        // 2. Main Split Container
        _splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 5,
            BackColor = Color.FromArgb(215, 220, 228)
        };

        // ==========================================
        // LEFT PANEL (SIDEBAR)
        // ==========================================
        _sidebarPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(248, 249, 251)
        };

        // Sidebar Header
        _sidebarHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(32, 43, 61),
            Padding = new Padding(12, 0, 8, 0)
        };

        _lblSidebarTitle = new Label
        {
            Text = "🖥️  Servers & Groups",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            Dock = DockStyle.Left,
            AutoSize = false,
            Width = 180,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _btnCollapseSidebar = new Button
        {
            Text = "◀",
            Dock = DockStyle.Right,
            Width = 32,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(200, 210, 225),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 6, 0, 6)
        };
        _btnCollapseSidebar.FlatAppearance.BorderSize = 0;
        _btnCollapseSidebar.Click += (_, _) => SetSidebarCollapsed(true);

        var toolTip = new ToolTip();
        toolTip.SetToolTip(_btnCollapseSidebar, "Collapse server panel (Ctrl+B)");

        _sidebarHeader.Controls.Add(_lblSidebarTitle);
        _sidebarHeader.Controls.Add(_btnCollapseSidebar);

        // Search Bar
        var searchPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(8, 6, 8, 4),
            BackColor = Color.FromArgb(242, 244, 247)
        };
        _txtSearch = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "🔍 Search connections or groups...",
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 9.5f)
        };
        _txtSearch.TextChanged += (_, _) => FilterServerTree(_txtSearch.Text);
        searchPanel.Controls.Add(_txtSearch);

        // Sidebar Action Toolbar
        _sidebarToolbar = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 38,
            Padding = new Padding(6, 4, 6, 4),
            BackColor = Color.FromArgb(238, 240, 244),
            WrapContents = false
        };

        _btnAddServer = CreateToolbarButton("➕ Add", Color.FromArgb(0, 120, 215), Color.White);
        _btnAddServer.Click += BtnAddServer_Click;
        toolTip.SetToolTip(_btnAddServer, "Add a new RDP server connection");

        _btnEditServer = CreateToolbarButton("✏ Edit", Color.FromArgb(225, 230, 238), Color.FromArgb(30, 30, 30));
        _btnEditServer.Enabled = false;
        _btnEditServer.Click += BtnEditServer_Click;
        toolTip.SetToolTip(_btnEditServer, "Edit selected connection (F2)");

        _btnDeleteServer = CreateToolbarButton("🗑 Delete", Color.FromArgb(225, 230, 238), Color.FromArgb(180, 40, 40));
        _btnDeleteServer.Enabled = false;
        _btnDeleteServer.Click += BtnDeleteServer_Click;
        toolTip.SetToolTip(_btnDeleteServer, "Delete selected server or group (Del)");

        _btnCredentials = CreateToolbarButton("🔑 Vault", Color.FromArgb(225, 230, 238), Color.FromArgb(30, 30, 30));
        _btnCredentials.Click += BtnCredentials_Click;
        toolTip.SetToolTip(_btnCredentials, "Manage reusable credential vault");

        _sidebarToolbar.Controls.AddRange(new Control[] { _btnAddServer, _btnEditServer, _btnDeleteServer, _btnCredentials });

        // TreeView
        _treeServers = new TreeView
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            ImageList = _imageList,
            ItemHeight = 26,
            Font = new Font("Segoe UI", 9.5f),
            ShowLines = true,
            ShowPlusMinus = true,
            ShowRootLines = true,
            FullRowSelect = true,
            HideSelection = false,
            BackColor = Color.FromArgb(252, 253, 254)
        };
        _treeServers.AfterSelect += TreeServers_AfterSelect;
        _treeServers.NodeMouseDoubleClick += TreeServers_NodeMouseDoubleClick;
        _treeServers.MouseUp += TreeServers_MouseUp;

        // Tree Context Menu
        _treeContextMenu = new ContextMenuStrip();
        BuildContextMenu();
        _treeServers.ContextMenuStrip = _treeContextMenu;

        _sidebarPanel.Controls.Add(_treeServers);
        _sidebarPanel.Controls.Add(_sidebarToolbar);
        _sidebarPanel.Controls.Add(searchPanel);
        _sidebarPanel.Controls.Add(_sidebarHeader);

        _splitContainer.Panel1.Controls.Add(_sidebarPanel);

        // ==========================================
        // RIGHT PANEL (REMOTE DESKTOP & SESSION BAR)
        // ==========================================
        _rdpContainer = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 24, 24)
        };

        // Session Toolbar
        _sessionToolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 44,
            BackColor = Color.FromArgb(36, 40, 48),
            Padding = new Padding(8, 0, 8, 0)
        };

        _btnExpandSidebar = new Button
        {
            Text = "▶ Servers",
            Dock = DockStyle.Left,
            Width = 90,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(55, 65, 80),
            Cursor = Cursors.Hand,
            Visible = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _btnExpandSidebar.FlatAppearance.BorderSize = 0;
        _btnExpandSidebar.Click += (_, _) => SetSidebarCollapsed(false);
        toolTip.SetToolTip(_btnExpandSidebar, "Expand server panel (Ctrl+B)");

        _lblSessionTitle = new Label
        {
            Text = "No active connection",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(235, 240, 245),
            Dock = DockStyle.Left,
            AutoSize = false,
            Width = 360,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0)
        };

        _lblStatusBadge = new Label
        {
            Text = "● Disconnected",
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(160, 165, 175),
            Dock = DockStyle.Left,
            AutoSize = false,
            Width = 200,
            TextAlign = ContentAlignment.MiddleLeft
        };

        var rightButtonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 5, 0, 0),
            WrapContents = false
        };

        _btnToggleSidebar = new Button
        {
            Text = "☰ Sidebar",
            Width = 80,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.FromArgb(200, 210, 220),
            BackColor = Color.FromArgb(50, 56, 68),
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9f)
        };
        _btnToggleSidebar.FlatAppearance.BorderSize = 0;
        _btnToggleSidebar.Click += (_, _) => SetSidebarCollapsed(!_splitContainer.Panel1Collapsed);
        toolTip.SetToolTip(_btnToggleSidebar, "Toggle server panel (Ctrl+B)");

        _btnReconnect = new Button
        {
            Text = "🔄 Reconnect",
            Width = 98,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(50, 56, 68),
            Cursor = Cursors.Hand,
            Enabled = false,
            Font = new Font("Segoe UI", 9f)
        };
        _btnReconnect.FlatAppearance.BorderSize = 0;
        _btnReconnect.Click += BtnReconnect_Click;

        _btnDisconnect = new Button
        {
            Text = "⏹ Disconnect",
            Width = 105,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(170, 45, 45),
            Cursor = Cursors.Hand,
            Enabled = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _btnDisconnect.FlatAppearance.BorderSize = 0;
        _btnDisconnect.Click += BtnDisconnect_Click;

        _btnConnect = new Button
        {
            Text = "▶ Connect",
            Width = 90,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(0, 122, 204),
            Cursor = Cursors.Hand,
            Enabled = false,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        _btnConnect.FlatAppearance.BorderSize = 0;
        _btnConnect.Click += BtnConnect_Click;

        rightButtonPanel.Controls.AddRange(new Control[] { _btnToggleSidebar, _btnReconnect, _btnDisconnect, _btnConnect });

        _sessionToolbar.Controls.Add(rightButtonPanel);
        _sessionToolbar.Controls.Add(_lblStatusBadge);
        _sessionToolbar.Controls.Add(_lblSessionTitle);
        _sessionToolbar.Controls.Add(_btnExpandSidebar);

        // Session Manager (owns tab strip, multiple RdpClientHost controls, and welcome overlay)
        _sessionManager = new RdpSessionManager
        {
            Dock = DockStyle.Fill
        };
        _sessionManager.StatusChanged        += RdpHost_StatusChanged;
        _sessionManager.ActiveSessionChanged += OnActiveSessionChanged;

        _rdpContainer.Controls.Add(_sessionManager);
        _rdpContainer.Controls.Add(_sessionToolbar);

        _splitContainer.Panel2.Controls.Add(_rdpContainer);
        Controls.Add(_splitContainer);
    }

    private Button CreateToolbarButton(string text, Color backColor, Color foreColor)
    {
        var btn = new Button
        {
            Text = text,
            Height = 28,
            AutoSize = true,
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = foreColor,
            Cursor = Cursors.Hand,
            Margin = new Padding(2, 0, 2, 0),
            Font = new Font("Segoe UI", 9f)
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    private void RestoreLayoutState()
    {
        try
        {
            int targetDist = _settings.SplitterDistance;
            int maxDist = Math.Max(150, _splitContainer.Width - _splitContainer.Panel2MinSize - 10);
            if (targetDist < _splitContainer.Panel1MinSize) targetDist = 280;
            if (targetDist > maxDist) targetDist = Math.Min(280, maxDist);

            if (targetDist >= _splitContainer.Panel1MinSize && targetDist <= maxDist)
            {
                _splitContainer.SplitterDistance = targetDist;
                _savedSplitterDistance = targetDist;
            }
        }
        catch
        {
            // Ignore splitter distance sizing quirks
        }

        if (_settings.WindowMaximized)
        {
            WindowState = FormWindowState.Maximized;
        }

        if (_settings.SidebarCollapsed)
        {
            SetSidebarCollapsed(true);
        }

        // Restore selection if any
        if (!string.IsNullOrEmpty(_settings.LastConnectedServerId))
        {
            SelectServerById(_settings.LastConnectedServerId);
        }
        else if (_treeServers.Nodes.Count > 0 && _treeServers.Nodes[0].Nodes.Count > 0)
        {
            _treeServers.SelectedNode = _treeServers.Nodes[0].Nodes[0];
        }
    }

    private void SetSidebarCollapsed(bool collapsed)
    {
        if (collapsed)
        {
            _savedSplitterDistance = _splitContainer.SplitterDistance;
            _splitContainer.Panel1Collapsed = true;
            _btnExpandSidebar.Visible = true;
            _settings.SidebarCollapsed = true;
        }
        else
        {
            _splitContainer.Panel1Collapsed = false;
            if (_savedSplitterDistance > 150)
            {
                _splitContainer.SplitterDistance = _savedSplitterDistance;
            }
            _btnExpandSidebar.Visible = false;
            _settings.SidebarCollapsed = false;
        }
    }

    private void LoadServerTree(string? filter = null)
    {
        _treeServers.BeginUpdate();
        _treeServers.Nodes.Clear();

        var query = _servers.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            string f = filter.Trim().ToLowerInvariant();
            query = query.Where(s =>
                s.DisplayName.ToLowerInvariant().Contains(f) ||
                s.Host.ToLowerInvariant().Contains(f) ||
                s.Group.ToLowerInvariant().Contains(f) ||
                s.Username.ToLowerInvariant().Contains(f));
        }

        var groups = query
            .GroupBy(s => string.IsNullOrWhiteSpace(s.Group) ? "General" : s.Group)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in groups)
        {
            var groupNode = new TreeNode(group.Key, 0, 0)
            {
                Tag = group.Key,
                NodeFont = new Font(_treeServers.Font, FontStyle.Bold)
            };

            foreach (var server in group.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase))
            {
                string label = $"{server.Title} ({server.FullAddress})";
                var serverNode = new TreeNode(label, 1, 1)
                {
                    Tag = server
                };
                groupNode.Nodes.Add(serverNode);
            }

            _treeServers.Nodes.Add(groupNode);
            groupNode.Expand();
        }

        _treeServers.EndUpdate();
    }

    private void FilterServerTree(string query)
    {
        LoadServerTree(query);
    }

    private void SelectServerById(string id)
    {
        foreach (TreeNode groupNode in _treeServers.Nodes)
        {
            foreach (TreeNode serverNode in groupNode.Nodes)
            {
                if (serverNode.Tag is RdpServerConnection server && server.Id == id)
                {
                    _treeServers.SelectedNode = serverNode;
                    serverNode.EnsureVisible();
                    return;
                }
            }
        }
    }

    private void TreeServers_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is RdpServerConnection server)
        {
            _selectedServer = server;
            _btnEditServer.Enabled = true;
            _btnDeleteServer.Enabled = true;
            _btnConnect.Enabled = true;

            if (!_sessionManager.IsConnected && !_sessionManager.IsConnecting)
            {
                _lblSessionTitle.Text = $"{server.Title} ({server.FullAddress})";
                _lblStatusBadge.Text = "● Ready to connect";
                _lblStatusBadge.ForeColor = Color.FromArgb(140, 180, 230);
            }
        }
        else
        {
            _selectedServer = null;
            _btnEditServer.Enabled = false;
            // Can delete empty or populated group node
            _btnDeleteServer.Enabled = e.Node?.Tag is string;
            _btnConnect.Enabled = false;

            if (!_sessionManager.IsConnected && !_sessionManager.IsConnecting)
            {
                _lblSessionTitle.Text = e.Node != null ? $"Group: {e.Node.Text}" : "No active connection";
                _lblStatusBadge.Text = "● Disconnected";
                _lblStatusBadge.ForeColor = Color.FromArgb(160, 165, 175);
            }
        }
    }

    private void TreeServers_NodeMouseDoubleClick(object? sender, TreeNodeMouseClickEventArgs e)
    {
        if (e.Node.Tag is RdpServerConnection server)
        {
            _selectedServer = server;
            StartConnection(server);
        }
    }

    private void TreeServers_MouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right)
        {
            var node = _treeServers.GetNodeAt(e.X, e.Y);
            if (node != null)
            {
                _treeServers.SelectedNode = node;
            }
        }
    }

    private void BuildContextMenu()
    {
        _treeContextMenu.Items.Clear();

        var miConnect = new ToolStripMenuItem("▶ Connect", null, (_, _) =>
        {
            if (_selectedServer != null) StartConnection(_selectedServer);
        })
        {
            Font = new Font(_treeServers.Font, FontStyle.Bold)
        };

        var miAdd = new ToolStripMenuItem("➕ Add New Server...", null, BtnAddServer_Click);
        var miEdit = new ToolStripMenuItem("✏ Edit Connection...", null, BtnEditServer_Click);
        var miDelete = new ToolStripMenuItem("🗑 Delete", null, BtnDeleteServer_Click);
        var miSep1 = new ToolStripSeparator();
        var miExpandAll = new ToolStripMenuItem("Expand All", null, (_, _) => _treeServers.ExpandAll());
        var miCollapseAll = new ToolStripMenuItem("Collapse All", null, (_, _) => _treeServers.CollapseAll());

        _treeContextMenu.Opening += (_, _) =>
        {
            bool isServer = _treeServers.SelectedNode?.Tag is RdpServerConnection;
            bool isGroup = _treeServers.SelectedNode?.Tag is string;

            miConnect.Visible = isServer;
            miEdit.Visible = isServer;
            miDelete.Visible = isServer || isGroup;
            miDelete.Text = isGroup ? "🗑 Delete Group and its Servers" : "🗑 Delete Server";
        };

        _treeContextMenu.Items.AddRange(new ToolStripItem[]
        {
            miConnect,
            miAdd,
            miEdit,
            miDelete,
            miSep1,
            miExpandAll,
            miCollapseAll
        });
    }

    private void BtnCredentials_Click(object? sender, EventArgs e)
    {
        using var dlg = new CredentialManagerDialog(_credentialProfiles);
        if (dlg.ShowDialog(this) == DialogResult.OK && dlg.HasChanges)
        {
            _credentialProfiles = dlg.Profiles;
            SaveConfiguration();
        }
    }

    private void BtnAddServer_Click(object? sender, EventArgs e)
    {
        var existingGroups = _servers.Select(s => s.Group).Distinct();
        string defaultGroup = "General";
        if (_treeServers.SelectedNode?.Tag is string gName)
        {
            defaultGroup = gName;
        }
        else if (_selectedServer != null)
        {
            defaultGroup = _selectedServer.Group;
        }

        var newServer = new RdpServerConnection { Group = defaultGroup };
        using var dlg = new ServerEditDialog(newServer, existingGroups, _credentialProfiles);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            _servers.Add(dlg.Server);
            SaveConfiguration();
            LoadServerTree(_txtSearch.Text);
            SelectServerById(dlg.Server.Id);
        }
    }

    private void BtnEditServer_Click(object? sender, EventArgs e)
    {
        if (_selectedServer == null) return;

        var existingGroups = _servers.Select(s => s.Group).Distinct();
        using var dlg = new ServerEditDialog(_selectedServer, existingGroups, _credentialProfiles);
        if (dlg.ShowDialog(this) == DialogResult.OK)
        {
            int idx = _servers.FindIndex(s => s.Id == _selectedServer.Id);
            if (idx >= 0)
            {
                _servers[idx] = dlg.Server;
            }
            _selectedServer = dlg.Server;
            SaveConfiguration();
            LoadServerTree(_txtSearch.Text);
            SelectServerById(dlg.Server.Id);
        }
    }

    private void BtnDeleteServer_Click(object? sender, EventArgs e)
    {
        var selectedNode = _treeServers.SelectedNode;
        if (selectedNode == null) return;

        if (selectedNode.Tag is RdpServerConnection server)
        {
            var res = MessageBox.Show(this,
                $"Are you sure you want to delete '{server.Title}' ({server.FullAddress})?\nThis will be removed from servers.ini.",
                "Confirm Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                _servers.RemoveAll(s => s.Id == server.Id);
                _selectedServer = null;
                SaveConfiguration();
                LoadServerTree(_txtSearch.Text);
            }
        }
        else if (selectedNode.Tag is string groupName)
        {
            int count = _servers.Count(s => string.Equals(s.Group, groupName, StringComparison.OrdinalIgnoreCase));
            string msg = count > 0
                ? $"Are you sure you want to delete group '{groupName}' and its {count} server connection(s)?\nThis will be removed from servers.ini."
                : $"Are you sure you want to remove group '{groupName}'?";

            var res = MessageBox.Show(this, msg, "Confirm Group Deletion", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (res == DialogResult.Yes)
            {
                _servers.RemoveAll(s => string.Equals(s.Group, groupName, StringComparison.OrdinalIgnoreCase));
                _selectedServer = null;
                SaveConfiguration();
                LoadServerTree(_txtSearch.Text);
            }
        }
    }

    private void BtnConnect_Click(object? sender, EventArgs e)
    {
        if (_selectedServer != null)
        {
            StartConnection(_selectedServer);
        }
    }

    private void BtnDisconnect_Click(object? sender, EventArgs e)
    {
        _sessionManager.DisconnectCurrent();
    }

    private void BtnReconnect_Click(object? sender, EventArgs e)
    {
        _sessionManager.ReconnectCurrent();
    }

    private void StartConnection(RdpServerConnection server)
    {
        _btnConnect.Enabled = false;
        _btnDisconnect.Enabled = true;
        _btnReconnect.Enabled = true;

        _lblSessionTitle.Text = $"{server.Title} ({server.FullAddress})";
        _settings.LastConnectedServerId = server.Id;

        // Resolve credentials if linked to a shared profile
        var connectionToUse = server.Clone();
        if (!string.IsNullOrEmpty(connectionToUse.CredentialProfileId))
        {
            var profile = _credentialProfiles.FirstOrDefault(p => p.Id == connectionToUse.CredentialProfileId);
            if (profile != null)
            {
                if (string.IsNullOrWhiteSpace(connectionToUse.Username))
                    connectionToUse.Username = profile.Username;
                if (string.IsNullOrWhiteSpace(connectionToUse.Domain))
                    connectionToUse.Domain = profile.Domain;
                if (string.IsNullOrWhiteSpace(connectionToUse.Password))
                    connectionToUse.Password = profile.Password;
            }
        }

        // Open a new session tab or switch to the existing one (no reconnect)
        _sessionManager.OpenOrSwitch(connectionToUse);
    }

    private void RdpHost_StatusChanged(string message, bool isConnected, bool isError)
    {
        _lblStatusBadge.Text = (isConnected ? "● " : "○ ") + message;

        if (isConnected)
        {
            _lblStatusBadge.ForeColor = Color.FromArgb(70, 205, 120); // Green
            _btnConnect.Enabled = false;
            _btnDisconnect.Enabled = true;
            _btnReconnect.Enabled = true;
        }
        else if (isError)
        {
            _lblStatusBadge.ForeColor = Color.FromArgb(240, 80, 80); // Red
            _btnConnect.Enabled = _selectedServer != null;
            _btnDisconnect.Enabled = false;
            _btnReconnect.Enabled = _sessionManager.SessionCount > 0;
        }
        else
        {
            // Disconnected / connecting
            _lblStatusBadge.ForeColor = Color.FromArgb(160, 165, 175); // Gray
            _btnConnect.Enabled = _selectedServer != null;
            _btnDisconnect.Enabled = false;
            _btnReconnect.Enabled = _sessionManager.SessionCount > 0;
        }
    }

    /// <summary>Called by <see cref="RdpSessionManager"/> when the active tab changes.</summary>
    private void OnActiveSessionChanged(RdpServerConnection? connection)
    {
        if (connection != null)
        {
            _lblSessionTitle.Text = $"{connection.Title} ({connection.FullAddress})";
            _btnDisconnect.Enabled = true;
            _btnReconnect.Enabled = true;
        }
        else
        {
            _lblSessionTitle.Text = "No active connection";
            _lblStatusBadge.Text = "● Disconnected";
            _lblStatusBadge.ForeColor = Color.FromArgb(160, 165, 175);
            _btnDisconnect.Enabled = false;
            _btnReconnect.Enabled = false;
            _btnConnect.Enabled = _selectedServer != null;
        }
    }

    private void MainForm_KeyDown(object? sender, KeyEventArgs e)
    {
        // Ctrl+B: Toggle Sidebar
        if (e.Control && e.KeyCode == Keys.B)
        {
            SetSidebarCollapsed(!_splitContainer.Panel1Collapsed);
            e.Handled = true;
        }
        // F2: Edit selected server
        else if (e.KeyCode == Keys.F2 && _selectedServer != null)
        {
            BtnEditServer_Click(this, EventArgs.Empty);
            e.Handled = true;
        }
        // Delete: Delete selected
        else if (e.KeyCode == Keys.Delete && (_selectedServer != null || _treeServers.SelectedNode?.Tag is string))
        {
            BtnDeleteServer_Click(this, EventArgs.Empty);
            e.Handled = true;
        }
        // F5: Reconnect
        else if (e.KeyCode == Keys.F5)
        {
            BtnReconnect_Click(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        // Disconnect all active sessions cleanly (RdpSessionManager.Dispose handles this)
        _sessionManager.Dispose();

        // Save current window and layout settings
        _settings.SplitterDistance = _splitContainer.SplitterDistance > 100 ? _splitContainer.SplitterDistance : _savedSplitterDistance;
        _settings.SidebarCollapsed = _splitContainer.Panel1Collapsed;
        _settings.WindowWidth = Width;
        _settings.WindowHeight = Height;
        _settings.WindowMaximized = WindowState == FormWindowState.Maximized;

        SaveConfiguration();
    }

    private void SaveConfiguration()
    {
        try
        {
            _iniManager.Save(_settings, _servers, _credentialProfiles);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Failed to save INI configuration: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static ImageList CreateImageList()
    {
        var list = new ImageList
        {
            ImageSize = new Size(16, 16),
            ColorDepth = ColorDepth.Depth32Bit
        };

        // 0: Folder (Group)
        list.Images.Add(CreateFolderIcon());
        // 1: Computer / Server (Connection)
        list.Images.Add(CreateServerIcon());

        return list;
    }

    private static Bitmap CreateFolderIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var brushTab = new SolidBrush(Color.FromArgb(235, 175, 55));
        using var brushBody = new SolidBrush(Color.FromArgb(250, 195, 75));
        using var penBorder = new Pen(Color.FromArgb(190, 135, 25), 1);

        g.FillRectangle(brushTab, 1, 3, 6, 4);
        g.FillRoundRectangle(brushBody, new Rectangle(1, 5, 14, 9), 2);
        g.DrawRoundRectangle(penBorder, new Rectangle(1, 5, 14, 9), 2);

        return bmp;
    }

    private void InitializeComponent()
    {

    }

    private static Bitmap CreateServerIcon()
    {
        var bmp = new Bitmap(16, 16);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using var brushScreen = new SolidBrush(Color.FromArgb(0, 120, 215));
        using var brushBase = new SolidBrush(Color.FromArgb(100, 110, 125));
        using var penScreen = new Pen(Color.FromArgb(20, 60, 110), 1);

        // Screen bezel
        g.FillRoundRectangle(brushScreen, new Rectangle(1, 2, 14, 9), 2);
        g.DrawRoundRectangle(penScreen, new Rectangle(1, 2, 14, 9), 2);

        // Inner display reflection
        using var innerBrush = new SolidBrush(Color.FromArgb(100, 255, 255, 255));
        g.FillRectangle(innerBrush, 3, 4, 10, 5);

        // Stand / Base
        g.FillRectangle(brushBase, 7, 11, 2, 3);
        g.FillRectangle(brushBase, 4, 13, 8, 2);

        return bmp;
    }
}

internal static class GraphicsExtensions
{
    public static void FillRoundRectangle(this Graphics g, Brush brush, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectanglePath(bounds, radius);
        g.FillPath(brush, path);
    }

    public static void DrawRoundRectangle(this Graphics g, Pen pen, Rectangle bounds, int radius)
    {
        using var path = CreateRoundedRectanglePath(bounds, radius);
        g.DrawPath(pen, path);
    }

    private static GraphicsPath CreateRoundedRectanglePath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = radius * 2;
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));

        // Top left
        path.AddArc(arc, 180, 90);
        // Top right
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        // Bottom right
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        // Bottom left
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }
}
