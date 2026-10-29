using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.UI.WebControls;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Data.Services;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Security;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Cases
{
    [Serializable]
    public class NewCaseDraft
    {
        public int? CustomerId { get; set; }
        public string CustomerLabel { get; set; }
        public string CategoryCode { get; set; }
        public string Subject { get; set; }
        public string Description { get; set; }
        public CasePriority Priority { get; set; } = CasePriority.Normal;
        public CaseChannel Channel { get; set; } = CaseChannel.Phone;
        public string QueueCode { get; set; }
        public bool AssignToMe { get; set; }
        /// <summary>Uploaded files waiting in the upload folder: stored name -> original name.</summary>
        public Dictionary<string, string> Files { get; set; } = new Dictionary<string, string>();
    }

    public partial class NewCase : CarePage
    {
        private const string DraftKey = "Cases.NewDraft";

        private NewCaseDraft Draft
        {
            get
            {
                var d = Session[DraftKey] as NewCaseDraft;
                if (d == null)
                {
                    d = new NewCaseDraft();
                    Session[DraftKey] = d;
                }

                return d;
            }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            Category.DataSource = ReferenceData.Categories.Where(c => c.IsActive).ToList();
            Category.DataBind();
            QueueOverride.DataSource = ReferenceData.ActiveQueues;
            QueueOverride.DataBind();
            foreach (CasePriority p in Enum.GetValues(typeof(CasePriority))) Priority.Items.Add(new ListItem(p.ToString(), p.ToString()));
            foreach (CaseChannel c in Enum.GetValues(typeof(CaseChannel))) Channel.Items.Add(new ListItem(c.ToString(), c.ToString()));

            // Started from a customer page.
            int customerId;
            if (int.TryParse(Request.QueryString["customer"], out customerId))
            {
                SelectCustomer(customerId);
            }

            ShowDraft();
        }

        protected void CustomerSearchButton_Click(object sender, EventArgs e)
        {
            CustomerResults.DataSource = new CustomerRepository().Search(CustomerSearch.Text, 20);
            CustomerResults.DataBind();
        }

        protected void CustomerResults_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectCustomer((int)CustomerResults.SelectedDataKey.Value);
        }

        protected void Category_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowSuggestions(Category.SelectedValue);
        }

        protected void UploadButton_Click(object sender, EventArgs e)
        {
            SaveDetails();
            if (!AttachmentUpload.HasFiles) return;

            var folder = WorkFolders.UploadsFor(Session.SessionID);
            foreach (var file in AttachmentUpload.PostedFiles)
            {
                if (file.ContentLength == 0) continue;
                if (file.ContentLength > CareSettings.MaxAttachmentBytes)
                {
                    ShowMessage(Path.GetFileName(file.FileName) + " is too large.", true);
                    continue;
                }

                var stored = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName);
                file.SaveAs(Path.Combine(folder, stored));
                Draft.Files[stored] = Path.GetFileName(file.FileName);
            }

            ShowFiles();
        }

        protected void NewCaseWizard_NextButtonClick(object sender, WizardNavigationEventArgs e)
        {
            if (e.CurrentStepIndex == 0 && Draft.CustomerId == null)
            {
                ShowMessage("Select a customer first.", true);
                e.Cancel = true;
                return;
            }

            if (e.CurrentStepIndex == 1)
            {
                SaveDetails();
            }
        }

        protected void NewCaseWizard_ActiveStepChanged(object sender, EventArgs e)
        {
            if (NewCaseWizard.ActiveStep == ReviewStep) ShowReview();
        }

        protected void NewCaseWizard_FinishButtonClick(object sender, WizardNavigationEventArgs e)
        {
            var d = Draft;
            Case created = null;
            var ok = Try(() =>
            {
                var service = new CaseService();
                created = service.Create(new NewCaseRequest
                {
                    CustomerId = d.CustomerId.GetValueOrDefault(),
                    CategoryCode = d.CategoryCode,
                    Subject = d.Subject,
                    Description = d.Description,
                    Priority = d.Priority,
                    Channel = d.Channel,
                    QueueCode = d.QueueCode,
                    AssignToMe = d.AssignToMe
                }, AccountName);

                var folder = WorkFolders.UploadsFor(Session.SessionID);
                foreach (var f in d.Files)
                {
                    var path = Path.Combine(folder, f.Key);
                    if (!File.Exists(path)) continue;
                    service.AddAttachment(created.Id, f.Value, System.Web.MimeMapping.GetMimeMapping(f.Value), File.ReadAllBytes(path), AccountName);
                    File.Delete(path);
                }
            });

            if (!ok)
            {
                e.Cancel = true;
                return;
            }

            Session.Remove(DraftKey);
            Response.Redirect("~/cases/" + created.Id, false);
            Context.ApplicationInstance.CompleteRequest();
        }

        protected void NewCaseWizard_CancelButtonClick(object sender, EventArgs e)
        {
            var folder = WorkFolders.UploadsFor(Session.SessionID);
            foreach (var f in Draft.Files.Keys)
            {
                var path = Path.Combine(folder, f);
                if (File.Exists(path)) File.Delete(path);
            }

            Session.Remove(DraftKey);
            Response.Redirect("~/cases", false);
            Context.ApplicationInstance.CompleteRequest();
        }

        private void SelectCustomer(int id)
        {
            var c = new CustomerRepository().Get(id);
            if (c == null) return;

            Draft.CustomerId = c.Id;
            Draft.CustomerLabel = c.Name + " (" + c.AccountNumber + ")" + (c.IsPriorityCustomer ? ", priority customer" : string.Empty);
            SelectedCustomer.Text = Draft.CustomerLabel;
        }

        private void SaveDetails()
        {
            var d = Draft;
            d.CategoryCode = Category.SelectedValue;
            d.Subject = Subject.Text.Trim();
            d.Description = Description.Text;
            d.Priority = (CasePriority)Enum.Parse(typeof(CasePriority), Priority.SelectedValue);
            d.Channel = (CaseChannel)Enum.Parse(typeof(CaseChannel), Channel.SelectedValue);
            d.QueueCode = QueueOverride.SelectedValue;
            d.AssignToMe = AssignToMe.Checked;
        }

        private void ShowDraft()
        {
            var d = Draft;
            SelectedCustomer.Text = d.CustomerLabel ?? "none";
            if (d.CategoryCode != null) Category.SelectedValue = d.CategoryCode;
            Subject.Text = d.Subject;
            Description.Text = d.Description;
            Priority.SelectedValue = d.Priority.ToString();
            Channel.SelectedValue = d.Channel.ToString();
            QueueOverride.SelectedValue = d.QueueCode ?? string.Empty;
            AssignToMe.Checked = d.AssignToMe;
            ShowFiles();
            ShowSuggestions(Category.SelectedValue);
        }

        private void ShowFiles()
        {
            UploadedFiles.DataSource = Draft.Files.Values.ToList();
            UploadedFiles.DataBind();
        }

        private void ShowSuggestions(string categoryCode)
        {
            var articles = string.IsNullOrEmpty(categoryCode) ? new List<KbArticle>() : new KnowledgeRepository().ForCategory(categoryCode, 5);
            Suggestions.Visible = articles.Count > 0;
            SuggestedArticles.DataSource = articles;
            SuggestedArticles.DataBind();
        }

        private void ShowReview()
        {
            var d = Draft;
            var customer = new CustomerRepository().Get(d.CustomerId.GetValueOrDefault());
            var queueCode = string.IsNullOrEmpty(d.QueueCode) ? ReferenceData.Router.Route(d.CategoryCode, customer) : d.QueueCode;
            var queue = ReferenceData.QueueByCode(queueCode);
            var priority = CaseRouter.InitialPriority(d.Priority, customer);
            var due = ReferenceData.SlaPolicy.Calculate(DateTime.UtcNow, priority, customer != null && customer.IsPriorityCustomer);

            ReviewCustomer.Text = d.CustomerLabel;
            ReviewSubject.Text = d.Subject;
            ReviewCategory.Text = ReferenceData.CategoryName(d.CategoryCode);
            ReviewQueue.Text = queue != null ? queue.Name : queueCode;
            ReviewPriority.Text = priority == d.Priority ? priority.ToString() : priority + " (raised for priority customer)";
            ReviewResponseDue.Text = LocalTime.Format(due.ResponseDueUtc);
            ReviewResolveDue.Text = LocalTime.Format(due.ResolveDueUtc);
            ReviewAttachments.Text = d.Files.Count == 0 ? "none" : string.Join(", ", d.Files.Values);
        }
    }
}
