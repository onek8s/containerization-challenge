<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Tailspin.CustomerCare.Web._Default" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Dashboard</h1>
    <p class="muted">Updated <asp:Literal ID="UpdatedAt" runat="server" />. <span id="stale" class="hidden">New changes, <a href="">refresh</a>.</span></p>

    <h2>Queues</h2>
    <div class="tiles">
        <asp:Repeater ID="QueueTiles" runat="server" ItemType="Tailspin.CustomerCare.Domain.QueueStats">
            <ItemTemplate>
                <a class="tile <%# Item.Breached > 0 ? "tile-alert" : Item.AtRisk > 0 ? "tile-warn" : "" %>" href="<%# ResolveUrl("~/board/" + Item.QueueCode) %>">
                    <span class="tile-title"><%#: Item.QueueName %></span>
                    <span class="tile-number"><%# Item.Open %></span>
                    <span class="tile-detail"><%# Item.Unassigned %> unassigned &middot; <%# Item.AtRisk %> at risk &middot; <%# Item.Breached %> breached</span>
                    <span class="tile-detail"><%# Item.CreatedToday %> new / <%# Item.ResolvedToday %> resolved today</span>
                </a>
            </ItemTemplate>
        </asp:Repeater>
    </div>

    <h2>My open cases</h2>
    <asp:GridView ID="MyCases" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" EmptyDataText="You have no open cases."
        ItemType="Tailspin.CustomerCare.Data.Repositories.CaseListItem" OnRowDataBound="MyCases_RowDataBound">
        <Columns>
            <asp:HyperLinkField DataTextField="CaseNumber" DataNavigateUrlFields="Id" DataNavigateUrlFormatString="~/cases/{0}" HeaderText="Case" />
            <asp:BoundField DataField="Subject" HeaderText="Subject" />
            <asp:BoundField DataField="CustomerName" HeaderText="Customer" />
            <asp:BoundField DataField="QueueName" HeaderText="Queue" />
            <asp:BoundField DataField="Priority" HeaderText="Priority" />
            <asp:TemplateField HeaderText="Status"><ItemTemplate><%#: Tailspin.CustomerCare.Domain.CaseWorkflow.DisplayName(Item.Status) %></ItemTemplate></asp:TemplateField>
            <asp:TemplateField HeaderText="Resolve due"><ItemTemplate><%#: Tailspin.CustomerCare.Web.Services.LocalTime.Short(Item.ResolveDueUtc) %></ItemTemplate></asp:TemplateField>
        </Columns>
    </asp:GridView>

    <asp:PlaceHolder ID="WorkloadSection" runat="server" Visible="false">
        <h2>Agent workload</h2>
        <asp:GridView ID="Workload" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None"
            ItemType="Tailspin.CustomerCare.Data.Repositories.AgentWorkload">
            <Columns>
                <asp:TemplateField HeaderText="Agent"><ItemTemplate><%#: Tailspin.CustomerCare.Web.Security.UserDirectory.DisplayName(Item.AssignedTo) %></ItemTemplate></asp:TemplateField>
                <asp:BoundField DataField="Open" HeaderText="Open" ItemStyle-CssClass="num" />
                <asp:BoundField DataField="Breached" HeaderText="Breached" ItemStyle-CssClass="num" />
                <asp:BoundField DataField="ResolvedLast7Days" HeaderText="Resolved (7 days)" ItemStyle-CssClass="num" />
            </Columns>
        </asp:GridView>
    </asp:PlaceHolder>
</asp:Content>
<asp:Content ID="Scripts" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        care.connect(function (hub) { hub.server.watchAll(); });
        care.onStatsChanged(function () { $('#stale').removeClass('hidden'); });
    </script>
</asp:Content>
