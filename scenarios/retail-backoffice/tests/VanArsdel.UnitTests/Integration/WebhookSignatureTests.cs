using FluentAssertions;
using VanArsdel.Integration.Payments;
using Xunit;

namespace VanArsdel.UnitTests.Integration;

public class WebhookSignatureTests
{
    private const string Secret = "psp-dev-secret";
    private const string Body = "{\"id\":\"evt_1\",\"type\":\"payment.authorized\"}";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_790_000_000);

    [Fact]
    public void Signature_created_by_the_sender_is_accepted()
    {
        var header = WebhookSignature.Create(Secret, Now.ToUnixTimeSeconds(), Body);

        WebhookSignature.Verify(Secret, header, Body, Now, TimeSpan.FromMinutes(5)).Should().BeTrue();
    }

    [Fact]
    public void Changed_body_is_rejected()
    {
        var header = WebhookSignature.Create(Secret, Now.ToUnixTimeSeconds(), Body);

        WebhookSignature.Verify(Secret, header, Body.Replace("authorized", "captured"), Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void Old_signature_is_rejected()
    {
        var header = WebhookSignature.Create(Secret, Now.AddMinutes(-10).ToUnixTimeSeconds(), Body);

        WebhookSignature.Verify(Secret, header, Body, Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("v1=abc")]
    [InlineData("t=abc,v1=abc")]
    public void Malformed_header_is_rejected(string header)
    {
        WebhookSignature.Verify(Secret, header, Body, Now, TimeSpan.FromMinutes(5)).Should().BeFalse();
    }

    [Fact]
    public void Upper_case_hex_is_accepted()
    {
        var header = WebhookSignature.Create(Secret, Now.ToUnixTimeSeconds(), Body);
        var upper = header.Substring(0, header.IndexOf("v1=", StringComparison.Ordinal) + 3) + header.Substring(header.IndexOf("v1=", StringComparison.Ordinal) + 3).ToUpperInvariant();

        WebhookSignature.Verify(Secret, upper, Body, Now, TimeSpan.FromMinutes(5)).Should().BeTrue();
    }
}
