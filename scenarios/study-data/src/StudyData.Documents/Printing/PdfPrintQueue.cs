using System.IO;
using System.Printing;
using System.Runtime.Versioning;
using System.Windows.Xps.Packaging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace StudyData.Documents.Printing;

/// <summary>
/// Prints XPS documents to PDF on the local "StudyData PDF" printer (Microsoft Print To PDF driver on a
/// local port that is a fixed file). Because every job lands in the same port file, jobs run one at a
/// time on the machine; a global mutex serialises the web applications and the jobs service.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class PdfPrintQueue
{
    private const string MutexName = @"Global\StudyData.PdfPrinter";

    private readonly DocumentOptions _options;
    private readonly ILogger<PdfPrintQueue> _log;
    private readonly SemaphoreSlim _local = new(1, 1);

    public PdfPrintQueue(IOptions<DocumentOptions> options, ILogger<PdfPrintQueue> log)
    {
        _options = options.Value;
        _log = log;
    }

    public async Task PrintXpsToPdfAsync(string xpsPath, string pdfPath, string jobName, CancellationToken ct)
    {
        await _local.WaitAsync(ct);
        try
        {
            await Task.Factory.StartNew(() => PrintExclusive(xpsPath, pdfPath, jobName, ct), ct,
                TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }
        finally
        {
            _local.Release();
        }
    }

    private void PrintExclusive(string xpsPath, string pdfPath, string jobName, CancellationToken ct)
    {
        using var mutex = new Mutex(false, MutexName);
        try
        {
            if (!mutex.WaitOne(_options.PrintTimeout))
            {
                throw new TimeoutException("The PDF printer is busy.");
            }
        }
        catch (AbandonedMutexException)
        {
            _log.LogWarning("Previous PDF print owner ended without releasing the printer");
        }

        try
        {
            if (File.Exists(_options.PrinterPortFile))
            {
                File.Delete(_options.PrinterPortFile);
            }

            using var server = new LocalPrintServer();
            using var queue = server.GetPrintQueue(_options.PdfPrinter);
            if (queue.IsOffline || queue.IsInError)
            {
                throw new InvalidOperationException($"Printer '{_options.PdfPrinter}' is {queue.QueueStatus}.");
            }

            // Same thread apartment rules as rendering: System.Printing needs STA.
            Exception? failure = null;
            var sta = new Thread(() =>
            {
                try
                {
                    using var xps = new XpsDocument(xpsPath, FileAccess.Read);
                    var sequence = xps.GetFixedDocumentSequence();
                    var writer = PrintQueue.CreateXpsDocumentWriter(queue);
                    queue.CurrentJobSettings.Description = jobName;
                    writer.Write(sequence, queue.DefaultPrintTicket);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });
            sta.SetApartmentState(ApartmentState.STA);
            sta.Start();
            sta.Join();
            if (failure is not null)
            {
                throw new InvalidOperationException($"Printing {jobName} failed: {failure.Message}", failure);
            }

            WaitForSpool(queue, ct);
            Directory.CreateDirectory(Path.GetDirectoryName(pdfPath)!);
            File.Copy(_options.PrinterPortFile, pdfPath, overwrite: true);
            File.Delete(_options.PrinterPortFile);
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    private void WaitForSpool(PrintQueue queue, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + _options.PrintTimeout;
        long lastLength = -1;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            queue.Refresh();
            if (queue.NumberOfJobs == 0 && File.Exists(_options.PrinterPortFile))
            {
                var length = new FileInfo(_options.PrinterPortFile).Length;
                if (length > 0 && length == lastLength)
                {
                    return;
                }

                lastLength = length;
            }

            Thread.Sleep(500);
        }

        throw new TimeoutException($"Printer '{_options.PdfPrinter}' did not finish in {_options.PrintTimeout.TotalMinutes:0} minutes.");
    }
}
