<%@ Page Title="New case" Language="C#" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="New.aspx.cs" Inherits="Tailspin.CustomerCare.Web.Cases.NewCase" %>
<asp:Content ID="Main" ContentPlaceHolderID="MainContent" runat="server">
    <h1>New case</h1>
    <asp:Wizard ID="NewCaseWizard" runat="server" DisplaySideBar="true" CssClass="wizard" SideBarStyle-CssClass="wizard-steps"
        OnNextButtonClick="NewCaseWizard_NextButtonClick" OnFinishButtonClick="NewCaseWizard_FinishButtonClick"
        OnActiveStepChanged="NewCaseWizard_ActiveStepChanged" OnCancelButtonClick="NewCaseWizard_CancelButtonClick"
        DisplayCancelButton="true" CancelButtonText="Discard" FinishCompleteButtonText="Create case">
        <WizardSteps>
            <asp:WizardStep ID="CustomerStep" runat="server" Title="1. Customer" StepType="Start">
                <h2>Find the customer</h2>
                <asp:Panel ID="CustomerSearchPanel" runat="server" DefaultButton="CustomerSearchButton">
                    <asp:TextBox ID="CustomerSearch" runat="server" placeholder="Name, account number, e-mail or phone" MaxLength="100" />
                    <asp:Button ID="CustomerSearchButton" runat="server" Text="Find" OnClick="CustomerSearchButton_Click" CausesValidation="false" />
                </asp:Panel>
                <asp:GridView ID="CustomerResults" runat="server" AutoGenerateColumns="false" CssClass="grid" GridLines="None" DataKeyNames="Id"
                    OnSelectedIndexChanged="CustomerResults_SelectedIndexChanged" EmptyDataText="No customers found.">
                    <Columns>
                        <asp:ButtonField CommandName="Select" Text="Select" />
                        <asp:BoundField DataField="AccountNumber" HeaderText="Account" />
                        <asp:BoundField DataField="Name" HeaderText="Name" />
                        <asp:BoundField DataField="City" HeaderText="City" />
                        <asp:BoundField DataField="Segment" HeaderText="Segment" />
                    </Columns>
                </asp:GridView>
                <p><strong>Selected:</strong> <asp:Literal ID="SelectedCustomer" runat="server" Mode="Encode" Text="none" /></p>
            </asp:WizardStep>

            <asp:WizardStep ID="DetailsStep" runat="server" Title="2. Details" StepType="Step">
                <h2>Case details</h2>
                <div class="form">
                    <label>Category <asp:DropDownList ID="Category" runat="server" DataTextField="Name" DataValueField="Code" AutoPostBack="true" OnSelectedIndexChanged="Category_SelectedIndexChanged" /></label>
                    <label>Subject <asp:TextBox ID="Subject" runat="server" MaxLength="200" CssClass="wide" />
                        <asp:RequiredFieldValidator ID="SubjectRequired" runat="server" ControlToValidate="Subject" ErrorMessage="Subject is required." CssClass="error" Display="Dynamic" />
                    </label>
                    <label>Description <asp:TextBox ID="Description" runat="server" TextMode="MultiLine" Rows="6" CssClass="wide" /></label>
                    <label>Priority <asp:DropDownList ID="Priority" runat="server" /></label>
                    <label>Channel <asp:DropDownList ID="Channel" runat="server" /></label>
                    <label>Queue <asp:DropDownList ID="QueueOverride" runat="server" AppendDataBoundItems="true" DataTextField="Name" DataValueField="Code"><asp:ListItem Text="Automatic (routing rules)" Value="" /></asp:DropDownList></label>
                    <label><asp:CheckBox ID="AssignToMe" runat="server" /> Assign to me</label>
                    <label>Attachments
                        <asp:FileUpload ID="AttachmentUpload" runat="server" AllowMultiple="true" />
                        <asp:Button ID="UploadButton" runat="server" Text="Add" OnClick="UploadButton_Click" CausesValidation="false" />
                    </label>
                    <asp:BulletedList ID="UploadedFiles" runat="server" CssClass="files" />
                </div>
                <asp:PlaceHolder ID="Suggestions" runat="server" Visible="false">
                    <h3>Knowledge base</h3>
                    <ul>
                        <asp:Repeater ID="SuggestedArticles" runat="server" ItemType="Tailspin.CustomerCare.Domain.KbArticle">
                            <ItemTemplate><li><a href="<%# ResolveUrl("~/kb/" + Item.Slug) %>" target="_blank"><%#: Item.Title %></a></li></ItemTemplate>
                        </asp:Repeater>
                    </ul>
                </asp:PlaceHolder>
            </asp:WizardStep>

            <asp:WizardStep ID="ReviewStep" runat="server" Title="3. Review" StepType="Finish">
                <h2>Review</h2>
                <dl class="review">
                    <dt>Customer</dt><dd><asp:Literal ID="ReviewCustomer" runat="server" Mode="Encode" /></dd>
                    <dt>Subject</dt><dd><asp:Literal ID="ReviewSubject" runat="server" Mode="Encode" /></dd>
                    <dt>Category</dt><dd><asp:Literal ID="ReviewCategory" runat="server" Mode="Encode" /></dd>
                    <dt>Queue</dt><dd><asp:Literal ID="ReviewQueue" runat="server" Mode="Encode" /></dd>
                    <dt>Priority</dt><dd><asp:Literal ID="ReviewPriority" runat="server" Mode="Encode" /></dd>
                    <dt>First response due</dt><dd><asp:Literal ID="ReviewResponseDue" runat="server" /></dd>
                    <dt>Resolve due</dt><dd><asp:Literal ID="ReviewResolveDue" runat="server" /></dd>
                    <dt>Attachments</dt><dd><asp:Literal ID="ReviewAttachments" runat="server" Mode="Encode" /></dd>
                </dl>
            </asp:WizardStep>
        </WizardSteps>
    </asp:Wizard>
</asp:Content>
