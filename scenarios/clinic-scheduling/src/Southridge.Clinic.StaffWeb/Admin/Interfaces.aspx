<%@ Page Title="Interfaces" Language="C#" MasterPageFile="~/Admin/Admin.master" AutoEventWireup="true" CodeBehind="Interfaces.aspx.cs" Inherits="Southridge.Clinic.StaffWeb.Admin.InterfacesPage" %>
<asp:Content ID="Main" ContentPlaceHolderID="AdminContent" runat="server">
    <h1>Interface messages</h1>
    <asp:UpdatePanel ID="InterfaceUpdate" runat="server">
        <ContentTemplate>
            <asp:Panel ID="FilterPanel" runat="server" DefaultButton="ShowButton" CssClass="filters">
                Partner
                <asp:DropDownList ID="PartnerList" runat="server">
                    <asp:ListItem Value="">(all)</asp:ListItem>
                    <asp:ListItem Value="HOSP">Hospital ADT / scheduling</asp:ListItem>
                    <asp:ListItem Value="LAB">Reference lab</asp:ListItem>
                    <asp:ListItem Value="HOSPBILL">Hospital billing</asp:ListItem>
                </asp:DropDownList>
                Status
                <asp:DropDownList ID="StatusList" runat="server">
                    <asp:ListItem Value="">(any)</asp:ListItem>
                    <asp:ListItem Value="0">Pending</asp:ListItem>
                    <asp:ListItem Value="1">Sent</asp:ListItem>
                    <asp:ListItem Value="2">Acknowledged</asp:ListItem>
                    <asp:ListItem Value="3">Processed</asp:ListItem>
                    <asp:ListItem Value="4">Error</asp:ListItem>
                    <asp:ListItem Value="5">Ignored</asp:ListItem>
                </asp:DropDownList>
                From <asp:TextBox ID="FromBox" runat="server" Columns="10" />
                <ajaxToolkit:CalendarExtender ID="FromCalendar" runat="server" TargetControlID="FromBox" Format="MM/dd/yyyy" />
                to <asp:TextBox ID="ToBox" runat="server" Columns="10" />
                <ajaxToolkit:CalendarExtender ID="ToCalendar" runat="server" TargetControlID="ToBox" Format="MM/dd/yyyy" />
                <asp:Button ID="ShowButton" runat="server" Text="Show" OnClick="ShowButton_Click" />
            </asp:Panel>
            <asp:ObjectDataSource ID="MessageSource" runat="server" TypeName="Southridge.Clinic.StaffWeb.DataSources.InterfaceDataSource" SelectMethod="Search"
                OnSelecting="MessageSource_Selecting">
                <SelectParameters>
                    <asp:ControlParameter Name="partner" ControlID="PartnerList" PropertyName="SelectedValue" ConvertEmptyStringToNull="true" />
                    <asp:ControlParameter Name="status" ControlID="StatusList" PropertyName="SelectedValue" Type="Int32" ConvertEmptyStringToNull="true" />
                    <asp:Parameter Name="fromDate" Type="DateTime" />
                    <asp:Parameter Name="toDate" Type="DateTime" />
                </SelectParameters>
            </asp:ObjectDataSource>
            <asp:GridView ID="MessageGrid" runat="server" DataSourceID="MessageSource" AutoGenerateColumns="false" CssClass="grid" DataKeyNames="MessageId"
                AllowPaging="true" PageSize="40" EmptyDataText="No messages." OnRowCommand="MessageGrid_RowCommand" OnRowDataBound="MessageGrid_RowDataBound">
                <Columns>
                    <asp:BoundField DataField="CreatedOn" HeaderText="Created" DataFormatString="{0:MM/dd HH:mm:ss}" />
                    <asp:TemplateField HeaderText="Dir"><ItemTemplate><%# (byte)Eval("Direction") == 0 ? "In" : "Out" %></ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="Partner" HeaderText="Partner" />
                    <asp:BoundField DataField="MessageType" HeaderText="Type" />
                    <asp:BoundField DataField="ControlId" HeaderText="Control id" />
                    <asp:TemplateField HeaderText="Status"><ItemTemplate><%# (Southridge.Clinic.Domain.MessageStatus)(byte)Eval("Status") %></ItemTemplate></asp:TemplateField>
                    <asp:BoundField DataField="Attempts" HeaderText="Tries" />
                    <asp:BoundField DataField="Error" HeaderText="Error" />
                    <asp:TemplateField>
                        <ItemTemplate>
                            <asp:LinkButton ID="ViewButton" runat="server" CommandName="View" CommandArgument='<%# Eval("MessageId") %>' Text="View" />
                            <asp:LinkButton ID="ResendButton" runat="server" CommandName="Resend" CommandArgument='<%# Eval("MessageId") %>' Text="Resend" />
                        </ItemTemplate>
                    </asp:TemplateField>
                </Columns>
            </asp:GridView>
            <asp:Panel ID="PayloadPanel" runat="server" Visible="false" CssClass="payload">
                <h2>Message <asp:Literal ID="PayloadTitle" runat="server" /></h2>
                <pre><asp:Literal ID="PayloadText" runat="server" Mode="Encode" /></pre>
                <h3>Acknowledgement</h3>
                <pre><asp:Literal ID="AckText" runat="server" Mode="Encode" /></pre>
            </asp:Panel>
        </ContentTemplate>
    </asp:UpdatePanel>
</asp:Content>
