using System;
using System.Collections.Generic;
using System.IO;
using Lucerne.ProjectSites.Domain;

namespace Lucerne.ProjectSites.Core.Services
{
    public class DocumentState
    {
        public string FileName { get; set; }
        public string DocumentNumber { get; set; }
        public string Discipline { get; set; }
        public string DocumentType { get; set; }
        public string Revision { get; set; }
        public DeliverableStatus Status { get; set; }
        public bool IsRecord { get; set; }
    }

    /// <summary>Rules enforced by the engineering document event receivers.</summary>
    public static class DocumentRules
    {
        private static readonly HashSet<string> BlockedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".exe", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".dll"
        };

        public static string ValidateAdd(DocumentState state, string libraryDiscipline)
        {
            var ext = Path.GetExtension(state.FileName ?? string.Empty);
            if (BlockedExtensions.Contains(ext))
            {
                return "Files of type " + ext + " cannot be stored in project libraries.";
            }

            if (!string.IsNullOrEmpty(state.Discipline) && !string.IsNullOrEmpty(libraryDiscipline)
                && !string.Equals(state.Discipline, libraryDiscipline, StringComparison.OrdinalIgnoreCase))
            {
                return "Discipline " + state.Discipline + " does not match the " + libraryDiscipline + " library.";
            }

            if (!string.IsNullOrEmpty(state.DocumentType) && Array.IndexOf(Domain.DocumentNumber.DocumentTypes, state.DocumentType) < 0)
            {
                return "Unknown document type " + state.DocumentType + ".";
            }

            if (!string.IsNullOrEmpty(state.Revision) && !RevisionRules.IsValid(state.Revision))
            {
                return "Revision must be P01-P99 or C01-C99.";
            }

            return null;
        }

        public static string ValidateUpdate(DocumentState before, DocumentState after)
        {
            if (before.IsRecord)
            {
                return "The document is a declared record and cannot be changed.";
            }

            if (before.Status == DeliverableStatus.Issued && after.Status == DeliverableStatus.Issued
                && string.Equals(before.Revision, after.Revision, StringComparison.Ordinal))
            {
                if (!string.Equals(before.DocumentNumber, after.DocumentNumber, StringComparison.Ordinal))
                {
                    return "The number of an issued document cannot be changed.";
                }
            }

            if (!string.IsNullOrEmpty(before.DocumentNumber) && !string.Equals(before.DocumentNumber, after.DocumentNumber, StringComparison.Ordinal))
            {
                Domain.DocumentNumber parsed;
                if (Domain.DocumentNumber.TryParse(before.DocumentNumber, out parsed))
                {
                    return "Document numbers are allocated by the system and cannot be edited.";
                }
            }

            var revisionError = RevisionRules.ValidateChange(before.Revision, after.Revision);
            if (revisionError != null)
            {
                return revisionError;
            }

            if (after.Status == DeliverableStatus.Issued && !RevisionRules.IsContractual(after.Revision) && !string.IsNullOrEmpty(after.Revision))
            {
                return "Issued documents need a contractual (C) revision.";
            }

            return null;
        }

        public static string ValidateDelete(DocumentState state)
        {
            if (state.IsRecord)
            {
                return "Declared records cannot be deleted.";
            }

            if (state.Status == DeliverableStatus.Issued || state.Status == DeliverableStatus.Superseded)
            {
                return "Issued documents cannot be deleted. Withdraw the document instead.";
            }

            return null;
        }
    }
}
