<%@ Page Title="Reset password" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Recover.aspx.cs" Inherits="Southridge.Clinic.PatientWeb.Account.RecoverPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Reset your password</h1>
    <asp:PasswordRecovery ID="Recovery" runat="server" UserNameTitleText="" UserNameInstructionText="Enter your user name. We will e-mail you a new password."
        SuccessText="A new password has been sent to the e-mail address on your account." OnSendingMail="Recovery_SendingMail" CssClass="form">
        <MailDefinition Subject="Your Southridge Medical Group password" BodyFileName="~/App_Data/PasswordMail.txt" />
    </asp:PasswordRecovery>
</asp:Content>
