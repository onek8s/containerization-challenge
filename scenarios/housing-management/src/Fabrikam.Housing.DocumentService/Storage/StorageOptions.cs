namespace Fabrikam.Housing.DocumentService.Storage
{
    public class StorageOptions
    {
        public string Root { get; set; } = @"D:\HousingDocuments";
        public string ThumbnailFolder { get; set; } = @"D:\HousingDocuments\_thumbs";
        public string IndexFolder { get; set; } = @"D:\HousingData\index";
        public long MaxUploadBytes { get; set; } = 25 * 1024 * 1024;
        /// <summary>Hour (local time) for the nightly re-index of documents not yet in the index.</summary>
        public int ReindexHour { get; set; } = 2;
    }
}
