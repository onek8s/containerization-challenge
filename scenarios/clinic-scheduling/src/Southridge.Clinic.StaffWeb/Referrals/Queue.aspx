<%@ Page Title="Referrals" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Queue.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Referrals.QueuePage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Referrals</h1>
    <p class="counts"><asp:Literal ID="CountsLabel" runat="server" /></p>
    <asp:Panel ID="FilterPanel" runat="server" DefaultButton="SearchButton" CssClass="filters">
        Specialty
        <asp:DropDownList ID="SpecialtyList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true">
            <asp:ListItem Value="">(all)</asp:ListItem>
        </asp:DropDownList>
        Status
        <asp:DropDownList ID="StatusList" runat="server">
            <asp:ListItem Value="open">Open queue</asp:ListItem>
            <asp:ListItem Value="">(any)</asp:ListItem>
            <asp:ListItem Value="0">Received</asp:ListItem>
            <asp:ListItem Value="1">In review</asp:ListItem>
            <asp:ListItem Value="2">Approved</asp:ListItem>
            <asp:ListItem Value="3">Declined</asp:ListItem>
            <asp:ListItem Value="4">Scheduled</asp:ListItem>
            <asp:ListItem Value="5">Expired</asp:ListItem>
            <asp:ListItem Value="6">Cancelled</asp:ListItem>
        </asp:DropDownList>
        Number, patient or referring provider
        <asp:TextBox ID="SearchBox" runat="server" Columns="25" />
        <asp:Button ID="SearchButton" runat="server" Text="Show" OnClick="SearchButton_Click" />
        <asp:HyperLink ID="NewLink" runat="server" NavigateUrl="~/Referrals/New.aspx">New referral</asp:HyperLink>
    </asp:Panel>

    <asp:ObjectDataSource ID="QueueSource" runat="server" TypeName="Southridge.Clinic.StaffWeb.DataSources.ReferralDataSource" SelectMethod="Queue">
        <SelectParameters>
            <asp:ControlParameter Name="specialtyCode" ControlID="SpecialtyList" PropertyName="SelectedValue" ConvertEmptyStringToNull="true" />
        </SelectParameters>
    </asp:ObjectDataSource>
    <asp:ObjectDataSource ID="SearchSource" runat="server" TypeName="Southridge.Clinic.StaffWeb.DataSources.ReferralDataSource" SelectMethod="Search"
        SelectCountMethod="SearchCount" EnablePaging="true">
        <SelectParameters>
            <asp:ControlParameter Name="text" ControlID="SearchBox" PropertyName="Text" ConvertEmptyStringToNull="true" />
            <asp:ControlParameter Name="specialtyCode" ControlID="SpecialtyList" PropertyName="SelectedValue" ConvertEmptyStringToNull="true" />
            <asp:ControlParameter Name="status" ControlID="StatusList" PropertyName="SelectedValue" Type="Int32" ConvertEmptyStringToNull="true" />
        </SelectParameters>
    </asp:ObjectDataSource>

    <asp:GridView ID="ReferralGrid" runat="server" DataSourceID="QueueSource" AutoGenerateColumns="false" CssClass="grid" AllowPaging="true" PageSize="25"
        EmptyDataText="No referrals match." OnRowDataBound="ReferralGrid_RowDataBound">
        <Columns>
            <asp:HyperLinkField DataTextField="ReferralNumber" DataNavigateUrlFields="Id" DataNavigateUrlFormatString="~/Referrals/Referral.aspx?id={0}" HeaderText="Referral" />
            <asp:BoundField DataField="Priority" HeaderText="Priority" />
            <asp:BoundField DataField="Patient.DisplayName" HeaderText="Patient" />
            <asp:BoundField DataField="SpecialtyCode" HeaderText="Specialty" />
            <asp:BoundField DataField="ReferringProviderName" HeaderText="Referred by" />
            <asp:BoundField DataField="ReceivedOn" HeaderText="Received" DataFormatString="{0:MM/dd h:mm tt}" />
            <asp:BoundField DataField="DueBy" HeaderText="Decision due" DataFormatString="{0:MM/dd h:mm tt}" />
            <asp:BoundField DataField="Status" HeaderText="Status" />
        </Columns>
    </asp:GridView>
</asp:Content>
