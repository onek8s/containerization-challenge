using System;
using System.Security.Principal;
using Microsoft.SqlServer.Server;

namespace ContosoFoods.TradeRebates.SqlClr.Integration
{
    /// <summary>
    /// Runs file system work as the Windows login that called the procedure, so share and NTFS
    /// permissions of that account apply instead of the SQL Server service account.
    /// SQL logins (no Windows identity) run as the service account.
    /// Data access is not allowed while impersonating: callers must undo before touching SqlContext.Pipe or the context connection.
    /// </summary>
    internal sealed class CallerContext : IDisposable
    {
        private readonly WindowsImpersonationContext? _context;

        private CallerContext(WindowsImpersonationContext? context)
        {
            _context = context;
        }

        public static CallerContext Impersonate()
        {
            WindowsIdentity? caller = SqlContext.IsAvailable ? SqlContext.WindowsIdentity : null;
            if (caller == null)
                return new CallerContext(null);
            return new CallerContext(caller.Impersonate());
        }

        public static string CallerName()
        {
            WindowsIdentity? caller = SqlContext.IsAvailable ? SqlContext.WindowsIdentity : null;
            return caller?.Name ?? "(service account)";
        }

        public void Dispose()
        {
            _context?.Undo();
        }
    }
}
