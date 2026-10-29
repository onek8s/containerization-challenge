<%@ Page Title="Dashboard" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.DefaultPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Today at <asp:Literal ID="SiteName" runat="server" /></h1>
    <asp:UpdatePanel ID="DashboardPanel" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <asp:Timer ID="RefreshTimer" runat="server" Interval="60000" OnTick="RefreshTimer_Tick" />
            <div class="tiles">
                <div class="tile"><span class="n"><asp:Literal ID="BookedCount" runat="server" /></span>booked</div>
                <div class="tile"><span class="n"><asp:Literal ID="WaitingCount" runat="server" /></span>waiting</div>
                <div class="tile"><span class="n"><asp:Literal ID="InRoomCount" runat="server" /></span>in room</div>
                <div class="tile"><span class="n"><asp:Literal ID="CompletedCount" runat="server" /></span>completed</div>
                <div class="tile warn"><span class="n"><asp:Literal ID="NoShowCount" runat="server" /></span>no shows</div>
            </div>
            <h2>Waiting room</h2>
            <asp:GridView ID="WaitingGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="Nobody is waiting." DataKeyNames="AppointmentId">
                <Columns>
                    <asp:BoundField DataField="StartsAt" HeaderText="Appt" DataFormatString="{0:t}" />
                    <asp:HyperLinkField DataTextField="PatientName" HeaderText="Patient" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Patients/Chart.aspx?id={0}" />
                    <asp:BoundField DataField="ProviderName" HeaderText="Provider" />
                    <asp:BoundField DataField="AppointmentTypeName" HeaderText="Visit" />
                    <asp:TemplateField HeaderText="Waiting">
                        <ItemTemplate><span class='<%# (int)Eval("WaitMinutes") > 20 ? "late" : "" %>'><%# Eval("WaitMinutes") %> min</span></ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
            <h2>Work queues</h2>
            <ul class="queues">
                <li><asp:HyperLink ID="ReferralLink" runat="server" NavigateUrl="~/Referrals/Queue.aspx" /></li>
                <li><asp:HyperLink ID="UnbilledLink" runat="server" NavigateUrl="~/Billing/Claims.aspx" /></li>
                <li><asp:HyperLink ID="InterfaceLink" runat="server" NavigateUrl="~/Admin/Interfaces.aspx?status=4" /></li>
            </ul>
            <p class="hint">Updated <asp:Literal ID="UpdatedAt" runat="server" />.</p>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
