// ========== FILE: Program.cs ==========
using System;
using System.Windows.Forms;
using WebScrapeCron.Forms;

namespace WebScrapeCron
{
    /* ========================================================================
     * CLASS: Program
     * ========================================================================
     * Entry point for the application. Configures WinForms visual styles,
     * high-DPI support, and launches MainForm.
     *
     * CRITICAL: Global exception handlers ensure the app never crashes
     * silently. Without these, unhandled exceptions in async void methods
     * (like the Shown event handler) would kill the process with no UI.
     * ======================================================================== */

    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Catch unhandled exceptions on the UI thread
            Application.ThreadException += (s, e) =>
            {
                MessageBox.Show($"Unhandled UI error:\n\n{e.Exception}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // Catch unhandled exceptions on non-UI threads
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                {
                    MessageBox.Show($"Fatal error:\n\n{ex}", "Fatal Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.Run(new MainForm());
        }
    }
}
