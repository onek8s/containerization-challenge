<%@ Page Title="Case" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Case.aspx.cs" Inherits="Tailspin.CustomerCare.Web.Cases.CaseDetail" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1><asp:Literal ID="CaseTitle" runat="server" Mode="Encode" /></h1>
    <p id="viewers" class="viewers hidden"></p>
    <p id="changed" class="message hidden">Someone else changed this case. <a href="">Reload</a> before saving.</p>

    <div class="columns">
        <section class="main-column">
            <dl class="facts">
                <dt>Customer</dt><dd><asp:HyperLink ID="CustomerLink" runat="server" /></dd>
                <dt>Status</dt><dd><asp:Literal ID="StatusText" runat="server" /></dd>
                <dt>SLA</dt><dd><asp:Label ID="SlaLabel" runat="server" /></dd>
                <dt>Response due</dt><dd><asp:Literal ID="ResponseDue" runat="server" /></dd>
                <dt>Resolve due</dt><dd><asp:Literal ID="ResolveDue" runat="server" /></dd>
                <dt>Created</dt><dd><asp:Literal ID="Created" runat="server" /></dd>
                <dt>Channel</dt><dd><asp:Literal ID="ChannelText" runat="server" /></dd>
                <dt>Reference</dt><dd><asp:Literal ID="ExternalReference" runat="server" Mode="Encode" /></dd>
            </dl>

            <h2>Description</h2>
            <div class="description"><asp:Literal ID="DescriptionText" runat="server" Mode="Encode" /></div>

            <h2>Notes</h2>
            <asp:Repeater ID="Notes" runat="server" ItemType="Tailspin.CustomerCare.Domain.CaseNote">
                <ItemTemplate>
                    <div class="note <%# Item.IsInternal ? "internal" : "" %>">
                        <div class="note-head"><%#: Tailspin.CustomerCare.Web.Security.UserDirectory.DisplayName(Item.Author) %>, <%#: Tailspin.CustomerCare.Web.Services.LocalTime.Format(Item.CreatedUtc) %><%# Item.IsInternal ? " (internal)" : "" %></div>
                        <div class="note-body"><%#: Item.Body %></div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
            <asp:Panel ID="NotePanel" runat="server" CssClass="form" DefaultButton="AddNoteButton">
                <asp:TextBox ID="NoteText" runat="server" TextMode="MultiLine" Rows="4" CssClass="wide" />
                <label><asp:CheckBox ID="NoteInternal" runat="server" Checked="true" /> Internal note (not shared with the customer)</label>
                <asp:Button ID="AddNoteButton" runat="server" Text="Add note" OnClick="AddNoteButton_Click" ValidationGroup="note" />
                <asp:RequiredFieldValidator ID="NoteRequired" runat="server" ControlToValidate="NoteText" ErrorMessage="Write the note first." CssClass="error" Display="Dynamic" ValidationGroup="note" />
            </asp:Panel>

            <h2>Attachments</h2>
            <asp:Repeater ID="Attachments" runat="server" ItemType="Tailspin.CustomerCare.Domain.CaseAttachment">
                <HeaderTemplate><ul class="files"></HeaderTemplate>
                <ItemTemplate><li><a href="<%# ResolveUrl("~/Cases/Attachment.ashx?id=" + Item.Id) %>"><%#: Item.FileName %></a> <span class="muted"><%# (Item.Length / 1024) %> KB, <%#: Tailspin.CustomerCare.Web.Security.CareUser.ShortName(Item.UploadedBy) %></span></li></ItemTemplate>
                <FooterTemplate></ul></FooterTemplate>
            </asp:Repeater>
            <asp:FileUpload ID="AttachmentUpload" runat="server" />
            <asp:Button ID="UploadButton" runat="server" Text="Upload" OnClick="UploadButton_Click" CausesValidation="false" />
        </section>

        <aside class="side-column">
            <h2>Work on the case</h2>
            <div class="form">
                <label>Move to <asp:DropDownList ID="NextStatus" runat="server" /></label>
                <asp:Button ID="ChangeStatusButton" runat="server" Text="Change status" OnClick="ChangeStatusButton_Click" CausesValidation="false" />
                <label>Assign to <asp:DropDownList ID="Assignee" runat="server" AppendDataBoundItems="true" DataTextField="DisplayName" DataValueField="AccountName"><asp:ListItem Text="(nobody)" Value="" /></asp:DropDownList></label>
                <asp:Button ID="AssignButton" runat="server" Text="Assign" OnClick="AssignButton_Click" CausesValidation="false" />
                <asp:Button ID="TakeButton" runat="server" Text="Take it" OnClick="TakeButton_Click" CausesValidation="false" />
            </div>

            <h2>Edit</h2>
            <div class="form">
                <label>Subject <asp:TextBox ID="EditSubject" runat="server" MaxLength="200" /></label>
                <asp:RequiredFieldValidator ID="EditSubjectRequired" runat="server" ControlToValidate="EditSubject" ErrorMessage="Subject is required." CssClass="error" Display="Dynamic" ValidationGroup="edit" />
                <label>Category <asp:DropDownList ID="EditCategory" runat="server" DataTextField="Name" DataValueField="Code" /></label>
                <label>Priority <asp:DropDownList ID="EditPriority" runat="server" /></label>
                <label>Queue <asp:DropDownList ID="EditQueue" runat="server" DataTextField="Name" DataValueField="Id" /></label>
                <asp:Button ID="SaveButton" runat="server" Text="Save" OnClick="SaveButton_Click" ValidationGroup="edit" />
            </div>

            <asp:PlaceHolder ID="ArticlesSection" runat="server">
                <h2>Knowledge base</h2>
                <ul>
                    <asp:Repeater ID="Articles" runat="server" ItemType="Tailspin.CustomerCare.Domain.KbArticle">
                        <ItemTemplate><li><a href="<%# ResolveUrl("~/kb/" + Item.Slug) %>" target="_blank"><%#: Item.Title %></a></li></ItemTemplate>
                    </asp:Repeater>
                </ul>
            </asp:PlaceHolder>
        </aside>
    </div>
    <asp:HiddenField ID="CaseIdField" runat="server" />
</asp:Content>
<asp:Content ID="Scripts" ContentPlaceHolderID="ScriptsContent" runat="server">
    <script>
        (function () {
            var caseId = parseInt($('#<%= CaseIdField.ClientID %>').val(), 10);
            care.connect(function (hub) {
                hub.server.openCase(caseId).done(function (viewers) { care.showViewers(viewers); });
            });
            care.onViewersChanged(function (id, viewers) { if (id === caseId) { care.showViewers(viewers); } });
            care.onCaseUpdated(function (c) { if (c.Id === caseId) { $('#changed').removeClass('hidden'); } });
        })();
    </script>
</asp:Content>
