using System.Configuration;
using System.Data;
using System.Windows;

namespace DisplayManagerClone;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // UI Exceptions
        this.DispatcherUnhandledException += (s, args) =>
        {
            MessageBox.Show($"UI Exception:\n{args.Exception.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // Non-UI Exceptions
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            Exception ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Background Exception:\n{ex?.Message}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
    }
}

