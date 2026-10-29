using System;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.Core;
using Lucerne.ProjectSites.Core.Services;
using Lucerne.ProjectSites.Domain;
using Lucerne.ProjectSites.Farm.Common;
using Lucerne.ProjectSites.ServerObjectModel;
using Microsoft.SharePoint;

namespace Lucerne.ProjectSites.Farm.Receivers
{
    /// <summary>
    /// Registered on the discipline libraries of project sites by the project site feature receiver.
    /// Allocates document numbers, enforces revision rules and protects issued documents and records.
    /// </summary>
    public class DocumentReceiver : SPItemEventReceiver
    {
        private static readonly IActivityLog Log = new UlsActivityLog("DocumentReceiver");

        public override void ItemAdding(SPItemEventProperties properties)
        {
            var after = State(properties.AfterProperties, properties.AfterUrl);
            var error = DocumentRules.ValidateAdd(after, LibraryDiscipline(properties.List));
            if (error != null)
            {
                Cancel(properties, error);
            }
        }

        public override void ItemAdded(SPItemEventProperties properties)
        {
            var item = properties.ListItem;
            var number = item[FieldNames.DocumentNumber] as string;
            var discipline = (item[FieldNames.Discipline] as string) ?? LibraryDiscipline(properties.List);
            var type = item[FieldNames.DocumentType] as string;
            if (!string.IsNullOrEmpty(number) || string.IsNullOrEmpty(discipline) || string.IsNullOrEmpty(type))
            {
                ObserveExisting(properties.Web, number);
                return;
            }

            try
            {
                var projectNumber = FarmSettings.ProjectNumber(properties.Web);
                using (var site = SsomSiteConnector.Wrap(properties.Web.Site.RootWeb))
                {
                    var allocated = new DocumentNumberAllocator(site).Next(projectNumber, discipline, type);
                    EventFiringEnabled = false;
                    item[FieldNames.DocumentNumber] = allocated.ToString();
                    item[FieldNames.Discipline] = discipline;
                    if (string.IsNullOrEmpty(item[FieldNames.Revision] as string))
                    {
                        item[FieldNames.Revision] = RevisionRules.First;
                    }

                    item[FieldNames.DeliverableStatus] = DeliverableStatus.WorkInProgress.ToString();
                    item.SystemUpdate(false);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Numbering failed for " + item.Url, ex);
            }
            finally
            {
                EventFiringEnabled = true;
            }
        }

        public override void ItemUpdating(SPItemEventProperties properties)
        {
            var before = State(properties.ListItem);
            var after = State(properties.ListItem);
            Merge(after, properties.AfterProperties);
            var error = DocumentRules.ValidateUpdate(before, after);
            if (error != null)
            {
                Cancel(properties, error);
            }
        }

        public override void ItemDeleting(SPItemEventProperties properties)
        {
            var error = DocumentRules.ValidateDelete(State(properties.ListItem));
            if (error != null)
            {
                Cancel(properties, error);
            }
        }

        public override void ItemCheckingIn(SPItemEventProperties properties)
        {
            ItemUpdating(properties);
        }

        private static void ObserveExisting(SPWeb web, string number)
        {
            DocumentNumber parsed;
            if (DocumentNumber.TryParse(number, out parsed))
            {
                using (var site = SsomSiteConnector.Wrap(web.Site.RootWeb))
                {
                    new DocumentNumberAllocator(site).Observe(parsed);
                }
            }
        }

        private static void Cancel(SPItemEventProperties properties, string message)
        {
            properties.Status = SPEventReceiverStatus.CancelWithError;
            properties.ErrorMessage = message;
        }

        private static string LibraryDiscipline(SPList list)
        {
            foreach (var d in Discipline.All)
            {
                if (string.Equals(d.LibraryName, list.Title, StringComparison.OrdinalIgnoreCase))
                {
                    return d.Code;
                }
            }

            return null;
        }

        private static DocumentState State(SPListItem item)
        {
            DeliverableStatus status;
            Enum.TryParse(item[FieldNames.DeliverableStatus] as string, true, out status);
            return new DocumentState
            {
                FileName = item.Name,
                DocumentNumber = item[FieldNames.DocumentNumber] as string,
                Discipline = item[FieldNames.Discipline] as string,
                DocumentType = item[FieldNames.DocumentType] as string,
                Revision = item[FieldNames.Revision] as string,
                Status = status,
                IsRecord = item[FieldNames.IsRecord] is bool && (bool)item[FieldNames.IsRecord]
            };
        }

        private static DocumentState State(SPItemEventDataCollection after, string url)
        {
            var state = new DocumentState { FileName = url };
            Merge(state, after);
            return state;
        }

        private static void Merge(DocumentState state, SPItemEventDataCollection after)
        {
            // AfterProperties only contain the fields that change.
            if (after[FieldNames.DocumentNumber] != null) state.DocumentNumber = after[FieldNames.DocumentNumber].ToString();
            if (after[FieldNames.Discipline] != null) state.Discipline = after[FieldNames.Discipline].ToString();
            if (after[FieldNames.DocumentType] != null) state.DocumentType = after[FieldNames.DocumentType].ToString();
            if (after[FieldNames.Revision] != null) state.Revision = after[FieldNames.Revision].ToString();
            DeliverableStatus status;
            if (after[FieldNames.DeliverableStatus] != null && Enum.TryParse(after[FieldNames.DeliverableStatus].ToString(), true, out status))
            {
                state.Status = status;
            }
        }
    }
}
