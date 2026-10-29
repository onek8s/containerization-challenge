<%@ Page Title="Patient chart" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Chart.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Patients.ChartPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <div class="chart-header">
        <h1><asp:Literal ID="PatientName" runat="server" /></h1>
        <div class="small">
            MRN <asp:Literal ID="Mrn" runat="server" /> &middot; DOB <asp:Literal ID="Dob" runat="server" /> (<asp:Literal ID="Age" runat="server" />)
            &middot; <asp:Literal ID="Phone" runat="server" /> &middot; Balance <asp:Literal ID="Balance" runat="server" />
        </div>
        <asp:Repeater ID="AlertRepeater" runat="server">
            <ItemTemplate><div class="alert"><%# Server.HtmlEncode((string)Eval("Text")) %></div></ItemTemplate>
        </asp:Repeater>
        <div class="toolbar">
            <asp:HyperLink ID="EditLink" runat="server">Edit demographics</asp:HyperLink>
            <asp:HyperLink ID="BookLink" runat="server">Book appointment</asp:HyperLink>
            <asp:HyperLink ID="ReferralLink" runat="server">New referral</asp:HyperLink>
            <asp:HyperLink ID="StatementLink" runat="server" Target="_blank">Statement preview</asp:HyperLink>
        </div>
    </div>
    <asp:Menu ID="ChartTabs" runat="server" Orientation="Horizontal" OnMenuItemClick="ChartTabs_MenuItemClick" CssClass="tabs" RenderingMode="List">
        <Items>
            <asp:MenuItem Text="Appointments" Value="0" Selected="true" />
            <asp:MenuItem Text="Visits" Value="1" />
            <asp:MenuItem Text="Lab results" Value="2" />
            <asp:MenuItem Text="Insurance" Value="3" />
            <asp:MenuItem Text="Notes" Value="4" />
            <asp:MenuItem Text="Billing" Value="5" />
        </Items>
    </asp:Menu>
    <asp:MultiView ID="ChartViews" runat="server" ActiveViewIndex="0">
        <asp:View ID="AppointmentsView" runat="server">
            <asp:GridView ID="AppointmentGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="No appointments.">
                <Columns>
                    <asp:HyperLinkField DataTextField="StartsAt" DataTextFormatString="{0:ddd MM/dd/yyyy h:mm tt}" HeaderText="When" DataNavigateUrlFields="AppointmentId" DataNavigateUrlFormatString="~/Schedule/Appointment.aspx?id={0}" />
                    <asp:BoundField DataField="AppointmentType" HeaderText="Visit" />
                    <asp:BoundField DataField="ProviderName" HeaderText="Provider" />
                    <asp:BoundField DataField="SiteName" HeaderText="Site" />
                    <asp:BoundField DataField="StatusText" HeaderText="Status" />
                    <asp:BoundField DataField="ConfirmationCode" HeaderText="Code" />
                </Columns>
            </asp:GridView>
        </asp:View>
        <asp:View ID="VisitsView" runat="server">
            <asp:GridView ID="VisitGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="No visits.">
                <Columns>
                    <asp:HyperLinkField DataTextField="DateOfService" DataTextFormatString="{0:MM/dd/yyyy}" HeaderText="Date" DataNavigateUrlFields="EncounterId" DataNavigateUrlFormatString="~/Clinical/Encounter.aspx?id={0}" />
                    <asp:BoundField DataField="ProviderName" HeaderText="Provider" />
                    <asp:BoundField DataField="SiteName" HeaderText="Site" />
                    <asp:BoundField DataField="ChiefComplaint" HeaderText="Reason" />
                    <asp:BoundField DataField="Diagnoses" HeaderText="Diagnoses" />
                    <asp:BoundField DataField="StatusText" HeaderText="Status" />
                    <asp:HyperLinkField Text="Summary" DataNavigateUrlFields="EncounterId" DataNavigateUrlFormatString="~/Handlers/VisitSummary.ashx?id={0}" Target="_blank" />
                </Columns>
            </asp:GridView>
        </asp:View>
        <asp:View ID="LabsView" runat="server">
            <asp:GridView ID="LabGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="No results." OnRowDataBound="LabGrid_RowDataBound">
                <Columns>
                    <asp:BoundField DataField="ObservedOn" HeaderText="Collected" DataFormatString="{0:MM/dd/yyyy}" />
                    <asp:BoundField DataField="ObservationName" HeaderText="Test" />
                    <asp:BoundField DataField="Value" HeaderText="Result" />
                    <asp:BoundField DataField="Units" HeaderText="Units" />
                    <asp:BoundField DataField="ReferenceRange" HeaderText="Range" />
                    <asp:BoundField DataField="AbnormalFlag" HeaderText="Flag" />
                    <asp:BoundField DataField="OrderNumber" HeaderText="Order" />
                </Columns>
            </asp:GridView>
        </asp:View>
        <asp:View ID="InsuranceView" runat="server">
            <asp:GridView ID="InsuranceGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="Self pay.">
                <Columns>
                    <asp:BoundField DataField="Sequence" HeaderText="#" />
                    <asp:BoundField DataField="PayerName" HeaderText="Payer" />
                    <asp:BoundField DataField="MemberId" HeaderText="Member ID" />
                    <asp:BoundField DataField="GroupNumber" HeaderText="Group" />
                    <asp:BoundField DataField="Copay" HeaderText="Copay" DataFormatString="{0:C}" />
                    <asp:BoundField DataField="EffectiveFrom" HeaderText="From" DataFormatString="{0:MM/dd/yyyy}" />
                    <asp:BoundField DataField="EffectiveTo" HeaderText="To" DataFormatString="{0:MM/dd/yyyy}" />
                    <asp:BoundField DataField="VerifiedOn" HeaderText="Verified" DataFormatString="{0:MM/dd/yyyy}" NullDisplayText="not verified" />
                </Columns>
            </asp:GridView>
        </asp:View>
        <asp:View ID="NotesView" runat="server">
            <asp:Repeater ID="NoteRepeater" runat="server">
                <ItemTemplate>
                    <div class='<%# (bool)Eval("IsAlert") ? "note alert" : "note" %>'>
                        <span class="small"><%# Eval("CreatedOn", "{0:MM/dd/yyyy h:mm tt}") %> &middot; <%# Eval("CreatedBy") %> &middot; <%# Eval("NoteType") %></span>
                        <p><%# Server.HtmlEncode((string)Eval("Text")) %></p>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
            <fieldset>
                <legend>Add note</legend>
                <asp:DropDownList ID="NoteTypeList" runat="server">
                    <asp:ListItem>General</asp:ListItem><asp:ListItem>Billing</asp:ListItem><asp:ListItem>Phone</asp:ListItem><asp:ListItem>Clinical</asp:ListItem>
                </asp:DropDownList>
                <asp:CheckBox ID="AlertBox" runat="server" Text="Show as alert" /><br />
                <asp:TextBox ID="NoteBox" runat="server" TextMode="MultiLine" Rows="3" Columns="80" />
                <asp:RequiredFieldValidator ID="NoteRequired" runat="server" ControlToValidate="NoteBox" ErrorMessage="Enter the note" ValidationGroup="Note" Display="Dynamic" />
                <asp:Button ID="AddNoteButton" runat="server" Text="Add" OnClick="AddNoteButton_Click" ValidationGroup="Note" />
            </fieldset>
        </asp:View>
        <asp:View ID="BillingView" runat="server">
            <h3>Claims</h3>
            <asp:GridView ID="ClaimGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="No claims.">
                <Columns>
                    <asp:BoundField DataField="ClaimNumber" HeaderText="Claim" />
                    <asp:BoundField DataField="DateOfService" HeaderText="DOS" DataFormatString="{0:MM/dd/yyyy}" />
                    <asp:BoundField DataField="PayerName" HeaderText="Payer" />
                    <asp:BoundField DataField="StatusText" HeaderText="Status" />
                    <asp:BoundField DataField="TotalCharge" HeaderText="Charged" DataFormatString="{0:C}" ItemStyle-CssClass="num" />
                    <asp:BoundField DataField="PaidAmount" HeaderText="Paid" DataFormatString="{0:C}" ItemStyle-CssClass="num" />
                    <asp:BoundField DataField="PatientResponsibility" HeaderText="Patient" DataFormatString="{0:C}" ItemStyle-CssClass="num" />
                    <asp:BoundField DataField="RejectReason" HeaderText="Note" />
                </Columns>
            </asp:GridView>
            <h3>Statements</h3>
            <asp:GridView ID="StatementGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="No statements sent.">
                <Columns>
                    <asp:BoundField DataField="StatementDate" HeaderText="Date" DataFormatString="{0:MM/dd/yyyy}" />
                    <asp:BoundField DataField="AmountDue" HeaderText="Amount due" DataFormatString="{0:C}" ItemStyle-CssClass="num" />
                    <asp:BoundField DataField="DueDate" HeaderText="Due" DataFormatString="{0:MM/dd/yyyy}" />
                    <asp:BoundField DataField="DunningLevel" HeaderText="Notice" />
                </Columns>
            </asp:GridView>
            <asp:HyperLink ID="PaymentLink" runat="server">Post a payment</asp:HyperLink>
        </asp:View>
    </asp:MultiView>
</asp:Content>
