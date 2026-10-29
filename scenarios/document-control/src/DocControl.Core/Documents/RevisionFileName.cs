using System.Diagnostics.CodeAnalysis;

namespace Litware.DocControl.Documents;

/// <summary>
/// File names in the drop share and in the vault: <c>{DocumentNumber}_{Revision}.{ext}</c>,
/// for example <c>P1042-LIT-ME-DRG-0012_P03.pdf</c>.
/// </summary>
public sealed record RevisionFileName(DocumentNumber Document, RevisionCode Revision, string Extension)
{
    public static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".pdf", ".dwg", ".dxf", ".docx", ".xlsx", ".ifc", ".step", ".stp" };

    public static bool TryParse(string fileName, [NotNullWhen(true)] out RevisionFileName? result, out string? error)
    {
        result = null;
        error = null;

        var ext = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(ext))
        {
            error = $"File type '{ext}' is not accepted.";
            return false;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var sep = stem.LastIndexOf('_');
        if (sep < 0)
        {
            error = "File name must be <document number>_<revision>.";
            return false;
        }

        if (!DocumentNumber.TryParse(stem[..sep], out var number))
        {
            error = $"'{stem[..sep]}' is not a valid document number.";
            return false;
        }

        if (!RevisionCode.TryParse(stem[(sep + 1)..], out var rev))
        {
            error = $"'{stem[(sep + 1)..]}' is not a valid revision code.";
            return false;
        }

        result = new RevisionFileName(number, rev, ext.ToLowerInvariant());
        return true;
    }

    public override string ToString() => $"{Document}_{Revision}{Extension}";
}
