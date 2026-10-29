<%@ Page Title="Queues and routing" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Queues.aspx.cs" Inherits="Tailspin.CustomerCare.Web.Admin.QueuesPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Queues and routing</h1>

    <h2>Queues</h2>
    <asp:GridView ID="QueueGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" DataKeyNames="Id"
        OnRowEditing="QueueGrid_RowEditing" OnRowCancelingEdit="QueueGrid_RowCancelingEdit" OnRowUpdating="QueueGrid_RowUpdating">
        <Columns>
            <asp:BoundField DataField="Code" HeaderText="Code" ReadOnly="true" />
            <asp:BoundField DataField="Name" HeaderText="Name" />
            <asp:BoundField DataField="Email" HeaderText="Mailbox" />
            <asp:BoundField DataField="SortOrder" HeaderText="Order" />
            <asp:CheckBoxField DataField="IsActive" HeaderText="Active" />
            <asp:CommandField ShowEditButton="true" />
        </Columns>
    </asp:GridView>

    <h3>Add queue</h3>
    <asp:Panel ID="NewQueuePanel" runat="server" CssClass="filters" DefaultButton="AddQueueButton">
        <label>Code <asp:TextBox ID="NewQueueCode" runat="server" MaxLength="12" /></label>
        <label>Name <asp:TextBox ID="NewQueueName" runat="server" MaxLength="100" /></label>
        <label>Mailbox <asp:TextBox ID="NewQueueEmail" runat="server" MaxLength="254" /></label>
        <asp:Button ID="AddQueueButton" runat="server" Text="Add" OnClick="AddQueueButton_Click" ValidationGroup="queue" />
        <asp:RequiredFieldValidator ID="NewQueueCodeRequired" runat="server" ControlToValidate="NewQueueCode" ErrorMessage="Code is required." CssClass="error" Display="Dynamic" ValidationGroup="queue" />
        <asp:RequiredFieldValidator ID="NewQueueNameRequired" runat="server" ControlToValidate="NewQueueName" ErrorMessage="Name is required." CssClass="error" Display="Dynamic" ValidationGroup="queue" />
    </asp:Panel>

    <h2>Routing rules</h2>
    <p class="muted">Rules are checked in order; the first match decides the queue. Empty fields match anything. Without a match the category's queue is used.</p>
    <asp:GridView ID="RuleGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" DataKeyNames="Id" OnRowDeleting="RuleGrid_RowDeleting">
        <Columns>
            <asp:BoundField DataField="Sequence" HeaderText="Order" />
            <asp:BoundField DataField="CategoryCode" HeaderText="Category" />
            <asp:BoundField DataField="Segment" HeaderText="Segment" />
            <asp:BoundField DataField="PostalCodePrefix" HeaderText="Postal code starts with" />
            <asp:BoundField DataField="QueueCode" HeaderText="Queue" />
            <asp:CommandField ShowDeleteButton="true" />
        </Columns>
    </asp:GridView>

    <h3>Add rule</h3>
    <asp:Panel ID="NewRulePanel" runat="server" CssClass="filters" DefaultButton="AddRuleButton">
        <label>Order <asp:TextBox ID="NewRuleSequence" runat="server" TextMode="Number" Text="100" /></label>
        <label>Category <asp:DropDownList ID="NewRuleCategory" runat="server" AppendDataBoundItems="true" DataTextField="Name" DataValueField="Code"><asp:ListItem Text="(any)" Value="" /></asp:DropDownList></label>
        <label>Segment <asp:DropDownList ID="NewRuleSegment" runat="server"><asp:ListItem Text="(any)" Value="" /><asp:ListItem>RES</asp:ListItem><asp:ListItem>SME</asp:ListItem><asp:ListItem>LCO</asp:ListItem></asp:DropDownList></label>
        <label>Postal code prefix <asp:TextBox ID="NewRulePostal" runat="server" MaxLength="5" /></label>
        <label>Queue <asp:DropDownList ID="NewRuleQueue" runat="server" DataTextField="Name" DataValueField="Code" /></label>
        <asp:Button ID="AddRuleButton" runat="server" Text="Add" OnClick="AddRuleButton_Click" />
    </asp:Panel>
</asp:Content>
