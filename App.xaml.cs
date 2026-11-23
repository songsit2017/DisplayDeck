using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Velopack;
using Velopack.Sources;

namespace DisplayManagerClone;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private UpdateManager? _updates;
    private DispatcherTimer? _updateTimer;
    private Task<string>? _updateCheck;

    private async void CheckForUpdates(object? sender, EventArgs e)
    {
        try { await CheckForUpdatesAsync(); }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("DisplayDeck update check failed: {0}", ex.Message);
        }
    }

    internal Task<string> CheckForUpdatesAsync()
    {
        if (_updates == null)
            return Task.FromResult("Automatic updates are available when DisplayDeck is installed using the Windows Setup executable.");
        if (_updateCheck is { IsCompleted: false }) return _updateCheck;
        return _updateCheck = DownloadAvailableUpdateAsync(_updates);
    }

    private static async Task<string> DownloadAvailableUpdateAsync(UpdateManager updates)
    {
        try
        {
            var update = await updates.CheckForUpdatesAsync();
            if (update != null && !Equals(updates.UpdatePendingRestart?.Version, update.TargetFullRelease.Version))
                await updates.DownloadUpdatesAsync(update);
        }
        catch when (updates.UpdatePendingRestart != null)
        {
            // An already downloaded update can still be installed without a connection.
        }
        return updates.UpdatePendingRestart is { } pending
            ? $"Version {pending.Version} is ready. Select Exit from the DisplayDeck tray menu to install it, then reopen the app."
            : $"DisplayDeck {updates.CurrentVersion} is up to date.";
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _updateTimer?.Stop();
        try
        {
            if (_updates?.UpdatePendingRestart is { } pending)
                _updates.WaitExitThenApplyUpdates(pending, silent: true, restart: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("DisplayDeck update apply failed: {0}", ex.Message);
        }
        base.OnExit(e);
    }

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

        try
        {
            var updates = new UpdateManager(new GithubSource("https://github.com/songsit2017/DisplayDeck", null, false));
            if (updates.IsInstalled)
            {
                _updates = updates;
                _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
                _updateTimer.Tick += CheckForUpdates;
                _updateTimer.Start();
                Dispatcher.BeginInvoke(() => CheckForUpdates(null, EventArgs.Empty), DispatcherPriority.ApplicationIdle);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceWarning("DisplayDeck updater initialization failed: {0}", ex.Message);
        }

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

