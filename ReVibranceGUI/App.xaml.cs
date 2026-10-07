using System.Threading;
using System.Windows;
using ReVibranceGUI.Services;

namespace ReVibranceGUI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        // R7: two instances would fight over the GPU vibrance setting.
        private const string MutexName = @"Local\ReVibranceGUI.SingleInstance";
        private Mutex? _instanceMutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _instanceMutex = new Mutex(initiallyOwned: true, MutexName, out bool createdNew);
            if (!createdNew)
            {
                System.Windows.MessageBox.Show(
                    "ReVibranceGUI is already running.\nCheck the system tray.",
                    "ReVibranceGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                _instanceMutex.Dispose();
                _instanceMutex = null;
                Shutdown();
                return;
            }

            DispatcherUnhandledException += (_, args) =>
            {
                Logger.Error("Unhandled UI exception", args.Exception);
            };
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                Logger.Error("Unhandled exception", args.ExceptionObject as Exception);
            };

            Logger.Info($"ReVibranceGUI {typeof(App).Assembly.GetName().Version} starting.");
            new MainWindow().Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_instanceMutex != null)
            {
                _instanceMutex.ReleaseMutex();
                _instanceMutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
