using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using log4net;
using Wingtip.FieldSales.Client.Services;
using Wingtip.FieldSales.Client.ViewModels;
using Wingtip.FieldSales.Client.Views;
using Wingtip.FieldSales.Contracts;

namespace Wingtip.FieldSales.Client
{
    public partial class App : Application
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(App));
        private ClientSettings _settings;
        private ApiClient _api;
        private TokenStore _tokens;

        private async void OnStartup(object sender, StartupEventArgs e)
        {
            log4net.Config.XmlConfigurator.Configure();
            Log.InfoFormat("Field Sales {0} starting on {1} as {2}, API {3}, deployed from {4}", DeploymentInfo.Version,
                Environment.MachineName, Environment.UserName, ClientSettings.Load().ApiBaseUrl, DeploymentInfo.UpdateLocation);

            _settings = ClientSettings.Load();
            Directory.CreateDirectory(LocalStore.RootFolder);
            _tokens = new TokenStore(LocalStore.RootFolder);
            _api = new ApiClient(_settings, _tokens);

            if (await OfferUpdateAsync()) return;

            if (_api.HasSession) await ShowMainAsync();
            else ShowLogin();
        }

        private void ShowLogin()
        {
            var vm = new LoginViewModel(_api, _api.UserName ?? Environment.UserName.ToLowerInvariant());
            var window = new LoginWindow { DataContext = vm };
            vm.SignedIn += async (s, a) =>
            {
                window.Hide();
                await ShowMainAsync();
                window.Close();
            };
            window.Closed += (s, a) => { if (MainWindow == null || MainWindow == window) Shutdown(); };
            MainWindow = window;
            window.Show();
        }

        private async Task ShowMainAsync()
        {
            MeResponse me = null;
            try
            {
                me = await _api.GetMeAsync();
            }
            catch (OfflineException ex)
            {
                Log.Info("Starting offline: " + ex.Message);
            }
            catch (SignInRequiredException)
            {
                ShowLogin();
                return;
            }

            var store = new LocalStore(_api.UserName);
            var vm = new MainViewModel(_api, store, _settings, me);
            var window = new MainWindow { DataContext = vm };
            vm.SignInRequired += (s, a) =>
            {
                vm.Stop();
                MainWindow = null;
                window.Close();
                ShowLogin();
            };
            vm.UpdateRequired += async (s, a) => await OfferUpdateAsync();
            window.Closed += (s, a) => { vm.Stop(); if (MainWindow == window) Shutdown(); };

            MainWindow = window;
            window.Show();
            await vm.SyncAsync();
        }

        /// <returns>True when the application restarts into a new version.</returns>
        private async Task<bool> OfferUpdateAsync()
        {
            var available = await Task.Run(() => DeploymentInfo.IsUpdateAvailable());
            if (!available) return false;

            var answer = MessageBox.Show("A new version of Field Sales is available. Install it now?\n\nOrders on this laptop are kept.",
                "Field Sales", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (answer != MessageBoxResult.Yes) return false;

            if (!await Task.Run(() => DeploymentInfo.Update())) return false;

            System.Windows.Forms.Application.Restart();
            Shutdown();
            return true;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Error("Unhandled error", e.Exception);
            MessageBox.Show("Something went wrong: " + e.Exception.Message + "\n\nThe details are in the log file under " +
                Path.Combine(LocalStore.RootFolder, "Logs"), "Field Sales", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
