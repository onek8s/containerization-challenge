using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Litware.OrderHub.Tests.Fakes
{
    /// <summary>Minimal stand-in for the webshop REST API: records requests, answers product lookups.</summary>
    internal sealed class FakeWebshop : IDisposable
    {
        private readonly HttpListener _listener = new HttpListener();

        public FakeWebshop()
        {
            int port = FreePort();
            BaseAddress = new Uri("http://localhost:" + port + "/wp-json/wc/v3/");
            _listener.Prefixes.Add("http://localhost:" + port + "/");
            _listener.Start();
            Task.Run(LoopAsync);
        }

        public Uri BaseAddress { get; }
        public ConcurrentQueue<(string Method, string Path, string Body)> Requests { get; } = new ConcurrentQueue<(string, string, string)>();

        /// <summary>JSON returned for GET products?sku=...</summary>
        public string ProductsJson { get; set; } = "[]";

        private async Task LoopAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = await _listener.GetContextAsync(); }
                catch { return; }

                string body = new StreamReader(ctx.Request.InputStream).ReadToEnd();
                Requests.Enqueue((ctx.Request.HttpMethod, ctx.Request.Url.PathAndQuery, body));

                string response = ctx.Request.HttpMethod == "GET" && ctx.Request.Url.AbsolutePath.EndsWith("/products") ? ProductsJson : "{}";
                byte[] bytes = Encoding.UTF8.GetBytes(response);
                ctx.Response.ContentType = "application/json";
                ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
                ctx.Response.Close();
            }
        }

        public static int FreePort()
        {
            var l = new TcpListener(IPAddress.Loopback, 0);
            l.Start();
            int port = ((IPEndPoint)l.LocalEndpoint).Port;
            l.Stop();
            return port;
        }

        public void Dispose()
        {
            _listener.Stop();
            _listener.Close();
        }
    }

    /// <summary>Accepts SMTP sessions and keeps the DATA part of each message.</summary>
    internal sealed class FakeSmtpServer : IDisposable
    {
        private readonly TcpListener _listener;

        public FakeSmtpServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Task.Run(LoopAsync);
        }

        public int Port { get; }
        public ConcurrentQueue<string> Messages { get; } = new ConcurrentQueue<string>();

        private async Task LoopAsync()
        {
            while (true)
            {
                TcpClient client;
                try { client = await _listener.AcceptTcpClientAsync(); }
                catch { return; }
                _ = Task.Run(() => Handle(client));
            }
        }

        private void Handle(TcpClient client)
        {
            using (client)
            using (var stream = client.GetStream())
            using (var reader = new StreamReader(stream, Encoding.ASCII))
            using (var writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\r\n", AutoFlush = true })
            {
                writer.WriteLine("220 fake ESMTP");
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string cmd = line.Length >= 4 ? line.Substring(0, 4).ToUpperInvariant() : line.ToUpperInvariant();
                    if (cmd == "EHLO") { writer.WriteLine("250-fake"); writer.WriteLine("250 8BITMIME"); }
                    else if (cmd == "HELO" || cmd == "MAIL" || cmd == "RCPT" || cmd == "RSET" || cmd == "NOOP") writer.WriteLine("250 OK");
                    else if (cmd == "DATA")
                    {
                        writer.WriteLine("354 go ahead");
                        var sb = new StringBuilder();
                        while ((line = reader.ReadLine()) != null && line != ".") sb.AppendLine(line);
                        Messages.Enqueue(sb.ToString());
                        writer.WriteLine("250 queued");
                    }
                    else if (cmd == "QUIT") { writer.WriteLine("221 bye"); return; }
                    else writer.WriteLine("502 not implemented");
                }
            }
        }

        public void Dispose()
        {
            _listener.Stop();
        }
    }
}
