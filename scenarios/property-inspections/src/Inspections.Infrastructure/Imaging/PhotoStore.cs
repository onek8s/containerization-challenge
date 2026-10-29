using System;
using System.Diagnostics;
using System.IO;
using Northwind.Inspections.Core;
using Northwind.Inspections.Infrastructure.Configuration;
using Northwind.Inspections.Infrastructure.Data;
using Northwind.Inspections.Infrastructure.Diagnostics;
using Northwind.Inspections.Infrastructure.Security;

namespace Northwind.Inspections.Infrastructure.Imaging
{
    /// <summary>Stores processed photos on the photo share and records them in the database.</summary>
    public sealed class PhotoStore
    {
        private readonly InspectionSettings settings;
        private readonly ShareAccess share;
        private readonly PhotoRepository photos;
        private readonly PhotoProcessor processor;

        public PhotoStore(InspectionSettings settings, Db db)
        {
            this.settings = settings;
            share = new ShareAccess(settings);
            photos = new PhotoRepository(db);
            processor = new PhotoProcessor(settings.MaxPhotoEdge, settings.ThumbnailEdge, settings.JpegQuality, settings.WatermarkFont);
        }

        public ShareAccess Share
        {
            get { return share; }
        }

        public Photo Add(Inspection inspection, string fileName, string caption, byte[] content, string uploadedBy)
        {
            var started = Stopwatch.GetTimestamp();
            ProcessedPhoto processed;
            var sequence = photos.NextSequence(inspection.Id);
            try
            {
                processed = processor.Process(content, inspection.ClaimNumber, sequence, inspection.AdjusterName, DateTime.Now);
            }
            catch (InvalidPhotoException)
            {
                InspectionsCounters.Instance.PhotoRejected();
                throw;
            }

            var photo = new Photo
            {
                Id = Guid.NewGuid(),
                InspectionId = inspection.Id,
                Sequence = sequence,
                OriginalFileName = PhotoStorage.SafeFileName(fileName),
                Caption = caption,
                Width = processed.Width,
                Height = processed.Height,
                Bytes = processed.Full.LongLength,
                TakenUtc = processed.Taken.HasValue ? processed.Taken.Value.ToUniversalTime() : (DateTime?)null,
                UploadedBy = uploadedBy,
                UploadedUtc = DateTime.UtcNow
            };
            photo.StoragePath = PhotoStorage.RelativePath(inspection.ClaimNumber, photo.Id);
            photo.ThumbnailPath = PhotoStorage.RelativeThumbnailPath(inspection.ClaimNumber, photo.Id);

            share.Run(() =>
            {
                Write(PhotoStorage.Combine(settings.PhotoShare, photo.StoragePath), processed.Full);
                Write(PhotoStorage.Combine(settings.PhotoShare, photo.ThumbnailPath), processed.Thumbnail);
            });

            photos.Insert(photo);
            InspectionsCounters.Instance.PhotoStored(Stopwatch.GetTimestamp() - started, content.LongLength);
            InspectionsLog.Info(EventId.PhotoStored, "Photo {0} ({1}) stored for {2} by {3}.", photo.Id, photo.OriginalFileName, inspection.ClaimNumber, uploadedBy);
            return photo;
        }

        public byte[] Read(Photo photo, PhotoSize size)
        {
            var relative = size == PhotoSize.Thumbnail ? photo.ThumbnailPath : photo.StoragePath;
            return share.Run(() => File.ReadAllBytes(PhotoStorage.Combine(settings.PhotoShare, relative)));
        }

        public void Remove(Photo photo)
        {
            share.Run(() =>
            {
                DeleteIfExists(PhotoStorage.Combine(settings.PhotoShare, photo.StoragePath));
                DeleteIfExists(PhotoStorage.Combine(settings.PhotoShare, photo.ThumbnailPath));
            });
        }

        public bool ShareReachable()
        {
            try
            {
                return share.Run(() => Directory.Exists(settings.PhotoShare));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void Write(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllBytes(temp, bytes);
            if (File.Exists(path))
            {
                File.Replace(temp, path, null);
            }
            else
            {
                File.Move(temp, path);
            }
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
