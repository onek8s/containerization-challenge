<%@ Page Title="Cases" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Tailspin.CustomerCare.Web.Cases.CaseList" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Cases <a class="button" href="<%: ResolveUrl("~/cases/new") %>">New case</a></h1>

    <asp:Panel ID="FilterPanel" runat="server" CssClass="filters" DefaultButton="ApplyButton">
        <label>Search <asp:TextBox ID="FilterText" runat="server" MaxLength="100" /></label>
        <label>Queue <asp:DropDownList ID="FilterQueue" runat="server" AppendDataBoundItems="true" DataTextField="Name" DataValueField="Id"><asp:ListItem Text="All queues" Value="" /></asp:DropDownList></label>
        <label>Status <asp:DropDownList ID="FilterStatus" runat="server" /></label>
        <label><asp:CheckBox ID="FilterMine" runat="server" /> Assigned to me</label>
        <label><asp:CheckBox ID="FilterBreached" runat="server" /> Breached only</label>
        <asp:Button ID="ApplyButton" runat="server" Text="Apply" OnClick="ApplyButton_Click" />
        <asp:LinkButton ID="ResetButton" runat="server" Text="Reset" OnClick="ResetButton_Click" />
    </asp:Panel>

    <p class="muted"><asp:Literal ID="ResultCount" runat="server" /> <span id="stale" class="hidden">Cases have changed, <a href="">refresh</a>.</span></p>

    <asp:GridView ID="CaseGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None"
        AllowPaging="true" AllowCustomPaging="true" AllowSorting="true" PageSize="25"
        ItemType="Tailspin.CustomerCare.Data.Repositories.CaseListItem" DataKeyNames="Id"
        OnPageIndexChanging="CaseGrid_PageIndexChanging" OnSorting="CaseGrid_Sorting" OnRowDataBound="CaseGrid_RowDataBound"
        EmptyDataText="No cases match the filter.">
        <PagerSettings Mode="NumericFirstLast" PageButtonCount="10" />
        <PagerStyle CssClass="pager" />
        <Columns>
            <asp:HyperLinkField DataTextField="CaseNumber" DataNavigateUrlFields="Id" DataNavigateUrlFormatString="~/cases/{0}" HeaderText="Case" SortExpression="CaseNumber" />
            <asp:BoundField DataField="Subject" HeaderText="Subject" SortExpression="Subject" />
            <asp:BoundField DataField="CustomerName" HeaderText="Customer" SortExpression="CustomerName" />
            <asp:BoundField DataField="QueueName" HeaderText="Queue" SortExpression="QueueName" />
            <asp:BoundField DataField="Priority" HeaderText="Priority" SortExpression="Priority" />
            <asp:TemplateField HeaderText="Status" SortExpression="Status"><ItemTemplate><%#: Tailspin.CustomerCare.Domain.CaseWorkflow.DisplayName(Item.Status) %></ItemTemplate></asp:TemplateField>
            <asp:TemplateField HeaderText="Assigned to" SortExpression="AssignedTo"><ItemTemplate><%#: Tailspin.CustomerCare.Web.Security.CareUser.ShortName(Item.AssignedTo) %></ItemTemplate></asp:TemplateField>
            <asp:TemplateField HeaderText="Created" SortExpression="CreatedUtc"><ItemTemplate><%#: Tailspin.CustomerCare.Web.Services.LocalTime.Short(Item.CreatedUtc) %></ItemTemplate></asp:TemplateField>
            <asp:TemplateField HeaderText="Resolve due" SortExpression="ResolveDueUtc"><ItemTemplate><%#: Tailspin.CustomerCare.Web.Services.LocalTime.Short(Item.ResolveDueUtc) %></ItemTemplate></asp:TemplateField>
        </Columns>
    </asp:GridView>
</asp:Content>
<asp:Content ID="Scripts" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        care.connect(function (hub) { hub.server.watchAll(); });
        care.onCaseChanged(function () { $('#stale').removeClass('hidden'); });
    </script>
</asp:Content>
