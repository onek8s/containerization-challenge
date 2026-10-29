using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fabrikam.Housing.Integration;
using Fabrikam.Housing.Integration.Geo;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Fabrikam.Housing.Tests.Integration
{
    /// <summary>Live clients against a local HTTP listener standing in for the remote service.</summary>
    public class LiveClientTests
    {
        private sealed class FakeServer : IDisposable
        {
            private readonly HttpListener _listener = new HttpListener();
            private readonly Func<HttpListenerRequest, (int Status, string Body)> _handler;
            public int Calls;
            public string LastUrl;

            public FakeServer(Func<HttpListenerRequest, (int, string)> handler)
            {
                _handler = handler;
                var port = FreePort();
                BaseUrl = $"http://localhost:{port}/";
                _listener.Prefixes.Add(BaseUrl);
                _listener.Start();
                Task.Run(Loop);
            }

            public string BaseUrl { get; }

            private async Task Loop()
            {
                while (_listener.IsListening)
                {
                    HttpListenerContext ctx;
                    try { ctx = await _listener.GetContextAsync(); } catch { return; }
                    Interlocked.Increment(ref Calls);
                    LastUrl = ctx.Request.Url.ToString();
                    var (status, body) = _handler(ctx.Request);
                    var bytes = Encoding.UTF8.GetBytes(body);
                    ctx.Response.StatusCode = status;
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
                    ctx.Response.Close();
                }
            }

            private static int FreePort()
            {
                var l = new TcpListener(IPAddress.Loopback, 0);
                l.Start();
                var port = ((IPEndPoint)l.LocalEndpoint).Port;
                l.Stop();
                return port;
            }

            public void Dispose() => _listener.Stop();
        }

        private static IOptions<IntegrationOptions> Options(Action<IntegrationOptions> configure)
        {
            var o = new IntegrationOptions();
            configure(o);
            return Microsoft.Extensions.Options.Options.Create(o);
        }

        [Fact]
        public async Task Postcode_lookup_is_cached_and_unknown_postcodes_return_null()
        {
            using (var server = new FakeServer(r => r.Url.AbsolutePath.EndsWith("ZZ11ZZ")
                ? (404, "{\"status\":404,\"error\":\"Postcode not found\"}")
                : (200, "{\"status\":200,\"result\":{\"postcode\":\"BS5 6XX\",\"latitude\":51.4652,\"longitude\":-2.5634,\"admin_district\":\"Bristol, City of\",\"admin_ward\":\"Easton\"}}")))
            {
                var options = Options(o => o.Postcodes.BaseUrl = server.BaseUrl);
                var lookup = new PostcodesIoLookup(new RestClientFactory(options), new MemoryCache(new MemoryCacheOptions()), options, NullLogger<PostcodesIoLookup>.Instance);

                var first = await lookup.LookupAsync("bs5 6xx");
                var second = await lookup.LookupAsync("BS5 6XX");
                first.Ward.Should().Be("Easton");
                second.Latitude.Should().Be(51.4652);
                server.Calls.Should().Be(1);
                server.LastUrl.Should().EndWith("/postcodes/BS56XX");

                (await lookup.LookupAsync("ZZ1 1ZZ")).Should().BeNull();
                (await lookup.LookupAsync("rubbish")).Should().BeNull();
                server.Calls.Should().Be(2);
            }
        }

        [Fact]
        public async Task Patch_locator_queries_the_gis_layer()
        {
            using (var server = new FakeServer(r => (200, "{\"features\":[{\"attributes\":{\"PATCH_CODE\":\"EAST\"}}]}")))
            {
                var options = Options(o => o.Gis.PatchLayerUrl = server.BaseUrl + "arcgis/rest/services/Housing/Patches/MapServer/0/");
                var locator = new ArcGisPatchLocator(new RestClientFactory(options), options, NullLogger<ArcGisPatchLocator>.Instance);
                (await locator.FindPatchAsync(51.4652, -2.5634)).Should().Be("EAST");
                server.LastUrl.Should().Contain("/MapServer/0/query?").And.ContainEquivalentOf("geometry=-2.5634%2C51.4652").And.Contain("f=json");
            }
        }

        [Fact]
        public async Task Server_errors_are_retried_then_reported()
        {
            using (var server = new FakeServer(r => (503, "{}")))
            {
                var options = Options(o => o.Gis.PatchLayerUrl = server.BaseUrl);
                var locator = new ArcGisPatchLocator(new RestClientFactory(options), options, NullLogger<ArcGisPatchLocator>.Instance);
                Func<Task> call = () => locator.FindPatchAsync(51.4, -2.5);
                (await call.Should().ThrowAsync<IntegrationException>()).Which.StatusCode.Should().Be(503);
                server.Calls.Should().Be(4);
            }
        }
    }
}
