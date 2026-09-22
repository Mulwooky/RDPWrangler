namespace RDPWrangler.Models;

public class AppSettings
{
    public int SplitterDistance { get; set; } = 280;
    public bool SidebarCollapsed { get; set; } = false;
    public string LastConnectedServerId { get; set; } = string.Empty;
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 800;
    public bool WindowMaximized { get; set; } = false;
}
