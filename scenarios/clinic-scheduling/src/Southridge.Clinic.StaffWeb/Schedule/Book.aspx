<%@ Page Title="Book appointment" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Book.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Schedule.BookPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>Book appointment</h1>
    <asp:UpdatePanel ID="BookPanel" runat="server">
        <ContentTemplate>
            <asp:MultiView ID="BookViews" runat="server" ActiveViewIndex="0">
                <asp:View ID="SearchView" runat="server">
                    <table class="form">
                        <tr>
                            <td>Patient</td>
                            <td>
                                <asp:TextBox ID="PatientBox" runat="server" Columns="50" autocomplete="off" />
                                <asp:HiddenField ID="PatientId" runat="server" />
                                <ajaxToolkit:AutoCompleteExtender ID="PatientComplete" runat="server" TargetControlID="PatientBox" ServicePath="~/Services/PatientLookup.asmx"
                                    ServiceMethod="Complete" MinimumPrefixLength="2" CompletionInterval="300" CompletionSetCount="15" FirstRowSelected="true"
                                    OnClientItemSelected="clinic.patientSelected" CompletionListCssClass="completion" />
                                <asp:HyperLink ID="NewPatientLink" runat="server" NavigateUrl="~/Patients/Edit.aspx">New patient</asp:HyperLink>
                            </td>
                        </tr>
                        <tr>
                            <td>Visit type</td>
                            <td><asp:DropDownList ID="TypeList" runat="server" DataTextField="Text" DataValueField="Value" /></td>
                        </tr>
                        <tr>
                            <td>Provider</td>
                            <td><asp:DropDownList ID="ProviderList" runat="server" DataTextField="Text" DataValueField="Value" AppendDataBoundItems="true"><asp:ListItem Value="" Text="(any)" /></asp:DropDownList>
                                <asp:CheckBox ID="AllSitesBox" runat="server" Text="All sites" AutoPostBack="true" OnCheckedChanged="AllSitesBox_CheckedChanged" /></td>
                        </tr>
                        <tr>
                            <td>From</td>
                            <td><asp:TextBox ID="FromBox" runat="server" Columns="10" />
                                <ajaxToolkit:CalendarExtender ID="FromCalendar" runat="server" TargetControlID="FromBox" Format="MM/dd/yyyy" />
                                for <asp:DropDownList ID="DaysList" runat="server"><asp:ListItem>3</asp:ListItem><asp:ListItem Selected="True">7</asp:ListItem><asp:ListItem>14</asp:ListItem><asp:ListItem>28</asp:ListItem></asp:DropDownList> days</td>
                        </tr>
                        <tr>
                            <td>Reason</td>
                            <td><asp:TextBox ID="ReasonBox" runat="server" Columns="60" MaxLength="200" /></td>
                        </tr>
                    </table>
                    <asp:Button ID="FindButton" runat="server" Text="Find times" OnClick="FindButton_Click" />
                    <asp:Repeater ID="SlotRepeater" runat="server" OnItemCommand="SlotRepeater_ItemCommand">
                        <HeaderTemplate><table class="grid slots"><tr><th>Time</th><th>Provider</th><th>Site</th><th></th></tr></HeaderTemplate>
                        <ItemTemplate>
                            <tr>
                                <td><%# Eval("Start", "{0:ddd MM/dd h:mm tt}") %></td>
                                <td><%# Eval("ProviderName") %></td>
                                <td><%# Eval("SiteName") %></td>
                                <td><asp:LinkButton ID="PickButton" runat="server" CommandName="Book" CommandArgument='<%# Eval("ProviderId") + "|" + Eval("SiteId") + "|" + ((DateTime)Eval("Start")).ToString("o") %>' Text="Book" /></td>
                            </tr>
                        </ItemTemplate>
                        <FooterTemplate></table></FooterTemplate>
                    </asp:Repeater>
                    <asp:Panel ID="NoSlotsPanel" runat="server" Visible="false" CssClass="message info">
                        No open times in this range.
                        <asp:LinkButton ID="WaitlistButton" runat="server" Text="Add the patient to the waitlist" OnClick="WaitlistButton_Click" />
                    </asp:Panel>
                    <asp:Panel ID="OverbookPanel" runat="server" Visible="false">
                        <h3>Overbook</h3>
                        <asp:TextBox ID="OverbookTime" runat="server" Columns="18" ToolTip="MM/dd/yyyy h:mm AM" />
                        <asp:Button ID="OverbookButton" runat="server" Text="Book anyway" OnClick="OverbookButton_Click" OnClientClick="return confirm('Book outside the template?');" />
                    </asp:Panel>
                </asp:View>
                <asp:View ID="DoneView" runat="server">
                    <div class="message info">
                        Booked <asp:Literal ID="BookedWhen" runat="server" />. Confirmation code <strong><asp:Literal ID="BookedCode" runat="server" /></strong>.
                    </div>
                    <asp:HyperLink ID="LetterLink" runat="server" Target="_blank">Print confirmation letter</asp:HyperLink> |
                    <asp:HyperLink ID="AppointmentLink" runat="server">Open appointment</asp:HyperLink> |
                    <asp:LinkButton ID="AnotherButton" runat="server" Text="Book another" OnClick="AnotherButton_Click" />
                </asp:View>
            </asp:MultiView>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
