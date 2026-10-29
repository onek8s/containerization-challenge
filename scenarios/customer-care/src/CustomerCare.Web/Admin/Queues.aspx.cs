using System;
using System.Web.UI.WebControls;
using Tailspin.CustomerCare.Data;
using Tailspin.CustomerCare.Data.Repositories;
using Tailspin.CustomerCare.Domain;
using Tailspin.CustomerCare.Web.Security;

namespace Tailspin.CustomerCare.Web.Admin
{
    public partial class QueuesPage : CarePage
    {
        private readonly AdminRepository _admin = new AdminRepository();

        protected override CareRole RequiredRole
        {
            get { return CareRole.Administrator; }
        }

        protected void Page_Load(object sender, EventArgs e)
        {
            if (IsPostBack) return;

            NewRuleCategory.DataSource = ReferenceData.Categories;
            NewRuleCategory.DataBind();
            Bind();
        }

        protected void QueueGrid_RowEditing(object sender, GridViewEditEventArgs e)
        {
            QueueGrid.EditIndex = e.NewEditIndex;
            Bind();
        }

        protected void QueueGrid_RowCancelingEdit(object sender, GridViewCancelEditEventArgs e)
        {
            QueueGrid.EditIndex = -1;
            Bind();
        }

        protected void QueueGrid_RowUpdating(object sender, GridViewUpdateEventArgs e)
        {
            var row = QueueGrid.Rows[e.RowIndex];
            var queue = new Queue
            {
                Id = (int)QueueGrid.DataKeys[e.RowIndex].Value,
                Name = ((TextBox)row.Cells[1].Controls[0]).Text.Trim(),
                Email = ((TextBox)row.Cells[2].Controls[0]).Text.Trim(),
                SortOrder = int.Parse(((TextBox)row.Cells[3].Controls[0]).Text),
                IsActive = ((CheckBox)row.Cells[4].Controls[0]).Checked,
                DefaultPriority = CasePriority.Normal
            };

            if (Try(() => _admin.SaveQueue(queue)))
            {
                QueueGrid.EditIndex = -1;
                ShowMessage("Queue saved.");
            }

            Bind();
        }

        protected void AddQueueButton_Click(object sender, EventArgs e)
        {
            if (!IsValid) return;
            var queue = new Queue
            {
                Code = NewQueueCode.Text,
                Name = NewQueueName.Text.Trim(),
                Email = NewQueueEmail.Text.Trim(),
                IsActive = true,
                SortOrder = 100,
                DefaultPriority = CasePriority.Normal
            };

            if (Try(() => _admin.SaveQueue(queue)))
            {
                NewQueueCode.Text = NewQueueName.Text = NewQueueEmail.Text = string.Empty;
                ShowMessage("Queue added.");
            }

            Bind();
        }

        protected void RuleGrid_RowDeleting(object sender, GridViewDeleteEventArgs e)
        {
            _admin.DeleteRule((int)RuleGrid.DataKeys[e.RowIndex].Value);
            Bind();
        }

        protected void AddRuleButton_Click(object sender, EventArgs e)
        {
            int sequence;
            if (!int.TryParse(NewRuleSequence.Text, out sequence))
            {
                ShowMessage("Order must be a number.", true);
                return;
            }

            var rule = new RoutingRule
            {
                Sequence = sequence,
                CategoryCode = Empty(NewRuleCategory.SelectedValue),
                Segment = Empty(NewRuleSegment.SelectedValue),
                PostalCodePrefix = Empty(NewRulePostal.Text),
                QueueCode = NewRuleQueue.SelectedValue
            };

            if (Try(() => _admin.AddRule(rule))) ShowMessage("Rule added.");
            Bind();
        }

        private void Bind()
        {
            var queues = _admin.Queues();
            QueueGrid.DataSource = queues;
            QueueGrid.DataBind();
            NewRuleQueue.DataSource = queues;
            NewRuleQueue.DataBind();
            RuleGrid.DataSource = _admin.Rules();
            RuleGrid.DataBind();
        }

        private static string Empty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
