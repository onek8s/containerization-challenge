namespace VanArsdel.BackOffice.Services;

/// <summary>Tokens captured in _Host.cshtml: HttpContext is not available inside a Blazor Server circuit.</summary>
public class InitialApplicationState
{
    public string AccessToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public class TokenProvider
{
    public string AccessToken { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsExpired => string.IsNullOrEmpty(AccessToken) || ExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1);
}
