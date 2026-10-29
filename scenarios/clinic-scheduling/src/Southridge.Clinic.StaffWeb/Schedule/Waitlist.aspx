<%@ Page Title="Waitlist" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Waitlist.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Schedule.WaitlistPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Waitlist</h1>
    <asp:LinqDataSource ID="WaitlistSource" runat="server" ContextTypeName="Southridge.Clinic.Data.ClinicDataContext" TableName="WaitlistEntries"
        Where="ResolvedOn == null &amp;&amp; (SiteId == @SiteId || SiteId == null)" OrderBy="Priority desc, CreatedOn" EnableUpdate="true" OnSelecting="WaitlistSource_Selecting">
        <WhereParameters>
            <asp:Parameter Name="SiteId" Type="Int32" />
        </WhereParameters>
    </asp:LinqDataSource>
    <asp:GridView ID="WaitlistGrid" runat="server" DataSourceID="WaitlistSource" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="WaitlistEntryId"
        AllowPaging="true" PageSize="30" EmptyDataText="The waitlist is empty." OnRowCommand="WaitlistGrid_RowCommand">
        <Columns>
            <asp:TemplateField HeaderText="Patient">
                <ItemTemplate><a href='<%# ResolveUrl("~/Patients/Chart.aspx?id=" + Eval("PatientId")) %>'><%# Eval("Patient.LastName") %>, <%# Eval("Patient.FirstName") %></a></ItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="AppointmentType.Name" HeaderText="Visit" ReadOnly="true" />
            <asp:BoundField DataField="Provider.LastName" HeaderText="Provider" ReadOnly="true" NullDisplayText="(any)" />
            <asp:BoundField DataField="EarliestDate" HeaderText="From" DataFormatString="{0:MM/dd/yyyy}" ReadOnly="true" />
            <asp:BoundField DataField="LatestDate" HeaderText="Until" DataFormatString="{0:MM/dd/yyyy}" ReadOnly="true" />
            <asp:BoundField DataField="Priority" HeaderText="Priority" />
            <asp:BoundField DataField="Notes" HeaderText="Notes" />
            <asp:BoundField DataField="OfferedOn" HeaderText="Offered" DataFormatString="{0:MM/dd h:mm tt}" ReadOnly="true" />
            <asp:CommandField ShowEditButton="true" />
            <asp:TemplateField>
                <ItemTemplate>
                    <asp:HyperLink ID="BookLink" runat="server" NavigateUrl='<%# "~/Schedule/Book.aspx?patientId=" + Eval("PatientId") %>' Text="Book" />
                    <asp:LinkButton ID="RemoveButton" runat="server" CommandName="Resolve" CommandArgument='<%# Eval("WaitlistEntryId") %>' Text="Remove" />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>
</asp:Content>
