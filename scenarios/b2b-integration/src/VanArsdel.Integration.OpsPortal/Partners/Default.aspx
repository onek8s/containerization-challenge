<%@ Page Title="Partners" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="VanArsdel.Integration.OpsPortal.Partners.DefaultPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h2>Trading partners</h2>
    <p class="text-muted">Partner records come from the integration database; the party column compares them with the BizTalk parties the EDI and AS2 runtime resolve by alias.</p>
    <asp:GridView ID="PartnerGrid" runat="server" ItemType="VanArsdel.Integration.OpsPortal.Models.PartnerRow" SelectMethod="GetPartners"
        DataKeyNames="PartnerId" AutoGenerateColumns="false" CssClass="table table-condensed table-striped" GridLines="None" OnRowCommand="PartnerGrid_RowCommand">
        <Columns>
            <asp:BoundField DataField="PartnerId" HeaderText="Id" />
            <asp:BoundField DataField="Name" HeaderText="Name" />
            <asp:BoundField DataField="Role" HeaderText="Role" />
            <asp:BoundField DataField="Standard" HeaderText="Standard" />
            <asp:BoundField DataField="Transport" HeaderText="Transport" />
            <asp:BoundField DataField="Interchange" HeaderText="Interchange id" />
            <asp:BoundField DataField="ErpNumber" HeaderText="ERP" />
            <asp:TemplateField HeaderText="Status">
                <ItemTemplate><%#: Item.Active ? "Active" : "Inactive" %><%#: Item.TestIndicator ? " (test)" : "" %></ItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="PartyState" HeaderText="BizTalk party" />
            <asp:TemplateField>
                <ItemTemplate>
                    <a href="<%#: "CrossReferences?partner=" + Item.PartnerId %>">Cross references</a>
                    <asp:PlaceHolder runat="server" Visible="<%# IsAdmin %>">
                        | <asp:LinkButton runat="server" CommandName="SyncParty" CommandArgument="<%# Item.PartnerId %>" Text="Sync party" />
                        | <asp:LinkButton runat="server" CommandName="ToggleActive" CommandArgument="<%# Item.PartnerId %>" Text="<%# Item.Active ? "Deactivate" : "Activate" %>"
                            data-confirm="<%# (Item.Active ? "Deactivate " : "Activate ") + Item.PartnerId + "? Inbound interchanges from an inactive partner suspend." %>" />
                    </asp:PlaceHolder>
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>
</asp:Content>
