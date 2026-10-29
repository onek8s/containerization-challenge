<%@ Page Title="Book a visit" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.cs" Inherits="Southridge.Clinic.PatientWeb.Book.BookPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Book a visit</h1>
    <asp:UpdatePanel ID="BookUpdate" runat="server">
        <ContentTemplate>
            <asp:Wizard ID="BookWizard" runat="server" DisplaySideBar="false" ActiveStepIndex="0" CssClass="wizard"
                OnNextButtonClick="BookWizard_NextButtonClick" OnFinishButtonClick="BookWizard_FinishButtonClick"
                StartNextButtonText="Find times" StepNextButtonText="Continue" FinishCompleteButtonText="Book this visit" FinishPreviousButtonText="Back">
                <WizardSteps>
                    <asp:WizardStep ID="VisitStep" runat="server" Title="Visit">
                        <table class="form">
                            <tr>
                                <th>Type of visit</th>
                                <td>
                                    <asp:DropDownList ID="TypeList" runat="server" DataTextField="Name" DataValueField="AppointmentTypeId" />
                                </td>
                            </tr>
                            <tr>
                                <th>Clinic</th>
                                <td>
                                    <asp:DropDownList ID="SiteList" runat="server" DataTextField="Name" DataValueField="SiteId" AppendDataBoundItems="true">
                                        <asp:ListItem Value="0">Any clinic</asp:ListItem>
                                    </asp:DropDownList>
                                </td>
                            </tr>
                            <tr>
                                <th>From</th>
                                <td>
                                    <asp:DropDownList ID="WeekList" runat="server" />
                                </td>
                            </tr>
                        </table>
                    </asp:WizardStep>
                    <asp:WizardStep ID="TimeStep" runat="server" Title="Time">
                        <p><asp:Literal ID="SlotSummary" runat="server" /></p>
                        <asp:RadioButtonList ID="SlotList" runat="server" RepeatLayout="UnorderedList" CssClass="slots" />
                        <asp:Label ID="NoSlotsLabel" runat="server" Visible="false" Text="There are no online times in that week. Try another week or clinic, or call us." />
                    </asp:WizardStep>
                    <asp:WizardStep ID="ConfirmStep" runat="server" Title="Confirm" StepType="Finish">
                        <p><strong><asp:Literal ID="ConfirmText" runat="server" Mode="Encode" /></strong></p>
                        <p>
                            Reason for the visit (optional)<br />
                            <asp:TextBox ID="ReasonBox" runat="server" MaxLength="200" Columns="60" />
                        </p>
                        <p class="small">If you need to cancel, please do so at least 24 hours ahead.</p>
                    </asp:WizardStep>
                    <asp:WizardStep ID="DoneStep" runat="server" Title="Booked" StepType="Complete">
                        <h2>You are booked</h2>
                        <p><asp:Literal ID="DoneText" runat="server" Mode="Encode" /></p>
                        <p>Confirmation code: <strong><asp:Literal ID="CodeText" runat="server" /></strong></p>
                        <p>
                            <asp:HyperLink ID="IntakeLink" runat="server">Fill in the pre-visit form now</asp:HyperLink> &middot;
                            <asp:HyperLink ID="AppointmentsLink" runat="server" NavigateUrl="~/MyHealth/Appointments">My appointments</asp:HyperLink>
                        </p>
                    </asp:WizardStep>
                </WizardSteps>
            </asp:Wizard>
            <asp:UpdateProgress ID="BookProgress" runat="server" AssociatedUpdatePanelID="BookUpdate" DisplayAfter="300">
                <ProgressTemplate><p class="small">Looking for times...</p></ProgressTemplate>
            </asp:UpdateProgress>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
