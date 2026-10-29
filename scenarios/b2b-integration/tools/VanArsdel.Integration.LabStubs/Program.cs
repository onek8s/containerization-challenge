using System.Globalization;
using System.Text;
using System.Xml;
using VanArsdel.Integration.Contracts;
using VanArsdel.Integration.LabStubs.EndToEnd;
using VanArsdel.Integration.LabStubs.Lab;
using VanArsdel.Integration.LabStubs.Pipelines;
using VanArsdel.Integration.LabStubs.Seed;
using VanArsdel.Integration.Messaging;

namespace VanArsdel.Integration.LabStubs
{
    public static class Program
    {
        private const string Usage = @"b2blab <command> [options]

  e2e [--work <folder>]                 two business days through the lab, with checks (exit code 1 on a failed check)
  pipeline <pipeline> <file> [--as2-from <id>] [--file-name <name>] [--out <folder>]
                                        runs a receive pipeline (biztalk/VanArsdel.Integration.Pipelines/*.btp) on a file;
                                        --out writes message-N.xml and ack-N.xml instead of printing them
  map <map> <file>                      runs a map (Xslt + extension objects) on an XML file
  serve [--port <port>] [--work <folder>]
                                        hosts the ERP order service and the AS2 endpoints until Ctrl+C

The work folder defaults to a new folder under the system temp directory.";

        public static async Task<int> Main(string[] args)
        {
            if (args.Length == 0 || args[0] == "-h" || args[0] == "--help")
            {
                Console.WriteLine(Usage);
                return args.Length == 0 ? 2 : 0;
            }

            try
            {
                switch (args[0])
                {
                    case "e2e":
                        return await new EndToEndRunner().RunAsync(Work(args));
                    case "pipeline":
                        return Pipeline(args);
                    case "map":
                        return MapFile(args);
                    case "serve":
                        return await Serve(args);
                    default:
                        Console.Error.WriteLine("Unknown command " + args[0]);
                        Console.Error.WriteLine(Usage);
                        return 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static int Pipeline(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            var lab = new LabContext(Work(args), SeedData.Load(ScenarioPaths.Seed), DateTime.Now);
            var context = new MessageContext();
            var as2From = Option(args, "--as2-from");
            if (as2From != null)
            {
                context.Promote(ContextProperties.As2From, ContextProperties.As2Namespace, as2From);
            }

            context.Promote(ContextProperties.ReceivedFileName, ContextProperties.FileNamespace, Option(args, "--file-name") ?? Path.GetFileName(args[2]));
            var result = new PipelineRunner(lab).Receive(args[1], File.ReadAllBytes(Input(args[2])), context);
            foreach (var step in result.Steps)
            {
                Console.Error.WriteLine("# " + step);
            }

            var outFolder = Option(args, "--out");
            var n = 0;
            foreach (var m in result.Messages)
            {
                Console.Error.WriteLine("# message " + m.Context.MessageType() + " partner " + m.Context.ReadVanArsdel(ContextProperties.PartnerId));
                Write(outFolder, "message-" + ++n + ".xml", Pretty(m.Body));
            }

            n = 0;
            foreach (var ack in result.Acks)
            {
                Console.Error.WriteLine("# acknowledgement " + ack.DocumentElement.LocalName);
                Write(outFolder, "ack-" + ++n + ".xml", Pretty(Encoding.UTF8.GetBytes(ack.OuterXml)));
            }

            return 0;
        }

        private static int MapFile(string[] args)
        {
            if (args.Length < 3)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            var lab = new LabContext(Work(args), SeedData.Load(ScenarioPaths.Seed), DateTime.Now);
            var input = new XmlDocument();
            input.Load(Input(args[2]));
            Console.WriteLine(Pretty(Encoding.UTF8.GetBytes(lab.Map(args[1]).Execute(input).OuterXml)));
            return 0;
        }

        private static async Task<int> Serve(string[] args)
        {
            var port = int.Parse(Option(args, "--port") ?? "8090", CultureInfo.InvariantCulture);
            var lab = new LabContext(Work(args), SeedData.Load(ScenarioPaths.Seed), DateTime.Now);
            await using var host = new LabHost(lab, port);
            await host.StartAsync();
            Console.WriteLine("ERP order service: " + host.BaseUrl + LabHost.ErpServicePath);
            Console.WriteLine("AS2 receive:       " + host.BaseUrl + "/b2b/as2");
            Console.WriteLine("partner AS2:       " + host.BaseUrl + "/partners/{as2id}/as2");
            Console.WriteLine("work folder:       " + lab.Work);
            var stop = new TaskCompletionSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                stop.TrySetResult();
            };

            await stop.Task;
            return 0;
        }

        private static string Work(string[] args)
        {
            var work = Option(args, "--work") ?? Path.Combine(Path.GetTempPath(), "b2blab", DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            return Path.GetFullPath(work);
        }

        private static void Write(string folder, string name, string xml)
        {
            if (folder == null)
            {
                Console.WriteLine(xml);
                return;
            }

            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, name), xml);
            Console.Error.WriteLine("#   -> " + Path.Combine(folder, name));
        }

        /// <summary>dotnet run starts in the project folder, so relative sample paths are also tried from the scenario root.</summary>
        private static string Input(string path)
        {
            return File.Exists(path) || Path.IsPathRooted(path) ? path : Path.Combine(ScenarioPaths.Root, path);
        }

        private static string Option(string[] args, string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        private static string Pretty(byte[] xml)
        {
            var doc = new XmlDocument();
            doc.Load(new MemoryStream(xml));
            var sb = new StringBuilder();
            using (var writer = XmlWriter.Create(sb, new XmlWriterSettings { Indent = true, OmitXmlDeclaration = true }))
            {
                doc.Save(writer);
            }

            return sb.ToString();
        }
    }
}
