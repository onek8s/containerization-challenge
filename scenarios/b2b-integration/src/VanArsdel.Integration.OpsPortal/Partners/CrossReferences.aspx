<%@ Page Title="Cross references" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CrossReferences.aspx.cs" Inherits="VanArsdel.Integration.OpsPortal.Partners.CrossReferencesPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <p><a href="./">&larr; Partners</a></p>
    <h2>Cross references for <asp:Literal ID="PartnerTitle" runat="server" Mode="Encode" /></h2>
    <div class="filters form-inline">
        <asp:DropDownList ID="KindFilter" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="KindFilter_Changed">
            <asp:ListItem Text="All kinds" Value="" />
            <asp:ListItem>Item</asp:ListItem>
            <asp:ListItem>UnitOfMeasure</asp:ListItem>
            <asp:ListItem>ShipTo</asp:ListItem>
            <asp:ListItem>Warehouse</asp:ListItem>
            <asp:ListItem>Carrier</asp:ListItem>
            <asp:ListItem>PaymentTerms</asp:ListItem>
        </asp:DropDownList>
    </div>

    <asp:GridView ID="XrefGrid" runat="server" ItemType="VanArsdel.Integration.Domain.Partners.PartnerCrossReference" SelectMethod="GetCrossReferences"
        UpdateMethod="UpdateCrossReference" DeleteMethod="DeleteCrossReference" DataKeyNames="PartnerId,Kind,PartnerValue"
        AutoGenerateColumns="false" AllowPaging="true" PageSize="100" CssClass="table table-condensed table-striped" GridLines="None"
        AutoGenerateEditButton="false" EmptyDataText="No cross references.">
        <Columns>
            <asp:BoundField DataField="Kind" HeaderText="Kind" ReadOnly="true" />
            <asp:BoundField DataField="PartnerValue" HeaderText="Partner value" ReadOnly="true" />
            <asp:BoundField DataField="InternalValue" HeaderText="Internal value" />
            <asp:BoundField DataField="ValidFrom" HeaderText="Valid from" DataFormatString="{0:yyyy-MM-dd}" ApplyFormatInEditMode="true" />
            <asp:BoundField DataField="ValidTo" HeaderText="Valid to" DataFormatString="{0:yyyy-MM-dd}" ApplyFormatInEditMode="true" />
            <asp:CommandField ShowEditButton="true" ShowDeleteButton="true" />
        </Columns>
    </asp:GridView>

    <asp:Panel ID="AddPanel" runat="server" CssClass="panel panel-default" DefaultButton="AddButton">
        <div class="panel-heading">Add a cross reference</div>
        <div class="panel-body form-inline">
            <asp:DropDownList ID="NewKind" runat="server" CssClass="form-control">
                <asp:ListItem>Item</asp:ListItem>
                <asp:ListItem>UnitOfMeasure</asp:ListItem>
                <asp:ListItem>ShipTo</asp:ListItem>
                <asp:ListItem>Warehouse</asp:ListItem>
                <asp:ListItem>Carrier</asp:ListItem>
                <asp:ListItem>PaymentTerms</asp:ListItem>
            </asp:DropDownList>
            <asp:TextBox ID="NewPartnerValue" runat="server" CssClass="form-control" placeholder="Partner value" MaxLength="50" />
            <asp:TextBox ID="NewInternalValue" runat="server" CssClass="form-control" placeholder="Internal value" MaxLength="50" />
            <asp:TextBox ID="NewValidFrom" runat="server" CssClass="form-control" placeholder="Valid from (yyyy-mm-dd)" />
            <asp:Button ID="AddButton" runat="server" Text="Add" CssClass="btn btn-primary" OnClick="Add_Click" ValidationGroup="Add" />
            <asp:RequiredFieldValidator ID="PartnerValueRequired" runat="server" ControlToValidate="NewPartnerValue" ValidationGroup="Add" ErrorMessage="Partner value is required." CssClass="error" Display="Dynamic" />
            <asp:RequiredFieldValidator ID="InternalValueRequired" runat="server" ControlToValidate="NewInternalValue" ValidationGroup="Add" ErrorMessage="Internal value is required." CssClass="error" Display="Dynamic" />
        </div>
    </asp:Panel>
</asp:Content>
