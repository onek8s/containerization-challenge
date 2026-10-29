<%@ Page Title="New referral" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="New.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Referrals.NewPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>New referral</h1>
    <asp:ValidationSummary ID="Summary" runat="server" CssClass="validation" />
    <table class="form">
        <tr>
            <th>Patient</th>
            <td>
                <asp:TextBox ID="PatientBox" runat="server" Columns="50" autocomplete="off" />
                <asp:HiddenField ID="PatientId" runat="server" />
                <ajaxToolkit:AutoCompleteExtender ID="PatientComplete" runat="server" TargetControlID="PatientBox" ServicePath="~/Services/PatientLookup.asmx"
                    ServiceMethod="Complete" MinimumPrefixLength="2" CompletionInterval="300" CompletionSetCount="15" FirstRowSelected="true"
                    OnClientItemSelected="clinic.patientSelected" CompletionListCssClass="completion" />
                <asp:CustomValidator ID="PatientRequired" runat="server" ErrorMessage="Choose the patient from the list." Display="Dynamic" OnServerValidate="PatientRequired_ServerValidate">*</asp:CustomValidator>
                <asp:HyperLink ID="NewPatientLink" runat="server" NavigateUrl="~/Patients/Edit.aspx">Register a new patient</asp:HyperLink>
            </td>
        </tr>
        <tr>
            <th>Received by</th>
            <td>
                <asp:RadioButtonList ID="SourceList" runat="server" RepeatDirection="Horizontal" RepeatLayout="Flow">
                    <asp:ListItem Selected="True">Fax</asp:ListItem>
                    <asp:ListItem>Phone</asp:ListItem>
                    <asp:ListItem>Internal</asp:ListItem>
                    <asp:ListItem>Mail</asp:ListItem>
                </asp:RadioButtonList>
            </td>
        </tr>
        <tr>
            <th>Referring provider</th>
            <td>
                <asp:TextBox ID="ReferringNameBox" runat="server" MaxLength="100" Columns="35" />
                <asp:RequiredFieldValidator ID="ReferringNameRequired" runat="server" ControlToValidate="ReferringNameBox" ErrorMessage="Referring provider is required." Display="Dynamic">*</asp:RequiredFieldValidator>
                NPI <asp:TextBox ID="NpiBox" runat="server" MaxLength="10" Columns="11" />
                <asp:CustomValidator ID="NpiValid" runat="server" ControlToValidate="NpiBox" ErrorMessage="The NPI check digit is wrong." Display="Dynamic" OnServerValidate="NpiValid_ServerValidate">*</asp:CustomValidator>
            </td>
        </tr>
        <tr>
            <th>Practice / phone</th>
            <td>
                <asp:TextBox ID="PracticeBox" runat="server" MaxLength="100" Columns="35" />
                <asp:TextBox ID="PhoneBox" runat="server" MaxLength="20" Columns="14" />
                <ajaxToolkit:MaskedEditExtender ID="PhoneMask" runat="server" TargetControlID="PhoneBox" Mask="(999) 999-9999" ClearMaskOnLostFocus="false" />
            </td>
        </tr>
        <tr>
            <th>Specialty</th>
            <td>
                <asp:DropDownList ID="SpecialtyList" runat="server" DataTextField="Text" DataValueField="Value" AutoPostBack="true" OnSelectedIndexChanged="SpecialtyList_SelectedIndexChanged" />
                Requested provider
                <asp:DropDownList ID="ProviderList" runat="server" DataTextField="Name" DataValueField="ProviderId" AppendDataBoundItems="true">
                    <asp:ListItem Value="">(first available)</asp:ListItem>
                </asp:DropDownList>
            </td>
        </tr>
        <tr>
            <th>Priority</th>
            <td>
                <asp:RadioButtonList ID="PriorityList" runat="server" RepeatDirection="Horizontal" RepeatLayout="Flow">
                    <asp:ListItem Value="0" Selected="True">Routine (72 h)</asp:ListItem>
                    <asp:ListItem Value="1">Urgent (24 h)</asp:ListItem>
                    <asp:ListItem Value="2">Stat (4 h)</asp:ListItem>
                </asp:RadioButtonList>
            </td>
        </tr>
        <tr>
            <th>Reason</th>
            <td>
                <asp:TextBox ID="ReasonBox" runat="server" TextMode="MultiLine" Rows="4" Columns="70" />
                <asp:RequiredFieldValidator ID="ReasonRequired" runat="server" ControlToValidate="ReasonBox" ErrorMessage="Reason is required." Display="Dynamic">*</asp:RequiredFieldValidator>
            </td>
        </tr>
        <tr>
            <th>Diagnosis</th>
            <td><asp:TextBox ID="DiagnosisBox" runat="server" MaxLength="8" Columns="9" /> (ICD-10-CM, optional)</td>
        </tr>
        <tr>
            <th>Referral letter</th>
            <td><asp:FileUpload ID="LetterUpload" runat="server" /> PDF or TIFF, up to 10 MB</td>
        </tr>
    </table>
    <asp:Button ID="SaveButton" runat="server" Text="Save referral" OnClick="SaveButton_Click" />
</asp:Content>
