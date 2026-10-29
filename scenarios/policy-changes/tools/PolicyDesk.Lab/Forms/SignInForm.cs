namespace PolicyDesk.Lab.Forms;

public sealed class SignInForm : LabForm
{
    private readonly TextBox _user;
    private readonly TextBox _password;
    private readonly ComboBox _environment;

    public SignInForm(string environment)
    {
        Text = "PolicyDesk - Sign in";
        StartPosition = FormStartPosition.CenterScreen;
        _user = AddText("User ID:", 200);
        _password = AddField("Password:", new TextBox { UseSystemPasswordChar = true }, 200);
        _environment = AddList("Environment:", "PROD", "TEST");
        _environment.SelectedItem = _environment.Items.Contains(environment) ? environment : "PROD";
        var signIn = AddButtons("Sign in", "Exit");
        AcceptButton = signIn;
        signIn.Click += (_, _) => SignIn();
        ButtonByText("Exit").Click += (_, _) => DialogResult = DialogResult.Cancel;
    }

    public string UserId => _user.Text.Trim().ToUpperInvariant();
    public string SelectedEnvironment => (string)_environment.SelectedItem!;

    private void SignIn()
    {
        if (UserId.Length == 0 || _password.Text.Length == 0)
        {
            Error(this, "User ID or password is incorrect.");
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
