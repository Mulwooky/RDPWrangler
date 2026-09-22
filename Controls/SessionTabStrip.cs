using System.Drawing.Drawing2D;

namespace RDPWrangler.Controls;

/// <summary>
/// A custom-painted tab strip that shows one tab per open RDP session.
/// Each tab has a label and an × close button. A + button on the right opens a new connection.
/// </summary>
public class SessionTabStrip : Control
{
    // ── Public events ─────────────────────────────────────────────────────────
    public event EventHandler<string>? TabSelected;   // sessionId
    public event EventHandler<string>? TabClosed;     // sessionId

    // ── Appearance constants ──────────────────────────────────────────────────
    private const int TabHeight       = 36;
    private const int TabPadding      = 14;   // horizontal padding inside tab label
    private const int CloseButtonSize = 16;
    private const int CloseButtonMargin = 6;
    private const int MinTabWidth     = 80;
    private const int MaxTabWidth     = 200;

    private static readonly Color ColBackground    = Color.FromArgb(28, 32, 40);
    private static readonly Color ColTabInactive   = Color.FromArgb(40, 46, 58);
    private static readonly Color ColTabActive     = Color.FromArgb(55, 65, 80);
    private static readonly Color ColTabHover      = Color.FromArgb(48, 55, 68);
    private static readonly Color ColTabBorder     = Color.FromArgb(70, 80, 95);
    private static readonly Color ColActiveAccent  = Color.FromArgb(0, 122, 204);
    private static readonly Color ColTextActive    = Color.FromArgb(235, 240, 248);
    private static readonly Color ColTextInactive  = Color.FromArgb(155, 165, 180);
    private static readonly Color ColClose         = Color.FromArgb(180, 185, 195);
    private static readonly Color ColCloseHover    = Color.FromArgb(210, 70, 70);

    // ── Internal tab state ────────────────────────────────────────────────────
    private readonly record struct TabInfo(string SessionId, string Label, Rectangle Bounds, Rectangle CloseBounds);

    private readonly List<(string SessionId, string Label)> _tabs = new();
    private string? _activeSessionId;
    private List<TabInfo> _renderedTabs = new();

    private string? _hoveredSessionId;
    private string? _hoveredCloseSessionId;
    private int _scrollOffset;

    public SessionTabStrip()
    {
        Height = TabHeight;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = ColBackground;
        Cursor = Cursors.Hand;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public string? ActiveSessionId => _activeSessionId;

    public void AddTab(string sessionId, string label)
    {
        if (_tabs.Any(t => t.SessionId == sessionId)) return;
        _tabs.Add((sessionId, label));
        _activeSessionId = sessionId;
        _scrollOffset = 0;
        Invalidate();
    }

    public void RemoveTab(string sessionId)
    {
        int idx = _tabs.FindIndex(t => t.SessionId == sessionId);
        if (idx < 0) return;
        _tabs.RemoveAt(idx);

        if (_activeSessionId == sessionId)
        {
            // Activate the tab to the left, or the first one remaining
            if (_tabs.Count > 0)
                _activeSessionId = _tabs[Math.Max(0, idx - 1)].SessionId;
            else
                _activeSessionId = null;
        }

        _scrollOffset = 0;
        Invalidate();
    }

    public void SetActiveTab(string sessionId)
    {
        if (_tabs.Any(t => t.SessionId == sessionId))
        {
            _activeSessionId = sessionId;
            Invalidate();
        }
    }

    public void UpdateTabLabel(string sessionId, string newLabel)
    {
        int idx = _tabs.FindIndex(t => t.SessionId == sessionId);
        if (idx >= 0)
        {
            _tabs[idx] = (_tabs[idx].SessionId, newLabel);
            Invalidate();
        }
    }

    public bool HasTab(string sessionId) => _tabs.Any(t => t.SessionId == sessionId);

    public int TabCount => _tabs.Count;

    // ── Painting ──────────────────────────────────────────────────────────────

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode     = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

        g.Clear(ColBackground);

        _renderedTabs = new List<TabInfo>();
        int x = -_scrollOffset;

        using var fontNormal = new Font("Segoe UI", 9f);
        using var fontBold   = new Font("Segoe UI", 9f, FontStyle.Bold);
        using var sfLeft     = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter };

        foreach (var (sessionId, label) in _tabs)
        {
            bool isActive = sessionId == _activeSessionId;
            bool isHovered = sessionId == _hoveredSessionId;
            bool isCloseHovered = sessionId == _hoveredCloseSessionId;

            var font = isActive ? fontBold : fontNormal;

            // Measure label text width
            int textWidth = (int)g.MeasureString(label, font).Width;
            int tabWidth  = Math.Clamp(textWidth + TabPadding * 2 + CloseButtonSize + CloseButtonMargin + 4, MinTabWidth, MaxTabWidth);

            var tabBounds  = new Rectangle(x, 0, tabWidth, TabHeight - 1);
            var closeBounds = new Rectangle(tabBounds.Right - CloseButtonSize - CloseButtonMargin,
                                            (TabHeight - CloseButtonSize) / 2,
                                            CloseButtonSize, CloseButtonSize);

            if (tabBounds.Right > 0 && tabBounds.Left < Width) // only draw if in view
            {
                Color tabBack = isActive ? ColTabActive : (isHovered ? ColTabHover : ColTabInactive);

                using var tabBrush = new SolidBrush(tabBack);
                g.FillRectangle(tabBrush, tabBounds);

                // Active accent line on top
                if (isActive)
                {
                    using var accentBrush = new SolidBrush(ColActiveAccent);
                    g.FillRectangle(accentBrush, new Rectangle(tabBounds.X, tabBounds.Y, tabBounds.Width, 2));
                }

                // Right border between tabs
                using var borderPen = new Pen(ColTabBorder, 1);
                g.DrawLine(borderPen, tabBounds.Right, tabBounds.Top, tabBounds.Right, tabBounds.Bottom);

                // Label (leaving room for the close button)
                var labelBounds = new Rectangle(tabBounds.X + TabPadding, tabBounds.Y,
                                                tabBounds.Width - TabPadding - CloseButtonSize - CloseButtonMargin - 4,
                                                TabHeight - 1);

                using var textBrush = new SolidBrush(isActive ? ColTextActive : ColTextInactive);
                g.DrawString(label, font, textBrush, labelBounds, sfLeft);

                // Close × button
                Color closeColor = isCloseHovered ? ColCloseHover : (isHovered || isActive ? ColClose : Color.FromArgb(90, 95, 105));
                using var closeBrush = new SolidBrush(closeColor);

                if (isHovered || isActive || isCloseHovered)
                {
                    if (isCloseHovered)
                    {
                        g.FillEllipse(new SolidBrush(Color.FromArgb(60, 210, 70, 70)), closeBounds);
                    }
                    DrawCloseX(g, closeBrush, closeBounds);
                }
            }

            _renderedTabs.Add(new TabInfo(sessionId, label, tabBounds, closeBounds));
            x += tabWidth;
        }

        // Bottom separator line
        using var sepPen = new Pen(ColTabBorder, 1);
        g.DrawLine(sepPen, 0, TabHeight - 1, Width, TabHeight - 1);
    }

    private static void DrawCloseX(Graphics g, Brush brush, Rectangle r)
    {
        float pad = 4f;
        float x1 = r.X + pad, y1 = r.Y + pad;
        float x2 = r.Right - pad, y2 = r.Bottom - pad;

        using var pen = new Pen(((SolidBrush)brush).Color, 1.8f);
        pen.StartCap = pen.EndCap = LineCap.Round;
        g.DrawLine(pen, x1, y1, x2, y2);
        g.DrawLine(pen, x2, y1, x1, y2);
    }

    // ── Mouse handling ────────────────────────────────────────────────────────

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string? newHover      = null;
        string? newCloseHover = null;

        foreach (var tab in _renderedTabs)
        {
            if (tab.Bounds.Contains(e.Location))
            {
                newHover = tab.SessionId;
                if (tab.CloseBounds.Contains(e.Location))
                    newCloseHover = tab.SessionId;
                break;
            }
        }

        if (newHover != _hoveredSessionId || newCloseHover != _hoveredCloseSessionId)
        {
            _hoveredSessionId      = newHover;
            _hoveredCloseSessionId = newCloseHover;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hoveredSessionId      = null;
        _hoveredCloseSessionId = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (e.Button != MouseButtons.Left) return;

        foreach (var tab in _renderedTabs)
        {
            if (tab.CloseBounds.Contains(e.Location))
            {
                TabClosed?.Invoke(this, tab.SessionId);
                return;
            }
            if (tab.Bounds.Contains(e.Location))
            {
                _activeSessionId = tab.SessionId;
                Invalidate();
                TabSelected?.Invoke(this, tab.SessionId);
                return;
            }
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        _scrollOffset = Math.Max(0, _scrollOffset - e.Delta / 3);
        Invalidate();
    }
}
