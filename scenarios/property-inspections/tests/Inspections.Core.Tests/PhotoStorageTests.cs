using System;
using Northwind.Inspections.Core;
using Xunit;

namespace Northwind.Inspections.Core.Tests
{
    public class PhotoStorageTests
    {
        private static readonly Guid Id = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");

        [Fact]
        public void BuildsRelativePaths()
        {
            Assert.Equal(@"2024\HO-2024-004512\0f8fad5bd9cb469fa16570867728950e.jpg", PhotoStorage.RelativePath("ho2024004512", Id));
            Assert.Equal(@"2024\HO-2024-004512\thumbs\0f8fad5bd9cb469fa16570867728950e.jpg", PhotoStorage.RelativeThumbnailPath("HO-2024-004512", Id));
        }

        [Fact]
        public void CombinesWithShareRoot()
        {
            Assert.Equal(@"\\files01.corp.local\Inspections$\2024\a.jpg", PhotoStorage.Combine(@"\\files01.corp.local\Inspections$\", @"2024\a.jpg"));
        }

        [Theory]
        [InlineData(@"..\secret.txt")]
        [InlineData(@"\2024\a.jpg")]
        [InlineData(@"C:\a.jpg")]
        public void RejectsEscapingPaths(string relative)
        {
            Assert.Throws<ArgumentException>(() => PhotoStorage.Combine(@"\\files01\Inspections$", relative));
        }

        [Theory]
        [InlineData("IMG_0001.JPG", true)]
        [InlineData("roof.png", true)]
        [InlineData("scan.tiff", true)]
        [InlineData("report.pdf", false)]
        [InlineData("noextension", false)]
        public void AcceptsImageFiles(string name, bool expected)
        {
            Assert.Equal(expected, PhotoStorage.IsAcceptedFileName(name));
        }

        [Fact]
        public void SanitizesFileNames()
        {
            Assert.Equal("a_b.jpg", PhotoStorage.SafeFileName("a:b.jpg"));
            Assert.Equal(120, PhotoStorage.SafeFileName(new string('x', 200) + ".jpg").Length);
        }
    }
}
