namespace Lucerne.ProjectSites.SharePoint
{
    public class CamlQuery
    {
        public CamlQuery()
        {
            RowLimit = 500;
        }

        /// <summary>The &lt;Where&gt;/&lt;OrderBy&gt; part without the surrounding &lt;Query&gt;.</summary>
        public string Query { get; set; }

        public string[] ViewFields { get; set; }

        /// <summary>Page size. Queries are paged with ListItemCollectionPosition by both implementations.</summary>
        public int RowLimit { get; set; }

        public bool Recursive { get; set; }

        /// <summary>Stops after this many items (0 = all).</summary>
        public int MaxItems { get; set; }

        public static CamlQuery All(int rowLimit = 500)
        {
            return new CamlQuery { RowLimit = rowLimit, Recursive = true };
        }

        /// <summary>View XML as CSOM expects it.</summary>
        public string ToViewXml()
        {
            var fields = string.Empty;
            if (ViewFields != null && ViewFields.Length > 0)
            {
                fields = "<ViewFields>";
                foreach (var f in ViewFields)
                {
                    fields += "<FieldRef Name='" + f + "' />";
                }

                fields += "</ViewFields>";
            }

            return "<View" + (Recursive ? " Scope='RecursiveAll'" : string.Empty) + "><Query>" + (Query ?? string.Empty) + "</Query>"
                + fields + "<RowLimit Paged='TRUE'>" + RowLimit + "</RowLimit></View>";
        }
    }
}
