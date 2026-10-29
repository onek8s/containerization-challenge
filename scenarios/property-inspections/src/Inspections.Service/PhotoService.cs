using System;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceModel.Activation;
using Northwind.Inspections.Contracts;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;
using Northwind.Inspections.Infrastructure.Imaging;

namespace Northwind.Inspections.Service
{
    [ServiceBehavior(Namespace = Namespaces.Service, InstanceContextMode = InstanceContextMode.PerCall, ConcurrencyMode = ConcurrencyMode.Multiple)]
    [AspNetCompatibilityRequirements(RequirementsMode = AspNetCompatibilityRequirementsMode.Allowed)]
    public class PhotoService : IPhotoService
    {
        private readonly InspectionRepository inspections = new InspectionRepository(ServiceRuntime.Db);
        private readonly PhotoRepository photos = new PhotoRepository(ServiceRuntime.Db);
        private readonly ReportQueue queue = new ReportQueue(ServiceRuntime.Db);

        public PhotoDto UploadPhoto(UploadPhotoRequest request)
        {
            if (request == null || request.Content == null || request.Content.Length == 0)
            {
                throw Fault("EMPTY", "No image content was sent.");
            }
            if (request.Content.LongLength > PhotoStorage.MaxUploadBytes)
            {
                throw Fault("TOO_LARGE", "The image is larger than 15 MB.");
            }
            if (!PhotoStorage.IsAcceptedFileName(request.FileName))
            {
                throw Fault("TYPE", "Only JPEG, PNG, BMP, GIF and TIFF images are accepted.");
            }

            var inspection = RequireInspection(request.InspectionId);
            if (inspection.Status >= InspectionStatus.ReportSent)
            {
                throw Fault("LOCKED", "Photos cannot be added after the report has been sent.");
            }

            try
            {
                var photo = ServiceRuntime.Store.Add(inspection, request.FileName, Watermark.Caption(request.Caption, 400), request.Content, request.UploadedBy);
                inspections.Audit(inspection.Id, "Photo added", request.UploadedBy, photo.OriginalFileName);
                return ToDto(photo);
            }
            catch (InvalidPhotoException ex)
            {
                InspectionsLog.Warning(EventId.PhotoRejected, "Rejected {0} for {1} from {2}: {3}", request.FileName, inspection.ClaimNumber, request.UploadedBy, ex.Message);
                throw Fault("INVALID_IMAGE", ex.Message);
            }
            catch (System.IO.IOException ex)
            {
                InspectionsLog.Error(EventId.ShareFailure, ex, "Writing to the photo share failed for {0}.", inspection.ClaimNumber);
                throw Fault("STORAGE", "The photo could not be stored. Try again later.");
            }
        }

        public PhotoDto[] GetPhotos(int inspectionId)
        {
            return photos.ForInspection(inspectionId).Select(ToDto).ToArray();
        }

        public void UpdateCaption(Guid photoId, string caption, string changedBy)
        {
            var photo = RequirePhoto(photoId);
            photos.UpdateCaption(photoId, Watermark.Caption(caption, 400));
            inspections.Audit(photo.InspectionId, "Caption changed", changedBy, photo.OriginalFileName);
        }

        public void DeletePhoto(Guid photoId, string deletedBy)
        {
            var photo = RequirePhoto(photoId);
            var inspection = RequireInspection(photo.InspectionId);
            if (inspection.Status >= InspectionStatus.ReportQueued)
            {
                throw Fault("LOCKED", "Photos cannot be deleted once a report has been requested.");
            }
            ServiceRuntime.Store.Remove(photo);
            photos.Delete(photoId);
            inspections.Audit(photo.InspectionId, "Photo deleted", deletedBy, photo.OriginalFileName);
            InspectionsLog.Info(EventId.PhotoDeleted, "Photo {0} of {1} deleted by {2}.", photoId, inspection.ClaimNumber, deletedBy);
        }

        public long RequestReport(int inspectionId, string requestedBy)
        {
            var inspection = RequireInspection(inspectionId);
            string reason;
            if (!ReportSchedule.CanRequestReport(inspection, inspection.PhotoCount, out reason))
            {
                throw Fault("NOT_READY", reason);
            }
            var id = queue.Enqueue(inspectionId, inspection.HandlerEmail, requestedBy);
            inspections.SetStatus(inspectionId, InspectionStatus.ReportQueued, requestedBy);
            InspectionsCounters.Instance.QueueDepth(queue.Depth());
            InspectionsLog.Info(EventId.ReportQueued, "Report {0} for {1} queued by {2} for {3}.", id, inspection.ClaimNumber, requestedBy, inspection.HandlerEmail);
            return id;
        }

        public ServiceStatusDto GetStatus()
        {
            var status = new ServiceStatusDto
            {
                MachineName = Environment.MachineName,
                Identity = WindowsIdentity.GetCurrent().Name,
                Version = Assembly.GetExecutingAssembly().GetName().Version.ToString()
            };
            try
            {
                status.QueuedReports = queue.Depth();
                status.DatabaseReachable = true;
            }
            catch (Exception)
            {
                status.DatabaseReachable = false;
            }
            status.ShareReachable = ServiceRuntime.Store.ShareReachable();
            return status;
        }

        internal static PhotoDto ToDto(Photo p)
        {
            return new PhotoDto
            {
                Id = p.Id,
                InspectionId = p.InspectionId,
                Sequence = p.Sequence,
                FileName = p.OriginalFileName,
                Caption = p.Caption,
                Width = p.Width,
                Height = p.Height,
                Bytes = p.Bytes,
                TakenUtc = p.TakenUtc,
                UploadedUtc = p.UploadedUtc,
                UploadedBy = p.UploadedBy,
                Purged = p.Purged
            };
        }

        private Inspection RequireInspection(int id)
        {
            var inspection = inspections.Get(id);
            if (inspection == null)
            {
                throw Fault("NOT_FOUND", "Inspection " + id + " does not exist.");
            }
            return inspection;
        }

        private Photo RequirePhoto(Guid id)
        {
            var photo = photos.Get(id);
            if (photo == null)
            {
                throw Fault("NOT_FOUND", "Photo " + id + " does not exist.");
            }
            return photo;
        }

        internal static FaultException<InspectionFault> Fault(string code, string message)
        {
            return new FaultException<InspectionFault>(new InspectionFault { Code = code, Message = message }, new FaultReason(message));
        }
    }
}
