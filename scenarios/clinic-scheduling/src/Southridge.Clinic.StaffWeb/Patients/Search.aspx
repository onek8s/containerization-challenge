<%@ Page Title="Find patient" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Search.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Patients.SearchPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Find patient</h1>
    <asp:Panel ID="SearchPanel" runat="server" DefaultButton="SearchButton" CssClass="toolbar">
        Last <asp:TextBox ID="LastNameBox" runat="server" Columns="14" />
        First <asp:TextBox ID="FirstNameBox" runat="server" Columns="10" />
        DOB <asp:TextBox ID="DateOfBirthBox" runat="server" Columns="10" ToolTip="MM/dd/yyyy" />
        MRN <asp:TextBox ID="MrnBox" runat="server" Columns="10" />
        Phone <asp:TextBox ID="PhoneBox" runat="server" Columns="12" />
        <asp:Button ID="SearchButton" runat="server" Text="Search" OnClick="SearchButton_Click" />
        <asp:HyperLink ID="RegisterLink" runat="server" NavigateUrl="~/Patients/Edit.aspx" CssClass="button">Register new patient</asp:HyperLink>
    </asp:Panel>
    <asp:ObjectDataSource ID="PatientSource" runat="server" TypeName="Southridge.Clinic.StaffWeb.DataSources.PatientDataSource"
        SelectMethod="Search" SelectCountMethod="SearchCount" EnablePaging="true" SortParameterName="sortExpression"
        StartRowIndexParameterName="startRowIndex" MaximumRowsParameterName="maximumRows">
        <SelectParameters>
            <asp:ControlParameter Name="lastName" ControlID="LastNameBox" PropertyName="Text" Type="String" />
            <asp:ControlParameter Name="firstName" ControlID="FirstNameBox" PropertyName="Text" Type="String" />
            <asp:ControlParameter Name="dateOfBirth" ControlID="DateOfBirthBox" PropertyName="Text" Type="String" />
            <asp:ControlParameter Name="mrn" ControlID="MrnBox" PropertyName="Text" Type="String" />
            <asp:ControlParameter Name="phone" ControlID="PhoneBox" PropertyName="Text" Type="String" />
        </SelectParameters>
    </asp:ObjectDataSource>
    <asp:GridView ID="PatientGrid" runat="server" DataSourceID="PatientSource" AutoGenerateColumns="false" CssClass="grid" AllowPaging="true" AllowSorting="true"
        PageSize="25" DataKeyNames="PatientId" EmptyDataText="No patients match." OnRowDataBound="PatientGrid_RowDataBound">
        <PagerSettings Mode="NumericFirstLast" />
        <Columns>
            <asp:HyperLinkField DataTextField="DisplayName" HeaderText="Name" SortExpression="Name" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Patients/Chart.aspx?id={0}" />
            <asp:BoundField DataField="Mrn" HeaderText="MRN" SortExpression="Mrn" />
            <asp:BoundField DataField="DateOfBirth" HeaderText="DOB" DataFormatString="{0:MM/dd/yyyy}" SortExpression="DateOfBirth" />
            <asp:TemplateField HeaderText="Phone">
                <ItemTemplate><%# Southridge.Clinic.Domain.PhoneNumber.Format((string)Eval("MobilePhone") ?? (string)Eval("HomePhone")) %></ItemTemplate>
            </asp:TemplateField>
            <asp:BoundField DataField="City" HeaderText="City" />
            <asp:BoundField DataField="PreferredSite" HeaderText="Clinic" />
            <asp:BoundField DataField="LastVisit" HeaderText="Last visit" DataFormatString="{0:MM/dd/yyyy}" SortExpression="LastVisit" />
            <asp:BoundField DataField="NextAppointment" HeaderText="Next appt" DataFormatString="{0:MM/dd/yyyy}" />
            <asp:BoundField DataField="Balance" HeaderText="Balance" DataFormatString="{0:C}" ItemStyle-CssClass="num" />
            <asp:HyperLinkField Text="Book" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Schedule/Book.aspx?patientId={0}" />
        </Columns>
    </asp:GridView>
</asp:Content>
