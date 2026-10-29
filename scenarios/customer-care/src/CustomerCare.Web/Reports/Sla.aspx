<%@ Page Title="SLA report" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Sla.aspx.cs" Inherits="Tailspin.CustomerCare.Web.Reports.SlaReport" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>SLA report</h1>
    <asp:Panel ID="FilterPanel" runat="server" CssClass="filters" DefaultButton="ShowButton">
        <label>Created from <asp:TextBox ID="FromDate" runat="server" TextMode="Date" /></label>
        <label>to <asp:TextBox ID="ToDate" runat="server" TextMode="Date" /></label>
        <label>Queue <asp:DropDownList ID="Queue" runat="server" AppendDataBoundItems="true" DataTextField="Name" DataValueField="Id"><asp:ListItem Text="All queues" Value="" /></asp:DropDownList></label>
        <asp:Button ID="ShowButton" runat="server" Text="Show" OnClick="ShowButton_Click" />
        <asp:Button ID="DownloadButton" runat="server" Text="Download CSV" OnClick="DownloadButton_Click" />
    </asp:Panel>

    <asp:GridView ID="Summary" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" EmptyDataText="No cases in the period.">
        <Columns>
            <asp:BoundField DataField="Queue" HeaderText="Queue" />
            <asp:BoundField DataField="Priority" HeaderText="Priority" />
            <asp:BoundField DataField="Cases" HeaderText="Cases" ItemStyle-CssClass="num" />
            <asp:BoundField DataField="ResponseMet" HeaderText="Response met" DataFormatString="{0:P0}" ItemStyle-CssClass="num" />
            <asp:BoundField DataField="ResolutionMet" HeaderText="Resolution met" DataFormatString="{0:P0}" ItemStyle-CssClass="num" />
            <asp:BoundField DataField="Open" HeaderText="Still open" ItemStyle-CssClass="num" />
        </Columns>
    </asp:GridView>
</asp:Content>
