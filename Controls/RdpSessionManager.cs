using RDPWrangler.Models;

namespace RDPWrangler.Controls;

/// <summary>
/// Manages multiple concurrent RDP sessions. Each session lives in its own
/// <see cref="RdpClientHost"/> control that is shown/hidden without being destroyed.
/// A <see cref="SessionTabStrip"/> provides tab-based navigation between sessions.
/// </summary>
public class RdpSessionManager : UserControl
{
    // ── Events ────────────────────────────────────────────────────────────────
    /// <summary>Raised whenever the status of the currently-active session changes.</summary>
    public event Action<string, bool, bool>? StatusChanged;

    /// <summary>Raised when the active tab changes (sessionId, connection or null).</summary>
    public event Action<RdpServerConnection?>? ActiveSessionChanged;

    // ── Child controls ────────────────────────────────────────────────────────
    private readonly SessionTabStrip _tabStrip;
    private readonly Panel _hostArea;        // fills below the tab strip
    private readonly Panel _welcomeOverlay;  // shown when no sessions are open

    // ── Session state ─────────────────────────────────────────────────────────
    private readonly List<RdpClientHost> _hosts = new();
    private RdpClientHost? _activeHost;

    // ── Public state ──────────────────────────────────────────────────────────
    public RdpServerConnection? CurrentConnection => _activeHost?.CurrentConnection;
    public bool IsConnected   => _activeHost?.IsConnected  ?? false;
    public bool IsConnecting  => _activeHost?.IsConnecting ?? false;
    public int  SessionCount  => _hosts.Count;

    public RdpSessionManager()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(24, 24, 24);

        // ── Tab strip ────────────────────────────────────────────────────────
        _tabStrip = new SessionTabStrip
        {
            Dock = DockStyle.Top
        };
        _tabStrip.TabSelected += OnTabSelected;
        _tabStrip.TabClosed   += OnTabClosed;

        // ── Host area ────────────────────────────────────────────────────────
        _hostArea = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 24, 24)
        };

        // ── Welcome overlay ───────────────────────────────────────────────────
        _welcomeOverlay = BuildWelcomeOverlay();

        _hostArea.Controls.Add(_welcomeOverlay);

        Controls.Add(_hostArea);
        Controls.Add(_tabStrip);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a new session for <paramref name="connection"/>, or switches to
    /// an existing session without reconnecting if one is already open.
    /// </summary>
    public void OpenOrSwitch(RdpServerConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var existing = _hosts.FirstOrDefault(h => h.SessionId == connection.Id);
        if (existing != null)
        {
            // Session already exists — just bring it to the front.
            ActivateHost(existing);
            return;
        }

        // ── Create a new host ─────────────────────────────────────────────────
        var host = new RdpClientHost
        {
            Dock      = DockStyle.Fill,
            SessionId = connection.Id,
            // Must be Visible = true when added to _hostArea so WinForms' DefaultLayout engine
            // applies the Dock = Fill bounds immediately. With Visible = false the layout engine
            // skips the control entirely, leaving Width/Height at 0; Connect() would then fall
            // back to the 1024×768 minimum rather than using the real panel size.
            Visible   = true
        };
        host.StatusChanged += (msg, connected, error) => OnHostStatusChanged(host, msg, connected, error);

        // Hide all existing hosts before adding the new one so there is no visual flash
        // while layout runs. ActivateHost will show exactly the right host afterwards.
        foreach (var h in _hosts)
            h.Visible = false;

        _hosts.Add(host);
        _hostArea.Controls.Add(host);   // layout runs here; host now has correct Dock bounds
        host.BringToFront();

        _tabStrip.AddTab(connection.Id, connection.Title);
        _welcomeOverlay.Visible = false;

        // Connect first — _currentConnection is set synchronously (TCP dial-out is async),
        // so ActivateHost can read the correct connection when it raises ActiveSessionChanged.
        host.Connect(connection);
        ActivateHost(host);
    }

    /// <summary>Disconnects the currently visible session.</summary>
    public void DisconnectCurrent()
    {
        _activeHost?.Disconnect();
    }

    /// <summary>Reconnects the currently visible session.</summary>
    public void ReconnectCurrent()
    {
        if (_activeHost?.CurrentConnection != null)
            _activeHost.Connect(_activeHost.CurrentConnection);
    }

    /// <summary>Disconnects and removes the session with <paramref name="sessionId"/>.</summary>
    public void CloseSession(string sessionId)
    {
        var host = _hosts.FirstOrDefault(h => h.SessionId == sessionId);
        if (host == null) return;

        host.Disconnect();

        // Remove from tab strip first so the strip picks the new active tab
        _tabStrip.RemoveTab(sessionId);
        _hosts.Remove(host);
        _hostArea.Controls.Remove(host);
        host.Dispose();

        // Activate whatever tab the strip has selected
        if (_tabStrip.ActiveSessionId != null)
        {
            var next = _hosts.FirstOrDefault(h => h.SessionId == _tabStrip.ActiveSessionId);
            if (next != null)
            {
                ActivateHost(next);
                return;
            }
        }

        // No sessions left
        _activeHost = null;
        _welcomeOverlay.Visible = true;
        ActiveSessionChanged?.Invoke(null);
        StatusChanged?.Invoke("No active connection", false, false);
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private void ActivateHost(RdpClientHost host)
    {
        foreach (var h in _hosts)
            h.Visible = false;

        host.Visible = true;
        host.BringToFront();
        _activeHost = host;
        _tabStrip.SetActiveTab(host.SessionId!);
        _welcomeOverlay.Visible = false;

        ActiveSessionChanged?.Invoke(host.CurrentConnection);

        // Immediately push the current status of this session upward
        if (host.IsConnected)
            StatusChanged?.Invoke($"Connected to {host.CurrentConnection?.Title} ({host.CurrentConnection?.FullAddress})", true, false);
        else if (host.IsConnecting)
            StatusChanged?.Invoke($"Connecting to {host.CurrentConnection?.Title}...", false, false);
        else if (host.CurrentConnection != null)
            StatusChanged?.Invoke($"Session closed — {host.CurrentConnection.Title}", false, false);
        else
            StatusChanged?.Invoke("Disconnected", false, false);
    }

    private void OnTabSelected(object? sender, string sessionId)
    {
        var host = _hosts.FirstOrDefault(h => h.SessionId == sessionId);
        if (host != null) ActivateHost(host);
    }

    private void OnTabClosed(object? sender, string sessionId)
    {
        CloseSession(sessionId);
    }

    private void OnHostStatusChanged(RdpClientHost host, string message, bool isConnected, bool isError)
    {
        // Only forward status to MainForm for the currently active host
        if (host == _activeHost)
        {
            StatusChanged?.Invoke(message, isConnected, isError);
        }

        // Keep the tab label up-to-date with the connection title
        if (host.CurrentConnection != null)
        {
            string label = isConnected
                ? host.CurrentConnection.Title
                : (isError ? $"⚠ {host.CurrentConnection.Title}" : host.CurrentConnection.Title);

            _tabStrip.UpdateTabLabel(host.SessionId!, label);
        }
    }

    private Panel BuildWelcomeOverlay()
    {
        var overlay = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = Color.FromArgb(28, 30, 36)
        };

        var content = new Panel
        {
            Size      = new Size(500, 180),
            BackColor = Color.Transparent
        };
        overlay.Resize += (_, _) =>
        {
            content.Location = new Point(
                Math.Max(0, (overlay.Width  - content.Width)  / 2),
                Math.Max(0, (overlay.Height - content.Height) / 2));
        };

        var title = new Label
        {
            Text      = "🖥️  RDPWrangler",
            Font      = new Font("Segoe UI", 20f, FontStyle.Bold),
            ForeColor = Color.FromArgb(235, 240, 250),
            Location  = new Point(0, 0),
            Size      = new Size(500, 50),
            TextAlign = ContentAlignment.MiddleCenter
        };

        var subtitle = new Label
        {
            Text      = "Select a server and click Connect, or double-click a connection.\n" +
                        "Each session opens as a tab — switch between them without reconnecting.",
            Font      = new Font("Segoe UI", 10.5f),
            ForeColor = Color.FromArgb(155, 165, 180),
            Location  = new Point(0, 60),
            Size      = new Size(500, 70),
            TextAlign = ContentAlignment.MiddleCenter
        };

        content.Controls.Add(title);
        content.Controls.Add(subtitle);
        overlay.Controls.Add(content);
        return overlay;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var host in _hosts.ToList())
            {
                host.Disconnect();
                host.Dispose();
            }
            _hosts.Clear();
        }
        base.Dispose(disposing);
    }
}
