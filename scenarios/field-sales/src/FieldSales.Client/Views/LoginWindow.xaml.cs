using System.Windows;
using Wingtip.FieldSales.Client.ViewModels;

namespace Wingtip.FieldSales.Client.Views
{
    public partial class LoginWindow : Window
    {
        public LoginWindow()
        {
            InitializeComponent();
            DataContextChanged += (s, e) =>
            {
                var vm = e.NewValue as LoginViewModel;
                if (vm != null) vm.PasswordProvider = () => Password.Password;
            };
        }
    }
}
