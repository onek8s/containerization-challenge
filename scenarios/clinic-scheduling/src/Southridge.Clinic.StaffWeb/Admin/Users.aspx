<%@ Page Title="Users" Language="C#" MasterPageFile="~/Admin/Admin.master" AutoEventWireup="true" CodeBehind="Users.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Admin.UsersPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="AdminContent" runat="server">
    <h1>Users</h1>
    <asp:Panel ID="FilterPanel" runat="server" CssClass="filters">
        Role
        <asp:DropDownList ID="RoleFilter" runat="server" AutoPostBack="true" AppendDataBoundItems="true" OnSelectedIndexChanged="RoleFilter_SelectedIndexChanged">
            <asp:ListItem Value="">(all)</asp:ListItem>
        </asp:DropDownList>
    </asp:Panel>
    <asp:ObjectDataSource ID="UserSource" runat="server" TypeName="Southridge.Clinic.StaffWeb.DataSources.UserDataSource" SelectMethod="All">
        <SelectParameters>
            <asp:ControlParameter Name="role" ControlID="RoleFilter" PropertyName="SelectedValue" ConvertEmptyStringToNull="true" />
        </SelectParameters>
    </asp:ObjectDataSource>
    <asp:GridView ID="UserGrid" runat="server" DataSourceID="UserSource" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="UserName"
        AllowPaging="true" PageSize="30" OnSelectedIndexChanged="UserGrid_SelectedIndexChanged">
        <Columns>
            <asp:ButtonField CommandName="Select" DataTextField="UserName" HeaderText="User" />
            <asp:BoundField DataField="FullName" HeaderText="Name" />
            <asp:BoundField DataField="Email" HeaderText="E-mail" />
            <asp:BoundField DataField="Roles" HeaderText="Roles" />
            <asp:CheckBoxField DataField="IsApproved" HeaderText="Active" />
            <asp:CheckBoxField DataField="IsLockedOut" HeaderText="Locked" />
            <asp:BoundField DataField="LastLoginDate" HeaderText="Last login" DataFormatString="{0:MM/dd/yyyy h:mm tt}" />
        </Columns>
    </asp:GridView>

    <fieldset>
        <legend><asp:Literal ID="EditTitle" runat="server" Text="New user" /></legend>
        <table class="form">
            <tr>
                <th>User name</th>
                <td>
                    <asp:TextBox ID="UserNameBox" runat="server" MaxLength="50" />
                    <asp:RequiredFieldValidator ID="UserNameRequired" runat="server" ControlToValidate="UserNameBox" ErrorMessage="Required" Display="Dynamic" ValidationGroup="User" />
                </td>
            </tr>
            <tr>
                <th>Full name</th>
                <td><asp:TextBox ID="FullNameBox" runat="server" MaxLength="100" Columns="35" /></td>
            </tr>
            <tr>
                <th>E-mail</th>
                <td>
                    <asp:TextBox ID="EmailBox" runat="server" MaxLength="100" Columns="35" />
                    <asp:RequiredFieldValidator ID="EmailRequired" runat="server" ControlToValidate="EmailBox" ErrorMessage="Required" Display="Dynamic" ValidationGroup="User" />
                    <asp:RegularExpressionValidator ID="EmailFormat" runat="server" ControlToValidate="EmailBox" ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" ErrorMessage="Not an e-mail address" Display="Dynamic" ValidationGroup="User" />
                </td>
            </tr>
            <tr>
                <th>Home site</th>
                <td><asp:DropDownList ID="SiteList" runat="server" DataTextField="Text" DataValueField="Value" /></td>
            </tr>
            <tr>
                <th>Provider</th>
                <td>
                    <asp:DropDownList ID="ProviderList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true">
                        <asp:ListItem Value="">(not a provider)</asp:ListItem>
                    </asp:DropDownList>
                </td>
            </tr>
            <tr>
                <th>Roles</th>
                <td><asp:CheckBoxList ID="RoleList" runat="server" RepeatColumns="4" RepeatLayout="Table" /></td>
            </tr>
            <tr>
                <th>Active</th>
                <td><asp:CheckBox ID="ActiveBox" runat="server" Checked="true" /></td>
            </tr>
        </table>
        <asp:Button ID="SaveButton" runat="server" Text="Create user" OnClick="SaveButton_Click" ValidationGroup="User" />
        <asp:Button ID="UnlockButton" runat="server" Text="Unlock" OnClick="UnlockButton_Click" CausesValidation="false" Visible="false" />
        <asp:Button ID="ResetButton" runat="server" Text="Reset password" OnClick="ResetButton_Click" CausesValidation="false" Visible="false"
            OnClientClick="return confirm('Reset the password? The user gets a temporary password.');" />
        <asp:Button ID="ClearButton" runat="server" Text="Clear" OnClick="ClearButton_Click" CausesValidation="false" />
        <p><asp:Label ID="PasswordLabel" runat="server" CssClass="secret" /></p>
    </fieldset>
</asp:Content>
