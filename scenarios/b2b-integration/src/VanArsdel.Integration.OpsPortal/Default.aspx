<%@ Page Title="Overview" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="VanArsdel.Integration.OpsPortal.DefaultPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Overview</h2>
    <div class="tiles">
        <div class="tile" id="SuspendedTile" runat="server"><div class="n"><asp:Literal ID="SuspendedCount" runat="server" /></div>suspended instances</div>
        <div class="tile" id="HostsTile" runat="server"><div class="n"><asp:Literal ID="HostsDown" runat="server" /></div>host instances not running</div>
        <div class="tile"><div class="n"><asp:Literal ID="SpoolDepth" runat="server" /></div>messages in the spool</div>
        <div class="tile"><div class="n"><asp:Literal ID="AlertCount" runat="server" /></div>alerts in the last 24 hours</div>
    </div>

    <h3>Suspended by service</h3>
    <asp:GridView ID="SuspendedGrid" runat="server" ItemType="VanArsdel.Integration.Monitoring.SuspendedInstanceGroup" SelectMethod="GetSuspendedGroups"
        AutoGenerateColumns="false" CssClass="table table-condensed table-striped" GridLines="None" EmptyDataText="Nothing is suspended.">
        <Columns>
            <asp:TemplateField HeaderText="Service">
                <ItemTemplate><a href="<%#: "Instances/?service=" + Server.UrlEncode(Item.ServiceName) %>"><%#: Item.ServiceName %></a></ItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="HostName" HeaderText="Host" />
            <asp:BoundField DataField="Count" HeaderText="Instances" />
            <asp:BoundField DataField="OldestSuspendedUtc" HeaderText="Oldest (UTC)" DataFormatString="{0:yyyy-MM-dd HH:mm}" />
            <asp:CheckBoxField DataField="Resumable" HeaderText="Resumable" />
            <asp:BoundField DataField="ErrorSample" HeaderText="Error" ItemStyle-CssClass="error" />
        </Columns>
    </asp:GridView>

    <h3>Host instances</h3>
    <asp:Repeater ID="HostRepeater" runat="server" ItemType="VanArsdel.Integration.OpsPortal.Models.HostRow" SelectMethod="GetHosts">
        <HeaderTemplate><ul class="list-inline"></HeaderTemplate>
        <ItemTemplate><li><span class="state-<%#: Item.State %>"><%#: Item.Name %></span> <%#: Item.Disabled ? "(disabled)" : "" %></li></ItemTemplate>
        <FooterTemplate></ul></FooterTemplate>
    </asp:Repeater>

    <h3>Latest alerts</h3>
    <asp:GridView ID="AlertGrid" runat="server" ItemType="VanArsdel.Integration.Monitoring.Alert" SelectMethod="GetAlerts"
        AutoGenerateColumns="false" CssClass="table table-condensed" GridLines="None" EmptyDataText="No alerts in the last 24 hours.">
        <Columns>
            <asp:BoundField DataField="RaisedUtc" HeaderText="Raised (UTC)" DataFormatString="{0:yyyy-MM-dd HH:mm}" />
            <asp:BoundField DataField="Severity" HeaderText="Severity" />
            <asp:BoundField DataField="SlaCode" HeaderText="SLA" />
            <asp:BoundField DataField="PartnerId" HeaderText="Partner" />
            <asp:BoundField DataField="Subject" HeaderText="Subject" />
        </Columns>
    </asp:GridView>
</asp:Content>
