<%@ Page Title="Sign in" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.cs" Inherits="Southridge.Clinic.PatientWeb.Account.LoginPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Sign in</h1>
    <asp:Login ID="PatientLogin" runat="server" UserNameLabelText="E-mail or user name:" DisplayRememberMe="true" RememberMeText="Keep me signed in on this computer"
        FailureText="That sign-in did not work. Check your user name and password." OnLoggedIn="PatientLogin_LoggedIn" CssClass="form" />
    <p>
        <asp:HyperLink ID="RecoverLink" runat="server" NavigateUrl="~/Account/Recover.aspx">Forgot your password?</asp:HyperLink> &middot;
        <asp:HyperLink ID="RegisterLink" runat="server" NavigateUrl="~/Account/Register.aspx">Create an account</asp:HyperLink>
    </p>
</asp:Content>
