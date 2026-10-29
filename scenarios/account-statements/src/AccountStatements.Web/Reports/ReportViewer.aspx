<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ReportViewer.aspx.cs" Inherits="Woodgrove.Statements.Web.Reports.ReportViewerPage" %>
<%@ Register Assembly="Microsoft.ReportViewer.WebForms, Version=15.0.0.0, Culture=neutral, PublicKeyToken=89845dcd8080cc91" Namespace="Microsoft.Reporting.WebForms" TagPrefix="rsweb" %>
<!DOCTYPE html>
<html>
<head runat="server">
    <meta http-equiv="X-UA-Compatible" content="IE=edge" />
    <title>Account Statements - Report</title>
    <link href="<%: ResolveUrl("~/Content/site.css") %>" rel="stylesheet" />
</head>
<body class="viewer">
    <form id="ViewerForm" runat="server">
        <asp:ScriptManager ID="Scripts" runat="server" AsyncPostBackTimeout="600" />
        <header class="topbar">
            <a class="brand" href="<%: ResolveUrl("~/") %>">Woodgrove Leasing &middot; Account Statements</a>
            <nav>
                <a href="<%: ResolveUrl("~/Reports") %>">Reports</a>
                <a href="<%: ResolveUrl("~/Customers") %>">Customers</a>
            </nav>
        </header>
        <div class="viewer-tools">
            <asp:Label ID="ReportTitle" runat="server" CssClass="viewer-title" />
            <asp:Panel ID="AsOfPanel" runat="server" CssClass="viewer-param">
                <asp:Label runat="server" AssociatedControlID="AsOf" Text="As of" />
                <asp:TextBox ID="AsOf" runat="server" TextMode="Date" />
            </asp:Panel>
            <asp:Panel ID="ControllerPanel" runat="server" CssClass="viewer-param">
                <asp:Label runat="server" AssociatedControlID="CreditController" Text="Credit controller" />
                <asp:DropDownList ID="CreditController" runat="server" AppendDataBoundItems="true">
                    <asp:ListItem Text="(all)" Value="" />
                </asp:DropDownList>
            </asp:Panel>
            <asp:Button ID="Refresh" runat="server" Text="View report" OnClick="Refresh_Click" CssClass="button" />
            <asp:Label ID="ErrorText" runat="server" CssClass="error" EnableViewState="false" />
        </div>
        <rsweb:ReportViewer ID="Viewer" runat="server" ProcessingMode="Local" Width="100%" Height="820px"
            ShowParameterPrompts="false" ShowRefreshButton="false" ShowPrintButton="true" ShowBackButton="true"
            AsyncRendering="true" SizeToReportContent="false" KeepSessionAlive="true" InteractivityPostBackMode="AlwaysAsynchronous"
            OnDrillthrough="Viewer_Drillthrough" />
    </form>
</body>
</html>
