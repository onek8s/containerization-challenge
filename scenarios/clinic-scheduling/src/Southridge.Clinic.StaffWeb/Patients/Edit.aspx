<%@ Page Title="Patient" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Edit.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Patients.EditPage" %>
<%@ Register TagPrefix="uc" TagName="PatientFields" Src="~/Controls/PatientFields.ascx" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <asp:MultiView ID="EditViews" runat="server">
        <asp:View ID="RegisterView" runat="server">
            <h1>Register patient</h1>
            <asp:Wizard ID="RegisterWizard" runat="server" DisplaySideBar="false" OnNextButtonClick="RegisterWizard_NextButtonClick"
                OnFinishButtonClick="RegisterWizard_FinishButtonClick" FinishCompleteButtonText="Register" CssClass="wizard">
                <WizardSteps>
                    <asp:WizardStep ID="DemographicsStep" runat="server" Title="Demographics">
                        <uc:PatientFields ID="RegisterFields" runat="server" ValidationGroup="" />
                        <asp:Panel ID="DuplicatePanel" runat="server" Visible="false" CssClass="message error">
                            <p>These patients look like the same person:</p>
                            <asp:GridView ID="DuplicateGrid" runat="server" AutoGenerateColumns="false" CssClass="grid">
                                <Columns>
                                    <asp:HyperLinkField DataTextField="DisplayName" HeaderText="Name" DataNavigateUrlFields="PatientId" DataNavigateUrlFormatString="~/Patients/Chart.aspx?id={0}" />
                                    <asp:BoundField DataField="Mrn" HeaderText="MRN" />
                                    <asp:BoundField DataField="DateOfBirth" HeaderText="DOB" DataFormatString="{0:MM/dd/yyyy}" />
                                    <asp:BoundField DataField="City" HeaderText="City" />
                                </Columns>
                            </asp:GridView>
                            <asp:CheckBox ID="NotDuplicateBox" runat="server" Text="This is a different person, register anyway" />
                        </asp:Panel>
                    </asp:WizardStep>
                    <asp:WizardStep ID="InsuranceStep" runat="server" Title="Insurance">
                        <asp:CheckBox ID="SelfPayBox" runat="server" Text="Self pay (no insurance)" />
                        <table class="form">
                            <tr><td>Payer</td><td><asp:DropDownList ID="PayerList" runat="server" DataTextField="Text" DataValueField="Value" /></td></tr>
                            <tr><td>Member ID</td><td><asp:TextBox ID="MemberIdBox" runat="server" MaxLength="30" /></td></tr>
                            <tr><td>Group</td><td><asp:TextBox ID="GroupBox" runat="server" MaxLength="30" /></td></tr>
                            <tr><td>Copay</td><td><asp:TextBox ID="CopayBox" runat="server" Columns="8" Text="0.00" />
                                <asp:CompareValidator ID="CopayType" runat="server" ControlToValidate="CopayBox" Type="Currency" Operator="DataTypeCheck" ErrorMessage="Amount" Display="Dynamic" /></td></tr>
                            <tr><td>Effective</td><td><asp:TextBox ID="EffectiveBox" runat="server" Columns="10" />
                                <ajaxToolkit:CalendarExtender ID="EffectiveCalendar" runat="server" TargetControlID="EffectiveBox" Format="MM/dd/yyyy" /></td></tr>
                        </table>
                    </asp:WizardStep>
                    <asp:WizardStep ID="ReviewStep" runat="server" Title="Review" StepType="Finish">
                        <p>Register <strong><asp:Literal ID="ReviewName" runat="server" /></strong> with <asp:Literal ID="ReviewCoverage" runat="server" />?</p>
                        <p class="hint">The registration is sent to the lab and to St. Catherine Hospital.</p>
                    </asp:WizardStep>
                </WizardSteps>
            </asp:Wizard>
        </asp:View>
        <asp:View ID="UpdateView" runat="server">
            <h1>Edit <asp:Literal ID="EditName" runat="server" /> <span class="small"><asp:Literal ID="EditMrn" runat="server" /></span></h1>
            <uc:PatientFields ID="EditFields" runat="server" ValidationGroup="Edit" />
            <asp:Button ID="SaveButton" runat="server" Text="Save" OnClick="SaveButton_Click" ValidationGroup="Edit" />
            <asp:HyperLink ID="ChartLink" runat="server">Back to chart</asp:HyperLink>
        </asp:View>
    </asp:MultiView>
</asp:Content>
