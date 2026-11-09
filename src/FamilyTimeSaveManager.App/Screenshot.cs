using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FamilyTimeSaveManager.Core;

namespace FamilyTimeSaveManager.App;

/// <summary>
/// Developer/CI helper: opens the app, runs a scan, navigates to a page and saves a real window
/// capture. Used to produce the screenshots in docs/screenshots without clicking through the UI.
/// </summary>
internal static class Screenshot
{
    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static int Capture(string page, string outputPath)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        Directory.SetCurrentDirectory(AppContext.BaseDirectory);

        using var services = AppServices.Create();
        using var form = new MainForm(services);
        var exitCode = 0;

        form.Shown += async (_, _) =>
        {
            try
            {
                await form.InitialScan;
                form.NavigateTo(page);
                form.PrepareScreenshot();
                form.TopMost = true;
                form.Activate();
                form.BringToFront();
                await Task.Delay(1200);
                form.Refresh();
                CaptureWindow(form, outputPath);
                form.TopMost = false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                exitCode = 1;
            }
            finally
            {
                form.Close();
            }
        };

        Application.Run(form);
        return exitCode;
    }

    private static void CaptureWindow(Form form, string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        GetWindowRect(form.Handle, out var rect);
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width <= 0 || height <= 0)
        {
            throw new InvalidOperationException("The window has no drawable area.");
        }

        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(0, 0, form.Width, form.Height));
        bitmap.Save(fullPath, ImageFormat.Png);
        Console.WriteLine($"Saved {fullPath} ({width}x{height})");
    }
}
