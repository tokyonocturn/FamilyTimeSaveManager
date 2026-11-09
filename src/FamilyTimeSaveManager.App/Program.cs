using FamilyTimeSaveManager.Core;

namespace FamilyTimeSaveManager.App;

internal static class Program
{
    public const string ApplicationName = "FamilyTime Save Manager";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0].Equals("--diagnostics", StringComparison.OrdinalIgnoreCase))
        {
            return Diagnostics.Run(args.Skip(1).ToArray());
        }

        if (args.Length >= 3 && args[0].Equals("--screenshot", StringComparison.OrdinalIgnoreCase))
        {
            return Screenshot.Capture(args[1], args[2]);
        }

        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var mutex = new Mutex(initiallyOwned: true, @"Local\FamilyTimeSaveManager.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show(
                "FamilyTime Save Manager is already running.",
                ApplicationName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return 0;
        }

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        using var services = AppServices.Create();
        var errors = new List<string>();

        Application.ThreadException += (_, e) =>
        {
            errors.Add(e.Exception.ToString());
            services.Log.Error("ui", e.Exception.Message);
            MessageBox.Show(
                "Something went wrong:\r\n\r\n" + e.Exception.Message +
                "\r\n\r\nThe details were written to the activity log in the data folder.",
                ApplicationName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                services.Log.Error("app", "Unhandled: " + exception);
            }
        };

        Application.Run(new MainForm(services));
        GC.KeepAlive(errors);
        return 0;
    }
}
