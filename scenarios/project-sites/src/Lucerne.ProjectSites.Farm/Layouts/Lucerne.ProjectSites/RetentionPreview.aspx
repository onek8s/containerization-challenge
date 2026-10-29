<%@ Assembly Name="Lucerne.ProjectSites.Farm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=$SharePoint.Project.AssemblyPublicKeyToken$" %>
<%@ Register Tagprefix="SharePoint" Namespace="Microsoft.SharePoint.WebControls" Assembly="Microsoft.SharePoint, Version=16.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c" %>
<%@ Page Language="C#" AutoEventWireup="false" Inherits="Lucerne.ProjectSites.Farm.Pages.RetentionPreviewPage" DynamicMasterPageFile="~masterurl/default.master" %>

<asp:Content ID="Main" ContentPlaceHolderID="PlaceHolderMain" runat="server">
    <div class="luc-page">
        <p><asp:Literal ID="Summary" runat="server" /></p>
        <SharePoint:SPGridView ID="DecisionsGrid" runat="server" />
        <p><asp:Button ID="ApplyButton" runat="server" Text="Apply now" CssClass="ms-ButtonHeightWidth" /></p>
        <p class="luc-note">The Lucerne Document Retention timer job applies these decisions every Sunday.</p>
    </div>
</asp:Content>

<asp:Content ID="PageTitle" ContentPlaceHolderID="PlaceHolderPageTitle" runat="server">Retention preview</asp:Content>
<asp:Content ID="PageTitleInTitleArea" ContentPlaceHolderID="PlaceHolderPageTitleInTitleArea" runat="server">Retention preview</asp:Content>
