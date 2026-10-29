<%@ Page Title="Settings" Language="C#" MasterPageFile="~/Admin/Admin.master" AutoEventWireup="true" CodeBehind="Settings.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Admin.SettingsPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="AdminContent" runat="server">
    <h1>Settings</h1>
    <p>These values are read by the web sites, services and nightly jobs. Most are cached for up to ten minutes.</p>
    <asp:ObjectDataSource ID="SettingSource" runat="server" TypeName="Southridge.Clinic.StaffWeb.DataSources.SettingDataSource"
        SelectMethod="All" UpdateMethod="Update" OnUpdated="SettingSource_Updated">
        <UpdateParameters>
            <asp:Parameter Name="name" Type="String" />
            <asp:Parameter Name="value" Type="String" />
        </UpdateParameters>
    </asp:ObjectDataSource>
    <asp:GridView ID="SettingGrid" runat="server" DataSourceID="SettingSource" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="Name">
        <Columns>
            <asp:BoundField DataField="Name" HeaderText="Name" ReadOnly="true" />
            <asp:BoundField DataField="Value" HeaderText="Value" ControlStyle-Width="300px" />
            <asp:BoundField DataField="Description" HeaderText="Description" ReadOnly="true" />
            <asp:BoundField DataField="ModifiedBy" HeaderText="Changed by" ReadOnly="true" />
            <asp:BoundField DataField="ModifiedOn" HeaderText="Changed" DataFormatString="{0:MM/dd/yyyy}" ReadOnly="true" />
            <asp:CommandField ShowEditButton="true" />
        </Columns>
    </asp:GridView>
</asp:Content>
