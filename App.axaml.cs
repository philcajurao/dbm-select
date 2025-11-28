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

        var splashScreen = new SplashWindow();
        desktop.MainWindow = splashScreen;
        splashScreen.Show();

        // Use Task.Run(async () => ...) to manage the background tasks,
        // and await the Dispatcher call to ensure the UI change happens correctly.
        Task.Run(async () =>
        {
            await Task.Delay(3000);

            await Dispatcher.UIThread.InvokeAsync(async () => // Make this async and await the LoadImages call
            {
                var vm = new MainWindowViewModel();
                
                // 1. FIX CS4014: Await the call to LoadImages
                // We're loading the path found in the ViewModel's constructor logic now, 
                // so we call the loading logic here. 
                // Note: The path "C:\Users\Phil\Pictures" is obsolete here, 
                // as the VM constructor now decides the correct path using saved settings.
                
                // Since the VM constructor already starts the loading process based on settings,
                // we'll rely on that. We remove the redundant LoadImages call here.
                
                // Original redundant call was: vm.LoadImages(@"C:\Users\Phil\Pictures");

                var mainWindow = new MainWindow
                {
                    DataContext = vm,
                };

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
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