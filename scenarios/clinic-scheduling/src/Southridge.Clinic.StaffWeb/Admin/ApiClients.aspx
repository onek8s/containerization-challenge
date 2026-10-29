<%@ Page Title="Kiosks and partners" Language="C#" MasterPageFile="~/Admin/Admin.master" AutoEventWireup="true" CodeBehind="ApiClients.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Admin.ApiClientsPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="AdminContent" runat="server">
    <h1>Kiosks and partners</h1>
    <p>Check-in kiosks, the patient site and partner systems call the clinic web services with an API key. Only a hash of the key is stored.</p>
    <asp:SqlDataSource ID="ClientSource" runat="server" ConnectionString="<%$ ConnectionStrings:Clinic %>"
        SelectCommand="SELECT c.ApiClientId, c.Name, c.ClientType, c.SiteId, s.Name AS SiteName, c.IsActive, c.LastUsedOn FROM dbo.ApiClient c LEFT JOIN dbo.Site s ON s.SiteId = c.SiteId ORDER BY c.ClientType, c.Name"
        UpdateCommand="UPDATE dbo.ApiClient SET Name = @Name, IsActive = @IsActive WHERE ApiClientId = @ApiClientId"
        OnUpdated="ClientSource_Updated">
        <UpdateParameters>
            <asp:Parameter Name="Name" Type="String" />
            <asp:Parameter Name="IsActive" Type="Boolean" />
            <asp:Parameter Name="ApiClientId" Type="Int32" />
        </UpdateParameters>
    </asp:SqlDataSource>
    <asp:GridView ID="ClientGrid" runat="server" DataSourceID="ClientSource" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="ApiClientId"
        OnRowCommand="ClientGrid_RowCommand">
        <Columns>
            <asp:BoundField DataField="Name" HeaderText="Name" />
            <asp:BoundField DataField="ClientType" HeaderText="Type" ReadOnly="true" />
            <asp:BoundField DataField="SiteName" HeaderText="Site" ReadOnly="true" NullDisplayText="-" />
            <asp:CheckBoxField DataField="IsActive" HeaderText="Active" />
            <asp:BoundField DataField="LastUsedOn" HeaderText="Last used" DataFormatString="{0:MM/dd/yyyy h:mm tt}" ReadOnly="true" NullDisplayText="never" />
            <asp:CommandField ShowEditButton="true" />
            <asp:TemplateField>
                <ItemTemplate>
                    <asp:LinkButton ID="RotateButton" runat="server" CommandName="Rotate" CommandArgument='<%# Eval("ApiClientId") %>' Text="New key"
                        OnClientClick="return confirm('Issue a new key? The old key stops working immediately.');" />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>

    <fieldset>
        <legend>Add a client</legend>
        Name <asp:TextBox ID="NameBox" runat="server" MaxLength="100" Columns="30" />
        <asp:RequiredFieldValidator ID="NameRequired" runat="server" ControlToValidate="NameBox" ErrorMessage="Required" Display="Dynamic" ValidationGroup="Client" />
        Type
        <asp:DropDownList ID="TypeList" runat="server">
            <asp:ListItem>Kiosk</asp:ListItem>
            <asp:ListItem>Portal</asp:ListItem>
            <asp:ListItem>Partner</asp:ListItem>
        </asp:DropDownList>
        Site
        <asp:DropDownList ID="SiteList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true">
            <asp:ListItem Value="">(none)</asp:ListItem>
        </asp:DropDownList>
        <asp:Button ID="AddButton" runat="server" Text="Add" OnClick="AddButton_Click" ValidationGroup="Client" />
    </fieldset>
    <asp:Panel ID="KeyPanel" runat="server" Visible="false" CssClass="secret">
        Key for <strong><asp:Literal ID="KeyClient" runat="server" /></strong>: <code><asp:Literal ID="KeyText" runat="server" Mode="Encode" /></code>
        <br />Copy it now. It is not shown again.
    </asp:Panel>
</asp:Content>
