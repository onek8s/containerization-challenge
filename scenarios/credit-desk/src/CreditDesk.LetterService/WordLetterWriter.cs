using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace CreditDesk.LetterService
{
    /// <summary>
    /// Fills the confirmation letter template through Word automation. The template contains one
    /// bookmark per field (see LetterFields.Bookmarks) and is printed on the letterhead tray.
    /// </summary>
    internal sealed class WordLetterWriter : IDisposable
    {
        private const int WdDoNotSaveChanges = 0;
        private const int WdExportFormatPdf = 17;
        private const int WdExportOptimizeForPrint = 0;
        private const int WdAlertsNone = 0;

        private readonly string _templatePath;
        private dynamic _word;

        public WordLetterWriter(string templatePath)
        {
            if (!File.Exists(templatePath))
                throw new FileNotFoundException("Letter template not found.", templatePath);
            _templatePath = templatePath;

            var type = Type.GetTypeFromProgID("Word.Application", true);
            _word = Activator.CreateInstance(type);
            _word.Visible = false;
            _word.DisplayAlerts = WdAlertsNone;
            _word.ScreenUpdating = false;
            _word.Options.SaveNormalPrompt = false;
            _word.Options.PrintBackground = false;
        }

        public void Write(IDictionary<string, string> fields, string pdfPath, string printer, int copies)
        {
            dynamic doc = _word.Documents.Add(_templatePath, false, 0, false);
            try
            {
                foreach (var field in fields)
                {
                    if (!(bool)doc.Bookmarks.Exists(field.Key))
                        throw new InvalidOperationException("Template has no bookmark '" + field.Key + "'.");
                    dynamic range = doc.Bookmarks.Item(field.Key).Range;
                    range.Text = field.Value;
                    Release(range);
                }
                doc.Fields.Update();

                doc.ExportAsFixedFormat(pdfPath, WdExportFormatPdf, false, WdExportOptimizeForPrint);
                doc.SaveAs2(Path.ChangeExtension(pdfPath, ".docx"));

                if (!string.IsNullOrWhiteSpace(printer) && copies > 0)
                {
                    _word.ActivePrinter = printer;
                    doc.PrintOut(false, Copies: copies);
                }
            }
            finally
            {
                doc.Close(WdDoNotSaveChanges);
                Release(doc);
            }
        }

        public void Dispose()
        {
            if (_word == null) return;
            try
            {
                _word.Quit(WdDoNotSaveChanges);
            }
            catch (COMException)
            {
                // Word already gone; nothing to clean up.
            }
            Release(_word);
            _word = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private static void Release(object com)
        {
            if (com != null && Marshal.IsComObject(com))
                Marshal.FinalReleaseComObject(com);
        }
    }
}
