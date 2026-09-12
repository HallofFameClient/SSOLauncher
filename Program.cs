namespace SSOLauncher;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        // To customize application configuration such as set high DPI settings or default font,
        // see https://aka.ms/applicationconfiguration.
        ApplicationConfiguration.Initialize();

        // Ohne diese Handler crasht die App bei unbehandelten Fehlern stillschweigend
        // (Prozess verschwindet ohne Meldung). Stattdessen: loggen + Dialog zeigen.
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => HandleFatal(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => HandleFatal(e.ExceptionObject as Exception);

        Application.Run(new Form1());
    }

    private static void HandleFatal(Exception? ex)
    {
        try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "last-error.txt"), $"[{DateTime.Now}] FATAL\n{ex}"); } catch { }
        try
        {
            MessageBox.Show(
                "An unexpected error occurred and was written to last-error.txt.\n\n" + ex?.Message,
                "SSO Custom Launcher - Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch { }
    }
}
