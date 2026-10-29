using System;
using System.Activities.Persistence;
using System.Collections.Generic;
using System.Xml.Linq;

namespace StaffOnboarding.Workflows.Services
{
    /// <summary>
    /// Promotes request number, kind and status into the instance store so operators can find a
    /// workflow instance with the InstancePromotedProperties view.
    /// </summary>
    public class RequestPromotion : PersistenceParticipant
    {
        public const string PromotionName = "StaffOnboardingRequest";
        private static readonly XNamespace Ns = "urn:litware:onboarding:promoted";

        public static readonly XName RequestNumberName = Ns.GetName("RequestNumber");
        public static readonly XName KindName = Ns.GetName("Kind");
        public static readonly XName StatusName = Ns.GetName("Status");
        public static readonly XName SubjectName = Ns.GetName("Subject");
        public static readonly XName NextStepAtName = Ns.GetName("NextStepAtUtc");

        public string RequestNumber { get; set; }
        public string Kind { get; set; }
        public string Status { get; set; }
        public string Subject { get; set; }
        public DateTime? NextStepAtUtc { get; set; }

        public static IEnumerable<XName> PromotedNames
        {
            get { return new[] { RequestNumberName, KindName, StatusName, SubjectName, NextStepAtName }; }
        }

        protected override void CollectValues(out IDictionary<XName, object> readWriteValues, out IDictionary<XName, object> writeOnlyValues)
        {
            readWriteValues = new Dictionary<XName, object>
            {
                { RequestNumberName, RequestNumber },
                { KindName, Kind },
                { StatusName, Status },
                { SubjectName, Subject },
                { NextStepAtName, NextStepAtUtc }
            };
            writeOnlyValues = null;
        }

        protected override void PublishValues(IDictionary<XName, object> readWriteValues)
        {
            object value;
            if (readWriteValues.TryGetValue(RequestNumberName, out value)) RequestNumber = value as string;
            if (readWriteValues.TryGetValue(KindName, out value)) Kind = value as string;
            if (readWriteValues.TryGetValue(StatusName, out value)) Status = value as string;
            if (readWriteValues.TryGetValue(SubjectName, out value)) Subject = value as string;
            if (readWriteValues.TryGetValue(NextStepAtName, out value)) NextStepAtUtc = value as DateTime?;
        }
    }
}
