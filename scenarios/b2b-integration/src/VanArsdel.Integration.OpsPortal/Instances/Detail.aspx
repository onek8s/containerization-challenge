<%@ Page Title="Instance" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Detail.aspx.cs" Inherits="VanArsdel.Integration.OpsPortal.Instances.DetailPage" ValidateRequest="false" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <p><a href="./">&larr; Suspended instances</a></p>
    <asp:Panel ID="GonePanel" runat="server" Visible="false" CssClass="alert alert-info">
        This instance is no longer suspended (it was resumed, terminated or completed). See <a href="../History">History</a>.
    </asp:Panel>

    <asp:Panel ID="InstancePanel" runat="server">
        <h2><asp:Literal ID="ServiceTitle" runat="server" Mode="Encode" /></h2>
        <dl class="dl-horizontal">
            <dt>Instance</dt><dd><asp:Literal ID="InstanceIdText" runat="server" /></dd>
            <dt>Kind</dt><dd><asp:Literal ID="KindText" runat="server" /></dd>
            <dt>Application</dt><dd><asp:Literal ID="ApplicationText" runat="server" Mode="Encode" /></dd>
            <dt>Host / server</dt><dd><asp:Literal ID="HostText" runat="server" Mode="Encode" /></dd>
            <dt>Adapter / address</dt><dd><asp:Literal ID="AddressText" runat="server" Mode="Encode" /></dd>
            <dt>Created / suspended</dt><dd><asp:Literal ID="TimesText" runat="server" /></dd>
            <dt>Error</dt><dd class="error"><pre><asp:Literal ID="ErrorText" runat="server" Mode="Encode" /></pre></dd>
        </dl>

        <asp:PlaceHolder ID="InstanceActions" runat="server">
            <asp:Button ID="ResumeButton" runat="server" Text="Resume" CssClass="btn btn-primary" OnClick="Resume_Click" />
            <asp:Button ID="TerminateButton" runat="server" Text="Terminate" CssClass="btn btn-danger" OnClick="Terminate_Click" data-confirm="Terminate this instance?" />
        </asp:PlaceHolder>

        <h3>Messages</h3>
        <asp:Repeater ID="MessageList" runat="server" ItemType="System.Guid" SelectMethod="GetMessageIds">
            <HeaderTemplate><ul class="nav nav-pills"></HeaderTemplate>
            <ItemTemplate><li class="<%#: Item == SelectedMessageId ? "active" : "" %>"><a href="<%#: "Detail?id=" + InstanceId + "&msg=" + Item %>"><%#: Item %></a></li></ItemTemplate>
            <FooterTemplate></ul></FooterTemplate>
        </asp:Repeater>

        <asp:Panel ID="MessagePanel" runat="server" Visible="false">
            <dl class="dl-horizontal">
                <dt>Message type</dt><dd><asp:Literal ID="MessageTypeText" runat="server" Mode="Encode" /></dd>
                <dt>Partner</dt><dd><asp:Literal ID="PartnerText" runat="server" Mode="Encode" /></dd>
                <dt>Received on</dt><dd><asp:Literal ID="ReceivedOnText" runat="server" Mode="Encode" /></dd>
                <dt>Resubmit to</dt><dd><asp:Literal ID="ResubmitTargetText" runat="server" Mode="Encode" /></dd>
            </dl>

            <ul class="nav nav-tabs">
                <li class="active"><a href="#body" data-toggle="tab">Body</a></li>
                <li><a href="#context" data-toggle="tab">Context</a></li>
            </ul>
            <div class="tab-content">
                <div class="tab-pane active" id="body">
                    <asp:TextBox ID="BodyText" runat="server" TextMode="MultiLine" Rows="22" CssClass="form-control body-view" ReadOnly="true" />
                    <asp:HyperLink ID="DownloadLink" runat="server" Text="Download body" />
                </div>
                <div class="tab-pane" id="context">
                    <asp:GridView ID="ContextGrid" runat="server" ItemType="VanArsdel.Integration.OpsPortal.Models.ContextRow" SelectMethod="GetContext"
                        AutoGenerateColumns="false" CssClass="table table-condensed" GridLines="None">
                        <Columns>
                            <asp:BoundField DataField="Name" HeaderText="Property" />
                            <asp:BoundField DataField="Namespace" HeaderText="Namespace" />
                            <asp:BoundField DataField="Value" HeaderText="Value" />
                            <asp:CheckBoxField DataField="Promoted" HeaderText="Promoted" />
                        </Columns>
                    </asp:GridView>
                </div>
            </div>

            <asp:Panel ID="ResubmitPanel" runat="server" CssClass="panel panel-default">
                <div class="panel-heading">Resubmit this message</div>
                <div class="panel-body">
                    <div class="form-group">
                        <asp:Label runat="server" AssociatedControlID="ReasonText" Text="Reason (logged)" />
                        <asp:TextBox ID="ReasonText" runat="server" CssClass="form-control" MaxLength="400" />
                        <asp:RequiredFieldValidator ID="ReasonRequired" runat="server" ControlToValidate="ReasonText" ValidationGroup="Resubmit" CssClass="error" ErrorMessage="Give a reason, for example the ticket number." Display="Dynamic" />
                    </div>
                    <div class="checkbox"><asp:CheckBox ID="EditBody" runat="server" Text="Edit the body before resubmitting (the edited body is stored with the log entry)" AutoPostBack="true" OnCheckedChanged="EditBody_CheckedChanged" /></div>
                    <div class="checkbox"><asp:CheckBox ID="TerminateOriginal" runat="server" Text="Terminate the suspended instance after resubmitting" Checked="true" /></div>
                    <asp:Button ID="ResubmitButton" runat="server" Text="Resubmit" CssClass="btn btn-warning" ValidationGroup="Resubmit" OnClick="Resubmit_Click" />
                </div>
            </asp:Panel>
        </asp:Panel>
    </asp:Panel>
    <script src="https://ajax.aspnetcdn.com/ajax/bootstrap/3.3.7/bootstrap.min.js"></script>
</asp:Content>
