using System;
using System.IO;
using log4net;
using log4net.Config;

namespace Tailspin.DealerOrders.Infrastructure.Logging
{
    /// <summary>
    /// Configures log4net from the application's config file. The log folder comes from the DEALERORDERS_LOG_DIR
    /// environment variable or the "Logging.Folder" app setting (default C:\DealerOrders\Logs) and is exposed to the
    /// appender configuration as %property{LogDir}.
    /// </summary>
    public static class LogSetup
    {
        public const string DefaultFolder = @"C:\DealerOrders\Logs";

        public static string Configure(string component)
        {
            var folder = Environment.GetEnvironmentVariable("DEALERORDERS_LOG_DIR");
            if (string.IsNullOrWhiteSpace(folder)) folder = System.Configuration.ConfigurationManager.AppSettings["Logging.Folder"];
            if (string.IsNullOrWhiteSpace(folder)) folder = DefaultFolder;
            Directory.CreateDirectory(folder);
            GlobalContext.Properties["LogDir"] = folder;
            GlobalContext.Properties["Component"] = component;
            XmlConfigurator.Configure();
            LogManager.GetLogger(typeof(LogSetup)).InfoFormat("{0} starting, version {1}, logs in {2}", component,
                typeof(LogSetup).Assembly.GetName().Version, folder);
            return folder;
        }
    }
}
