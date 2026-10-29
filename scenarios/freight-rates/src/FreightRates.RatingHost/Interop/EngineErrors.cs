using FreightRates.Core;

namespace FreightRates.RatingHost.Interop;

internal static class EngineErrors
{
    public const int MR_OK = 0;
    public const int MR_E_NOT_INITIALIZED = -1;
    public const int MR_E_BAD_HANDLE = -2;
    public const int MR_E_WRONG_THREAD = -3;
    public const int MR_E_NOKEY = -101;
    public const int MR_E_KEY_MISMATCH = -102;
    public const int MR_E_LICENCE_EXPIRED = -103;
    public const int MR_E_NO_SEATS = -104;
    public const int MR_E_BAD_PASSWORD = -105;
    public const int MR_E_DATA_PATH = -201;
    public const int MR_E_TARIFF_MISSING = -202;
    public const int MR_E_TARIFF_EXPIRED = -203;
    public const int MR_E_BAD_ORIGIN = -301;
    public const int MR_E_BAD_DEST = -302;
    public const int MR_E_POINT_NOT_SERVED = -303;
    public const int MR_E_BAD_CLASS = -304;
    public const int MR_E_BAD_WEIGHT = -305;
    public const int MR_E_TOO_MANY_LINES = -306;

    public static string Describe(int code) => code switch
    {
        MR_E_NOT_INITIALIZED => "Engine not initialized",
        MR_E_BAD_HANDLE => "Invalid session handle",
        MR_E_WRONG_THREAD => "Session used from a different thread",
        MR_E_NOKEY => "Licence key not found",
        MR_E_KEY_MISMATCH => "Licence file does not match the licence key",
        MR_E_LICENCE_EXPIRED => "Licence expired",
        MR_E_NO_SEATS => "No free licence seats",
        MR_E_BAD_PASSWORD => "Licence password rejected",
        MR_E_DATA_PATH => "Tariff data path not found",
        MR_E_TARIFF_MISSING => "Tariff not installed",
        MR_E_TARIFF_EXPIRED => "Tariff not effective for ship date",
        MR_E_BAD_ORIGIN => "Origin ZIP code not found",
        MR_E_BAD_DEST => "Destination ZIP code not found",
        MR_E_POINT_NOT_SERVED => "Point not served",
        MR_E_BAD_CLASS => "Invalid freight class",
        MR_E_BAD_WEIGHT => "Invalid weight",
        MR_E_TOO_MANY_LINES => "Too many lines",
        _ => $"Engine error {code}",
    };

    public static RatingErrorKind Classify(int code) => code switch
    {
        MR_E_BAD_ORIGIN or MR_E_BAD_DEST or MR_E_POINT_NOT_SERVED => RatingErrorKind.PointNotServed,
        MR_E_BAD_CLASS or MR_E_BAD_WEIGHT or MR_E_TOO_MANY_LINES or MR_E_TARIFF_EXPIRED => RatingErrorKind.InvalidShipment,
        _ => RatingErrorKind.EngineUnavailable,
    };

    /// <summary>Codes after which the session has to be opened again.</summary>
    public static bool RequiresReinitialize(int code) =>
        code is MR_E_NOT_INITIALIZED or MR_E_BAD_HANDLE or MR_E_NOKEY or MR_E_KEY_MISMATCH or MR_E_NO_SEATS;
}
