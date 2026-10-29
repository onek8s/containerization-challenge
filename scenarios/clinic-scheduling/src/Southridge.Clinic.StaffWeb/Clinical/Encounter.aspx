<%@ Page Title="Visit" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Encounter.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Clinical.EncounterPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Visit <asp:Literal ID="DateLabel" runat="server" /></h1>
    <p class="banner">
        <asp:HyperLink ID="PatientLink" runat="server" /> &middot;
        <asp:Literal ID="ProviderLabel" runat="server" /> &middot;
        <asp:Literal ID="SiteLabel" runat="server" /> &middot;
        <strong><asp:Literal ID="StatusLabel" runat="server" /></strong>
        <asp:HyperLink ID="SummaryLink" runat="server" Target="_blank" Text="Visit summary (PDF)" />
    </p>

    <asp:UpdatePanel ID="EncounterUpdate" runat="server" UpdateMode="Conditional">
        <ContentTemplate>
            <fieldset>
                <legend>Vitals and notes</legend>
                <table class="form">
                    <tr>
                        <th>Chief complaint</th>
                        <td colspan="7"><asp:TextBox ID="ComplaintBox" runat="server" MaxLength="200" Columns="70" /></td>
                    </tr>
                    <tr>
                        <th>BP</th>
                        <td>
                            <asp:TextBox ID="BpBox" runat="server" MaxLength="10" Columns="7" />
                            <asp:RegularExpressionValidator ID="BpFormat" runat="server" ControlToValidate="BpBox" ValidationExpression="^\d{2,3}/\d{2,3}$" ErrorMessage="e.g. 120/80" Display="Dynamic" ValidationGroup="Vitals" />
                        </td>
                        <th>Pulse</th>
                        <td>
                            <asp:TextBox ID="PulseBox" runat="server" MaxLength="3" Columns="4" />
                            <asp:RangeValidator ID="PulseRange" runat="server" ControlToValidate="PulseBox" Type="Integer" MinimumValue="20" MaximumValue="250" ErrorMessage="20-250" Display="Dynamic" ValidationGroup="Vitals" />
                        </td>
                        <th>Weight (lb)</th>
                        <td>
                            <asp:TextBox ID="WeightBox" runat="server" MaxLength="6" Columns="6" />
                            <asp:RangeValidator ID="WeightRange" runat="server" ControlToValidate="WeightBox" Type="Double" MinimumValue="1" MaximumValue="900" ErrorMessage="1-900" Display="Dynamic" ValidationGroup="Vitals" />
                        </td>
                        <th>Temp (F)</th>
                        <td>
                            <asp:TextBox ID="TempBox" runat="server" MaxLength="5" Columns="5" />
                            <asp:RangeValidator ID="TempRange" runat="server" ControlToValidate="TempBox" Type="Double" MinimumValue="90" MaximumValue="110" ErrorMessage="90-110" Display="Dynamic" ValidationGroup="Vitals" />
                        </td>
                    </tr>
                    <tr>
                        <th>Notes</th>
                        <td colspan="7"><asp:TextBox ID="NotesBox" runat="server" TextMode="MultiLine" Rows="6" Columns="90" /></td>
                    </tr>
                </table>
                <asp:Button ID="SaveButton" runat="server" Text="Save" OnClick="SaveButton_Click" ValidationGroup="Vitals" />
                <asp:Button ID="SignButton" runat="server" Text="Sign visit" OnClick="SignButton_Click" ValidationGroup="Vitals"
                    OnClientClick="return confirm('Sign this visit? Notes cannot be changed afterwards.');" />
                <asp:Label ID="SignedLabel" runat="server" CssClass="note" />
            </fieldset>

            <fieldset>
                <legend>Diagnoses (<asp:Literal ID="CodeSetLabel" runat="server" />)</legend>
                <asp:GridView ID="DiagnosisGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="Sequence"
                    EmptyDataText="No diagnoses yet." OnRowDeleting="DiagnosisGrid_RowDeleting">
                    <Columns>
                        <asp:BoundField DataField="Sequence" HeaderText="#" />
                        <asp:BoundField DataField="Code" HeaderText="Code" />
                        <asp:BoundField DataField="Description" HeaderText="Description" />
                        <asp:CommandField ShowDeleteButton="true" DeleteText="Remove" />
                    </Columns>
                </asp:GridView>
                <asp:Panel ID="AddDiagnosisPanel" runat="server" DefaultButton="AddDiagnosisButton">
                    <asp:TextBox ID="DiagnosisBox" runat="server" MaxLength="8" Columns="9" />
                    <ajaxToolkit:AutoCompleteExtender ID="DiagnosisComplete" runat="server" TargetControlID="DiagnosisBox" ServicePath="~/Services/PatientLookup.asmx"
                        ServiceMethod="Diagnoses" MinimumPrefixLength="2" CompletionSetCount="15" UseContextKey="true" />
                    <asp:Button ID="AddDiagnosisButton" runat="server" Text="Add diagnosis" OnClick="AddDiagnosisButton_Click" ValidationGroup="Diagnosis" />
                    <asp:RequiredFieldValidator ID="DiagnosisRequired" runat="server" ControlToValidate="DiagnosisBox" ErrorMessage="Enter a code" Display="Dynamic" ValidationGroup="Diagnosis" />
                </asp:Panel>
            </fieldset>

            <fieldset>
                <legend>Charges</legend>
                <asp:GridView ID="ChargeGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="ChargeId" ShowFooter="true"
                    EmptyDataText="No charges entered." OnRowDeleting="ChargeGrid_RowDeleting" OnRowDataBound="ChargeGrid_RowDataBound">
                    <Columns>
                        <asp:BoundField DataField="LineNumber" HeaderText="Line" />
                        <asp:BoundField DataField="CptCode" HeaderText="CPT" />
                        <asp:BoundField DataField="Description" HeaderText="Description" />
                        <asp:BoundField DataField="Modifiers" HeaderText="Mod" />
                        <asp:BoundField DataField="Units" HeaderText="Units" />
                        <asp:BoundField DataField="DiagnosisPointers" HeaderText="Dx ptr" />
                        <asp:BoundField DataField="Amount" HeaderText="Amount" DataFormatString="{0:C}" ItemStyle-CssClass="money" FooterStyle-CssClass="money" />
                        <asp:BoundField DataField="StatusText" HeaderText="Status" />
                        <asp:CommandField ShowDeleteButton="true" DeleteText="Void" />
                    </Columns>
                </asp:GridView>
                <asp:Panel ID="AddChargePanel" runat="server" DefaultButton="AddChargeButton">
                    CPT <asp:DropDownList ID="CptList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true">
                        <asp:ListItem Value="">(select)</asp:ListItem>
                    </asp:DropDownList>
                    Mod <asp:TextBox ID="ModifierBox" runat="server" MaxLength="2" Columns="2" />
                    Units <asp:TextBox ID="UnitsBox" runat="server" MaxLength="2" Columns="2" Text="1" />
                    Dx pointers <asp:TextBox ID="PointerBox" runat="server" MaxLength="4" Columns="4" Text="1" ToolTip="Up to four diagnosis numbers, e.g. 12" />
                    <asp:Button ID="AddChargeButton" runat="server" Text="Add charge" OnClick="AddChargeButton_Click" ValidationGroup="Charge" />
                    <asp:RequiredFieldValidator ID="CptRequired" runat="server" ControlToValidate="CptList" ErrorMessage="Pick a procedure" Display="Dynamic" ValidationGroup="Charge" />
                    <asp:RangeValidator ID="UnitsRange" runat="server" ControlToValidate="UnitsBox" Type="Integer" MinimumValue="1" MaximumValue="99" ErrorMessage="1-99 units" Display="Dynamic" ValidationGroup="Charge" />
                    <asp:RegularExpressionValidator ID="PointerFormat" runat="server" ControlToValidate="PointerBox" ValidationExpression="^[1-9]{1,4}$" ErrorMessage="Digits 1-9" Display="Dynamic" ValidationGroup="Charge" />
                </asp:Panel>
                <asp:Button ID="ReadyButton" runat="server" Text="Charges complete" OnClick="ReadyButton_Click" CausesValidation="false" />
            </fieldset>

            <fieldset>
                <legend>Lab orders</legend>
                <asp:GridView ID="LabOrderGrid" runat="server" AutoGenerateColumns="false" CssClass="grid" EmptyDataText="No lab orders.">
                    <Columns>
                        <asp:BoundField DataField="OrderNumber" HeaderText="Order" />
                        <asp:BoundField DataField="TestName" HeaderText="Test" />
                        <asp:BoundField DataField="Priority" HeaderText="Priority" />
                        <asp:BoundField DataField="Status" HeaderText="Status" />
                        <asp:BoundField DataField="OrderedOn" HeaderText="Ordered" DataFormatString="{0:MM/dd h:mm tt}" />
                        <asp:BoundField DataField="ResultedOn" HeaderText="Resulted" DataFormatString="{0:MM/dd h:mm tt}" NullDisplayText="-" />
                    </Columns>
                </asp:GridView>
                <asp:Panel ID="AddLabPanel" runat="server">
                    <asp:DropDownList ID="LabTestList" runat="server" DataTextField="Name" DataValueField="TestCode" />
                    <asp:DropDownList ID="LabPriorityList" runat="server">
                        <asp:ListItem Value="R">Routine</asp:ListItem>
                        <asp:ListItem Value="S">Stat</asp:ListItem>
                    </asp:DropDownList>
                    <asp:TextBox ID="ClinicalInfoBox" runat="server" MaxLength="200" Columns="40" ToolTip="Clinical information for the lab" />
                    <asp:Button ID="OrderLabButton" runat="server" Text="Order" OnClick="OrderLabButton_Click" CausesValidation="false" />
                </asp:Panel>
            </fieldset>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
