<%@ Page Title="Referral" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Referral.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Referrals.ReferralPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Referral <asp:Literal ID="NumberLabel" runat="server" /> <span class="status"><asp:Literal ID="StatusLabel" runat="server" /></span></h1>
    <asp:DetailsView ID="ReferralDetails" runat="server" AutoGenerateRows="false" CssClass="details" GridLines="None">
        <Fields>
            <asp:HyperLinkField HeaderText="Patient" DataTextField="PatientName" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Patients/Chart.aspx?id={0}" />
            <asp:BoundField HeaderText="Priority" DataField="Priority" />
            <asp:BoundField HeaderText="Specialty" DataField="SpecialtyCode" />
            <asp:BoundField HeaderText="Requested provider" DataField="RequestedProvider" NullDisplayText="(first available)" />
            <asp:BoundField HeaderText="Referred by" DataField="ReferringProvider" />
            <asp:BoundField HeaderText="Reason" DataField="Reason" />
            <asp:BoundField HeaderText="Diagnosis" DataField="DiagnosisCode" NullDisplayText="-" />
            <asp:BoundField HeaderText="Received" DataField="Received" />
            <asp:BoundField HeaderText="Decision due" DataField="DueBy" DataFormatString="{0:MM/dd/yyyy h:mm tt}" />
            <asp:BoundField HeaderText="Decision" DataField="Decision" NullDisplayText="-" />
            <asp:BoundField HeaderText="Authorization" DataField="Authorization" NullDisplayText="-" />
            <asp:HyperLinkField HeaderText="Appointment" DataTextField="AppointmentText" DataNavigateUrlFields="AppointmentId" DataNavigateUrlFormatString="~/Schedule/Appointment.aspx?id={0}" />
        </Fields>
    </asp:DetailsView>

    <asp:Panel ID="ActionsPanel" runat="server" CssClass="actions">
        <asp:Button ID="ReviewButton" runat="server" Text="Start review" OnClick="ReviewButton_Click" CausesValidation="false" />
        <asp:HyperLink ID="BookLink" runat="server" Text="Book appointment" CssClass="button" />
        <fieldset id="DecisionFields" runat="server">
            <legend>Decision</legend>
            Authorization number <asp:TextBox ID="AuthorizationBox" runat="server" MaxLength="30" Columns="15" />
            Visits <asp:TextBox ID="VisitsBox" runat="server" MaxLength="2" Columns="3" />
            <asp:RangeValidator ID="VisitsRange" runat="server" ControlToValidate="VisitsBox" Type="Integer" MinimumValue="1" MaximumValue="52" ErrorMessage="1-52" Display="Dynamic" ValidationGroup="Decision" />
            <br />
            Note <asp:TextBox ID="DecisionNoteBox" runat="server" MaxLength="400" Columns="70" />
            <br />
            <asp:Button ID="ApproveButton" runat="server" Text="Approve" OnClick="ApproveButton_Click" ValidationGroup="Decision" />
            <asp:Button ID="DeclineButton" runat="server" Text="Decline" OnClick="DeclineButton_Click" ValidationGroup="Decision" />
        </fieldset>
        <fieldset id="CancelFields" runat="server">
            <legend>Cancel referral</legend>
            Reason <asp:TextBox ID="CancelReasonBox" runat="server" MaxLength="200" Columns="50" />
            <asp:Button ID="CancelButton" runat="server" Text="Cancel referral" OnClick="CancelButton_Click" ValidationGroup="Cancel"
                OnClientClick="return confirm('Cancel this referral?');" />
            <asp:RequiredFieldValidator ID="CancelReasonRequired" runat="server" ControlToValidate="CancelReasonBox" ErrorMessage="Enter a reason" Display="Dynamic" ValidationGroup="Cancel" />
        </fieldset>
    </asp:Panel>

    <h2>Documents</h2>
    <asp:Repeater ID="DocumentRepeater" runat="server">
        <HeaderTemplate><ul class="documents"></HeaderTemplate>
        <ItemTemplate>
            <li><a href='<%# ResolveUrl("~/Handlers/ReferralDocument.ashx?id=" + Eval("Id")) %>' target="_blank"><%# Server.HtmlEncode((string)Eval("FileName")) %></a>
                <span class="note"><%# Eval("UploadedOn", "{0:MM/dd/yyyy h:mm tt}") %> by <%# Server.HtmlEncode((string)Eval("UploadedBy")) %></span></li>
        </ItemTemplate>
        <FooterTemplate></ul></FooterTemplate>
    </asp:Repeater>
    <asp:FileUpload ID="DocumentUpload" runat="server" />
    <asp:Button ID="UploadButton" runat="server" Text="Attach" OnClick="UploadButton_Click" CausesValidation="false" />

    <h2>Notes</h2>
    <asp:GridView ID="NoteGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" ShowHeader="false">
        <Columns>
            <asp:BoundField DataField="CreatedOn" DataFormatString="{0:MM/dd/yyyy h:mm tt}" />
            <asp:BoundField DataField="Author" />
            <asp:BoundField DataField="Text" />
        </Columns>
    </asp:GridView>
    <asp:Panel ID="NotePanel" runat="server" DefaultButton="AddNoteButton">
        <asp:TextBox ID="NoteBox" runat="server" MaxLength="1000" Columns="80" />
        <asp:Button ID="AddNoteButton" runat="server" Text="Add note" OnClick="AddNoteButton_Click" ValidationGroup="Note" />
        <asp:RequiredFieldValidator ID="NoteRequired" runat="server" ControlToValidate="NoteBox" ErrorMessage="Enter a note" Display="Dynamic" ValidationGroup="Note" />
    </asp:Panel>
</asp:Content>
