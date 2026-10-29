using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Fabrikam.Housing.Repairs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Fabrikam.Housing.TenantPortal.Services
{
    public class RepairDraft
    {
        public string Area { get; set; }
        public string OptionKey { get; set; }
        public string Location { get; set; }
        public string Description { get; set; }
        public List<DraftPhoto> Photos { get; set; } = new List<DraftPhoto>();
        public DateTime? AppointmentDate { get; set; }
        public AppointmentSlot? Slot { get; set; }
    }

    public class DraftPhoto
    {
        public string FileName { get; set; }
        public string StoredName { get; set; }
        public string ContentType { get; set; }
        public long Size { get; set; }
    }

    /// <summary>
    /// Keeps the report-a-repair wizard in the session and the uploaded photos in a folder per session.
    /// </summary>
    public class RepairWizardStore
    {
        private const string Key = "repair-draft";
        public const int MaxPhotos = 3;
        public const long MaxPhotoBytes = 8 * 1024 * 1024;
        private static readonly string[] AllowedTypes = { "image/jpeg", "image/png", "image/heic", "image/gif" };

        private readonly IHttpContextAccessor _http;
        private readonly string _root;

        public RepairWizardStore(IHttpContextAccessor http, IConfiguration configuration)
        {
            _http = http;
            _root = configuration["Uploads:TempFolder"] ?? @"D:\HousingData\uploads";
        }

        private ISession Session => _http.HttpContext.Session;

        public RepairDraft Load()
        {
            var json = Session.GetString(Key);
            return json == null ? new RepairDraft() : JsonConvert.DeserializeObject<RepairDraft>(json);
        }

        public void Save(RepairDraft draft) => Session.SetString(Key, JsonConvert.SerializeObject(draft));

        public string PhotoFolder => Path.Combine(_root, Session.Id);

        public static bool IsAllowedPhoto(IFormFile file) =>
            file != null && file.Length > 0 && file.Length <= MaxPhotoBytes && AllowedTypes.Contains(file.ContentType?.ToLowerInvariant());

        public async Task<DraftPhoto> AddPhotoAsync(RepairDraft draft, IFormFile file)
        {
            Directory.CreateDirectory(PhotoFolder);
            var stored = Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName).ToLowerInvariant();
            using (var target = File.Create(Path.Combine(PhotoFolder, stored)))
                await file.CopyToAsync(target);
            var photo = new DraftPhoto { FileName = Path.GetFileName(file.FileName), StoredName = stored, ContentType = file.ContentType, Size = file.Length };
            draft.Photos.Add(photo);
            Save(draft);
            return photo;
        }

        public void RemovePhoto(RepairDraft draft, string storedName)
        {
            var photo = draft.Photos.FirstOrDefault(p => p.StoredName == storedName);
            if (photo == null) return;
            var path = Path.Combine(PhotoFolder, photo.StoredName);
            if (File.Exists(path)) File.Delete(path);
            draft.Photos.Remove(photo);
            Save(draft);
        }

        public Stream OpenPhoto(DraftPhoto photo) => File.OpenRead(Path.Combine(PhotoFolder, photo.StoredName));

        public void Clear()
        {
            Session.Remove(Key);
            if (Directory.Exists(PhotoFolder)) Directory.Delete(PhotoFolder, recursive: true);
        }
    }
}
