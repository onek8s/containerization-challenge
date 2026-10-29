using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Litware.OrderHub.Core.Channels;
using Litware.OrderHub.Core.Warehouse;

namespace Litware.OrderHub.LabTools
{
    /// <summary>
    /// Test tools for environments without the real webshop and warehouse:
    ///   send-webshop-order &lt;json file&gt; &lt;intake url&gt; &lt;secret&gt;
    ///   webshop-stub &lt;prefix, e.g. http://+:8090/&gt; [products json]
    ///   warehouse-sim &lt;local SFTP root of the warehouse account&gt; [carrier]
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.Error.WriteLine("Commands: send-webshop-order <file> <url> <secret> | webshop-stub <prefix> [products.json] | warehouse-sim <root> [carrier]");
                return 2;
            }

            switch (args[0])
            {
                case "send-webshop-order": return SendWebshopOrder(args[1], args[2], args[3]);
                case "webshop-stub": return WebshopStub(args[1], args.Length > 2 ? args[2] : null);
                case "warehouse-sim": return WarehouseSimulator(args[1], args.Length > 2 ? args[2] : "POSTI");
                default:
                    Console.Error.WriteLine("Unknown command " + args[0]);
                    return 2;
            }
        }

        private static int SendWebshopOrder(string file, string url, string secret)
        {
            byte[] body = File.ReadAllBytes(file);
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(body) };
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                request.Headers.Add(WebhookSignature.HeaderName, WebhookSignature.Compute(body, secret));
                request.Headers.Add("X-WC-Webhook-Topic", "order.updated");
                var response = client.SendAsync(request).GetAwaiter().GetResult();
                Console.WriteLine((int)response.StatusCode + " " + response.ReasonPhrase);
                return response.IsSuccessStatusCode ? 0 : 1;
            }
        }

        /// <summary>Answers the webshop REST calls of the shop sync service and prints them.</summary>
        private static int WebshopStub(string prefix, string productsFile)
        {
            string products = productsFile != null ? File.ReadAllText(productsFile) : "[]";
            var listener = new HttpListener();
            listener.Prefixes.Add(prefix);
            listener.Start();
            Console.WriteLine("Webshop stub on " + prefix + ", Ctrl+C to stop");

            while (true)
            {
                var ctx = listener.GetContext();
                string body = new StreamReader(ctx.Request.InputStream).ReadToEnd();
                Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " " + ctx.Request.HttpMethod + " " + ctx.Request.Url.PathAndQuery);
                if (body.Length > 0) Console.WriteLine("  " + body);

                string response = ctx.Request.HttpMethod == "GET" && ctx.Request.Url.AbsolutePath.EndsWith("/products") ? products : "{}";
                byte[] bytes = Encoding.UTF8.GetBytes(response);
                ctx.Response.ContentType = "application/json";
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.Close();
            }
        }

        /// <summary>
        /// Plays the 3PL warehouse on a local SFTP root: picks order files from "in", writes a shipment
        /// confirmation to "out" and a stock snapshot to "out\stock" every few minutes.
        /// </summary>
        private static int WarehouseSimulator(string root, string carrier)
        {
            string inDir = Path.Combine(root, "in");
            string outDir = Path.Combine(root, "out");
            string stockDir = Path.Combine(outDir, "stock");
            string doneDir = Path.Combine(inDir, "picked");
            foreach (var d in new[] { inDir, outDir, stockDir, doneDir }) Directory.CreateDirectory(d);
            var stock = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var random = new Random();
            DateTime nextStock = DateTime.MinValue;

            Console.WriteLine("Warehouse simulator on " + root + ", Ctrl+C to stop");
            while (true)
            {
                foreach (var file in Directory.GetFiles(inDir, "ORD_*.xml"))
                {
                    var doc = XDocument.Load(file);
                    var ns = WarehouseOrderFile.Ns;
                    string orderNo = doc.Root.Element(ns + "OrderNo").Value;
                    string tracking = "JJFI" + random.Next(10000000, 99999999).ToString(CultureInfo.InvariantCulture) + "0000000000";
                    var sb = new StringBuilder("OrderNo,LineNo,Sku,QtyShipped,Carrier,TrackingNo,ShipDate\r\n");
                    foreach (var line in doc.Root.Element(ns + "Lines").Elements(ns + "Line"))
                    {
                        string sku = line.Element(ns + "Sku").Value;
                        int qty = int.Parse(line.Element(ns + "Qty").Value, CultureInfo.InvariantCulture);
                        stock[sku] = (stock.TryGetValue(sku, out var s) ? s : 50) - qty;
                        sb.Append(orderNo).Append(',').Append(line.Attribute("no").Value).Append(',').Append(sku).Append(',')
                          .Append(qty).Append(',').Append(carrier).Append(',').Append(tracking).Append(',')
                          .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture)).Append("\r\n");
                    }
                    string confName = "SHIPCONF_" + DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + orderNo + ".csv";
                    File.WriteAllText(Path.Combine(outDir, confName), sb.ToString());
                    File.Move(file, Path.Combine(doneDir, Path.GetFileName(file)));
                    Console.WriteLine("Shipped " + orderNo + " -> " + confName);
                }

                if (DateTime.Now >= nextStock && stock.Count > 0)
                {
                    var sb = new StringBuilder("Sku,OnHand,Allocated,Blocked\r\n");
                    foreach (var s in stock.OrderBy(s => s.Key)) sb.Append(s.Key).Append(',').Append(Math.Max(0, s.Value)).Append(",0,0\r\n");
                    string name = "STOCK_" + DateTime.Now.ToString("yyyyMMddHHmm") + ".csv";
                    File.WriteAllText(Path.Combine(stockDir, name), sb.ToString());
                    Console.WriteLine("Stock snapshot " + name);
                    nextStock = DateTime.Now.AddMinutes(5);
                }

                Thread.Sleep(5000);
            }
        }
    }
}
