namespace Southridge.Clinic.Domain
{
    public enum AppointmentStatus
    {
        Scheduled = 0,
        Confirmed = 1,
        CheckedIn = 2,
        InRoom = 3,
        Completed = 4,
        Cancelled = 5,
        NoShow = 6,
        Rescheduled = 7
    }

    public enum AppointmentSource
    {
        Staff = 0,
        Online = 1,
        Kiosk = 2,
        Interface = 3,
        Phone = 4
    }

    public enum Sex
    {
        Unknown = 0,
        Female = 1,
        Male = 2
    }

    public enum PayerType
    {
        SelfPay = 0,
        Commercial = 1,
        Medicare = 2,
        Medicaid = 3,
        WorkersComp = 4
    }

    public enum EncounterStatus
    {
        Open = 0,
        ChargesEntered = 1,
        Billed = 2,
        Voided = 3
    }

    public enum ChargeStatus
    {
        Entered = 0,
        Ready = 1,
        Billed = 2,
        Paid = 3,
        Adjusted = 4,
        Voided = 5
    }

    public enum ClaimStatus
    {
        Ready = 0,
        Submitted = 1,
        Accepted = 2,
        Rejected = 3,
        Paid = 4,
        PartiallyPaid = 5,
        Denied = 6,
        Closed = 7
    }

    public enum ReferralStatus
    {
        Received = 0,
        InReview = 1,
        Approved = 2,
        Declined = 3,
        Scheduled = 4,
        Expired = 5,
        Cancelled = 6
    }

    public enum ReferralPriority
    {
        Routine = 0,
        Urgent = 1,
        Stat = 2
    }

    public enum MessageDirection
    {
        Inbound = 0,
        Outbound = 1
    }

    public enum MessageStatus
    {
        Pending = 0,
        Sent = 1,
        Acknowledged = 2,
        Processed = 3,
        Error = 4,
        Ignored = 5
    }

    public enum InsuredRelationship
    {
        Self = 18,
        Spouse = 1,
        Child = 19,
        Other = 21
    }
}
