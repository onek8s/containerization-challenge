<%@ Page Title="Claims" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Claims.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Billing.ClaimsPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Claims</h1>
    <asp:Panel ID="FilterPanel" runat="server" DefaultButton="ShowButton" CssClass="filters">
        Status
        <asp:DropDownList ID="StatusList" runat="server">
            <asp:ListItem Value="-1">(any)</asp:ListItem>
            <asp:ListItem Value="0">Ready</asp:ListItem>
            <asp:ListItem Value="1">Submitted</asp:ListItem>
            <asp:ListItem Value="2">Accepted</asp:ListItem>
            <asp:ListItem Value="3" Selected="True">Rejected</asp:ListItem>
            <asp:ListItem Value="4">Paid</asp:ListItem>
            <asp:ListItem Value="5">Partially paid</asp:ListItem>
            <asp:ListItem Value="6">Denied</asp:ListItem>
            <asp:ListItem Value="7">Closed</asp:ListItem>
        </asp:DropDownList>
        Payer
        <asp:DropDownList ID="PayerList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true">
            <asp:ListItem Value="0">(all)</asp:ListItem>
        </asp:DropDownList>
        Service from <asp:TextBox ID="FromBox" runat="server" Columns="10" />
        <ajaxToolkit:CalendarExtender ID="FromCalendar" runat="server" TargetControlID="FromBox" Format="MM/dd/yyyy" />
        to <asp:TextBox ID="ToBox" runat="server" Columns="10" />
        <ajaxToolkit:CalendarExtender ID="ToCalendar" runat="server" TargetControlID="ToBox" Format="MM/dd/yyyy" />
        Claim number <asp:TextBox ID="NumberBox" runat="server" Columns="12" />
        <asp:Button ID="ShowButton" runat="server" Text="Show" OnClick="ShowButton_Click" />
    </asp:Panel>

    <asp:SqlDataSource ID="ClaimSource" runat="server" ConnectionString="<%$ ConnectionStrings:Clinic %>" CancelSelectOnNullParameter="false"
        SelectCommand="SELECT TOP 500 c.ClaimId, c.ClaimNumber, c.PatientId, p.LastName + ', ' + p.FirstName AS PatientName, p.Mrn, py.Name AS PayerName,
                              c.DateOfService, c.TotalCharge, c.PaidAmount, c.AdjustmentAmount, c.PatientResponsibility, c.Status, c.RejectReason,
                              c.SubmittedOn, c.AdjudicatedOn, b.FileName AS BatchFile
                       FROM dbo.Claim c
                       JOIN dbo.Patient p ON p.PatientId = c.PatientId
                       JOIN dbo.Payer py ON py.PayerId = c.PayerId
                       LEFT JOIN dbo.ClaimBatch b ON b.ClaimBatchId = c.ClaimBatchId
                       WHERE (@Status = -1 OR c.Status = @Status)
                         AND (@PayerId = 0 OR c.PayerId = @PayerId)
                         AND c.DateOfService BETWEEN @FromDate AND @ToDate
                         AND (@ClaimNumber IS NULL OR c.ClaimNumber = @ClaimNumber)
                       ORDER BY c.DateOfService DESC, c.ClaimNumber"
        OnSelecting="ClaimSource_Selecting">
        <SelectParameters>
            <asp:ControlParameter Name="Status" ControlID="StatusList" PropertyName="SelectedValue" Type="Int32" />
            <asp:ControlParameter Name="PayerId" ControlID="PayerList" PropertyName="SelectedValue" Type="Int32" />
            <asp:Parameter Name="FromDate" Type="DateTime" />
            <asp:Parameter Name="ToDate" Type="DateTime" />
            <asp:ControlParameter Name="ClaimNumber" ControlID="NumberBox" PropertyName="Text" Type="String" ConvertEmptyStringToNull="true" />
        </SelectParameters>
    </asp:SqlDataSource>

    <asp:GridView ID="ClaimGrid" runat="server" DataSourceID="ClaimSource" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="ClaimId"
        AllowPaging="true" AllowSorting="true" PageSize="30" EmptyDataText="No claims match." OnRowCommand="ClaimGrid_RowCommand" OnRowDataBound="ClaimGrid_RowDataBound">
        <Columns>
            <asp:BoundField DataField="ClaimNumber" HeaderText="Claim" SortExpression="ClaimNumber" />
            <asp:HyperLinkField DataTextField="PatientName" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Patients/Chart.aspx?id={0}" HeaderText="Patient" SortExpression="PatientName" />
            <asp:BoundField DataField="PayerName" HeaderText="Payer" SortExpression="PayerName" />
            <asp:BoundField DataField="DateOfService" HeaderText="Service" DataFormatString="{0:MM/dd/yyyy}" SortExpression="DateOfService" />
            <asp:BoundField DataField="TotalCharge" HeaderText="Charge" DataFormatString="{0:C}" ItemStyle-CssClass="money" />
            <asp:BoundField DataField="PaidAmount" HeaderText="Paid" DataFormatString="{0:C}" ItemStyle-CssClass="money" />
            <asp:BoundField DataField="PatientResponsibility" HeaderText="Patient" DataFormatString="{0:C}" ItemStyle-CssClass="money" />
            <asp:TemplateField HeaderText="Status" SortExpression="Status">
                <ItemTemplate><%# StatusText(Eval("Status")) %></ItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="RejectReason" HeaderText="Reason" />
            <asp:BoundField DataField="BatchFile" HeaderText="File" NullDisplayText="-" />
            <asp:TemplateField>
                <ItemTemplate>
                    <asp:LinkButton ID="ResubmitButton" runat="server" CommandName="Resubmit" CommandArgument='<%# Eval("ClaimId") %>' Text="Resubmit"
                        OnClientClick="return confirm('Put this claim back in the next claim file?');" />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>

    <h2>Claim files</h2>
    <asp:SqlDataSource ID="BatchSource" runat="server" ConnectionString="<%$ ConnectionStrings:Clinic %>"
        SelectCommand="SELECT TOP 20 ClaimBatchId, FileName, InterchangeControlNumber, ClaimCount, TotalCharge, Status, CreatedOn, SentOn, AckStatus, AckOn FROM dbo.ClaimBatch ORDER BY CreatedOn DESC" />
    <asp:GridView ID="BatchGrid" runat="server" DataSourceID="BatchSource" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="No claim files yet.">
        <Columns>
            <asp:BoundField DataField="FileName" HeaderText="File" />
            <asp:BoundField DataField="InterchangeControlNumber" HeaderText="ISA13" />
            <asp:BoundField DataField="ClaimCount" HeaderText="Claims" />
            <asp:BoundField DataField="TotalCharge" HeaderText="Total" DataFormatString="{0:C}" ItemStyle-CssClass="money" />
            <asp:BoundField DataField="Status" HeaderText="Status" />
            <asp:BoundField DataField="CreatedOn" HeaderText="Created" DataFormatString="{0:MM/dd h:mm tt}" />
            <asp:BoundField DataField="SentOn" HeaderText="Sent" DataFormatString="{0:MM/dd h:mm tt}" NullDisplayText="-" />
            <asp:BoundField DataField="AckStatus" HeaderText="999" NullDisplayText="-" />
        </Columns>
    </asp:GridView>
</asp:Content>
