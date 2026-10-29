<%@ Page Title="Your patient record" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Link.aspx.cs" Inherits="Southridge.Clinic.PatientWeb.Account.LinkPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Your patient record</h1>
    <asp:MultiView ID="LinkViews" runat="server" ActiveViewIndex="0">
        <asp:View ID="ChooseView" runat="server">
            <p>Have you been seen at a Southridge clinic before?</p>
            <asp:Button ID="ExistingButton" runat="server" Text="Yes, I have a medical record number" OnClick="ExistingButton_Click" CausesValidation="false" />
            <asp:Button ID="NewButton" runat="server" Text="No, I am a new patient" OnClick="NewButton_Click" CausesValidation="false" />
        </asp:View>

        <asp:View ID="ExistingView" runat="server">
            <p>Your medical record number (MRN) is printed on your statements and visit summaries.</p>
            <table class="form">
                <tr>
                    <th>MRN</th>
                    <td><asp:TextBox ID="MrnBox" runat="server" MaxLength="12" Columns="12" />
                        <asp:RequiredFieldValidator ID="MrnRequired" runat="server" ControlToValidate="MrnBox" ErrorMessage="Required" Display="Dynamic" ValidationGroup="Link" /></td>
                </tr>
                <tr>
                    <th>Last name</th>
                    <td><asp:TextBox ID="LinkLastNameBox" runat="server" MaxLength="50" />
                        <asp:RequiredFieldValidator ID="LinkLastNameRequired" runat="server" ControlToValidate="LinkLastNameBox" ErrorMessage="Required" Display="Dynamic" ValidationGroup="Link" /></td>
                </tr>
                <tr>
                    <th>Date of birth</th>
                    <td><asp:TextBox ID="LinkBirthBox" runat="server" MaxLength="10" Columns="10" ToolTip="MM/DD/YYYY" /> MM/DD/YYYY
                        <asp:CompareValidator ID="LinkBirthValid" runat="server" ControlToValidate="LinkBirthBox" Type="Date" Operator="DataTypeCheck" ErrorMessage="Enter a date" Display="Dynamic" ValidationGroup="Link" />
                        <asp:RequiredFieldValidator ID="LinkBirthRequired" runat="server" ControlToValidate="LinkBirthBox" ErrorMessage="Required" Display="Dynamic" ValidationGroup="Link" /></td>
                </tr>
            </table>
            <asp:Button ID="LinkButton" runat="server" Text="Connect my record" OnClick="LinkButton_Click" ValidationGroup="Link" />
        </asp:View>

        <asp:View ID="NewView" runat="server">
            <asp:ValidationSummary ID="NewSummary" runat="server" ValidationGroup="New" CssClass="validation" />
            <table class="form">
                <tr>
                    <th>First name</th>
                    <td><asp:TextBox ID="FirstNameBox" runat="server" MaxLength="50" />
                        <asp:RequiredFieldValidator ID="FirstNameRequired" runat="server" ControlToValidate="FirstNameBox" ErrorMessage="First name is required." Display="None" ValidationGroup="New" /></td>
                </tr>
                <tr>
                    <th>Last name</th>
                    <td><asp:TextBox ID="LastNameBox" runat="server" MaxLength="50" />
                        <asp:RequiredFieldValidator ID="LastNameRequired" runat="server" ControlToValidate="LastNameBox" ErrorMessage="Last name is required." Display="None" ValidationGroup="New" /></td>
                </tr>
                <tr>
                    <th>Date of birth</th>
                    <td><asp:TextBox ID="BirthBox" runat="server" MaxLength="10" Columns="10" /> MM/DD/YYYY
                        <asp:RequiredFieldValidator ID="BirthRequired" runat="server" ControlToValidate="BirthBox" ErrorMessage="Date of birth is required." Display="None" ValidationGroup="New" />
                        <asp:CompareValidator ID="BirthValid" runat="server" ControlToValidate="BirthBox" Type="Date" Operator="DataTypeCheck" ErrorMessage="Date of birth is not a date." Display="None" ValidationGroup="New" /></td>
                </tr>
                <tr>
                    <th>Sex</th>
                    <td><asp:RadioButtonList ID="SexList" runat="server" RepeatDirection="Horizontal" RepeatLayout="Flow">
                        <asp:ListItem Value="F">Female</asp:ListItem>
                        <asp:ListItem Value="M">Male</asp:ListItem>
                        <asp:ListItem Value="U" Selected="True">Prefer not to say</asp:ListItem>
                    </asp:RadioButtonList></td>
                </tr>
                <tr>
                    <th>Street address</th>
                    <td><asp:TextBox ID="AddressBox" runat="server" MaxLength="100" Columns="40" /></td>
                </tr>
                <tr>
                    <th>City / state / ZIP</th>
                    <td><asp:TextBox ID="CityBox" runat="server" MaxLength="50" Columns="20" />
                        <asp:TextBox ID="StateBox" runat="server" MaxLength="2" Columns="2" Text="OH" />
                        <asp:TextBox ID="ZipBox" runat="server" MaxLength="10" Columns="10" />
                        <asp:RegularExpressionValidator ID="ZipFormat" runat="server" ControlToValidate="ZipBox" ValidationExpression="^\d{5}(-\d{4})?$" ErrorMessage="ZIP code is not valid." Display="None" ValidationGroup="New" /></td>
                </tr>
                <tr>
                    <th>Mobile phone</th>
                    <td><asp:TextBox ID="PhoneBox" runat="server" MaxLength="14" Columns="14" />
                        <asp:RegularExpressionValidator ID="PhoneFormat" runat="server" ControlToValidate="PhoneBox" ValidationExpression="^\(?\d{3}\)?[ .-]?\d{3}[ .-]?\d{4}$" ErrorMessage="Phone number is not valid." Display="None" ValidationGroup="New" /></td>
                </tr>
                <tr>
                    <th>Preferred clinic</th>
                    <td><asp:DropDownList ID="SiteList" runat="server" DataTextField="Name" DataValueField="SiteId" AppendDataBoundItems="true">
                        <asp:ListItem Value="">(no preference)</asp:ListItem>
                    </asp:DropDownList></td>
                </tr>
            </table>
            <asp:Button ID="RegisterButton" runat="server" Text="Register" OnClick="RegisterButton_Click" ValidationGroup="New" />
        </asp:View>
    </asp:MultiView>
</asp:Content>
