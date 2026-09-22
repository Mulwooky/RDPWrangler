using System.ComponentModel;
using AxMSTSCLib;
using RDPWrangler.Models;

namespace RDPWrangler.Controls;

public class RdpClientHost : UserControl
{
    private AxMsRdpClient9NotSafeForScripting? _rdpClient;
    private RdpServerConnection? _currentConnection;
    private bool _isConnecting;
    private bool _isConnected;

    public event Action<string, bool, bool>? StatusChanged;

    /// <summary>Stable identifier that matches <see cref="RdpServerConnection.Id"/> for the connection this host was opened for.</summary>
    public string? SessionId { get; set; }

    public bool IsConnected => _isConnected;
    public bool IsConnecting => _isConnecting;
    public RdpServerConnection? CurrentConnection => _currentConnection;

    public RdpClientHost()
    {
        DoubleBuffered = true;
        BackColor = Color.FromArgb(30, 30, 30);
    }

    public void Connect(RdpServerConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        // Clean up any existing connection first
        Disconnect();
        DestroyClient();

        _currentConnection = connection.Clone();
        _isConnecting = true;
        _isConnected = false;

        NotifyStatus($"Connecting to {_currentConnection.Title} ({_currentConnection.FullAddress})...", isConnected: false, isError: false);

        try
        {
            _rdpClient = new AxMsRdpClient9NotSafeForScripting();
            ((ISupportInitialize)_rdpClient).BeginInit();

            _rdpClient.Dock = DockStyle.Fill;
            Controls.Add(_rdpClient);

            ((ISupportInitialize)_rdpClient).EndInit();

            // Wire up ActiveX event handlers
            _rdpClient.OnConnecting += OnConnectingHandler;
            _rdpClient.OnConnected += OnConnectedHandler;
            _rdpClient.OnDisconnected += OnDisconnectedHandler;
            _rdpClient.OnFatalError += OnFatalErrorHandler;
            _rdpClient.OnLogonError += OnLogonErrorHandler;
            _rdpClient.OnWarning += OnWarningHandler;

            // Configure connection parameters
            _rdpClient.Server = _currentConnection.Host;
            if (!string.IsNullOrWhiteSpace(_currentConnection.Username))
            {
                _rdpClient.UserName = _currentConnection.Username;
            }
            if (!string.IsNullOrWhiteSpace(_currentConnection.Domain))
            {
                _rdpClient.Domain = _currentConnection.Domain;
            }

            // Desktop sizing and smart sizing
            _rdpClient.DesktopWidth = Math.Max(Width, 1024);
            _rdpClient.DesktopHeight = Math.Max(Height, 768);
            _rdpClient.ColorDepth = 32;

            var adv = _rdpClient.AdvancedSettings9;
            adv.RDPPort = _currentConnection.Port > 0 ? _currentConnection.Port : 3389;
            adv.SmartSizing = _currentConnection.SmartSizing;
            adv.DisplayConnectionBar = true;
            adv.EnableWindowsKey = 1;
            adv.RedirectClipboard = true;
            adv.EnableCredSspSupport = true;

            if (!string.IsNullOrEmpty(_currentConnection.Password))
            {
                adv.ClearTextPassword = _currentConnection.Password;
            }

            _rdpClient.Connect();
        }
        catch (Exception ex)
        {
            _isConnecting = false;
            _isConnected = false;
            NotifyStatus($"Connection error: {ex.Message}", isConnected: false, isError: true);
            DestroyClient();
        }
    }

    public void Disconnect()
    {
        if (_rdpClient != null)
        {
            try
            {
                if (_rdpClient.Connected == 1) // 1 = connected
                {
                    _rdpClient.Disconnect();
                }
            }
            catch
            {
                // Ignore exceptions during disconnect cleanup
            }
        }

        _isConnecting = false;
        _isConnected = false;
        NotifyStatus("Disconnected", isConnected: false, isError: false);
    }

    private void DestroyClient()
    {
        if (_rdpClient != null)
        {
            try
            {
                _rdpClient.OnConnecting -= OnConnectingHandler;
                _rdpClient.OnConnected -= OnConnectedHandler;
                _rdpClient.OnDisconnected -= OnDisconnectedHandler;
                _rdpClient.OnFatalError -= OnFatalErrorHandler;
                _rdpClient.OnLogonError -= OnLogonErrorHandler;
                _rdpClient.OnWarning -= OnWarningHandler;

                Controls.Remove(_rdpClient);
                _rdpClient.Dispose();
            }
            catch
            {
                // Best effort cleanup
            }
            finally
            {
                _rdpClient = null;
            }
        }
    }

    private void OnConnectingHandler(object? sender, EventArgs e)
    {
        _isConnecting = true;
        _isConnected = false;
        NotifyStatus($"Connecting to {_currentConnection?.Title}...", isConnected: false, isError: false);
    }

    private void OnConnectedHandler(object? sender, EventArgs e)
    {
        _isConnecting = false;
        _isConnected = true;
        NotifyStatus($"Connected to {_currentConnection?.Title} ({_currentConnection?.FullAddress})", isConnected: true, isError: false);
    }

    private void OnDisconnectedHandler(object? sender, IMsTscAxEvents_OnDisconnectedEvent e)
    {
        _isConnecting = false;
        _isConnected = false;

        string reason = FormatDisconnectReason(e.discReason);
        bool isError = e.discReason > 3; // 1 = Local, 2 = Remote by user, 3 = Remote by server (normal)

        NotifyStatus($"Session closed ({reason})", isConnected: false, isError: isError);
    }

    private void OnFatalErrorHandler(object? sender, IMsTscAxEvents_OnFatalErrorEvent e)
    {
        _isConnecting = false;
        _isConnected = false;
        NotifyStatus($"RDP Fatal Error code: {e.errorCode}", isConnected: false, isError: true);
    }

    private void OnLogonErrorHandler(object? sender, IMsTscAxEvents_OnLogonErrorEvent e)
    {
        NotifyStatus($"Logon error code: {e.lError}", isConnected: false, isError: true);
    }

    private void OnWarningHandler(object? sender, IMsTscAxEvents_OnWarningEvent e)
    {
        NotifyStatus($"Warning code: {e.warningCode}", isConnected: _isConnected, isError: false);
    }

    private void NotifyStatus(string message, bool isConnected, bool isError)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action(() => StatusChanged?.Invoke(message, isConnected, isError)));
        }
        else
        {
            StatusChanged?.Invoke(message, isConnected, isError);
        }
    }

    private static string FormatDisconnectReason(int code)
    {
        return code switch
        {
            1 => "Local user disconnected",
            2 => "Disconnected remotely by user",
            3 => "Disconnected by server",
            260 => "DNS host name could not be resolved",
            262 => "Out of memory",
            264 => "Connection timed out",
            516 => "Unable to connect to host (unreachable or port closed)",
            518 => "Out of memory",
            772 => "Security policy restriction",
            1028 => "Protocol security error",
            1286 => "Encryption negotiation failed",
            2308 => "Socket connection reset or closed",
            _ => $"Code {code}"
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disconnect();
            DestroyClient();
        }
        base.Dispose(disposing);
    }
}
