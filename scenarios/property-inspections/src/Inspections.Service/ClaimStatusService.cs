using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using Northwind.Inspections.Contracts;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Imaging;

namespace Northwind.Inspections.Service
{
    /// <summary>Read-only endpoint for loss adjusting partners and the claims system (wsHttpBinding, user name over HTTPS).</summary>
    [ServiceBehavior(Namespace = Namespaces.Partner, InstanceContextMode = InstanceContextMode.PerCall)]
    public class ClaimStatusService : IClaimStatusService
    {
        private readonly InspectionRepository inspections = new InspectionRepository(ServiceRuntime.Db);
        private readonly PhotoRepository photos = new PhotoRepository(ServiceRuntime.Db);

        public ClaimInspectionDto[] GetInspections(string claimNumber)
        {
            string normalized;
            if (!ClaimNumber.TryNormalize(claimNumber, out normalized))
            {
                throw PhotoService.Fault("CLAIM", "Invalid claim number.");
            }
            return inspections.ForClaim(normalized).Select(i => new ClaimInspectionDto
            {
                InspectionId = i.Id,
                ClaimNumber = i.ClaimNumber,
                Status = i.Status.ToString(),
                InspectedOn = i.InspectedOn,
                Adjuster = i.AdjusterName,
                EstimatedLoss = i.EstimatedLoss,
                PhotoCount = i.PhotoCount,
                ReportSentUtc = i.ReportSentUtc
            }).ToArray();
        }

        public byte[] GetContactSheet(int inspectionId)
        {
            var inspection = inspections.Get(inspectionId);
            if (inspection == null || inspection.Status < InspectionStatus.ReportSent)
            {
                throw PhotoService.Fault("NOT_FOUND", "No reported inspection " + inspectionId + ".");
            }
            var store = ServiceRuntime.Store;
            var thumbs = photos.ForInspection(inspectionId).Where(p => !p.Purged)
                .Select(p => new KeyValuePair<Photo, byte[]>(p, store.Read(p, PhotoSize.Thumbnail))).ToList();
            return ContactSheet.Render(inspection, thumbs, ServiceRuntime.Settings.WatermarkFont);
        }
    }
}
