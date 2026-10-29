namespace Tailspin.DealerOrders.Domain
{
    public enum DealerStatus
    {
        Active = 0,
        Suspended = 1,
        Terminated = 2
    }

    public enum DiscountTier
    {
        /// <summary>Full-line dealer with service workshop.</summary>
        A = 0,
        B = 1,
        C = 2,
        /// <summary>Sub-dealer buying through a main dealer.</summary>
        Sub = 3
    }

    public enum QuoteStatus
    {
        Draft = 0,
        Submitted = 1,
        PendingApproval = 2,
        Approved = 3,
        Rejected = 4,
        RegisteredInAx = 5,
        Accepted = 6,
        Converted = 7,
        Expired = 8,
        Cancelled = 9
    }

    public enum OrderStatus
    {
        Draft = 0,
        Submitted = 1,
        PendingApproval = 2,
        CreditHold = 3,
        Approved = 4,
        SentToAx = 5,
        CreatedInAx = 6,
        Confirmed = 7,
        InProduction = 8,
        PartiallyShipped = 9,
        Shipped = 10,
        Invoiced = 11,
        Cancelled = 12,
        Rejected = 13,
        Error = 14
    }

    public enum OrderSource
    {
        Portal = 0,
        Edi = 1,
        SalesDesk = 2
    }

    public enum OrderType
    {
        /// <summary>Configured machine built to order.</summary>
        Machine = 0,
        /// <summary>Stock machine from a dealer or factory yard.</summary>
        StockMachine = 1,
        /// <summary>Spare parts and attachments.</summary>
        Parts = 2
    }

    public enum ReservationStatus
    {
        None = 0,
        Requested = 1,
        Reserved = 2,
        PartiallyReserved = 3,
        NotAvailable = 4,
        Released = 5
    }

    public enum ApprovalReason
    {
        DiscountAboveLimit = 0,
        CreditLimitExceeded = 1,
        CreditHold = 2,
        PriceOverride = 3,
        NonStandardTerms = 4,
        ExportControl = 5
    }

    public enum ApprovalDecision
    {
        Pending = 0,
        Approved = 1,
        Rejected = 2
    }

    public enum DeliveryTerms
    {
        /// <summary>Ex works factory (Incoterms 2010 EXW).</summary>
        Exw = 0,
        /// <summary>Free carrier, loaded at the factory (FCA).</summary>
        Fca = 1,
        /// <summary>Carriage paid to the dealer yard (CPT).</summary>
        Cpt = 2,
        /// <summary>Delivered at place (DAP).</summary>
        Dap = 3
    }
}
