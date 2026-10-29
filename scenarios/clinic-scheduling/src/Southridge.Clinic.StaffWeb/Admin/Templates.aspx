<%@ Page Title="Schedule templates" Language="C#" MasterPageFile="~/Admin/Admin.master" AutoEventWireup="true" CodeBehind="Templates.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Admin.TemplatesPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="AdminContent" runat="server">
    <h1>Schedule templates</h1>
    <p>Weekly clinic hours per provider and site. Visit type codes limit what can be booked into a session; leave empty for any type.</p>
    <asp:Panel ID="FilterPanel" runat="server" CssClass="filters">
        Provider
        <asp:DropDownList ID="ProviderList" runat="server" DataTextField="Text" DataValueField="Value" AutoPostBack="true" OnSelectedIndexChanged="ProviderList_SelectedIndexChanged" />
    </asp:Panel>
    <asp:LinqDataSource ID="TemplateSource" runat="server" ContextTypeName="Southridge.Clinic.Data.ClinicDataContext" TableName="ScheduleTemplates"
        Where="ProviderId == @ProviderId" OrderBy="DayOfWeek, StartTime" EnableUpdate="true" EnableInsert="true" EnableDelete="true"
        OnInserting="TemplateSource_Inserting" OnUpdating="TemplateSource_Updating" OnUpdated="TemplateSource_Changed" OnInserted="TemplateSource_Changed" OnDeleted="TemplateSource_Changed">
        <WhereParameters>
            <asp:ControlParameter Name="ProviderId" ControlID="ProviderList" PropertyName="SelectedValue" Type="Int32" />
        </WhereParameters>
    </asp:LinqDataSource>
    <asp:GridView ID="TemplateGrid" runat="server" DataSourceID="TemplateSource" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="TemplateId"
        EmptyDataText="No templates for this provider.">
        <Columns>
            <asp:TemplateField HeaderText="Day">
                <ItemTemplate><%# (DayOfWeek)(byte)Eval("DayOfWeek") %></ItemTemplate>
                <EditItemTemplate>
                    <asp:DropDownList ID="DayList" runat="server" SelectedValue='<%# Bind("DayOfWeek") %>' DataSource='<%# Days %>' DataTextField="Text" DataValueField="Value" />
                </EditItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="Site">
                <ItemTemplate><%# Eval("Site.Name") %></ItemTemplate>
                <EditItemTemplate>
                    <asp:DropDownList ID="SiteEditList" runat="server" SelectedValue='<%# Bind("SiteId") %>' DataSource='<%# StaffCache.Sites() %>' DataTextField="Text" DataValueField="Value" />
                </EditItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="StartTime" HeaderText="From" ControlStyle-Width="60px" />
            <asp:BoundField DataField="EndTime" HeaderText="To" ControlStyle-Width="60px" />
            <asp:BoundField DataField="SlotMinutes" HeaderText="Slot min" ControlStyle-Width="40px" />
            <asp:BoundField DataField="AppointmentTypes" HeaderText="Visit types" ConvertEmptyStringToNull="true" />
            <asp:BoundField DataField="MaxOverbook" HeaderText="Overbook" ControlStyle-Width="40px" />
            <asp:BoundField DataField="EffectiveFrom" HeaderText="Effective" DataFormatString="{0:MM/dd/yyyy}" ApplyFormatInEditMode="true" ControlStyle-Width="80px" />
            <asp:BoundField DataField="EffectiveTo" HeaderText="Until" DataFormatString="{0:MM/dd/yyyy}" ApplyFormatInEditMode="true" ConvertEmptyStringToNull="true" ControlStyle-Width="80px" />
            <asp:CommandField ShowEditButton="true" ShowDeleteButton="true" />
        </Columns>
    </asp:GridView>

    <asp:DetailsView ID="NewTemplate" runat="server" DataSourceID="TemplateSource" DefaultMode="Insert" AutoGenerateRows="false" CssClass="details" GridLines="None"
        HeaderText="Add a session">
        <Fields>
            <asp:TemplateField HeaderText="Day">
                <InsertItemTemplate>
                    <asp:DropDownList ID="DayList" runat="server" SelectedValue='<%# Bind("DayOfWeek") %>' DataSource='<%# Days %>' DataTextField="Text" DataValueField="Value" />
                </InsertItemTemplate>
            </asp:TemplateField>
            <asp:TemplateField HeaderText="Site">
                <InsertItemTemplate>
                    <asp:DropDownList ID="SiteInsertList" runat="server" SelectedValue='<%# Bind("SiteId") %>' DataSource='<%# StaffCache.Sites() %>' DataTextField="Text" DataValueField="Value" />
                </InsertItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="StartTime" HeaderText="From (hh:mm)" />
            <asp:BoundField DataField="EndTime" HeaderText="To (hh:mm)" />
            <asp:BoundField DataField="SlotMinutes" HeaderText="Slot minutes" />
            <asp:BoundField DataField="AppointmentTypes" HeaderText="Visit types" ConvertEmptyStringToNull="true" />
            <asp:BoundField DataField="MaxOverbook" HeaderText="Overbook" />
            <asp:BoundField DataField="EffectiveFrom" HeaderText="Effective (MM/dd/yyyy)" />
            <asp:CommandField ShowInsertButton="true" ShowCancelButton="false" />
        </Fields>
    </asp:DetailsView>
</asp:Content>
