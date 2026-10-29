namespace VanArsdel.Integration.Edi.Validation
{
    public enum EdiErrorSeverity
    {
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// Error codes. Segment level codes are X12 AK304 values, element level codes are AK403 values; the CONTRL
    /// builder maps them to UN/EDIFACT syntax error codes (0085).
    /// </summary>
    public static class EdiErrorCodes
    {
        // AK304
        public const string UnrecognizedSegment = "1";
        public const string UnexpectedSegment = "2";
        public const string MissingSegment = "3";
        public const string LoopTooManyTimes = "4";
        public const string SegmentExceedsMaximum = "5";

        // AK403
        public const string MandatoryElementMissing = "E1";
        public const string TooManyElements = "E3";
        public const string TooShort = "E4";
        public const string TooLong = "E5";
        public const string InvalidCharacter = "E6";
        public const string InvalidCode = "E7";
        public const string InvalidDate = "E8";
        public const string InvalidTime = "E9";

        // Trailer checks (AK5 code 4 / 5 / 3)
        public const string SegmentCount = "T4";
        public const string ControlNumberMismatch = "T3";

        public static bool IsElementCode(string code)
        {
            return code != null && code.StartsWith("E", System.StringComparison.Ordinal);
        }

        public static string ToAk4(string code)
        {
            return IsElementCode(code) ? code.Substring(1) : code;
        }

        public static string ToEdifact(string code)
        {
            switch (code)
            {
                case UnrecognizedSegment:
                case UnexpectedSegment:
                    return "15";
                case MissingSegment:
                    return "14";
                case LoopTooManyTimes:
                case SegmentExceedsMaximum:
                    return "35";
                case MandatoryElementMissing:
                    return "13";
                case TooManyElements:
                    return "16";
                case TooShort:
                    return "39";
                case TooLong:
                    return "40";
                case InvalidCharacter:
                    return "21";
                case InvalidCode:
                    return "12";
                case InvalidDate:
                case InvalidTime:
                    return "12";
                case SegmentCount:
                case ControlNumberMismatch:
                    return "13";
                default:
                    return "2";
            }
        }
    }

    public class EdiError
    {
        public EdiError(string segmentTag, int segmentPosition, int elementPosition, string code, string text, EdiErrorSeverity severity = EdiErrorSeverity.Error)
        {
            SegmentTag = segmentTag;
            SegmentPosition = segmentPosition;
            ElementPosition = elementPosition;
            Code = code;
            Text = text;
            Severity = severity;
        }

        public string SegmentTag { get; private set; }
        public int SegmentPosition { get; private set; }

        /// <summary>1-based element position, 0 for a segment level error.</summary>
        public int ElementPosition { get; private set; }

        public string Code { get; private set; }
        public string Text { get; private set; }
        public EdiErrorSeverity Severity { get; private set; }
        public string BadValue { get; set; }

        public static EdiError Envelope(string text, string code)
        {
            return new EdiError(null, 0, 0, code, text);
        }

        public override string ToString()
        {
            var where = SegmentTag == null ? "envelope" : SegmentTag + "@" + SegmentPosition + (ElementPosition > 0 ? "/" + ElementPosition : string.Empty);
            return where + " [" + Code + "] " + Text;
        }
    }
}
