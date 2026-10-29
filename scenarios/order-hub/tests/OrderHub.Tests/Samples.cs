using System;
using System.IO;

namespace Litware.OrderHub.Tests
{
    internal static class Samples
    {
        public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "samples", name);
        public static string Text(string name) => File.ReadAllText(Path(name));
        public static byte[] Bytes(string name) => File.ReadAllBytes(Path(name));
    }
}
