using CustomerSupport.Application.Channels.WebForms;
using CustomerSupport.Application.Common.Localization;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Channels;

public class WebFormValidatorTests
{
    private readonly IWebFormValidator _validator;

    public WebFormValidatorTests()
    {
        var localizer = Substitute.For<IMessageLocalizer>();
        localizer[Arg.Any<string>()].Returns(ci => (string)ci[0]);
        _validator = new WebFormValidator(localizer);
    }

    private static WebFormField Text(string key, bool required = false, int? maxLength = null, string? pattern = null) =>
        new(key, "text", key, key, null, null, required, maxLength, pattern, null, null);

    [Fact]
    public void RequiredFieldMissing_ReturnsError()
    {
        var errors = _validator.Validate([Text("name", required: true)], new Dictionary<string, string>());

        errors.Should().ContainKey("name");
    }

    [Fact]
    public void OptionalFieldMissing_NoError()
    {
        var errors = _validator.Validate([Text("nickname")], new Dictionary<string, string>());

        errors.Should().BeEmpty();
    }

    [Fact]
    public void ValueExceedsMaxLength_ReturnsError()
    {
        var errors = _validator.Validate(
            [Text("note", maxLength: 5)], new Dictionary<string, string> { ["note"] = "too long a value" });

        errors.Should().ContainKey("note");
    }

    [Fact]
    public void ValueDoesNotMatchPattern_ReturnsError()
    {
        var field = Text("orderNumber", pattern: @"^ORD-\d{4}$");
        var errors = _validator.Validate([field], new Dictionary<string, string> { ["orderNumber"] = "not-an-order" });

        errors.Should().ContainKey("orderNumber");
    }

    [Fact]
    public void ValueMatchesPattern_NoError()
    {
        var field = Text("orderNumber", pattern: @"^ORD-\d{4}$");
        var errors = _validator.Validate([field], new Dictionary<string, string> { ["orderNumber"] = "ORD-1234" });

        errors.Should().BeEmpty();
    }

    [Fact]
    public void SelectValueNotInOptions_ReturnsError()
    {
        var field = new WebFormField(
            "issueType", "select", "Issue type", "نوع المشكلة", null, null, true, null, null,
            [new WebFormFieldOption("billing", "Billing", "الفواتير")], "categoryCode");

        var errors = _validator.Validate([field], new Dictionary<string, string> { ["issueType"] = "not-a-real-option" });

        errors.Should().ContainKey("issueType");
    }

    [Fact]
    public void SelectValueInOptions_NoError()
    {
        var field = new WebFormField(
            "issueType", "select", "Issue type", "نوع المشكلة", null, null, true, null, null,
            [new WebFormFieldOption("billing", "Billing", "الفواتير")], "categoryCode");

        var errors = _validator.Validate([field], new Dictionary<string, string> { ["issueType"] = "billing" });

        errors.Should().BeEmpty();
    }

    [Fact]
    public void InvalidEmailFormat_ReturnsError()
    {
        var field = new WebFormField("email", "email", "Email", "البريد", null, null, true, null, null, null, "customerEmail");
        var errors = _validator.Validate([field], new Dictionary<string, string> { ["email"] = "not-an-email" });

        errors.Should().ContainKey("email");
    }

    [Fact]
    public void CatastrophicBacktrackingPattern_TimesOutRatherThanHanging()
    {
        // A classic ReDoS shape: (a+)+ against a long non-matching string with no early exit.
        var field = Text("value", pattern: "^(a+)+$");
        var input = new string('a', 40) + "!";

        var act = () => _validator.Validate([field], new Dictionary<string, string> { ["value"] = input });

        act.Should().NotThrow();
        act().Should().ContainKey("value");
    }
}
