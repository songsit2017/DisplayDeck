using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace DisplayManagerClone;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static void LogException(string type, Exception ex)
    {
        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string logFolder = Path.Combine(appData, "DisplayManagerClone");
            Directory.CreateDirectory(logFolder);
            string logFile = Path.Combine(logFolder, "crash.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{type}] {ex?.ToString()}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logFile, entry);
        }
        catch { }
    }

    private static bool IsFatalException(Exception ex)
    {
        return ex is OutOfMemoryException ||
               ex is StackOverflowException ||
               ex is AccessViolationException ||
               ex is System.Runtime.InteropServices.SEHException;
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI Exceptions
        this.DispatcherUnhandledException += (s, args) =>
        {
            LogException("DispatcherUnhandledException", args.Exception);

            if (IsFatalException(args.Exception))
            {
                MessageBox.Show($"A critical unrecoverable error occurred:\n{args.Exception.Message}\n\nThe application will now close.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = false;
                Current?.Shutdown(-1);
            }
            else
            {
                MessageBox.Show($"An unexpected UI error occurred:\n{args.Exception.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                args.Handled = true;
            }
        };

        // Non-UI Background Exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Exception? ex = args.ExceptionObject as Exception;
            if (ex != null)
            {
                LogException("AppDomainUnhandledException", ex);
            }

            MessageBox.Show($"A critical background error occurred:\n{ex?.Message}\n\nThe application will now close.", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }
}

