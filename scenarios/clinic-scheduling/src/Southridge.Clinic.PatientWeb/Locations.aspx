<%@ Page Title="Locations" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Locations.aspx.cs" Inherits="Southridge.Clinic.PatientWeb.LocationsPage" %>
<%@ OutputCache Duration="600" VaryByParam="none" Location="Server" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Our clinics</h1>
    <asp:Repeater ID="SiteRepeater" runat="server">
        <ItemTemplate>
            <div class="site">
                <h2><%# Server.HtmlEncode((string)Eval("Name")) %></h2>
                <p><%# Server.HtmlEncode((string)Eval("Address")) %><br /><%# Server.HtmlEncode((string)Eval("Phone")) %></p>
            </div>
        </ItemTemplate>
    </asp:Repeater>
</asp:Content>
