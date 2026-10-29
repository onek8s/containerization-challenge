using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Lucerne.ProjectSites.Contracts.Schema;
using Lucerne.ProjectSites.LabStubs.InMemory;
using Lucerne.ProjectSites.SharePoint;
using Lucerne.ProjectSites.SharePoint.Auth;
using Xunit;

namespace Lucerne.ProjectSites.Tests
{
    public class CamlTests
    {
        [Fact]
        public void And_with_three_operands_nests_to_the_right()
        {
            var xml = Caml.And("<A/>", "<B/>", "<C/>");
            Assert.Equal("<And><A/><And><B/><C/></And></And>", xml);
        }

        [Fact]
        public void Empty_operands_are_ignored()
        {
            Assert.Equal("<A/>", Caml.Or(null, "<A/>", string.Empty));
            Assert.Equal(string.Empty, Caml.Where(Caml.And()));
        }

        [Fact]
        public void Values_are_escaped()
        {
            var xml = Caml.Eq("Title", "Text", "R&D <test>");
            Assert.Contains("R&amp;D &lt;test&gt;", xml);
        }

        [Fact]
        public void Dates_use_iso_format_with_time()
        {
            var xml = Caml.Geq("Modified", "DateTime", new DateTime(2019, 3, 4, 5, 6, 7, DateTimeKind.Utc));
            Assert.Contains("2019-03-04T05:06:07Z", xml);
            Assert.Contains("IncludeTimeValue='TRUE'", xml);
        }

        [Fact]
        public void View_xml_contains_scope_fields_and_row_limit()
        {
            var q = new CamlQuery { Query = Caml.Where(Caml.IsNotNull("Title")), ViewFields = new[] { "ID", "Title" }, RowLimit = 50, Recursive = true };
            var xml = q.ToViewXml();
            Assert.StartsWith("<View Scope='RecursiveAll'>", xml);
            Assert.Contains("<FieldRef Name='Title' />", xml);
            Assert.Contains("<RowLimit Paged='TRUE'>50</RowLimit>", xml);
        }
    }

    public class InMemoryCamlTests
    {
        private static InMemoryList CreateList()
        {
            var farm = new InMemoryFarm();
            var site = farm.CreateSite("http://projects.corp.local/sites/t", "T", Enumerable.Empty<Lucerne.ProjectSites.Core.ListDefinition>());
            var list = site.CreateList("Risks", false);
            list.Add(new FieldValues { { "Title", "Crane" }, { "LucSeverity", "High" }, { "LucScore", 12 } });
            list.Add(new FieldValues { { "Title", "Ground water" }, { "LucSeverity", "Critical" }, { "LucScore", 20 } });
            list.Add(new FieldValues { { "Title", "Permit" }, { "LucSeverity", "Low" }, { "LucScore", 3 } });
            return list;
        }

        [Fact]
        public void Evaluator_filters_and_orders()
        {
            var list = CreateList();
            var items = list.Query(new CamlQuery
            {
                Query = Caml.Where(Caml.Or(Caml.Eq("LucSeverity", "Choice", "High"), Caml.Geq("LucScore", "Number", 15))) + Caml.OrderBy("LucScore", false)
            }).ToList();

            Assert.Equal(new[] { "Ground water", "Crane" }, items.Select(i => i.GetString("Title")));
        }

        [Fact]
        public void Evaluator_supports_in_and_begins_with()
        {
            var list = CreateList();
            Assert.Equal(2, list.Query(new CamlQuery { Query = Caml.Where(Caml.In("LucSeverity", "Choice", "Low", "High")) }).Count());
            Assert.Single(list.Query(new CamlQuery { Query = Caml.Where(Caml.BeginsWith("Title", "Gro")) }));
        }

        [Fact]
        public void Max_items_limits_results()
        {
            var list = CreateList();
            Assert.Equal(2, list.Query(new CamlQuery { MaxItems = 2 }).Count());
        }
    }

    public class TokenTests
    {
        private static X509Certificate2 CreateCertificate()
        {
            using (var rsa = RSA.Create(2048))
            {
                var request = new CertificateRequest("CN=Lucerne Project Sites Lab", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
                return new X509Certificate2(cert.Export(X509ContentType.Pfx, "x"), "x", X509KeyStorageFlags.Exportable);
            }
        }

        private const string Realm = "5a3f2c11-7e0b-4f1d-9c55-0d2e6b8a4f90";
        private const string IssuerId = "8f1e9d34-2c6a-4b7e-a0d3-51c9e2f7b6a8";
        private const string ClientId = "c4d7e2a9-1b3f-4e6d-8a5c-9f0b2d4e6a71";

        [Fact]
        public void App_only_token_is_signed_with_sharepoint_audience()
        {
            var cert = CreateCertificate();
            var issuer = new HighTrustTokenIssuer(cert, IssuerId, ClientId, Realm);
            var raw = issuer.CreateAppOnlyAccessToken(new Uri("http://projects.corp.local/sites/hub"));

            var token = new JwtSecurityTokenHandler().ReadJwtToken(raw);
            Assert.Equal("RS256", token.Header.Alg);
            Assert.Equal(IssuerId + "@" + Realm, token.Issuer);
            Assert.Equal("00000003-0000-0ff1-ce00-000000000000/projects.corp.local@" + Realm, token.Audiences.Single());
            Assert.Equal(ClientId + "@" + Realm, token.Claims.First(c => c.Type == "nameid").Value);
            Assert.DoesNotContain(token.Claims, c => c.Type == "trustedfordelegation");

            var parameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidIssuer = IssuerId + "@" + Realm,
                ValidAudience = issuer.Audience(new Uri("http://projects.corp.local/")),
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.X509SecurityKey(cert)
            };
            new JwtSecurityTokenHandler().ValidateToken(raw, parameters, out _);
        }

        [Fact]
        public void User_token_wraps_actor_token_and_sid()
        {
            var issuer = new HighTrustTokenIssuer(CreateCertificate(), IssuerId, ClientId, Realm);
            var raw = issuer.CreateUserAccessToken(new Uri("https://projects.corp.local/sites/p19041"), "S-1-5-21-1004336348-1177238915-682003330-1105");

            var outer = new JwtSecurityTokenHandler().ReadJwtToken(raw);
            Assert.Equal("none", outer.Header.Alg);
            Assert.Equal("s-1-5-21-1004336348-1177238915-682003330-1105", outer.Payload["nameid"]);
            Assert.Equal("urn:office:idp:activedirectory", outer.Payload["nii"]);

            var actor = new JwtSecurityTokenHandler().ReadJwtToken((string)outer.Payload["actortoken"]);
            Assert.Contains(actor.Claims, c => c.Type == "trustedfordelegation" && c.Value == "true");
        }

        [Fact]
        public void Certificate_without_private_key_is_rejected()
        {
            var publicOnly = new X509Certificate2(CreateCertificate().Export(X509ContentType.Cert));
            Assert.Throws<ArgumentException>(() => new HighTrustTokenIssuer(publicOnly, IssuerId, ClientId, Realm));
        }

        [Fact]
        public void Realm_is_parsed_from_bearer_challenge()
        {
            var header = "Bearer realm=\"" + Realm + "\",client_id=\"00000003-0000-0ff1-ce00-000000000000\",trusted_issuers=\"00000005-0000-0000-c000-000000000000@*\"";
            Assert.Equal(Realm, RealmDiscovery.ParseRealm(header));
            Assert.Null(RealmDiscovery.ParseRealm("NTLM"));
        }
    }
}
