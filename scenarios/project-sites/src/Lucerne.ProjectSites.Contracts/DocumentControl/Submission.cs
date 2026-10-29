using System;
using Newtonsoft.Json;

namespace Lucerne.ProjectSites.Contracts.DocumentControl
{
    /// <summary>Document submission to the external document-control system (doccontrol, /api/v1/submissions).</summary>
    public class DocumentSubmission
    {
        [JsonProperty("projectNumber")] public string ProjectNumber { get; set; }
        [JsonProperty("documentNumber")] public string DocumentNumber { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("revision")] public string Revision { get; set; }
        [JsonProperty("discipline")] public string Discipline { get; set; }
        [JsonProperty("documentType")] public string DocumentType { get; set; }
        [JsonProperty("approvedBy")] public string ApprovedBy { get; set; }
        [JsonProperty("approvedOn")] public DateTime? ApprovedOn { get; set; }
        [JsonProperty("sourceUrl")] public string SourceUrl { get; set; }
        [JsonProperty("fileName")] public string FileName { get; set; }
        [JsonProperty("sha256")] public string Sha256 { get; set; }
    }

    public class SubmissionReceipt
    {
        [JsonProperty("submissionId")] public string SubmissionId { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("receivedAt")] public DateTime ReceivedAt { get; set; }
    }

    public class SubmissionStatus
    {
        [JsonProperty("submissionId")] public string SubmissionId { get; set; }
        [JsonProperty("status")] public string Status { get; set; }
        [JsonProperty("registeredNumber")] public string RegisteredNumber { get; set; }
        [JsonProperty("comment")] public string Comment { get; set; }
        [JsonProperty("updatedAt")] public DateTime UpdatedAt { get; set; }

        public bool IsFinal
        {
            get { return Status == SubmissionStates.Registered || Status == SubmissionStates.Rejected; }
        }
    }

    public static class SubmissionStates
    {
        public const string Received = "Received";
        public const string Checking = "Checking";
        public const string Registered = "Registered";
        public const string Rejected = "Rejected";
    }
}
