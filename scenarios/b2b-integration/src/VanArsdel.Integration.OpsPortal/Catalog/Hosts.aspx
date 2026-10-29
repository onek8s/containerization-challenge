<%@ Page Title="Host instances" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Hosts.aspx.cs" Inherits="VanArsdel.Integration.OpsPortal.Catalog.HostsPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Host instances</h2>
    <asp:GridView ID="HostGrid" runat="server" ItemType="VanArsdel.Integration.OpsPortal.Models.HostRow" SelectMethod="GetHosts"
        AutoGenerateColumns="false" CssClass="table table-condensed table-striped" GridLines="None" OnRowCommand="HostGrid_RowCommand"
        EmptyDataText="The WMI provider returned no host instances.">
        <Columns>
            <asp:BoundField DataField="HostName" HeaderText="Host" />
            <asp:BoundField DataField="ServerName" HeaderText="Server" />
            <asp:TemplateField HeaderText="State">
                <ItemTemplate><span class="state-<%#: Item.State %>"><%#: Item.State %></span><%#: Item.Disabled ? " (disabled)" : "" %></ItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField>
                <ItemTemplate>
                    <asp:LinkButton runat="server" CommandName="<%# Item.Running ? "StopHost" : "StartHost" %>" CommandArgument="<%# Item.HostName + "|" + Item.ServerName %>"
                        Text="<%# Item.Running ? "Stop" : "Start" %>" Visible="<%# CanOperate && !Item.Disabled && Item.State != "NotApplicable" %>"
                        data-confirm="<%# (Item.Running ? "Stop " : "Start ") + Item.Name + "?" %>" />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>
    <p class="text-muted">Isolated hosts (the AS2 and gateway receive hosts in IIS) have no state here; recycle their application pools instead.</p>
</asp:Content>
