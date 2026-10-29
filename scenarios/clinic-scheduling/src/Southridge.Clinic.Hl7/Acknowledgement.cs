using System;

namespace Southridge.Clinic.Hl7
{
    public static class AckCode
    {
        public const string Accept = "AA";
        public const string Error = "AE";
        public const string Reject = "AR";
    }

    public static class Acknowledgement
    {
        public static Hl7Message Create(Hl7Message received, string code, string text, string controlId)
        {
            Hl7Message ack = Hl7Message.Create(
                "ACK",
                received.TriggerEvent,
                "ACK",
                received.Get("MSH-5"),
                received.Get("MSH-6"),
                received.Get("MSH-3"),
                received.Get("MSH-4"),
                controlId,
                DateTime.Now,
                string.IsNullOrEmpty(received.Version) ? "2.3" : received.Version);
            ack.Set("MSA-1", code);
            ack.Set("MSA-2", received.ControlId);
            if (!string.IsNullOrEmpty(text))
            {
                ack.Set("MSA-3", text.Length > 80 ? text.Substring(0, 80) : text);
            }

            if (code != AckCode.Accept && !string.IsNullOrEmpty(text))
            {
                ack.Set("ERR-1", text.Length > 80 ? text.Substring(0, 80) : text);
            }

            return ack;
        }

        public static bool IsPositive(Hl7Message ack)
        {
            string code = ack.Get("MSA-1");
            return code == AckCode.Accept || code == "CA";
        }
    }
}
