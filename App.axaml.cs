using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading; // Required for Dispatcher
using dbm_select.ViewModels;
using dbm_select.Views;
using System.Linq;
using System.Threading.Tasks; // Required for Task

namespace dbm_select
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations
                DisableAvaloniaDataAnnotationValidation();

                // 1. Create and Show Splash Screen FIRST
                var splashScreen = new SplashWindow();
                desktop.MainWindow = splashScreen;
                splashScreen.Show();

                // 2. Run the Loading Logic in the background
                Task.Run(async () =>
                {
                    // Simulate heavy loading work (e.g., 3 seconds)
                    await Task.Delay(3000);

                    // 3. Switch to Main Window (Must be done on UI Thread)
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        // Initialize ViewModel
                        var vm = new MainWindowViewModel();
                        // Note: The ViewModel constructor likely loads images by default, 
                        // but we keep your specific path here to be safe.
                        vm.LoadImages(@"C:\Users\Phil\Pictures");

                        // Create the real Main Window
                        var mainWindow = new MainWindow
                        {
                            DataContext = vm,
                        };

                        // Swap the main window reference
                        desktop.MainWindow = mainWindow;
                        mainWindow.Show();

                        // Close the splash screen
                        splashScreen.Close();
                    });
                });
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}