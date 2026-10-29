<%@ Page Title="Payments" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Payments.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Billing.PaymentsPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Payments</h1>
    <fieldset>
        <legend>Post a patient payment</legend>
        <table class="form">
            <tr>
                <th>Patient</th>
                <td>
                    <asp:TextBox ID="PatientBox" runat="server" Columns="50" autocomplete="off" />
                    <asp:HiddenField ID="PatientId" runat="server" />
                    <ajaxToolkit:AutoCompleteExtender ID="PatientComplete" runat="server" TargetControlID="PatientBox" ServicePath="~/Services/PatientLookup.asmx"
                        ServiceMethod="Complete" MinimumPrefixLength="2" CompletionInterval="300" CompletionSetCount="15" FirstRowSelected="true"
                        OnClientItemSelected="clinic.patientSelected" CompletionListCssClass="completion" />
                    <asp:Button ID="LoadButton" runat="server" Text="Show balance" OnClick="LoadButton_Click" CausesValidation="false" />
                    <strong><asp:Literal ID="BalanceLabel" runat="server" /></strong>
                </td>
            </tr>
            <tr>
                <th>Amount</th>
                <td>
                    <asp:TextBox ID="AmountBox" runat="server" Columns="10" />
                    <asp:RequiredFieldValidator ID="AmountRequired" runat="server" ControlToValidate="AmountBox" ErrorMessage="Enter the amount" Display="Dynamic" ValidationGroup="Payment" />
                    <asp:RangeValidator ID="AmountRange" runat="server" ControlToValidate="AmountBox" Type="Currency" MinimumValue="0.01" MaximumValue="50000" ErrorMessage="0.01 - 50,000.00" Display="Dynamic" ValidationGroup="Payment" />
                </td>
            </tr>
            <tr>
                <th>Method</th>
                <td>
                    <asp:DropDownList ID="MethodList" runat="server">
                        <asp:ListItem>Card</asp:ListItem>
                        <asp:ListItem>Check</asp:ListItem>
                        <asp:ListItem>Cash</asp:ListItem>
                    </asp:DropDownList>
                    Reference <asp:TextBox ID="ReferenceBox" runat="server" MaxLength="40" Columns="20" ToolTip="Check number or card authorization" />
                </td>
            </tr>
        </table>
        <asp:Button ID="PostButton" runat="server" Text="Post payment" OnClick="PostButton_Click" ValidationGroup="Payment" />
    </fieldset>

    <h2><asp:Literal ID="ListTitle" runat="server" Text="Payments posted today" /></h2>
    <asp:SqlDataSource ID="PaymentSource" runat="server" ConnectionString="<%$ ConnectionStrings:Clinic %>" CancelSelectOnNullParameter="false"
        SelectCommand="SELECT TOP 200 pm.PaymentId, pm.PostedOn, pm.PatientId, p.LastName + ', ' + p.FirstName AS PatientName, pm.Source, pm.Method,
                              pm.Amount, pm.AdjustmentAmount, pm.Reference, pm.PostedBy, c.ClaimNumber
                       FROM dbo.Payment pm
                       JOIN dbo.Patient p ON p.PatientId = pm.PatientId
                       LEFT JOIN dbo.Claim c ON c.ClaimId = pm.ClaimId
                       WHERE (@PatientId IS NULL AND pm.PostedOn >= CAST(GETDATE() AS date)) OR pm.PatientId = @PatientId
                       ORDER BY pm.PostedOn DESC">
        <SelectParameters>
            <asp:ControlParameter Name="PatientId" ControlID="PatientId" PropertyName="Value" Type="Int32" ConvertEmptyStringToNull="true" />
        </SelectParameters>
    </asp:SqlDataSource>
    <asp:GridView ID="PaymentGrid" runat="server" DataSourceID="PaymentSource" AutoGenerateColumns="false" CssClass="grid" AllowPaging="true" PageSize="25"
        EmptyDataText="No payments.">
        <Columns>
            <asp:BoundField DataField="PostedOn" HeaderText="Posted" DataFormatString="{0:MM/dd/yyyy h:mm tt}" />
            <asp:HyperLinkField DataTextField="PatientName" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Patients/Chart.aspx?id={0}" HeaderText="Patient" />
            <asp:BoundField DataField="Source" HeaderText="Source" />
            <asp:BoundField DataField="Method" HeaderText="Method" />
            <asp:BoundField DataField="Amount" HeaderText="Amount" DataFormatString="{0:C}" ItemStyle-CssClass="money" />
            <asp:BoundField DataField="AdjustmentAmount" HeaderText="Adjustment" DataFormatString="{0:C}" ItemStyle-CssClass="money" />
            <asp:BoundField DataField="ClaimNumber" HeaderText="Claim" NullDisplayText="-" />
            <asp:BoundField DataField="Reference" HeaderText="Reference" />
            <asp:BoundField DataField="PostedBy" HeaderText="By" />
        </Columns>
    </asp:GridView>

    <h2>Remittances (835)</h2>
    <asp:SqlDataSource ID="RemittanceSource" runat="server" ConnectionString="<%$ ConnectionStrings:Clinic %>"
        SelectCommand="SELECT TOP 20 FileName, PayerName, TraceNumber, PaymentMethod, PaymentDate, TotalPaid, ClaimCount, UnmatchedCount, ReceivedOn FROM dbo.Remittance ORDER BY ReceivedOn DESC" />
    <asp:GridView ID="RemittanceGrid" runat="server" DataSourceID="RemittanceSource" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="No remittances received yet.">
        <Columns>
            <asp:BoundField DataField="ReceivedOn" HeaderText="Received" DataFormatString="{0:MM/dd h:mm tt}" />
            <asp:BoundField DataField="PayerName" HeaderText="Payer" />
            <asp:BoundField DataField="TraceNumber" HeaderText="Trace" />
            <asp:BoundField DataField="PaymentMethod" HeaderText="Method" />
            <asp:BoundField DataField="PaymentDate" HeaderText="Paid" DataFormatString="{0:MM/dd/yyyy}" />
            <asp:BoundField DataField="TotalPaid" HeaderText="Total" DataFormatString="{0:C}" ItemStyle-CssClass="money" />
            <asp:BoundField DataField="ClaimCount" HeaderText="Claims" />
            <asp:BoundField DataField="UnmatchedCount" HeaderText="Unmatched" />
            <asp:BoundField DataField="FileName" HeaderText="File" />
        </Columns>
    </asp:GridView>
</asp:Content>
