<%@ Page Title="Appointment" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Appointment.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Schedule.AppointmentPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Appointment <asp:Literal ID="CodeLabel" runat="server" /></h1>
    <asp:DetailsView ID="AppointmentDetails" runat="server" AutoGenerateRows="false" CssClass="details" GridLines="None">
        <Fields>
            <asp:HyperLinkField HeaderText="Patient" DataTextField="PatientName" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Patients/Chart.aspx?id={0}" />
            <asp:BoundField HeaderText="MRN" DataField="Mrn" />
            <asp:BoundField HeaderText="When" DataField="StartsAt" DataFormatString="{0:dddd MM/dd/yyyy h:mm tt}" />
            <asp:BoundField HeaderText="Length" DataField="Minutes" DataFormatString="{0} min" />
            <asp:BoundField HeaderText="Provider" DataField="ProviderName" />
            <asp:BoundField HeaderText="Site" DataField="SiteName" />
            <asp:BoundField HeaderText="Visit" DataField="TypeName" />
            <asp:BoundField HeaderText="Reason" DataField="Reason" />
            <asp:BoundField HeaderText="Status" DataField="StatusText" />
            <asp:BoundField HeaderText="Booked" DataField="BookedText" />
            <asp:BoundField HeaderText="Referral" DataField="ReferralNumber" NullDisplayText="-" />
            <asp:BoundField HeaderText="Cancelled" DataField="CancelText" NullDisplayText="-" />
        </Fields>
    </asp:DetailsView>
    <p>
        <asp:HyperLink ID="LetterLink" runat="server" Target="_blank">Confirmation letter</asp:HyperLink>
        <asp:HyperLink ID="EncounterLink" runat="server" Visible="false">Visit / charges</asp:HyperLink>
    </p>
    <asp:Panel ID="ActionsPanel" runat="server" CssClass="actions">
        <fieldset>
            <legend>Reschedule</legend>
            New time <asp:TextBox ID="NewTimeBox" runat="server" Columns="18" ToolTip="MM/dd/yyyy h:mm AM" />
            <asp:Button ID="RescheduleButton" runat="server" Text="Reschedule" OnClick="RescheduleButton_Click" ValidationGroup="Reschedule" />
            <asp:RequiredFieldValidator ID="NewTimeRequired" runat="server" ControlToValidate="NewTimeBox" ErrorMessage="Enter a date and time" ValidationGroup="Reschedule" Display="Dynamic" />
            <asp:HyperLink ID="FindTimesLink" runat="server">find open times</asp:HyperLink>
        </fieldset>
        <fieldset>
            <legend>Cancel</legend>
            Reason
            <asp:DropDownList ID="CancelReasonList" runat="server">
                <asp:ListItem>Patient request</asp:ListItem>
                <asp:ListItem>Patient ill</asp:ListItem>
                <asp:ListItem>Provider unavailable</asp:ListItem>
                <asp:ListItem>Insurance not verified</asp:ListItem>
                <asp:ListItem>Weather closure</asp:ListItem>
                <asp:ListItem>Other</asp:ListItem>
            </asp:DropDownList>
            <asp:Button ID="CancelButton" runat="server" Text="Cancel appointment" OnClick="CancelButton_Click" OnClientClick="return confirm('Cancel this appointment?');" CausesValidation="false" />
        </fieldset>
    </asp:Panel>
    <h2>Waitlist for this slot</h2>
    <asp:GridView ID="WaitlistGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="Nobody on the waitlist matches.">
        <Columns>
            <asp:HyperLinkField DataTextField="PatientName" HeaderText="Patient" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Schedule/Book.aspx?patientId={0}" />
            <asp:BoundField DataField="MobilePhone" HeaderText="Mobile" />
            <asp:BoundField DataField="EarliestDate" HeaderText="From" DataFormatString="{0:MM/dd}" />
            <asp:BoundField DataField="CreatedOn" HeaderText="Waiting since" DataFormatString="{0:MM/dd/yyyy}" />
        </Columns>
    </asp:GridView>
</asp:Content>
