using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.SharePoint.Client;
using SpCamlQuery = Microsoft.SharePoint.Client.CamlQuery;

namespace Lucerne.ProjectSites.SharePoint.Csom
{
    internal sealed class CsomListGateway : IListGateway
    {
        private readonly ClientContext _ctx;
        private readonly Web _web;
        private readonly List _list;
        private readonly UserCache _users;

        public CsomListGateway(ClientContext ctx, Web web, List list, UserCache users)
        {
            _ctx = ctx;
            _web = web;
            _list = list;
            _users = users;
        }

        public string Title
        {
            get { return _list.Title; }
        }

        public bool IsLibrary
        {
            get { return _list.BaseType == BaseType.DocumentLibrary; }
        }

        public IEnumerable<ListItemData> Query(CamlQuery query)
        {
            var position = (ListItemCollectionPosition)null;
            var returned = 0;
            do
            {
                var caml = new SpCamlQuery { ViewXml = query.ToViewXml(), ListItemCollectionPosition = position };
                var items = _list.GetItems(caml);
                _ctx.Load(items, i => i.ListItemCollectionPosition, i => i.Include(x => x.Id, x => x.FieldValuesAsText, x => x));
                Csom.Execute(_ctx);

                var page = items.ToList();
                _users.Prefetch(page.SelectMany(i => i.FieldValues.Values.OfType<FieldUserValue>().Select(u => u.LookupId)));

                foreach (var item in page)
                {
                    yield return Convert(item);
                    if (query.MaxItems > 0 && ++returned >= query.MaxItems)
                    {
                        yield break;
                    }
                }

                position = items.ListItemCollectionPosition;
            }
            while (position != null);
        }

        public ListItemData GetById(int id)
        {
            var item = _list.GetItemById(id);
            _ctx.Load(item);
            Csom.Execute(_ctx);
            _users.Prefetch(item.FieldValues.Values.OfType<FieldUserValue>().Select(u => u.LookupId));
            return Convert(item);
        }

        public int Add(FieldValues values)
        {
            var item = _list.AddItem(new ListItemCreationInformation());
            Apply(item, values);
            item.Update();
            _ctx.Load(item, i => i.Id);
            Csom.Execute(_ctx);
            return item.Id;
        }

        public void Update(int id, FieldValues values, bool systemUpdate)
        {
            var item = _list.GetItemById(id);
            if (systemUpdate)
            {
                // SharePoint 2016 CSOM has no SystemUpdate: keep Modified and Editor by writing them back.
                _ctx.Load(item, i => i["Modified"], i => i["Editor"]);
                Csom.Execute(_ctx);
                var modified = item["Modified"];
                var editor = item["Editor"] as FieldUserValue;
                Apply(item, values);
                item["Modified"] = modified;
                if (editor != null)
                {
                    item["Editor"] = new FieldUserValue { LookupId = editor.LookupId };
                }
            }
            else
            {
                Apply(item, values);
            }

            item.Update();
            Csom.Execute(_ctx);
        }

        public void Recycle(int id)
        {
            _list.GetItemById(id).Recycle();
            Csom.Execute(_ctx);
        }

        public int UploadFile(string fileName, byte[] content, FieldValues values, bool overwrite)
        {
            using (var stream = new MemoryStream(content))
            {
                var file = _list.RootFolder.Files.Add(new FileCreationInformation
                {
                    ContentStream = stream,
                    Url = fileName,
                    Overwrite = overwrite
                });
                var item = file.ListItemAllFields;
                if (values != null && values.Count > 0)
                {
                    Apply(item, values);
                    item.Update();
                }

                _ctx.Load(item, i => i.Id);
                Csom.Execute(_ctx);
                return item.Id;
            }
        }

        public byte[] OpenFile(int id)
        {
            var file = _list.GetItemById(id).File;
            var stream = file.OpenBinaryStream();
            Csom.Execute(_ctx);
            using (var ms = new MemoryStream())
            {
                stream.Value.CopyTo(ms);
                return ms.ToArray();
            }
        }

        public string MoveFile(int id, string destinationLibrary)
        {
            var target = _web.Lists.GetByTitle(destinationLibrary);
            var file = _list.GetItemById(id).File;
            _ctx.Load(target.RootFolder, f => f.ServerRelativeUrl);
            _ctx.Load(file, f => f.Name);
            Csom.Execute(_ctx);

            var destination = target.RootFolder.ServerRelativeUrl.TrimEnd('/') + "/" + file.Name;
            file.MoveTo(destination, MoveOperations.Overwrite);
            Csom.Execute(_ctx);
            return destination;
        }

        private ListItemData Convert(ListItem item)
        {
            var values = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in item.FieldValues)
            {
                values[pair.Key] = ConvertValue(pair.Value);
            }

            return new ListItemData(item.Id, values);
        }

        private object ConvertValue(object value)
        {
            var user = value as FieldUserValue;
            if (user != null)
            {
                return new UserValue(_users.Login(user.LookupId), user.LookupValue, user.LookupId);
            }

            var users = value as FieldUserValue[];
            if (users != null)
            {
                return users.Length == 0 ? null : new UserValue(_users.Login(users[0].LookupId), users[0].LookupValue, users[0].LookupId);
            }

            var lookup = value as FieldLookupValue;
            if (lookup != null)
            {
                return new LookupValue(lookup.LookupId, lookup.LookupValue);
            }

            var url = value as FieldUrlValue;
            if (url != null)
            {
                return new UrlValue(url.Url, url.Description);
            }

            var content = value as ContentTypeId;
            if (content != null)
            {
                return content.StringValue;
            }

            return value;
        }

        private static void Apply(ListItem item, FieldValues values)
        {
            foreach (var pair in values)
            {
                item[pair.Key] = ToCsom(pair.Value);
            }
        }

        private static object ToCsom(object value)
        {
            var user = value as UserValue;
            if (user != null)
            {
                return user.Id > 0 ? new FieldUserValue { LookupId = user.Id } : FieldUserValue.FromUser(user.Login);
            }

            var lookup = value as LookupValue;
            if (lookup != null)
            {
                return new FieldLookupValue { LookupId = lookup.Id };
            }

            var url = value as UrlValue;
            if (url != null)
            {
                return new FieldUrlValue { Url = url.Url, Description = url.Description };
            }

            if (value is decimal)
            {
                return (double)(decimal)value;
            }

            return value;
        }
    }
}
