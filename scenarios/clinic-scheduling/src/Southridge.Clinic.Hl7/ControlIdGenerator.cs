using System;
using System.Globalization;
using System.Threading;

namespace Southridge.Clinic.Hl7
{
    /// <summary>Message control ids: prefix + yyMMddHHmmss + 4 digit counter (unique per process).</summary>
    public static class ControlIdGenerator
    {
        private static int _counter;

        public static string Next(string prefix)
        {
            int n = Interlocked.Increment(ref _counter) % 10000;
            return prefix + DateTime.Now.ToString("yyMMddHHmmss", CultureInfo.InvariantCulture) + n.ToString("0000", CultureInfo.InvariantCulture);
        }
    }
}
