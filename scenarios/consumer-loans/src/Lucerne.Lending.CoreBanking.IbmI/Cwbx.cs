using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Lucerne.Lending.CoreBanking.IbmI;

/// <summary>
/// IBM i Access programming component (cwbx.dll, "IBM i Access ActiveX automation objects").
/// Used late bound so that the same build works with the 32 and 64 bit registrations of
/// the Windows Application Package; enum values are from the cwbx type library.
/// </summary>
[SupportedOSPlatform("windows")]
internal static class Cwbx
{
    public const string AS400System = "cwbx.AS400System";
    public const string Program = "cwbx.Program";
    public const string ProgramParameters = "cwbx.ProgramParameters";
    public const string DataQueue = "cwbx.DataQueue";
    public const string StringConverter = "cwbx.StringConverter";
    public const string PackedConverter = "cwbx.PackedConverter";
    public const string ZonedConverter = "cwbx.ZonedConverter";

    // cwbcoServiceEnum
    public const int ServiceDataQueues = 6;
    public const int ServiceRemoteCmd = 7;
    public const int ServiceAll = 100;

    // cwbcoPromptModeEnum
    public const int PromptNever = 2;

    // cwbcoDefaultUserModeEnum
    public const int DefaultUserUseWinLogon = 3;

    // cwbrcParameterTypeEnum
    public const int ParameterInput = 1;
    public const int ParameterOutput = 2;
    public const int ParameterInOut = 3;

    public static dynamic Create(string progId)
    {
        var type = Type.GetTypeFromProgID(progId, throwOnError: false)
            ?? throw new CoreBankingException(
                $"COM class {progId} is not registered. Install the IBM i Access Client Solutions Windows Application Package (programming component).");
        return Activator.CreateInstance(type)
            ?? throw new CoreBankingException($"Could not create {progId}.");
    }

    public static bool IsRegistered() => Type.GetTypeFromProgID(AS400System, throwOnError: false) != null;

    /// <summary>Text of the errors collected by a cwbx object (Errors collection).</summary>
    public static string ErrorText(dynamic cwbxObject)
    {
        try
        {
            dynamic errors = cwbxObject.Errors;
            int count = errors.Count;
            var parts = new List<string>();
            for (var i = 1; i <= count; i++) parts.Add((string)errors[i].Text);
            return string.Join(" | ", parts);
        }
        catch (COMException)
        {
            return "";
        }
    }

    public static void Release(object? comObject)
    {
        if (comObject != null && Marshal.IsComObject(comObject)) Marshal.FinalReleaseComObject(comObject);
    }
}
