<%@ Page Title="Reports" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Reports.ReportsPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Reports</h1>
    <table class="form">
        <tr><td>Report</td><td>
            <asp:RadioButtonList ID="ReportList" runat="server" AutoPostBack="true" OnSelectedIndexChanged="ReportList_SelectedIndexChanged" RepeatLayout="Flow">
                <asp:ListItem Value="DailySchedule" Selected="True">Daily schedule</asp:ListItem>
                <asp:ListItem Value="ProviderProductivity">Provider productivity</asp:ListItem>
                <asp:ListItem Value="ClaimSummary">Claim summary by payer</asp:ListItem>
                <asp:ListItem Value="ArAging">Accounts receivable aging</asp:ListItem>
            </asp:RadioButtonList></td></tr>
        <tr id="SiteRow" runat="server"><td>Site</td><td><asp:DropDownList ID="SiteList" runat="server" DataTextField="Text" DataValueField="Value" /></td></tr>
        <tr id="DateRow" runat="server"><td><asp:Literal ID="DateLabel" runat="server" Text="Date" /></td><td>
            <asp:TextBox ID="DateBox" runat="server" Columns="10" /><ajaxToolkit:CalendarExtender ID="DateCalendar" runat="server" TargetControlID="DateBox" Format="yyyy-MM-dd" /></td></tr>
        <tr id="ToRow" runat="server"><td>To</td><td>
            <asp:TextBox ID="ToBox" runat="server" Columns="10" /><ajaxToolkit:CalendarExtender ID="ToCalendar" runat="server" TargetControlID="ToBox" Format="yyyy-MM-dd" /></td></tr>
        <tr><td>Format</td><td>
            <asp:DropDownList ID="FormatList" runat="server">
                <asp:ListItem Value="Pdf">PDF</asp:ListItem>
                <asp:ListItem Value="Excel">Excel (report layout)</asp:ListItem>
                <asp:ListItem Value="Xls">Excel (data)</asp:ListItem>
            </asp:DropDownList></td></tr>
    </table>
    <asp:Button ID="RunButton" runat="server" Text="Run report" OnClick="RunButton_Click" />
    <p class="hint">Reports come from the report server; when it is not available the built-in copy of the report is used.</p>
</asp:Content>
