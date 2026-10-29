<%@ Page Title="Statements" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Statements.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Billing.StatementsPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Statements</h1>
    <p>Statements are produced by the nightly billing run. Use this page to see who is due one and to send a single statement now.</p>
    <asp:Panel ID="FilterPanel" runat="server" DefaultButton="ShowButton" CssClass="filters">
        Minimum balance <asp:TextBox ID="MinimumBox" runat="server" Columns="6" Text="10.00" />
        <asp:RangeValidator ID="MinimumRange" runat="server" ControlToValidate="MinimumBox" Type="Currency" MinimumValue="0" MaximumValue="100000" ErrorMessage="Enter an amount" Display="Dynamic" />
        <asp:Button ID="ShowButton" runat="server" Text="Show" OnClick="ShowButton_Click" />
    </asp:Panel>
    <asp:GridView ID="CandidateGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="PatientId" AllowPaging="true" PageSize="30"
        EmptyDataText="No patients are due a statement." OnRowCommand="CandidateGrid_RowCommand" OnPageIndexChanging="CandidateGrid_PageIndexChanging">
        <Columns>
            <asp:HyperLinkField DataTextField="PatientName" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Patients/Chart.aspx?id={0}" HeaderText="Patient" />
            <asp:BoundField DataField="Balance" HeaderText="Balance" DataFormatString="{0:C}" ItemStyle-CssClass="money" />
            <asp:BoundField DataField="Mrn" HeaderText="MRN" />
            <asp:TemplateField>
                <ItemTemplate>
                    <a href='<%# ResolveUrl("~/Handlers/Statement.ashx?id=" + Eval("PatientId")) %>' target="_blank">Preview</a>
                    <asp:LinkButton ID="IssueButton" runat="server" CommandName="Issue" CommandArgument='<%# Eval("PatientId") %>' Text="Issue and print" />
                </ItemTemplate>
            </asp:TemplateField>
        </Columns>
    </asp:GridView>
</asp:Content>
