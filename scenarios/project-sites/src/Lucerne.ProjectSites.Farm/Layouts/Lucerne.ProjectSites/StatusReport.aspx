<%@ Assembly Name="Lucerne.ProjectSites.Farm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=$SharePoint.Project.AssemblyPublicKeyToken$" %>
<%@ Import Namespace="Microsoft.SharePoint" %>
<%@ Register Tagprefix="SharePoint" Namespace="Microsoft.SharePoint.WebControls" Assembly="Microsoft.SharePoint, Version=16.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c" %>
<%@ Page Language="C#" AutoEventWireup="false" Inherits="Lucerne.ProjectSites.Farm.Pages.StatusReportPage" DynamicMasterPageFile="~masterurl/default.master" %>

<asp:Content ID="PageHead" ContentPlaceHolderID="PlaceHolderAdditionalPageHead" runat="server">
    <link rel="stylesheet" href="/_layouts/15/Lucerne.ProjectSites/css/lucerne.css" />
</asp:Content>

<asp:Content ID="Main" ContentPlaceHolderID="PlaceHolderMain" runat="server">
    <div class="luc-page">
        <h2><asp:Literal ID="ProjectTitle" runat="server" /></h2>
        <p>Overall status: <asp:Literal ID="OverallStatus" runat="server" /></p>
        <p class="luc-summary"><asp:Literal ID="StatusDetails" runat="server" /></p>
        <h3>Open deliverables</h3>
        <SharePoint:SPGridView ID="DeliverablesGrid" runat="server" />
        <p>
            <asp:Button ID="GenerateButton" runat="server" Text="Create monthly report" CssClass="ms-ButtonHeightWidth" />
            <asp:Label ID="Message" runat="server" />
        </p>
        <p class="luc-note">The report is saved to Project Management / Reports and opened when it is ready.</p>
    </div>
</asp:Content>

<asp:Content ID="PageTitle" ContentPlaceHolderID="PlaceHolderPageTitle" runat="server">Project status report</asp:Content>
<asp:Content ID="PageTitleInTitleArea" ContentPlaceHolderID="PlaceHolderPageTitleInTitleArea" runat="server">Project status report</asp:Content>
