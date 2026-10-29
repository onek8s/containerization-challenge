<%@ Assembly Name="Lucerne.ProjectSites.Farm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=$SharePoint.Project.AssemblyPublicKeyToken$" %>
<%@ Page Language="C#" AutoEventWireup="false" Inherits="Lucerne.ProjectSites.Farm.Pages.ManualStatusPage" DynamicMasterPageFile="~masterurl/default.master" %>

<asp:Content ID="Main" ContentPlaceHolderID="PlaceHolderMain" runat="server">
    <div class="luc-page">
        <p><asp:Literal ID="Current" runat="server" /></p>
        <table class="luc-form">
            <tr><td>Status</td><td><asp:DropDownList ID="StatusList" runat="server" /></td></tr>
            <tr><td>Comment</td><td><asp:TextBox ID="CommentBox" runat="server" TextMode="MultiLine" Rows="4" Columns="60" /></td></tr>
        </table>
        <p>
            <asp:Button ID="SaveButton" runat="server" Text="Save" CssClass="ms-ButtonHeightWidth" />
            <asp:Button ID="ClearButton" runat="server" Text="Remove override" CssClass="ms-ButtonHeightWidth" />
        </p>
        <p class="luc-note">An override replaces the calculated status in the portfolio for seven days.</p>
    </div>
</asp:Content>

<asp:Content ID="PageTitle" ContentPlaceHolderID="PlaceHolderPageTitle" runat="server">Set project status</asp:Content>
<asp:Content ID="PageTitleInTitleArea" ContentPlaceHolderID="PlaceHolderPageTitleInTitleArea" runat="server">Set project status</asp:Content>
