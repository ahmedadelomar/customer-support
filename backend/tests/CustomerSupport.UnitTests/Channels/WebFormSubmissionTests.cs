using CustomerSupport.Application.Channels.WebForms;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Localization;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Organization;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using ValidationException = CustomerSupport.Application.Common.Exceptions.ValidationException;
using Xunit;

namespace CustomerSupport.UnitTests.Channels;

/// <summary>
/// Web form submission (CS-305) — store-then-create ordering, customer matching, portal-category
/// enforcement, rate limiting, captcha gating and the retry path. A real Sqlite connection, same
/// reasoning as the other channel test fixtures in this folder.
/// </summary>
public class WebFormSubmissionTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private IDateTimeProvider _clock = null!;
    private IWebFormTicketFactory _ticketFactory = null!;
    private Guid _formId;
    private Guid _categoryId;
    private Guid _hiddenCategoryId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        var department = new Department { Name = new LocalizedText("Support", "الدعم"), Code = "sup", IsActive = true };
        _db.Departments.Add(department);

        var category = new TicketCategory { Code = "billing", Name = new LocalizedText("Billing", "الفواتير"), IsActive = true, IsVisibleInPortal = true };
        var hiddenCategory = new TicketCategory { Code = "internal-only", Name = new LocalizedText("Internal", "داخلي"), IsActive = true, IsVisibleInPortal = false };
        _db.TicketCategories.AddRange(category, hiddenCategory);

        _db.TicketPriorities.Add(new TicketPriority { Code = "normal", Name = new LocalizedText("Normal", "عادي"), IsDefault = true });
        _db.TicketStatuses.Add(new TicketStatus { Code = "new", Name = new LocalizedText("New", "جديد"), Kind = TicketStatusKind.New, IsDefault = true });

        var fields = new List<WebFormField>
        {
            new("email", "email", "Email", "البريد", null, null, true, null, null, null, "customerEmail"),
            new("name", "text", "Name", "الاسم", null, null, false, null, null, null, "customerName"),
            new("issueType", "select", "Issue type", "نوع المشكلة", null, null, true, null, null,
                [new WebFormFieldOption("billing", "Billing", "الفواتير"), new WebFormFieldOption("internal-only", "Internal", "داخلي")], "categoryCode"),
            new("orderNumber", "text", "Order number", "رقم الطلب", null, null, false, 50, null, null, null),
        };

        var form = new WebFormDefinition
        {
            Key = "contact-us",
            Title = new LocalizedText("Contact us", "تواصل معنا"),
            ThankYouMessage = new LocalizedText("Thanks!", "شكراً!"),
            FieldsJson = WebFormFieldSchemaSerializer.Serialize(fields),
            RequireCaptcha = true,
            RateLimitPerHour = 2,
            IsActive = true,
        };
        _db.WebFormDefinitions.Add(form);

        await _db.SaveChangesAsync();
        _formId = form.Id;
        _categoryId = category.Id;
        _hiddenCategoryId = hiddenCategory.Id;

        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(_ => DateTimeOffset.UtcNow);

        _ticketFactory = new WebFormTicketFactory(
            _db, new ReferenceNumberGenerator(_db, _clock), new NoOpEventRecorder(),
            Substitute.For<IInteractionRecorder>(), Substitute.For<ISlaEngine>(), Substitute.For<IAssignmentEngine>(),
            _clock, NullLogger<WebFormTicketFactory>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private SubmitWebFormCommandHandler Handler(ICaptchaVerifier? captcha = null) => new(
        _db, new WebFormValidator(NoOpLocalizer()), captcha ?? AlwaysPassCaptcha(), _ticketFactory, _clock,
        NullLogger<SubmitWebFormCommandHandler>.Instance);

    private static IMessageLocalizer NoOpLocalizer()
    {
        var localizer = Substitute.For<IMessageLocalizer>();
        localizer[Arg.Any<string>()].Returns(ci => (string)ci[0]);
        return localizer;
    }

    private static ICaptchaVerifier AlwaysPassCaptcha()
    {
        var captcha = Substitute.For<ICaptchaVerifier>();
        captcha.VerifyAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(true);
        return captcha;
    }

    private static Dictionary<string, string> ValidValues(string email = "customer@example.com") => new()
    {
        ["email"] = email,
        ["name"] = "A Customer",
        ["issueType"] = "billing",
        ["orderNumber"] = "ORD-1234",
    };

    [Fact]
    public async Task ValidSubmission_StoresSubmissionAndCreatesLinkedTicket()
    {
        var result = await Handler().Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(ValidValues(), "token"), "1.1.1.1", "agent"), CancellationToken.None);

        result.TicketId.Should().NotBeNull();
        var submission = _db.WebFormSubmissions.Single(s => s.Id == result.SubmissionId);
        submission.Status.Should().Be("Processed");
        submission.TicketId.Should().Be(result.TicketId);

        var ticket = _db.Tickets.Single(t => t.Id == result.TicketId);
        ticket.CategoryId.Should().Be(_categoryId);
        ticket.Channel.Should().Be(ChannelKey.WebForm);
    }

    [Fact]
    public async Task UnmappedField_AppearsInDescriptionAsLabelledLine()
    {
        var result = await Handler().Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(ValidValues(), "token"), "1.1.1.1", "agent"), CancellationToken.None);

        var ticket = _db.Tickets.Single(t => t.Id == result.TicketId);
        ticket.Description.Should().Contain("Order number").And.Contain("ORD-1234");
    }

    [Fact]
    public async Task ScriptInjectionInField_IsStoredAsPlainTextNotEscaped()
    {
        var values = ValidValues();
        values["orderNumber"] = "<script>alert(1)</script>";

        var result = await Handler().Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(values, "token"), "1.1.1.1", "agent"), CancellationToken.None);

        var ticket = _db.Tickets.Single(t => t.Id == result.TicketId);
        // Never HTML-escaped: the defence is that the description is always rendered as plain text
        // downstream, never innerHTML — encoding it here would just show mangled entities instead.
        ticket.Description.Should().Contain("<script>alert(1)</script>");
    }

    [Fact]
    public async Task MissingRequiredField_ThrowsValidationException_AndStoresNothing()
    {
        var values = ValidValues();
        values.Remove("email");

        var act = async () => await Handler().Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(values, "token"), "1.1.1.1", "agent"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _db.WebFormSubmissions.Should().BeEmpty();
    }

    [Fact]
    public async Task SelectValueNotOffered_IsRejectedByServerSideOptionCheck()
    {
        var values = ValidValues();
        values["issueType"] = "made-up-value";

        var act = async () => await Handler().Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(values, "token"), "1.1.1.1", "agent"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task HiddenCategoryCode_PostedDirectly_NeverCreatesATicketInIt()
    {
        // Bypassing the client entirely: post the hidden category's own code as if it were a
        // declared option. Story rule: only portal-visible categories may be selected, enforced
        // server-side regardless of what the request claims.
        var values = ValidValues();
        values["issueType"] = "internal-only";

        var result = await Handler().Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(values, "token"), "1.1.1.1", "agent"), CancellationToken.None);

        var ticket = _db.Tickets.Single(t => t.Id == result.TicketId);
        ticket.CategoryId.Should().NotBe(_hiddenCategoryId);
        ticket.CategoryId.Should().Be(_categoryId); // falls back to the one active, portal-visible category
    }

    [Fact]
    public async Task MissingCaptchaToken_WhenRequired_IsRejected()
    {
        var captcha = Substitute.For<ICaptchaVerifier>();
        captcha.VerifyAsync(null, Arg.Any<CancellationToken>()).Returns(false);

        var act = async () => await Handler(captcha).Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(ValidValues(), null), "1.1.1.1", "agent"), CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
        _db.WebFormSubmissions.Should().BeEmpty();
    }

    [Fact]
    public async Task ExceedingHourlyRateLimit_IsRejected()
    {
        var handler = Handler();
        var request = new SubmitWebFormRequest(ValidValues(), "token");

        await handler.Handle(new SubmitWebFormCommand("contact-us", request, "9.9.9.9", "agent"), CancellationToken.None);
        await handler.Handle(new SubmitWebFormCommand("contact-us", request, "9.9.9.9", "agent"), CancellationToken.None);

        // Form's RateLimitPerHour is 2 — the third from the same IP within the hour is refused.
        var act = async () => await handler.Handle(new SubmitWebFormCommand("contact-us", request, "9.9.9.9", "agent"), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task SameEmailTwice_LinksToTheSameCustomer_NoDuplicateProfile()
    {
        var handler = Handler();
        var first = await handler.Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(ValidValues(), "token"), "2.2.2.2", "agent"), CancellationToken.None);
        var second = await handler.Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(ValidValues(), "token"), "3.3.3.3", "agent"), CancellationToken.None);

        var firstTicket = _db.Tickets.Single(t => t.Id == first.TicketId);
        var secondTicket = _db.Tickets.Single(t => t.Id == second.TicketId);

        firstTicket.CustomerId.Should().Be(secondTicket.CustomerId);
        _db.Customers.Count(c => c.PrimaryEmail == "customer@example.com").Should().Be(1);
    }

    [Fact]
    public async Task TicketCreationFailure_StoresSubmissionAsFailed_ThenRetrySucceedsOnceFixed()
    {
        // Deactivate the only category so ticket creation genuinely fails.
        var category = _db.TicketCategories.Single(c => c.Id == _categoryId);
        category.IsActive = false;
        await _db.SaveChangesAsync();

        var result = await Handler().Handle(
            new SubmitWebFormCommand("contact-us", new SubmitWebFormRequest(ValidValues(), "token"), "4.4.4.4", "agent"), CancellationToken.None);

        result.TicketId.Should().BeNull();
        var submission = _db.WebFormSubmissions.Single(s => s.Id == result.SubmissionId);
        submission.Status.Should().Be("Failed");
        submission.FailureReason.Should().NotBeNullOrWhiteSpace();

        // Fix the cause, then retry from the stored payload.
        category.IsActive = true;
        await _db.SaveChangesAsync();

        var retryService = new WebFormSubmissionRetryService(_db, _ticketFactory, _clock, NullLogger<WebFormSubmissionRetryService>.Instance);
        var ticketId = await retryService.RetryAsync(submission, CancellationToken.None);

        ticketId.Should().NotBeNull();
        submission.Status.Should().Be("Processed");
    }

    private class NoOpEventRecorder : ITicketEventRecorder
    {
        public void Record(Guid ticketId, TicketEventType eventType, string? field = null, string? oldValue = null,
            string? newValue = null, string? oldDisplay = null, string? newDisplay = null, string? metadataJson = null,
            string? triggeredByRule = null)
        {
        }
    }
}
