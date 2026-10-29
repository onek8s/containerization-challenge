<%@ Page Title="Settings" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Settings.aspx.cs" Inherits="Tailspin.CustomerCare.Web.Admin.SettingsPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Settings</h1>

    <h2>Service level targets (business hours)</h2>
    <table class="grid">
        <tr><th>Priority</th><th>First response</th><th>Resolution</th></tr>
        <asp:Repeater ID="Targets" runat="server" ItemType="Tailspin.CustomerCare.Domain.SlaTarget">
            <ItemTemplate>
                <tr>
                    <td><%#: Item.Priority %><asp:HiddenField ID="TargetPriority" runat="server" Value="<%# (int)Item.Priority %>" /></td>
                    <td><asp:TextBox ID="ResponseHours" runat="server" Text="<%# Item.ResponseHours %>" Columns="5" /> h</td>
                    <td><asp:TextBox ID="ResolveHours" runat="server" Text="<%# Item.ResolveHours %>" Columns="5" /> h</td>
                </tr>
            </ItemTemplate>
        </asp:Repeater>
    </table>
    <asp:Button ID="SaveTargetsButton" runat="server" Text="Save targets" OnClick="SaveTargetsButton_Click" />
    <p class="muted">New targets apply to cases created after saving and to cases whose priority is changed.</p>

    <h2>Holidays</h2>
    <asp:GridView ID="HolidayGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" DataKeyNames="Date" OnRowDeleting="HolidayGrid_RowDeleting">
        <Columns>
            <asp:BoundField DataField="Date" HeaderText="Date" DataFormatString="{0:ddd dd.MM.yyyy}" />
            <asp:BoundField DataField="Name" HeaderText="Name" />
            <asp:CommandField ShowDeleteButton="true" />
        </Columns>
    </asp:GridView>
    <asp:Panel ID="NewHolidayPanel" runat="server" CssClass="filters" DefaultButton="AddHolidayButton">
        <label>Date <asp:TextBox ID="HolidayDate" runat="server" TextMode="Date" /></label>
        <label>Name <asp:TextBox ID="HolidayName" runat="server" MaxLength="100" /></label>
        <asp:Button ID="AddHolidayButton" runat="server" Text="Add" OnClick="AddHolidayButton_Click" />
    </asp:Panel>

    <h2>Server</h2>
    <dl class="facts">
        <dt>Server</dt><dd><asp:Literal ID="ServerName" runat="server" Mode="Encode" /></dd>
        <dt>Time zone</dt><dd><asp:Literal ID="TimeZoneName" runat="server" Mode="Encode" /></dd>
        <dt>Office hours</dt><dd><asp:Literal ID="OfficeHours" runat="server" Mode="Encode" /></dd>
        <dt>Application started</dt><dd><asp:Literal ID="StartedAt" runat="server" Mode="Encode" /></dd>
        <dt>Active sessions</dt><dd><asp:Literal ID="SessionCount" runat="server" Mode="Encode" /></dd>
    </dl>
    <asp:Button ID="ClearCacheButton" runat="server" Text="Reload reference data" OnClick="ClearCacheButton_Click" />
</asp:Content>
