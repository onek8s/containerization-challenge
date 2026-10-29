<%@ Page Title="Create an account" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Register.aspx.cs" Inherits="Southridge.Clinic.PatientWeb.Account.RegisterPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Create an account</h1>
    <p>With an account you can book visits online, see your appointments and bills, and send your pre-visit form.</p>
    <asp:CreateUserWizard ID="CreateAccount" runat="server" LoginCreatedUser="true" RequireEmail="true" ContinueDestinationPageUrl="~/Account/Link.aspx"
        OnCreatedUser="CreateAccount_CreatedUser" CssClass="form" DuplicateUserNameErrorMessage="That user name is taken. Please choose another."
        InvalidPasswordErrorMessage="Passwords need at least {0} characters.">
        <WizardSteps>
            <asp:CreateUserWizardStep ID="AccountStep" runat="server" Title="Your account" />
            <asp:CompleteWizardStep ID="DoneStep" runat="server" Title="Account created">
                <ContentTemplate>
                    <p>Your account is ready. Next we connect it to your patient record.</p>
                    <asp:Button ID="ContinueButton" runat="server" CommandName="Continue" Text="Continue" ValidationGroup="CreateAccount" />
                </ContentTemplate>
            </asp:CompleteWizardStep>
        </WizardSteps>
    </asp:CreateUserWizard>
</asp:Content>
