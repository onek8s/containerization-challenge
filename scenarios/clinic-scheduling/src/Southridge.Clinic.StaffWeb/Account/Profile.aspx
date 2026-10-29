<%@ Page Title="My profile" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Profile.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Account.ProfilePage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>My profile</h1>
    <fieldset>
        <legend>Preferences</legend>
        <table class="form">
            <tr>
                <th>Name</th>
                <td><asp:TextBox ID="FullNameBox" runat="server" MaxLength="100" Columns="35" /></td>
            </tr>
            <tr>
                <th>Home site</th>
                <td><asp:DropDownList ID="SiteList" runat="server" DataTextField="Text" DataValueField="Value" /></td>
            </tr>
            <tr>
                <th>Referral queue</th>
                <td>
                    <asp:DropDownList ID="SpecialtyList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true">
                        <asp:ListItem Value="">(all specialties)</asp:ListItem>
                    </asp:DropDownList>
                </td>
            </tr>
            <tr>
                <th>Rows per page</th>
                <td>
                    <asp:DropDownList ID="PageSizeList" runat="server">
                        <asp:ListItem>15</asp:ListItem>
                        <asp:ListItem>25</asp:ListItem>
                        <asp:ListItem>50</asp:ListItem>
                        <asp:ListItem>100</asp:ListItem>
                    </asp:DropDownList>
                </td>
            </tr>
            <tr>
                <th>Day view</th>
                <td><asp:CheckBox ID="ShowCancelledBox" runat="server" Text="Show cancelled appointments" /></td>
            </tr>
        </table>
        <asp:Button ID="SaveButton" runat="server" Text="Save" OnClick="SaveButton_Click" CausesValidation="false" />
    </fieldset>

    <asp:ChangePassword ID="PasswordChange" runat="server" ChangePasswordTitleText="Change password" CancelButtonType="Link" CancelDestinationPageUrl="~/Default.aspx"
        ContinueDestinationPageUrl="~/Account/Profile.aspx" SuccessText="Your password has been changed." OnChangedPassword="PasswordChange_ChangedPassword" />
</asp:Content>
