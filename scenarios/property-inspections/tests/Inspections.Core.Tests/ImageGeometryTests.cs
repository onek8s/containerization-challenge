using System;
using Northwind.Inspections.Core;
using Xunit;

namespace Northwind.Inspections.Core.Tests
{
    public class ImageGeometryTests
    {
        [Theory]
        [InlineData(1, OrientationFix.None)]
        [InlineData(3, OrientationFix.Rotate180)]
        [InlineData(6, OrientationFix.Rotate90)]
        [InlineData(8, OrientationFix.Rotate270)]
        [InlineData(0, OrientationFix.None)]
        [InlineData(42, OrientationFix.None)]
        public void MapsExifOrientation(int value, OrientationFix expected)
        {
            Assert.Equal(expected, ImageGeometry.FromExifOrientation(value));
        }

        [Fact]
        public void RotationSwapsAxes()
        {
            Assert.True(ImageGeometry.SwapsAxes(OrientationFix.Rotate90));
            Assert.False(ImageGeometry.SwapsAxes(OrientationFix.Rotate180));
        }

        [Theory]
        [InlineData(4000, 3000, 1600, 1600, 1200)]
        [InlineData(3000, 4000, 1600, 1200, 1600)]
        [InlineData(800, 600, 1600, 800, 600)]
        [InlineData(5000, 10, 320, 320, 1)]
        public void FitsWithinLongestEdge(int w, int h, int max, int ew, int eh)
        {
            var size = ImageGeometry.FitWithin(w, h, max);
            Assert.Equal(ew, size.Width);
            Assert.Equal(eh, size.Height);
        }

        [Theory]
        [InlineData(0, 0, 0)]
        [InlineData(3, 3, 1)]
        [InlineData(10, 4, 3)]
        [InlineData(8, 4, 2)]
        public void CalculatesGrid(int count, int expectedCols, int expectedRows)
        {
            var grid = ImageGeometry.Grid(count, 4);
            Assert.Equal(expectedCols, grid.Width);
            Assert.Equal(expectedRows, grid.Height);
        }

        [Fact]
        public void ParsesExifDate()
        {
            Assert.Equal(new DateTime(2024, 5, 17, 14, 3, 9), ImageGeometry.ParseExifDate("2024:05:17 14:03:09\0"));
            Assert.Null(ImageGeometry.ParseExifDate("not a date"));
        }
    }
}
