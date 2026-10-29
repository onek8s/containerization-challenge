using System;
using System.Threading.Tasks;
using log4net;
using Wingtip.FieldSales.Client.Services;

namespace Wingtip.FieldSales.Client.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(LoginViewModel));
        private readonly ApiClient _api;
        private string _userName;
        private string _error;
        private bool _busy;

        public LoginViewModel(ApiClient api, string lastUser)
        {
            _api = api;
            _userName = lastUser;
            SignInCommand = new AsyncCommand(SignInAsync, () => !_busy && !string.IsNullOrWhiteSpace(UserName));
        }

        public event EventHandler SignedIn;

        /// <summary>Set by the view from the PasswordBox; WPF does not allow binding to the password.</summary>
        public Func<string> PasswordProvider { get; set; }

        public string UserName
        {
            get { return _userName; }
            set { Set(ref _userName, value); }
        }

        public string Error
        {
            get { return _error; }
            set { Set(ref _error, value); }
        }

        public bool Busy
        {
            get { return _busy; }
            set { Set(ref _busy, value); }
        }

        public string Version
        {
            get { return "Version " + DeploymentInfo.Version; }
        }

        public AsyncCommand SignInCommand { get; private set; }

        private async Task SignInAsync()
        {
            Busy = true;
            Error = null;
            try
            {
                await _api.SignInAsync(UserName.Trim().ToLowerInvariant(), PasswordProvider == null ? "" : PasswordProvider());
                Log.InfoFormat("{0} signed in", UserName);
                var handler = SignedIn;
                if (handler != null) handler(this, EventArgs.Empty);
            }
            catch (SignInRequiredException ex)
            {
                Error = ex.Message;
            }
            catch (OfflineException ex)
            {
                Error = ex.Message + " The first sign-in on this laptop needs a connection to the office (VPN or office Wi-Fi).";
            }
            catch (ApiException ex)
            {
                Log.Error("Sign-in failed", ex);
                Error = "Sign-in failed: " + ex.Message;
            }
            finally
            {
                Busy = false;
            }
        }
    }
}
