namespace Southridge.Clinic.Hl7
{
    public static class MessageTypes
    {
        public const string AdmitVisit = "ADT^A01";
        public const string Discharge = "ADT^A03";
        public const string RegisterPatient = "ADT^A04";
        public const string UpdatePatient = "ADT^A08";
        public const string MergePatient = "ADT^A40";
        public const string NewAppointment = "SIU^S12";
        public const string RescheduleAppointment = "SIU^S13";
        public const string ModifyAppointment = "SIU^S14";
        public const string CancelAppointment = "SIU^S15";
        public const string NoShow = "SIU^S26";
        public const string PostCharges = "DFT^P03";
        public const string Order = "ORM^O01";
        public const string Result = "ORU^R01";
    }

    /// <summary>Partner codes used for routing (MSH-5/MSH-6 and the outbox partner column).</summary>
    public static class Partners
    {
        public const string Hospital = "HOSP";
        public const string Lab = "LAB";
        public const string HospitalBilling = "HOSPBILL";
    }
}
