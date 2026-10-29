using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Tailspin.DealerOrders.LabTools
{
    /// <summary>Runs db\*.sql in order the way sqlcmd does: batches split on GO, $(Name) variables substituted.</summary>
    internal static class SchemaCommand
    {
        public static int Run(Options options)
        {
            var server = options["server", "localhost"];
            var builder = new SqlConnectionStringBuilder { DataSource = server, InitialCatalog = "master", ConnectTimeout = 30 };
            if (options["user"] != null)
            {
                builder.UserID = options["user"];
                builder.Password = options["password"];
            }
            else builder.IntegratedSecurity = true;

            var variables = options.All("var").Select(v => v.Split(new[] { '=' }, 2)).ToDictionary(p => p[0], p => p.Length > 1 ? p[1] : "");
            var folder = options["folder", Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db")];
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                connection.Open();
                foreach (var file in Directory.GetFiles(folder, "*.sql").OrderBy(f => f, StringComparer.Ordinal))
                {
                    var batches = Batches(File.ReadAllText(file), variables).ToList();
                    Console.WriteLine($"{Path.GetFileName(file)}: {batches.Count} batches");
                    foreach (var batch in batches)
                    {
                        using (var command = new SqlCommand(batch, connection) { CommandTimeout = 300 })
                        {
                            try
                            {
                                command.ExecuteNonQuery();
                            }
                            catch (SqlException ex)
                            {
                                Console.Error.WriteLine($"{Path.GetFileName(file)}: {ex.Message}");
                                Console.Error.WriteLine(batch.Length > 400 ? batch.Substring(0, 400) + "..." : batch);
                                return 3;
                            }
                        }
                    }
                }
            }
            return 0;
        }

        public static IEnumerable<string> Batches(string script, IDictionary<string, string> variables)
        {
            script = Regex.Replace(script, @"\$\((\w+)\)", m =>
                variables.TryGetValue(m.Groups[1].Value, out var value) ? value.Replace("'", "''") : throw new InvalidOperationException($"Variable {m.Groups[1].Value} has no value (--var {m.Groups[1].Value}=...)."));
            foreach (var batch in Regex.Split(script, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                if (batch.Trim().Length > 0) yield return batch;
        }
    }
}
