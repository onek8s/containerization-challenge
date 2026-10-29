<%@ Control Language="C#" AutoEventWireup="true" CodeBehind="PatientFields.ascx.cs" Inherits="Southridge.Clinic.StaffWeb.Controls.PatientFields" %>
<table class="form">
    <tr>
        <td>Name</td>
        <td>
            <asp:TextBox ID="FirstNameBox" runat="server" MaxLength="50" Columns="15" ToolTip="First" />
            <asp:TextBox ID="MiddleNameBox" runat="server" MaxLength="50" Columns="8" ToolTip="Middle" />
            <asp:TextBox ID="LastNameBox" runat="server" MaxLength="50" Columns="20" ToolTip="Last" />
            <asp:RequiredFieldValidator ID="FirstNameRequired" runat="server" ControlToValidate="FirstNameBox" ErrorMessage="First name" Text="*" />
            <asp:RequiredFieldValidator ID="LastNameRequired" runat="server" ControlToValidate="LastNameBox" ErrorMessage="Last name" Text="*" />
        </td>
    </tr>
    <tr>
        <td>Date of birth</td>
        <td>
            <asp:TextBox ID="DateOfBirthBox" runat="server" Columns="10" />
            <ajaxToolkit:MaskedEditExtender ID="DateOfBirthMask" runat="server" TargetControlID="DateOfBirthBox" Mask="99/99/9999" MaskType="Date" CultureName="en-US" />
            <asp:RequiredFieldValidator ID="DateOfBirthRequired" runat="server" ControlToValidate="DateOfBirthBox" ErrorMessage="Date of birth" Text="*" />
            <asp:CompareValidator ID="DateOfBirthType" runat="server" ControlToValidate="DateOfBirthBox" Type="Date" Operator="DataTypeCheck" ErrorMessage="Date of birth is not a date" Text="*" />
            Sex
            <asp:DropDownList ID="SexList" runat="server">
                <asp:ListItem Value="1">Female</asp:ListItem>
                <asp:ListItem Value="2">Male</asp:ListItem>
                <asp:ListItem Value="0">Unknown</asp:ListItem>
            </asp:DropDownList>
        </td>
    </tr>
    <tr>
        <td>Address</td>
        <td>
            <asp:TextBox ID="Address1Box" runat="server" MaxLength="100" Columns="40" /><br />
            <asp:TextBox ID="Address2Box" runat="server" MaxLength="100" Columns="40" /><br />
            <asp:TextBox ID="CityBox" runat="server" MaxLength="50" Columns="18" />
            <asp:DropDownList ID="StateList" runat="server">
                <asp:ListItem Selected="True">WA</asp:ListItem><asp:ListItem>OR</asp:ListItem><asp:ListItem>ID</asp:ListItem><asp:ListItem>AK</asp:ListItem>
                <asp:ListItem>CA</asp:ListItem><asp:ListItem>MT</asp:ListItem><asp:ListItem>Other</asp:ListItem>
            </asp:DropDownList>
            <asp:TextBox ID="ZipBox" runat="server" MaxLength="10" Columns="10" />
            <asp:RegularExpressionValidator ID="ZipFormat" runat="server" ControlToValidate="ZipBox" ValidationExpression="^\d{5}(-\d{4})?$" ErrorMessage="ZIP code" Text="*" />
        </td>
    </tr>
    <tr>
        <td>Phone</td>
        <td>
            Home <asp:TextBox ID="HomePhoneBox" runat="server" Columns="14" />
            Mobile <asp:TextBox ID="MobilePhoneBox" runat="server" Columns="14" />
            <ajaxToolkit:MaskedEditExtender ID="MobileMask" runat="server" TargetControlID="MobilePhoneBox" Mask="(999) 999-9999" ClearMaskOnLostFocus="false" />
        </td>
    </tr>
    <tr>
        <td>E-mail</td>
        <td>
            <asp:TextBox ID="EmailBox" runat="server" MaxLength="256" Columns="35" />
            <asp:RegularExpressionValidator ID="EmailFormat" runat="server" ControlToValidate="EmailBox" ValidationExpression="^[^@\s]+@[^@\s]+\.[^@\s]+$" ErrorMessage="E-mail address" Text="*" />
        </td>
    </tr>
    <tr>
        <td>Contact by</td>
        <td>
            <asp:DropDownList ID="ContactList" runat="server">
                <asp:ListItem>Phone</asp:ListItem><asp:ListItem>Text</asp:ListItem><asp:ListItem>Email</asp:ListItem><asp:ListItem>Mail</asp:ListItem>
            </asp:DropDownList>
            <asp:CheckBox ID="ReminderBox" runat="server" Text="Send appointment reminders" Checked="true" />
        </td>
    </tr>
    <tr>
        <td>Clinic</td>
        <td>
            <asp:DropDownList ID="SiteList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true"><asp:ListItem Value="" Text="(none)" /></asp:DropDownList>
            Primary provider
            <asp:DropDownList ID="ProviderList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true"><asp:ListItem Value="" Text="(none)" /></asp:DropDownList>
        </td>
    </tr>
</table>
<asp:ValidationSummary ID="Summary" runat="server" HeaderText="Please check:" DisplayMode="List" CssClass="validation" />
