<%@ Page Title="Schedule" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Day.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Schedule.DayPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Schedule</h1>
    <asp:UpdatePanel ID="SchedulePanel" runat="server">
        <ContentTemplate>
            <div class="toolbar">
                <asp:LinkButton ID="PreviousDay" runat="server" Text="&laquo;" OnClick="PreviousDay_Click" ToolTip="Previous day" />
                <asp:TextBox ID="DateBox" runat="server" Columns="10" AutoPostBack="true" OnTextChanged="Filter_Changed" />
                <ajaxToolkit:CalendarExtender ID="DateCalendar" runat="server" TargetControlID="DateBox" Format="MM/dd/yyyy" />
                <asp:LinkButton ID="NextDay" runat="server" Text="&raquo;" OnClick="NextDay_Click" ToolTip="Next day" />
                <asp:LinkButton ID="TodayLink" runat="server" Text="Today" OnClick="TodayLink_Click" />
                Provider
                <asp:DropDownList ID="ProviderList" runat="server" AutoPostBack="true" OnSelectedIndexChanged="Filter_Changed" AppendDataBoundItems="true" DataTextField="Text" DataValueField="Value">
                    <asp:ListItem Value="" Text="(all)" />
                </asp:DropDownList>
                <asp:CheckBox ID="ShowCancelledBox" runat="server" Text="Show cancelled" AutoPostBack="true" OnCheckedChanged="Filter_Changed" />
                <asp:HyperLink ID="BookLink" runat="server" NavigateUrl="~/Schedule/Book.aspx" CssClass="button">Book appointment</asp:HyperLink>
                <asp:HyperLink ID="PrintLink" runat="server" Target="_blank" CssClass="button">Print</asp:HyperLink>
            </div>
            <asp:ObjectDataSource ID="DaySource" runat="server" TypeName="Southridge.Clinic.StaffWeb.DataSources.AppointmentDataSource" SelectMethod="Day" OnSelecting="DaySource_Selecting">
                <SelectParameters>
                    <asp:Parameter Name="siteId" Type="Int32" />
                    <asp:Parameter Name="date" Type="DateTime" />
                    <asp:ControlParameter Name="providerId" ControlID="ProviderList" PropertyName="SelectedValue" Type="Int32" ConvertEmptyStringToNull="true" />
                    <asp:ControlParameter Name="includeCancelled" ControlID="ShowCancelledBox" PropertyName="Checked" Type="Boolean" />
                </SelectParameters>
            </asp:ObjectDataSource>
            <asp:GridView ID="DayGrid" runat="server" DataSourceID="DaySource" AutoGenerateColumns="false" CssClass="grid schedule" DataKeyNames="AppointmentId,PatientId,Status,Copay"
                EmptyDataText="No appointments." OnRowDataBound="DayGrid_RowDataBound" OnRowCommand="DayGrid_RowCommand">
                <Columns>
                    <asp:TemplateField HeaderText="Time">
                        <ItemTemplate><asp:HyperLink ID="TimeLink" runat="server" NavigateUrl='<%# "~/Schedule/Appointment.aspx?id=" + Eval("AppointmentId") %>' Text='<%# Eval("StartsAt", "{0:h:mm tt}") %>' /></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="ProviderName" HeaderText="Provider" />
                    <asp:TemplateField HeaderText="Patient">
                        <ItemTemplate>
                            <asp:HyperLink ID="PatientLink" runat="server" NavigateUrl='<%# "~/Patients/Chart.aspx?id=" + Eval("PatientId") %>' Text='<%# Eval("PatientName") %>' />
                            <asp:Image ID="AlertIcon" runat="server" ImageUrl="~/Content/alert.gif" AlternateText="Alert" Visible='<%# Eval("HasAlert") %>' />
                            <br /><span class="small"><%# Eval("Mrn") %> &middot; <%# Eval("DateOfBirth", "{0:MM/dd/yyyy}") %></span>
                        </ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField HeaderText="Visit">
                        <ItemTemplate><span class="type" style='<%# "border-left-color:" + Eval("Color") %>'><%# Eval("AppointmentTypeName") %></span> <%# (bool)Eval("IsOverbook") ? "<em>(overbook)</em>" : "" %></ItemTemplate>
                    </asp:TemplateField>
                    <asp:BoundField DataField="Reason" HeaderText="Reason" />
                    <asp:BoundField DataField="PrimaryPayer" HeaderText="Coverage" NullDisplayText="Self pay" />
                    <asp:TemplateField HeaderText="Status">
                        <ItemTemplate><asp:Label ID="StatusLabel" runat="server" /></ItemTemplate>
                    </asp:TemplateField>
                    <asp:TemplateField>
                        <ItemTemplate>
                            <asp:LinkButton ID="CheckInButton" runat="server" CommandName="CheckIn" CommandArgument='<%# Container.DataItemIndex %>' Text="Check in" />
                            <asp:LinkButton ID="RoomButton" runat="server" CommandName="Room" CommandArgument='<%# Container.DataItemIndex %>' Text="Room" />
                            <asp:LinkButton ID="CompleteButton" runat="server" CommandName="Complete" CommandArgument='<%# Container.DataItemIndex %>' Text="Complete" />
                            <asp:LinkButton ID="NoShowButton" runat="server" CommandName="NoShow" CommandArgument='<%# Container.DataItemIndex %>' Text="No show" OnClientClick="return confirm('Mark as no show?');" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>

            <asp:Button ID="HiddenTarget" runat="server" Style="display:none" />
            <ajaxToolkit:ModalPopupExtender ID="CheckInPopup" runat="server" TargetControlID="HiddenTarget" PopupControlID="CheckInDialog" BackgroundCssClass="modal-bg" CancelControlID="CancelCheckIn" />
            <asp:Panel ID="CheckInDialog" runat="server" CssClass="dialog" Style="display:none">
                <h3>Check in <asp:Literal ID="CheckInPatient" runat="server" /></h3>
                <asp:HiddenField ID="CheckInAppointmentId" runat="server" />
                <p>Copay due: <asp:Literal ID="CopayDue" runat="server" /></p>
                <p>Collected <asp:TextBox ID="CopayBox" runat="server" Columns="8" />
                    <asp:CompareValidator ID="CopayNumber" runat="server" ControlToValidate="CopayBox" Type="Currency" Operator="DataTypeCheck" ErrorMessage="Amount" ValidationGroup="CheckIn" Display="Dynamic" /></p>
                <asp:Button ID="ConfirmCheckIn" runat="server" Text="Check in" OnClick="ConfirmCheckIn_Click" ValidationGroup="CheckIn" />
                <asp:Button ID="CancelCheckIn" runat="server" Text="Cancel" CausesValidation="false" />
            </asp:Panel>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
