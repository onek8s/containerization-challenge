<%@ Page Title="Customers" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Tailspin.CustomerCare.Web.Customers.CustomerList" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Customers</h1>
    <asp:Panel ID="SearchPanel" runat="server" CssClass="filters" DefaultButton="FindButton">
        <asp:TextBox ID="Query" runat="server" placeholder="Name, account number, e-mail or phone" MaxLength="100" />
        <asp:Button ID="FindButton" runat="server" Text="Find" OnClick="FindButton_Click" />
    </asp:Panel>
    <asp:GridView ID="Results" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" EmptyDataText="No customers found."
        ItemType="Tailspin.CustomerCare.Domain.Customer">
        <Columns>
            <asp:HyperLinkField DataTextField="AccountNumber" DataNavigateUrlFields="Id" DataNavigateUrlFormatString="~/customers/{0}" HeaderText="Account" />
            <asp:BoundField DataField="Name" HeaderText="Name" />
            <asp:BoundField DataField="Street" HeaderText="Address" />
            <asp:BoundField DataField="PostalCode" HeaderText="Postal code" />
            <asp:BoundField DataField="City" HeaderText="City" />
            <asp:BoundField DataField="Segment" HeaderText="Segment" />
            <asp:TemplateField HeaderText=""><ItemTemplate><%# Item.IsPriorityCustomer ? "<span class=\"badge\">Priority</span>" : "" %></ItemTemplate></asp:TemplateField>
        </Columns>
    </asp:GridView>
</asp:Content>
