using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using Fabrikam.Housing.Documents;
using Fabrikam.Housing.Documents.Imaging;
using Fabrikam.Housing.Documents.Search;
using FluentAssertions;
using Xunit;

namespace Fabrikam.Housing.Tests.Documents
{
    public class IndexAndImageTests : IDisposable
    {
        private readonly string _folder = Path.Combine(Path.GetTempPath(), "housing-index-" + Guid.NewGuid().ToString("N"));

        [Fact]
        public void Lucene_index_adds_searches_filters_and_deletes()
        {
            var gas = Guid.NewGuid();
            var damp = Guid.NewGuid();
            using (var index = new LuceneDocumentIndex(_folder))
            {
                index.Add(new IndexedDocument { DocumentKey = gas, Title = "Gas safety record CP12-880001", Category = "Certificate", Text = "Landlord gas safety record. Boiler Worcester 28CDi passed. Flue flow test satisfactory.", PropertyId = 10, UploadedAt = new DateTime(2021, 4, 1, 10, 0, 0) });
                index.Add(new IndexedDocument { DocumentKey = damp, Title = "Letter from tenant", Category = "ScannedPost", Text = "There is black mould in the bedroom and the damp patch is getting bigger.", TenancyId = 7, PropertyId = 11, UploadedAt = new DateTime(2021, 4, 2) });
                index.Commit();

                var hits = index.Search("boiler", null, null, 10);
                hits.Should().ContainSingle().Which.DocumentKey.Should().Be(gas);
                hits[0].PropertyId.Should().Be(10);
                hits[0].TenancyId.Should().BeNull();
                hits[0].UploadedAt.Should().Be(new DateTime(2021, 4, 1, 10, 0, 0));
                hits[0].Snippet.Should().Contain("Boiler");

                index.Search("mould", 7, null, 10).Should().ContainSingle().Which.Category.Should().Be("ScannedPost");
                index.Search("mould", 8, null, 10).Should().BeEmpty();
                index.Search("", null, 11, 10).Should().ContainSingle();
                index.Search("unbalanced \"quote ( AND", null, null, 10).Should().BeEmpty();

                index.Add(new IndexedDocument { DocumentKey = gas, Title = "Gas safety record CP12-880001 (amended)", Category = "Certificate", Text = "Boiler replaced", PropertyId = 10, UploadedAt = DateTime.Now });
                index.Delete(damp);
                index.Commit();
                index.Search("", null, null, 10).Should().ContainSingle().Which.Title.Should().Contain("amended");
            }

            using (var reopened = new LuceneDocumentIndex(_folder))
                reopened.Search("amended", null, null, 10).Should().ContainSingle();
        }

        [Fact]
        public void Thumbnailer_scales_images_and_ignores_other_types()
        {
            var thumbnailer = new Thumbnailer();
            thumbnailer.Create(new byte[] { 1 }, "application/pdf").Should().BeNull();

            byte[] jpeg;
            using (var bmp = new Bitmap(1200, 800))
            using (var ms = new MemoryStream())
            {
                using (var g = Graphics.FromImage(bmp)) g.Clear(Color.SteelBlue);
                bmp.Save(ms, ImageFormat.Jpeg);
                jpeg = ms.ToArray();
            }
            var thumb = thumbnailer.Create(jpeg, "image/jpeg");
            using (var image = Image.FromStream(new MemoryStream(thumb)))
            {
                image.Width.Should().Be(240);
                image.Height.Should().Be(160);
            }
        }

        public void Dispose()
        {
            try { Directory.Delete(_folder, true); } catch (IOException) { }
        }
    }
}
