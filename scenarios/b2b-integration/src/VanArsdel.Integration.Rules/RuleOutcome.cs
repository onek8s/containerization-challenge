using System;
using System.Collections.Generic;
using System.Linq;

namespace VanArsdel.Integration.Rules
{
    public class RuleRejection
    {
        public string Code { get; set; }
        public string Text { get; set; }
        public int LineNumber { get; set; }

        public override string ToString()
        {
            return Code + (LineNumber > 0 ? " (line " + LineNumber + ")" : string.Empty) + ": " + Text;
        }
    }

    /// <summary>
    /// What the policies decided. A fact of its own (asserted together with the order facts) so the orchestration
    /// can read all results after Policy.Execute without digging in the order message.
    /// </summary>
    [Serializable]
    public class RuleOutcome
    {
        private readonly List<RuleRejection> _rejections = new List<RuleRejection>();
        private readonly List<string> _firedRules = new List<string>();
        private readonly List<string> _notes = new List<string>();

        public IList<RuleRejection> Rejections
        {
            get { return _rejections; }
        }

        public IList<string> FiredRules
        {
            get { return _firedRules; }
        }

        public IList<string> Notes
        {
            get { return _notes; }
        }

        public bool Rejected
        {
            get { return _rejections.Count > 0; }
        }

        public bool CreditCheckRequired { get; private set; }
        public bool CreditHold { get; private set; }
        public string HoldReason { get; private set; }
        public string ShipVia { get; private set; }
        public string RoutingSlip { get; private set; }
        public int Priority { get; private set; }
        public string OrderType { get; private set; }
        public bool ManualReview { get; private set; }

        public void Reject(string code, string text)
        {
            _rejections.Add(new RuleRejection { Code = code, Text = text });
        }

        public void RejectLine(string code, string text, int lineNumber)
        {
            _rejections.Add(new RuleRejection { Code = code, Text = text, LineNumber = lineNumber });
        }

        public void RequireCreditCheck()
        {
            CreditCheckRequired = true;
        }

        public void PlaceOnCreditHold(string reason)
        {
            CreditHold = true;
            HoldReason = reason;
        }

        public void SetShipVia(string shipVia)
        {
            ShipVia = shipVia;
        }

        public void SetRoutingSlip(string slip)
        {
            RoutingSlip = slip;
        }

        public void SetPriority(int priority)
        {
            if (priority > Priority)
            {
                Priority = priority;
            }
        }

        public void SetOrderType(string orderType)
        {
            OrderType = orderType;
        }

        public void RequireManualReview(string note)
        {
            ManualReview = true;
            _notes.Add(note);
        }

        public void AddNote(string note)
        {
            _notes.Add(note);
        }

        public void RuleFired(string rule)
        {
            _firedRules.Add(rule);
        }

        public string Summary()
        {
            if (Rejected)
            {
                return "Rejected: " + string.Join("; ", _rejections.Select(r => r.ToString()));
            }

            return CreditHold ? "Credit hold: " + HoldReason : (ManualReview ? "Manual review" : "Accepted");
        }
    }
}
