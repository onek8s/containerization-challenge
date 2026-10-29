<%@ Page Title="History" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="History.aspx.cs" Inherits="VanArsdel.Integration.OpsPortal.HistoryPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h2>History</h2>
    <div class="filters form-inline">
        <asp:DropDownList ID="DaysFilter" runat="server" CssClass="form-control" AutoPostBack="true" OnSelectedIndexChanged="Days_Changed">
            <asp:ListItem Value="1">Last 24 hours</asp:ListItem>
            <asp:ListItem Value="7" Selected="True">Last 7 days</asp:ListItem>
            <asp:ListItem Value="30">Last 30 days</asp:ListItem>
        </asp:DropDownList>
    </div>

    <h3>Resubmissions</h3>
    <asp:GridView ID="ResubmitGrid" runat="server" ItemType="VanArsdel.Integration.Data.ResubmitEntry" SelectMethod="GetResubmissions"
        AutoGenerateColumns="false" CssClass="table table-condensed table-striped" GridLines="None" EmptyDataText="No resubmissions.">
        <Columns>
            <asp:BoundField DataField="ResubmitId" HeaderText="#" />
            <asp:BoundField DataField="ResubmittedUtc" HeaderText="When (UTC)" DataFormatString="{0:yyyy-MM-dd HH:mm}" />
            <asp:BoundField DataField="ResubmittedBy" HeaderText="By" />
            <asp:BoundField DataField="PartnerId" HeaderText="Partner" />
            <asp:BoundField DataField="ServiceName" HeaderText="Service" />
            <asp:BoundField DataField="Reason" HeaderText="Reason" />
            <asp:CheckBoxField DataField="BodyEdited" HeaderText="Edited" />
            <asp:CheckBoxField DataField="OriginalTerminated" HeaderText="Original terminated" />
            <asp:BoundField DataField="Target" HeaderText="Dropped to" />
        </Columns>
    </asp:GridView>

    <h3>Alerts</h3>
    <asp:GridView ID="AlertGrid" runat="server" ItemType="VanArsdel.Integration.Monitoring.Alert" SelectMethod="GetAlerts"
        AutoGenerateColumns="false" AllowPaging="true" PageSize="100" CssClass="table table-condensed" GridLines="None" EmptyDataText="No alerts.">
        <Columns>
            <asp:BoundField DataField="RaisedUtc" HeaderText="Raised (UTC)" DataFormatString="{0:yyyy-MM-dd HH:mm}" />
            <asp:BoundField DataField="Severity" HeaderText="Severity" />
            <asp:BoundField DataField="SlaCode" HeaderText="SLA" />
            <asp:BoundField DataField="PartnerId" HeaderText="Partner" />
            <asp:BoundField DataField="Subject" HeaderText="Subject" />
            <asp:BoundField DataField="Detail" HeaderText="Detail" />
        </Columns>
    </asp:GridView>
</asp:Content>
