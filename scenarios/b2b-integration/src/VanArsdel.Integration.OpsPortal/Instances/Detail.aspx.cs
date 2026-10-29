using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AutoMapper;
using log4net;
using Microsoft.Practices.Unity;
using VanArsdel.Integration.Management.Operations;
using VanArsdel.Integration.OpsPortal.Models;

namespace VanArsdel.Integration.OpsPortal.Instances
{
    public partial class DetailPage : OpsPage
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(DetailPage));
        private SuspendedInstanceInfo _instance;
        private InstanceMessage _message;

        [Dependency]
        public IInstanceOperations Operations { get; set; }

        [Dependency]
        public ResubmitService Resubmitter { get; set; }

        protected Guid InstanceId { get; private set; }

        protected Guid SelectedMessageId { get; private set; }

        protected void Page_Load(object sender, EventArgs e)
        {
            InstanceId = QueryGuid("id");
            _instance = Operations.Find(InstanceId);
            if (_instance == null)
            {
                GonePanel.Visible = true;
                InstancePanel.Visible = false;
                return;
            }

            Guid msg;
            SelectedMessageId = Guid.TryParse(Request.QueryString["msg"], out msg) ? msg : (_instance.MessageIds.Count > 0 ? _instance.MessageIds[0] : Guid.Empty);
            if (SelectedMessageId != Guid.Empty)
            {
                _message = Operations.GetMessage(InstanceId, SelectedMessageId);
            }

            if (Request.QueryString["download"] == "1" && _message != null)
            {
                SendBody();
                return;
            }

            InstanceActions.Visible = CanOperate;
            ResumeButton.Enabled = _instance.Resumable;
            ResubmitPanel.Visible = CanOperate && _message != null;

            if (!IsPostBack)
            {
                ShowInstance();
            }
        }

        public IEnumerable<Guid> GetMessageIds()
        {
            return _instance == null ? new List<Guid>() : _instance.MessageIds;
        }

        public IEnumerable<ContextRow> GetContext()
        {
            return _message == null ? new List<ContextRow>() : Mapper.Map<List<ContextRow>>(_message.Context);
        }

        protected void Resume_Click(object sender, EventArgs e)
        {
            Demand(PageAccess.Operate);
            var result = Operations.Resume(InstanceId);
            Log.InfoFormat("{0} by {1}", result.Message, UserName);
            Flash(result.Message, !result.Succeeded);
            Response.Redirect("./", false);
        }

        protected void Terminate_Click(object sender, EventArgs e)
        {
            Demand(PageAccess.Operate);
            var result = Operations.Terminate(InstanceId);
            Log.InfoFormat("{0} by {1}", result.Message, UserName);
            Flash(result.Message, !result.Succeeded);
            Response.Redirect("./", false);
        }

        protected void EditBody_CheckedChanged(object sender, EventArgs e)
        {
            BodyText.ReadOnly = !EditBody.Checked;
        }

        protected void Resubmit_Click(object sender, EventArgs e)
        {
            Demand(PageAccess.Operate);
            if (!IsValid || _message == null)
            {
                return;
            }

            try
            {
                var outcome = Resubmitter.Resubmit(new ResubmitRequest
                {
                    InstanceId = InstanceId,
                    MessageId = SelectedMessageId,
                    Reason = ReasonText.Text,
                    EditedBody = EditBody.Checked ? BodyText.Text : null,
                    TerminateOriginal = TerminateOriginal.Checked,
                    RequestedBy = UserName
                });

                Flash(string.Format(CultureInfo.CurrentCulture, "Resubmission {0} written to {1}.{2}", outcome.ResubmitId, outcome.DroppedFile,
                    outcome.Warning == null ? string.Empty : " " + outcome.Warning), outcome.Warning != null);
                Response.Redirect(outcome.OriginalTerminated ? "./" : Request.RawUrl, false);
            }
            catch (Exception ex)
            {
                Log.Error("Resubmission of " + SelectedMessageId + " failed", ex);
                Flash("Resubmission failed: " + ex.Message, true);
                Response.Redirect(Request.RawUrl, false);
            }
        }

        private void ShowInstance()
        {
            ServiceTitle.Text = _instance.ShortServiceName;
            InstanceIdText.Text = _instance.InstanceId.ToString();
            KindText.Text = _instance.Kind + (_instance.Resumable ? " (resumable)" : " (not resumable)");
            ApplicationText.Text = _instance.Application;
            HostText.Text = _instance.HostName + " / " + _instance.ProcessingServer;
            AddressText.Text = (_instance.Adapter ?? "-") + " " + _instance.Uri;
            TimesText.Text = _instance.CreatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) + " / " + _instance.SuspendedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
            ErrorText.Text = (_instance.ErrorCode == null ? string.Empty : _instance.ErrorCode + ": ") + _instance.ErrorDescription;

            if (_message == null)
            {
                return;
            }

            MessagePanel.Visible = true;
            MessageTypeText.Text = _message.MessageType;
            PartnerText.Text = _message.PartnerId ?? "(not resolved)";
            ReceivedOnText.Text = _message.ReceivePortName + " " + _message.InboundTransportLocation;
            ResubmitTargetText.Text = Resubmitter.TargetFor(_message) ?? "(no resubmit folder configured)";
            BodyText.Text = Decode(_message);
            DownloadLink.NavigateUrl = "Detail?id=" + InstanceId + "&msg=" + SelectedMessageId + "&download=1";
        }

        private void SendBody()
        {
            Response.Clear();
            Response.ContentType = "application/octet-stream";
            Response.AddHeader("Content-Disposition", "attachment; filename=" + SelectedMessageId.ToString("N") + ".dat");
            Response.BinaryWrite(_message.Body ?? new byte[0]);
            Response.Flush();
            Context.ApplicationInstance.CompleteRequest();
        }

        private static string Decode(InstanceMessage message)
        {
            var body = message.Body ?? new byte[0];
            Encoding encoding;
            try
            {
                encoding = string.IsNullOrEmpty(message.Charset) ? Encoding.UTF8 : Encoding.GetEncoding(message.Charset);
            }
            catch (ArgumentException)
            {
                encoding = Encoding.UTF8;
            }

            var text = encoding.GetString(body);

            // EDI without line breaks is unreadable in a text box: one segment per line for display only
            if (text.StartsWith("ISA", StringComparison.Ordinal) && text.Length > 105 && text.IndexOf('\n') < 0)
            {
                text = text.Replace(text[105].ToString(), text[105] + Environment.NewLine);
            }
            else if (text.StartsWith("UN", StringComparison.Ordinal) && text.IndexOf('\n') < 0)
            {
                text = text.Replace("'", "'" + Environment.NewLine);
            }

            return text;
        }
    }
}
