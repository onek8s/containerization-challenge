<%@ Page Title="Customer" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Customer.aspx.cs" Inherits="Tailspin.CustomerCare.Web.Customers.CustomerDetail" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1><asp:Literal ID="CustomerTitle" runat="server" Mode="Encode" /> <asp:HyperLink ID="NewCaseLink" runat="server" CssClass="button" Text="New case" /></h1>

    <div class="columns">
        <section class="main-column">
            <h2>Cases</h2>
            <asp:GridView ID="CaseHistory" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" EmptyDataText="No cases."
                ItemType="Tailspin.CustomerCare.Domain.Case">
                <Columns>
                    <asp:HyperLinkField DataTextField="CaseNumber" DataNavigateUrlFields="Id" DataNavigateUrlFormatString="~/cases/{0}" HeaderText="Case" />
                    <asp:BoundField DataField="Subject" HeaderText="Subject" />
                    <asp:TemplateField HeaderText="Queue"><ItemTemplate><%#: Item.Queue.Name %></ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Status"><ItemTemplate><%#: Tailspin.CustomerCare.Domain.CaseWorkflow.DisplayName(Item.Status) %></ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Created"><ItemTemplate><%#: Tailspin.CustomerCare.Web.Services.LocalTime.Format(Item.CreatedUtc) %></ItemTemplate></asp:TemplateField>
                    <asp:TemplateField HeaderText="Closed"><ItemTemplate><%#: Tailspin.CustomerCare.Web.Services.LocalTime.Format(Item.ClosedUtc) %></ItemTemplate></asp:TemplateField>
                </Columns>
            </asp:GridView>
        </section>
        <aside class="side-column">
            <h2>Details</h2>
            <div class="form">
                <label>Account <asp:Literal ID="AccountNumber" runat="server" /></label>
                <label>Segment <asp:Literal ID="Segment" runat="server" /></label>
                <label>Name <asp:TextBox ID="CustomerName" runat="server" MaxLength="200" /></label>
                <asp:RequiredFieldValidator ID="NameRequired" runat="server" ControlToValidate="CustomerName" ErrorMessage="Name is required." CssClass="error" Display="Dynamic" />
                <label>E-mail <asp:TextBox ID="Email" runat="server" MaxLength="254" TextMode="Email" /></label>
                <label>Phone <asp:TextBox ID="Phone" runat="server" MaxLength="30" /></label>
                <label>Street <asp:TextBox ID="Street" runat="server" MaxLength="200" /></label>
                <label>Postal code <asp:TextBox ID="PostalCode" runat="server" MaxLength="5" /></label>
                <asp:RegularExpressionValidator ID="PostalCodeFormat" runat="server" ControlToValidate="PostalCode" ValidationExpression="^\d{5}$" ErrorMessage="Postal code must be 5 digits." CssClass="error" Display="Dynamic" />
                <label>City <asp:TextBox ID="City" runat="server" MaxLength="100" /></label>
                <label><asp:CheckBox ID="PriorityCustomer" runat="server" /> Priority customer (medical equipment, critical infrastructure)</label>
                <asp:Button ID="SaveButton" runat="server" Text="Save" OnClick="SaveButton_Click" />
                <p class="muted">Contact details are overwritten by the nightly import from billing.</p>
            </div>
        </aside>
    </div>
</asp:Content>
