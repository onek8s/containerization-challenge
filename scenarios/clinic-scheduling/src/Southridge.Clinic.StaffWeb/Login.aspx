<%@ Page Title="Sign in" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.LoginPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="login-box">
        <h1>Sign in</h1>
        <asp:Login ID="StaffLogin" runat="server" DestinationPageUrl="~/Default.aspx" DisplayRememberMe="false"
            TitleText="" UserNameLabelText="User name" PasswordLabelText="Password" LoginButtonText="Sign in"
            FailureText="The user name or password is incorrect, or the account is locked."
            OnLoggedIn="StaffLogin_LoggedIn" OnLoginError="StaffLogin_LoginError" CssClass="form" />
        <p class="hint">Use your clinic system account. Contact the help desk at extension 4400 if you are locked out.</p>
        <asp:HyperLink ID="SetupLink" runat="server" NavigateUrl="~/Setup.aspx" Visible="false">First time setup</asp:HyperLink>
    </div>
</asp:Content>
