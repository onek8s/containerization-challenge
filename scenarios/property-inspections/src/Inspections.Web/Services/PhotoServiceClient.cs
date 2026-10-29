using System;
using System.ServiceModel;
using Northwind.Inspections.Contracts;

namespace Northwind.Inspections.Web.Services
{
    /// <summary>Client for the photo service; endpoint "PhotoService" in Web.config.</summary>
    public class PhotoServiceClient : ClientBase<IPhotoService>, IPhotoService
    {
        public PhotoServiceClient() : base("PhotoService")
        {
        }

        public PhotoDto UploadPhoto(UploadPhotoRequest request) { return Channel.UploadPhoto(request); }
        public PhotoDto[] GetPhotos(int inspectionId) { return Channel.GetPhotos(inspectionId); }
        public void UpdateCaption(Guid photoId, string caption, string changedBy) { Channel.UpdateCaption(photoId, caption, changedBy); }
        public void DeletePhoto(Guid photoId, string deletedBy) { Channel.DeletePhoto(photoId, deletedBy); }
        public long RequestReport(int inspectionId, string requestedBy) { return Channel.RequestReport(inspectionId, requestedBy); }
        public ServiceStatusDto GetStatus() { return Channel.GetStatus(); }

        /// <summary>Calls the service and closes or aborts the channel as WCF requires.</summary>
        public static T Call<T>(Func<IPhotoService, T> call)
        {
            var client = new PhotoServiceClient();
            try
            {
                var result = call(client);
                client.Close();
                return result;
            }
            catch
            {
                client.Abort();
                throw;
            }
        }

        public static void Call(Action<IPhotoService> call)
        {
            Call<object>(c =>
            {
                call(c);
                return null;
            });
        }
    }
}
