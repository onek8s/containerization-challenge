using System;
using System.Linq;
using System.Web;
using System.Web.UI.WebControls;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Data.Services;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Security;
using Tailspin.CustomerCare.Web.Services;

namespace Tailspin.CustomerCare.Web.Cases
{
    public partial class CaseDetail : CarePage
    {
        private readonly CaseRepository _cases = new CaseRepository();
        private readonly CaseService _service = new CaseService();

        private int CaseId
        {
            get { return (int)ViewState["CaseId"]; }
            set { ViewState["CaseId"] = value; }
        }

        /// <summary>Version of the case the page was rendered from; saving with an older version fails.</summary>
        private byte[] RowVersion
        {
            get { return (byte[])ViewState["RowVersion"]; }
            set { ViewState["RowVersion"] = value; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            var id = QueryInt("id");
            if (id == null) throw new HttpException(404, "Case not found.");

            EditCategory.DataSource = ReferenceData.Categories;
            EditCategory.DataBind();
            EditQueue.DataSource = ReferenceData.Queues;
            EditQueue.DataBind();
            foreach (CasePriority p in Enum.GetValues(typeof(CasePriority))) EditPriority.Items.Add(new ListItem(p.ToString(), ((int)p).ToString()));
            Assignee.DataSource = UserDirectory.Members(CareRole.Agent);
            Assignee.DataBind();

            CaseId = id.Value;
            Show();
        }

        protected void ChangeStatusButton_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(NextStatus.SelectedValue)) return;
            var status = (CaseStatus)int.Parse(NextStatus.SelectedValue);
            if (Try(() => _service.ChangeStatus(CaseId, RowVersion, status, AccountName)))
            {
                ShowMessage("Status changed to " + CaseWorkflow.DisplayName(status) + ".");
            }

            Show();
        }

        protected void AssignButton_Click(object sender, EventArgs e)
        {
            var assignee = string.IsNullOrEmpty(Assignee.SelectedValue) ? null : Assignee.SelectedValue;
            if (Try(() => _service.Assign(CaseId, assignee, AccountName)))
            {
                ShowMessage(assignee == null ? "Case returned to the queue." : "Case assigned to " + UserDirectory.DisplayName(assignee) + ".");
            }

            Show();
        }

        protected void TakeButton_Click(object sender, EventArgs e)
        {
            if (Try(() => _service.Assign(CaseId, AccountName, AccountName)))
            {
                ShowMessage("The case is now yours.");
            }

            Show();
        }

        protected void SaveButton_Click(object sender, EventArgs e)
        {
            if (!IsValid) return;
            if (Try(() => _service.Update(CaseId, RowVersion, EditSubject.Text.Trim(), EditCategory.SelectedValue,
                (CasePriority)int.Parse(EditPriority.SelectedValue), int.Parse(EditQueue.SelectedValue), AccountName)))
            {
                ShowMessage("Case saved.");
            }

            Show();
        }

        protected void AddNoteButton_Click(object sender, EventArgs e)
        {
            if (!IsValid) return;
            if (Try(() => _service.AddNote(CaseId, NoteText.Text, NoteInternal.Checked, AccountName)))
            {
                NoteText.Text = string.Empty;
            }

            Show();
        }

        protected void UploadButton_Click(object sender, EventArgs e)
        {
            if (!AttachmentUpload.HasFile) return;
            var file = AttachmentUpload.PostedFile;
            var content = new byte[file.ContentLength];
            file.InputStream.Read(content, 0, content.Length);

            if (Try(() => _service.AddAttachment(CaseId, file.FileName, file.ContentType, content, AccountName)))
            {
                ShowMessage(AttachmentUpload.FileName + " uploaded.");
            }

            Show();
        }

        private void Show()
        {
            var c = _cases.Get(CaseId);
            if (c == null) throw new HttpException(404, "Case not found.");

            RowVersion = c.RowVersion;
            CaseIdField.Value = c.Id.ToString();
            Title = c.CaseNumber;
            CaseTitle.Text = c.CaseNumber + " " + c.Subject;

            CustomerLink.Text = HttpUtility.HtmlEncode(c.Customer.Name + " (" + c.Customer.AccountNumber + ")");
            CustomerLink.NavigateUrl = "~/customers/" + c.CustomerId;
            StatusText.Text = HttpUtility.HtmlEncode(CaseWorkflow.DisplayName(c.Status) + (string.IsNullOrEmpty(c.AssignedTo) ? ", unassigned" : ", " + UserDirectory.DisplayName(c.AssignedTo)));
            ChannelText.Text = c.Channel.ToString();
            ExternalReference.Text = c.ExternalReference;
            Created.Text = HttpUtility.HtmlEncode(LocalTime.Format(c.CreatedUtc) + " by " + UserDirectory.DisplayName(c.CreatedBy));
            ResponseDue.Text = LocalTime.Format(c.ResponseDueUtc) + (c.FirstResponseUtc.HasValue ? " (answered " + LocalTime.Format(c.FirstResponseUtc) + ")" : string.Empty);
            ResolveDue.Text = LocalTime.Format(c.ResolveDueUtc);
            DescriptionText.Text = c.Description;

            var sla = ReferenceData.SlaPolicy.Evaluate(c, DateTime.UtcNow);
            SlaLabel.Text = sla == SlaState.AtRisk ? "At risk" : sla.ToString();
            SlaLabel.CssClass = "sla sla-" + sla.ToString().ToLowerInvariant();

            Notes.DataSource = c.Notes.OrderBy(n => n.CreatedUtc).ToList();
            Notes.DataBind();
            Attachments.DataSource = _cases.ListAttachments(c.Id);
            Attachments.DataBind();

            NextStatus.Items.Clear();
            foreach (var s in CaseWorkflow.NextStatuses(c.Status))
            {
                NextStatus.Items.Add(new ListItem(CaseWorkflow.DisplayName(s), ((int)s).ToString()));
            }

            var open = c.IsOpen;
            NextStatus.Enabled = ChangeStatusButton.Enabled = NextStatus.Items.Count > 0;
            AssignButton.Enabled = TakeButton.Enabled = SaveButton.Enabled = open;
            TakeButton.Visible = !string.Equals(c.AssignedTo, AccountName, StringComparison.OrdinalIgnoreCase);

            if (Assignee.Items.FindByValue(c.AssignedTo ?? string.Empty) == null && c.AssignedTo != null)
            {
                Assignee.Items.Add(new ListItem(UserDirectory.DisplayName(c.AssignedTo), c.AssignedTo));
            }

            Assignee.SelectedValue = c.AssignedTo ?? string.Empty;
            EditSubject.Text = c.Subject;
            EditCategory.SelectedValue = c.CategoryCode;
            EditPriority.SelectedValue = ((int)c.Priority).ToString();
            EditQueue.SelectedValue = c.QueueId.ToString();

            var articles = new KnowledgeRepository().ForCategory(c.CategoryCode, 5);
            ArticlesSection.Visible = articles.Count > 0;
            Articles.DataSource = articles;
            Articles.DataBind();
        }
    }
}
