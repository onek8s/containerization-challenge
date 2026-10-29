using System;
using System.Collections.Generic;
using System.Linq;
using Lucerne.ProjectSites.SharePoint;
using Microsoft.SharePoint;

namespace Lucerne.ProjectSites.ServerObjectModel
{
    internal sealed class SsomListGateway : IListGateway
    {
        private static readonly string[] AlwaysRead = { "ID", "Title", "FileRef", "FileLeafRef", "ContentTypeId", "Modified", "Created", "Author", "Editor" };
        private readonly SPWeb _web;
        private readonly SPList _list;

        public SsomListGateway(SPWeb web, SPList list)
        {
            _web = web;
            _list = list;
        }

        public string Title
        {
            get { return _list.Title; }
        }

        public bool IsLibrary
        {
            get { return _list.BaseType == SPBaseType.DocumentLibrary; }
        }

        public IEnumerable<ListItemData> Query(CamlQuery query)
        {
            var spQuery = new SPQuery
            {
                Query = query.Query ?? string.Empty,
                RowLimit = (uint)Math.Max(1, query.RowLimit),
                ViewAttributes = query.Recursive ? "Scope=\"RecursiveAll\"" : string.Empty
            };

            if (query.ViewFields != null && query.ViewFields.Length > 0)
            {
                spQuery.ViewFields = string.Concat(query.ViewFields.Concat(AlwaysRead).Distinct().Select(f => "<FieldRef Name='" + f + "' />"));
                spQuery.ViewFieldsOnly = true;
            }

            var returned = 0;
            do
            {
                var items = _list.GetItems(spQuery);
                foreach (SPListItem item in items)
                {
                    yield return Convert(item);
                    if (query.MaxItems > 0 && ++returned >= query.MaxItems)
                    {
                        yield break;
                    }
                }

                spQuery.ListItemCollectionPosition = items.ListItemCollectionPosition;
            }
            while (spQuery.ListItemCollectionPosition != null);
        }

        public ListItemData GetById(int id)
        {
            return Convert(_list.GetItemById(id));
        }

        public int Add(FieldValues values)
        {
            var item = _list.AddItem();
            Apply(item, values);
            item.Update();
            return item.ID;
        }

        public void Update(int id, FieldValues values, bool systemUpdate)
        {
            var item = _list.GetItemById(id);
            Apply(item, values);
            if (systemUpdate)
            {
                item.SystemUpdate(false);
            }
            else
            {
                item.Update();
            }
        }

        public void Recycle(int id)
        {
            _list.GetItemById(id).Recycle();
        }

        public int UploadFile(string fileName, byte[] content, FieldValues values, bool overwrite)
        {
            var file = _list.RootFolder.Files.Add(fileName, content, overwrite);
            var item = file.Item;
            if (values != null && values.Count > 0)
            {
                Apply(item, values);
                item.SystemUpdate(false);
            }

            return item.ID;
        }

        public byte[] OpenFile(int id)
        {
            return _list.GetItemById(id).File.OpenBinary();
        }

        public string MoveFile(int id, string destinationLibrary)
        {
            var target = _web.Lists[destinationLibrary];
            var file = _list.GetItemById(id).File;
            var destination = target.RootFolder.ServerRelativeUrl.TrimEnd('/') + "/" + file.Name;
            file.MoveTo(destination, true);
            return destination;
        }

        private ListItemData Convert(SPListItem item)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (SPField field in item.Fields)
            {
                if (field.Hidden && Array.IndexOf(AlwaysRead, field.InternalName) < 0)
                {
                    continue;
                }

                object raw;
                try
                {
                    raw = item[field.Id];
                }
                catch (ArgumentException)
                {
                    continue; // not in ViewFields
                }

                values[field.InternalName] = ConvertValue(field, raw);
            }

            return new ListItemData(item.ID, values);
        }

        private object ConvertValue(SPField field, object raw)
        {
            if (raw == null)
            {
                return null;
            }

            switch (field.Type)
            {
                case SPFieldType.User:
                    var user = new SPFieldUserValue(_web, raw.ToString());
                    return user.User == null ? null : new UserValue(user.User.LoginName, user.User.Name, user.LookupId);
                case SPFieldType.Lookup:
                    var lookup = new SPFieldLookupValue(raw.ToString());
                    return new LookupValue(lookup.LookupId, lookup.LookupValue);
                case SPFieldType.URL:
                    var url = new SPFieldUrlValue(raw.ToString());
                    return new UrlValue(url.Url, url.Description);
                case SPFieldType.MultiChoice:
                    var multi = new SPFieldMultiChoiceValue(raw.ToString());
                    var arr = new string[multi.Count];
                    for (var i = 0; i < multi.Count; i++)
                    {
                        arr[i] = multi[i];
                    }

                    return arr;
                default:
                    return raw;
            }
        }

        private void Apply(SPListItem item, FieldValues values)
        {
            foreach (var pair in values)
            {
                item[pair.Key] = ToSsom(pair.Value);
            }
        }

        private object ToSsom(object value)
        {
            var user = value as UserValue;
            if (user != null)
            {
                var spUser = _web.EnsureUser(user.Login);
                return new SPFieldUserValue(_web, spUser.ID, spUser.LoginName);
            }

            var lookup = value as LookupValue;
            if (lookup != null)
            {
                return new SPFieldLookupValue(lookup.Id, lookup.Value);
            }

            var url = value as UrlValue;
            if (url != null)
            {
                return new SPFieldUrlValue { Url = url.Url, Description = url.Description };
            }

            var arr = value as string[];
            if (arr != null)
            {
                var multi = new SPFieldMultiChoiceValue();
                foreach (var s in arr)
                {
                    multi.Add(s);
                }

                return multi;
            }

            if (value is decimal)
            {
                return (double)(decimal)value;
            }

            return value;
        }
    }
}
