using CustomerSupport.Infrastructure.Channels;
using FluentAssertions;
using Xunit;

namespace CustomerSupport.UnitTests.Channels;

/// <summary>HMAC verification behind every inbound provider webhook (CS-301, and CS-302/304 next).</summary>
public class WebhookSignatureTests
{
    private const string Secret = "shared-secret";
    private const string Body = """{"messageId":"abc","from":"a@b.com"}""";

    [Fact]
    public void CorrectSignature_Verifies()
    {
        var signature = WebhookSignature.Compute(Body, Secret);
        WebhookSignature.Verify(Body, signature, Secret).Should().BeTrue();
    }

    [Fact]
    public void WrongSecret_Fails()
    {
        var signature = WebhookSignature.Compute(Body, "different-secret");
        WebhookSignature.Verify(Body, signature, Secret).Should().BeFalse();
    }

    [Fact]
    public void TamperedBody_Fails()
    {
        var signature = WebhookSignature.Compute(Body, Secret);
        WebhookSignature.Verify(Body + "x", signature, Secret).Should().BeFalse();
    }

    [Fact]
    public void MissingSignature_Fails()
    {
        WebhookSignature.Verify(Body, null, Secret).Should().BeFalse();
        WebhookSignature.Verify(Body, "", Secret).Should().BeFalse();
    }

    [Fact]
    public void ComputedSignature_HasTheExpectedPrefix()
    {
        WebhookSignature.Compute(Body, Secret).Should().StartWith("sha256=");
    }
}
