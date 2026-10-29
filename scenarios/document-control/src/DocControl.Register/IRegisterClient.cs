using System.Runtime.InteropServices;

namespace Litware.DocControl.Register
{
    /// <summary>Called from the VBA macros of the transmittal register workbook.</summary>
    [ComVisible(true)]
    [Guid("b21d7e4a-93c5-4f0e-8a6d-1c5f2e8b7a93")]
    [InterfaceType(ComInterfaceType.InterfaceIsDual)]
    public interface IRegisterClient
    {
        [DispId(1)] string BaseUrl { get; set; }
        [DispId(2)] string LastError { get; }
        [DispId(3)] int TimeoutSeconds { get; set; }

        [DispId(10)] object GetProjects();
        [DispId(11)] object GetRegister(string project);
        [DispId(12)] object GetRevisions(string documentNumber);
        [DispId(13)] object GetTransmittals(string project);
        [DispId(14)] string CreateTransmittal(string project, string purpose, string recipientCompany, string recipientName,
            string recipientEmail, string notes, string documentNumbers);
        [DispId(15)] void OpenInPortal(string documentNumber);
        [DispId(16)] string Version();
    }
}
