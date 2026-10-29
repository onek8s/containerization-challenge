<%@ Page Title="Ports" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="VanArsdel.Integration.OpsPortal.Catalog.DefaultPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Applications and ports</h2>
    <asp:Repeater ID="ApplicationList" runat="server" ItemType="VanArsdel.Integration.Management.Catalog.ApplicationSummary" SelectMethod="GetApplications">
        <ItemTemplate>
            <div class="panel panel-default">
                <div class="panel-heading"><strong><%#: Item.Name %></strong> <span class="state-<%#: Item.Status %>"><%#: Item.Status %></span> <small><%#: Item.Description %></small></div>
                <table class="table table-condensed">
                    <tr><th colspan="6">Receive locations</th></tr>
                    <asp:Repeater ID="Locations" runat="server" DataSource="<%# Item.ReceivePorts.SelectMany(p => p.Locations) %>" ItemType="VanArsdel.Integration.Management.Catalog.ReceiveLocationSummary" OnItemCommand="Location_Command">
                        <ItemTemplate>
                            <tr>
                                <td><%#: Item.Name %><br /><small><%#: Item.ReceivePort %></small></td>
                                <td><%#: Item.Transport %></td>
                                <td><code><%#: Item.Address %></code></td>
                                <td><%#: Item.Pipeline %></td>
                                <td><%#: Item.Handler %><%#: Item.ServiceWindow ? " (service window)" : "" %></td>
                                <td>
                                    <span class="<%#: Item.Enabled ? "state-Started" : "state-Stopped" %>"><%#: Item.Enabled ? "Enabled" : "Disabled" %></span>
                                    <asp:LinkButton runat="server" Visible="<%# CanOperate %>" CommandName="<%# Item.Enabled ? "Disable" : "Enable" %>" CommandArgument="<%# Item.ReceivePort + "|" + Item.Name %>"
                                        Text="<%# Item.Enabled ? "disable" : "enable" %>" data-confirm="<%# (Item.Enabled ? "Disable " : "Enable ") + Item.Name + "?" %>" />
                                </td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                    <tr><th colspan="6">Send ports</th></tr>
                    <asp:Repeater ID="SendPorts" runat="server" DataSource="<%# Item.SendPorts %>" ItemType="VanArsdel.Integration.Management.Catalog.SendPortSummary" OnItemCommand="SendPort_Command">
                        <ItemTemplate>
                            <tr>
                                <td><%#: Item.Name %><%#: Item.Dynamic ? " (dynamic)" : "" %></td>
                                <td><%#: Item.Transport %></td>
                                <td><code><%#: Item.Address %></code></td>
                                <td><%#: Item.Pipeline %></td>
                                <td><%#: Item.Handler %>, retry <%#: Item.RetryCount %> x <%#: Item.RetryIntervalMinutes %> min<%#: Item.OrderedDelivery ? ", ordered" : "" %></td>
                                <td>
                                    <span class="state-<%#: Item.Status %>"><%#: Item.Status %></span>
                                    <asp:LinkButton runat="server" Visible="<%# CanOperate && Item.Status != "Bound" %>" CommandName="<%# Item.Status == "Started" ? "Stop" : "Start" %>" CommandArgument="<%# Item.Name %>"
                                        Text="<%# Item.Status == "Started" ? "stop" : "start" %>" data-confirm="<%# (Item.Status == "Started" ? "Stop " : "Start ") + Item.Name + "? A stopped port queues its messages." %>" />
                                </td>
                            </tr>
                        </ItemTemplate>
                    </asp:Repeater>
                    <tr><th colspan="6">Orchestrations</th></tr>
                    <asp:Repeater ID="Orchestrations" runat="server" DataSource="<%# Item.Orchestrations %>" ItemType="VanArsdel.Integration.Management.Catalog.OrchestrationSummary">
                        <ItemTemplate>
                            <tr><td colspan="4"><%#: Item.FullName %></td><td><%#: Item.Host %></td><td class="state-<%#: Item.Status %>"><%#: Item.Status %></td></tr>
                        </ItemTemplate>
                    </asp:Repeater>
                </table>
            </div>
        </ItemTemplate>
    </asp:Repeater>
</asp:Content>
