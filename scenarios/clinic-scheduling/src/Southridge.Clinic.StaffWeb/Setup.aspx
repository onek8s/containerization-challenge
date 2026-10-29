<%@ Page Title="First time setup" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Setup.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.SetupPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>First time setup</h1>
    <asp:MultiView ID="SetupViews" runat="server" ActiveViewIndex="0">
        <asp:View ID="CreateView" runat="server">
            <p>No users exist yet. Create the first administrator account.</p>
            <table class="form">
                <tr><td>User name</td><td><asp:TextBox ID="UserNameBox" runat="server" MaxLength="50" />
                    <asp:RequiredFieldValidator ID="UserNameRequired" runat="server" ControlToValidate="UserNameBox" ErrorMessage="Required" Display="Dynamic" /></td></tr>
                <tr><td>Full name</td><td><asp:TextBox ID="FullNameBox" runat="server" MaxLength="100" /></td></tr>
                <tr><td>E-mail</td><td><asp:TextBox ID="EmailBox" runat="server" MaxLength="200" />
                    <asp:RegularExpressionValidator ID="EmailFormat" runat="server" ControlToValidate="EmailBox" ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" ErrorMessage="Not an e-mail address" Display="Dynamic" /></td></tr>
                <tr><td>Password</td><td><asp:TextBox ID="PasswordBox" runat="server" TextMode="Password" />
                    <asp:RequiredFieldValidator ID="PasswordRequired" runat="server" ControlToValidate="PasswordBox" ErrorMessage="Required" Display="Dynamic" /></td></tr>
                <tr><td>Confirm</td><td><asp:TextBox ID="ConfirmBox" runat="server" TextMode="Password" />
                    <asp:CompareValidator ID="PasswordMatch" runat="server" ControlToValidate="ConfirmBox" ControlToCompare="PasswordBox" ErrorMessage="Passwords do not match" Display="Dynamic" /></td></tr>
            </table>
            <asp:Button ID="CreateButton" runat="server" Text="Create administrator" OnClick="CreateButton_Click" />
        </asp:View>
        <asp:View ID="DoneView" runat="server">
            <p>Setup is complete. <asp:HyperLink ID="LoginLink" runat="server" NavigateUrl="~/Login.aspx">Sign in</asp:HyperLink>.</p>
        </asp:View>
    </asp:MultiView>
</asp:Content>
