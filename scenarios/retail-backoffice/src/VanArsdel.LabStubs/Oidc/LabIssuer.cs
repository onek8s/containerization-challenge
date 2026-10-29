using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using VanArsdel.Contracts;

namespace VanArsdel.LabStubs.Oidc;

/// <summary>
/// Minimal OpenID Connect provider standing in for Entra ID: discovery, JWKS, authorization code + PKCE,
/// client credentials and password grants. Tokens look like Entra v2 tokens (roles, scp, store_code).
/// </summary>
public class LabIssuer
{
    public const string Audience = "api://vanarsdel-retail";
    private const string KeyId = "lab-1";

    private readonly RSA _rsa;
    private readonly SigningCredentials _credentials;
    private readonly ConcurrentDictionary<string, PendingCode> _codes = new();
    private readonly ILogger<LabIssuer> _logger;

    public LabIssuer(IConfiguration configuration, ILogger<LabIssuer> logger)
    {
        _logger = logger;
        Issuer = configuration["Lab:Issuer"] ?? "http://localhost:5100";
        _rsa = LoadOrCreateKey(configuration["Lab:DataFolder"] ?? "../../.labdata");
        _credentials = new SigningCredentials(new RsaSecurityKey(_rsa) { KeyId = KeyId }, SecurityAlgorithms.RsaSha256);
    }

    public string Issuer { get; }

    private record PendingCode(string ClientId, string RedirectUri, LabUser User, string Nonce, string Challenge, string Scope, DateTime Expires);

    public void Map(WebApplication app)
    {
        app.MapGet("/.well-known/openid-configuration", () => Results.Json(new Dictionary<string, object>
        {
            ["issuer"] = Issuer,
            ["authorization_endpoint"] = Issuer + "/connect/authorize",
            ["token_endpoint"] = Issuer + "/connect/token",
            ["jwks_uri"] = Issuer + "/.well-known/jwks",
            ["end_session_endpoint"] = Issuer + "/connect/endsession",
            ["response_types_supported"] = new[] { "code" },
            ["subject_types_supported"] = new[] { "public" },
            ["id_token_signing_alg_values_supported"] = new[] { "RS256" },
            ["scopes_supported"] = new[] { "openid", "profile", "email", "offline_access", "retail.api" },
            ["grant_types_supported"] = new[] { "authorization_code", "client_credentials", "password" },
            ["code_challenge_methods_supported"] = new[] { "S256" },
            ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_post", "client_secret_basic", "none" },
        }));

        app.MapGet("/.well-known/jwks", () =>
        {
            var p = _rsa.ExportParameters(false);
            return Results.Json(new
            {
                keys = new[]
                {
                    new { kty = "RSA", use = "sig", kid = KeyId, alg = "RS256", n = Base64UrlEncoder.Encode(p.Modulus), e = Base64UrlEncoder.Encode(p.Exponent) },
                },
            });
        });

        app.MapGet("/connect/authorize", (HttpRequest request) => Results.Content(LoginPage(request.QueryString.Value), "text/html"));
        app.MapGet("/connect/authorize/approve", Approve);
        app.MapPost("/connect/token", Token);
        app.MapGet("/connect/endsession", (string post_logout_redirect_uri) =>
            string.IsNullOrEmpty(post_logout_redirect_uri) ? Results.Content("Signed out.") : Results.Redirect(post_logout_redirect_uri));
    }

    private static string LoginPage(string query)
    {
        var sb = new StringBuilder("<html><head><title>Lab sign-in</title><style>body{font-family:sans-serif;margin:40px}a{display:block;margin:6px 0}</style></head><body><h2>Lab sign-in</h2><p>Pick a user:</p>");
        foreach (var u in LabUsers.All)
        {
            sb.Append($"<a href=\"/connect/authorize/approve{WebUtility.HtmlEncode(query)}&amp;user={u.UserName}\">{WebUtility.HtmlEncode(u.Name)} ({string.Join(", ", u.Roles)}{(u.StoreCode != null ? ", " + u.StoreCode : "")})</a>");
        }
        return sb.Append("</body></html>").ToString();
    }

    private IResult Approve(string client_id, string redirect_uri, string state, string nonce, string code_challenge, string scope, string user, string response_mode)
    {
        var u = LabUsers.Find(user);
        if (u == null || string.IsNullOrEmpty(redirect_uri)) return Results.BadRequest("Unknown user or redirect_uri");
        var code = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        _codes[code] = new PendingCode(client_id, redirect_uri, u, nonce, code_challenge, scope, DateTime.UtcNow.AddMinutes(5));

        if (response_mode == "form_post")
        {
            var html = $"<html><body onload=\"document.forms[0].submit()\"><form method=\"post\" action=\"{WebUtility.HtmlEncode(redirect_uri)}\">" +
                       $"<input type=\"hidden\" name=\"code\" value=\"{code}\"/><input type=\"hidden\" name=\"state\" value=\"{WebUtility.HtmlEncode(state)}\"/></form></body></html>";
            return Results.Content(html, "text/html");
        }
        var separator = redirect_uri.Contains('?') ? "&" : "?";
        return Results.Redirect($"{redirect_uri}{separator}code={code}&state={Uri.EscapeDataString(state ?? "")}");
    }

    private async Task<IResult> Token(HttpRequest request)
    {
        var form = await request.ReadFormAsync();
        var (clientId, secret) = ClientCredentials(request, form);
        var grant = form["grant_type"].ToString();

        switch (grant)
        {
            case "client_credentials":
                if (secret != LabUsers.ClientSecret) return Error("invalid_client");
                return Tokens(Access(clientId, clientId, clientId, new[] { Roles.Service }, null, null), null);

            case "password":
                if (secret != LabUsers.ClientSecret && !LabUsers.IsPublicClient(clientId)) return Error("invalid_client");
                var user = LabUsers.Find(form["username"]);
                if (user == null || form["password"] != LabUsers.Password) return Error("invalid_grant");
                return Tokens(Access(user.UserName, user.Name, user.Email, user.Roles, user.StoreCode, clientId), IdToken(user, clientId, null));

            case "authorization_code":
                if (!_codes.TryRemove(form["code"], out var pending) || pending.Expires < DateTime.UtcNow) return Error("invalid_grant");
                if (pending.ClientId != clientId || pending.RedirectUri != form["redirect_uri"]) return Error("invalid_grant");
                if (!string.IsNullOrEmpty(pending.Challenge))
                {
                    var verifier = form["code_verifier"].ToString();
                    var hash = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
                    if (hash != pending.Challenge) return Error("invalid_grant", "PKCE verification failed");
                }
                else if (secret != LabUsers.ClientSecret)
                {
                    return Error("invalid_client");
                }
                var u = pending.User;
                return Tokens(Access(u.UserName, u.Name, u.Email, u.Roles, u.StoreCode, clientId), IdToken(u, clientId, pending.Nonce));

            default:
                return Error("unsupported_grant_type");
        }
    }

    public string Access(string subject, string name, string email, IEnumerable<string> roles, string storeCode, string clientId)
    {
        var claims = new List<Claim>
        {
            new("sub", subject), new("name", name), new("preferred_username", email ?? subject), new("scp", "retail.api"),
            new("azp", clientId ?? subject),
        };
        claims.AddRange(roles.Select(r => new Claim("roles", r)));
        if (storeCode != null) claims.Add(new Claim("store_code", storeCode));
        return Write(claims, Audience, TimeSpan.FromHours(1));
    }

    private string IdToken(LabUser user, string clientId, string nonce)
    {
        var claims = new List<Claim> { new("sub", user.UserName), new("name", user.Name), new("email", user.Email), new("preferred_username", user.Email) };
        claims.AddRange(user.Roles.Select(r => new Claim("roles", r)));
        if (user.StoreCode != null) claims.Add(new Claim("store_code", user.StoreCode));
        if (!string.IsNullOrEmpty(nonce)) claims.Add(new Claim("nonce", nonce));
        return Write(claims, clientId, TimeSpan.FromHours(1));
    }

    private string Write(IEnumerable<Claim> claims, string audience, TimeSpan lifetime)
    {
        var now = DateTime.UtcNow;
        // oidc-client (store manager app) rejects id tokens without "iat".
        claims = claims.Append(new Claim("iat", EpochTime.GetIntDate(now).ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        var token = new JwtSecurityToken(Issuer, audience, claims, now, now.Add(lifetime), _credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static IResult Tokens(string accessToken, string idToken)
    {
        var body = new Dictionary<string, object> { ["access_token"] = accessToken, ["token_type"] = "Bearer", ["expires_in"] = 3600 };
        if (idToken != null) body["id_token"] = idToken;
        return Results.Json(body);
    }

    private static IResult Error(string error, string description = null) =>
        Results.Json(new { error, error_description = description ?? error }, statusCode: 400);

    private static (string ClientId, string Secret) ClientCredentials(HttpRequest request, IFormCollection form)
    {
        var header = request.Headers.Authorization.ToString();
        if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header.Substring(6))).Split(':', 2);
            return (Uri.UnescapeDataString(decoded[0]), decoded.Length > 1 ? Uri.UnescapeDataString(decoded[1]) : null);
        }
        return (form["client_id"], form["client_secret"]);
    }

    private RSA LoadOrCreateKey(string folder)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "lab-signing-key.pem");
        var rsa = RSA.Create(2048);
        if (File.Exists(path))
        {
            rsa.ImportFromPem(File.ReadAllText(path));
        }
        else
        {
            File.WriteAllText(path, new string(PemEncoding.Write("RSA PRIVATE KEY", rsa.ExportRSAPrivateKey())));
            _logger.LogInformation("Created lab signing key {Path}", Path.GetFullPath(path));
        }
        return rsa;
    }
}
