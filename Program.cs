using RDPWrangler.Forms;

namespace RDPWrangler;

internal static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "debug.log");
        try
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Program.Main starting...\n");
            ApplicationConfiguration.Initialize();
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] ApplicationConfiguration initialized.\n");
            var mainForm = new MainForm();
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] MainForm instantiated. Calling Application.Run...\n");
            Application.Run(mainForm);
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] Application.Run exited normally.\n");
        }
        catch (Exception ex)
        {
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] FATAL ERROR: {ex}\n");
            MessageBox.Show($"Startup failed:\n{ex}", "RDPWrangler Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
