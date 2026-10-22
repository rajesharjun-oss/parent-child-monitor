using System.Diagnostics;
using System.Threading;
using System.Windows;
using ChildScreenTime.Windows.Services;
using ChildScreenTime.Windows.Views;

namespace ChildScreenTime.Windows;

public partial class App : System.Windows.Application, IDisposable
{
    private Mutex? singleInstanceMutex;
    private AppServices? services;
    private bool ownsSingleInstanceMutex;
    private bool disposed;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var configureOnly = e.Args.Any(argument => string.Equals(argument, "--configure", StringComparison.OrdinalIgnoreCase));
        var serviceManaged = e.Args.Any(argument => string.Equals(argument, "--service-managed", StringComparison.OrdinalIgnoreCase));

        singleInstanceMutex = new Mutex(initiallyOwned: true, @"Local\ChildScreenTime.Windows.Agent", out var isFirstInstance);
        ownsSingleInstanceMutex = isFirstInstance;
        if (!isFirstInstance)
        {
            System.Windows.MessageBox.Show("Child Screen Time is already running.", "Child Screen Time", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        try
        {
            services = new AppServices();
            RegisterErrorHandlers(services.Paths);

            if (!services.Settings.GetSnapshot().IsConfigured)
            {
                var setup = new SetupWindow(services);
                if (setup.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            if (configureOnly)
            {
                Shutdown();
                return;
            }

            StartupRegistrationService.EnsureAppropriateRegistration(serviceManaged);
            var mainWindow = new MainWindow(services, serviceManaged);
            MainWindow = mainWindow;
            services.Monitoring.Start();

            var backgroundStart = e.Args.Any(argument => string.Equals(argument, "--background", StringComparison.OrdinalIgnoreCase));
            if (!backgroundStart)
            {
                mainWindow.Show();
            }
        }
        catch (Exception exception)
        {
            System.Windows.MessageBox.Show(
                "Child Screen Time could not start securely. Its protected data may be unavailable or damaged. No monitoring has started.\n\n" + exception.Message,
                "Startup failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (services is not null)
        {
            services.Monitoring.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        Dispose();
        base.OnExit(e);
    }

    private void RegisterErrorHandlers(AppDataPaths paths)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            WriteDiagnostic(paths, args.Exception);
            System.Windows.MessageBox.Show("An unexpected error occurred. Monitoring will continue where possible.", "Child Screen Time", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                WriteDiagnostic(paths, exception);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            WriteDiagnostic(paths, args.Exception);
            args.SetObserved();
        };
    }

    private static void WriteDiagnostic(AppDataPaths paths, Exception exception)
    {
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O}\t{exception.GetType().Name}\t{exception.Message.ReplaceLineEndings(" ")}\n";
            File.AppendAllText(paths.DiagnosticLog, line);
            var log = new FileInfo(paths.DiagnosticLog);
            if (log.Length > 2 * 1024 * 1024)
            {
                File.WriteAllText(paths.DiagnosticLog, line);
            }
        }
        catch (Exception loggingException)
        {
            Debug.WriteLine(loggingException);
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        if (ownsSingleInstanceMutex)
        {
            singleInstanceMutex?.ReleaseMutex();
        }
        singleInstanceMutex?.Dispose();
        disposed = true;
        GC.SuppressFinalize(this);
    }
}
