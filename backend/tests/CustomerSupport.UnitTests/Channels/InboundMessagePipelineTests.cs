using CustomerSupport.Application.Channels.Inbound;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Channels;

/// <summary>
/// The shared inbound pipeline (CS-301) — idempotency, threading, follow-up/reopen and mail-loop
/// protection. A real Sqlite connection is required: idempotency depends on the actual unique-index
/// violation on <c>ExternalMessageId</c>, which the InMemory provider never enforces.
/// </summary>
public class InboundMessagePipelineTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private InboundMessagePipeline _pipeline = null!;
    private Guid _channelAccountId;
    private Guid _categoryId;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        var channel = new Channel { Key = ChannelKey.Email, Name = new LocalizedText("Email", "بريد") };
        _db.Channels.Add(channel);

        var account = new ChannelAccount
        {
            Channel = channel,
            Name = "Support",
            Identifier = "support@example.com",
            SendAutoReply = true,
            AutoReplyBody = new LocalizedText("We got your message.", "لقد استلمنا رسالتك."),
            IsActive = true,
        };
        _db.ChannelAccounts.Add(account);

        var category = new TicketCategory { Code = "general", Name = new LocalizedText("General", "عام") };
        _db.TicketCategories.Add(category);
        _db.TicketPriorities.Add(new TicketPriority { Code = "normal", Name = new LocalizedText("Normal", "عادي"), IsDefault = true });
        _db.TicketStatuses.Add(new TicketStatus { Code = "new", Name = new LocalizedText("New", "جديد"), Kind = TicketStatusKind.New, IsDefault = true });
        _db.TicketStatuses.Add(new TicketStatus { Code = "resolved", Name = new LocalizedText("Resolved", "تم الحل"), Kind = TicketStatusKind.Resolved });
        _db.TicketStatuses.Add(new TicketStatus { Code = "open", Name = new LocalizedText("Open", "مفتوح"), Kind = TicketStatusKind.Open });
        _db.TicketStatuses.Add(new TicketStatus { Code = "closed", Name = new LocalizedText("Closed", "مغلق"), Kind = TicketStatusKind.Closed, IsTerminal = true });

        await _db.SaveChangesAsync();
        _channelAccountId = account.Id;
        _categoryId = category.Id;

        var fileStorage = Substitute.For<IFileStorage>();
        fileStorage.SaveAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Guid.NewGuid().ToString());

        var attachmentPolicy = Substitute.For<IAttachmentPolicyProvider>();
        attachmentPolicy.GetPolicyAsync(Arg.Any<CancellationToken>())
            // Dotted, matching the real AttachmentPolicyProvider's convention (and Path.GetExtension()'s
            // own output) — a stripped-dot list here would mask the mismatch this exact shape caught live.
            .Returns(new AttachmentPolicy(5 * 1024 * 1024, new[] { ".pdf", ".png", ".jpg" }));

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(_ => DateTimeOffset.UtcNow);

        _pipeline = new InboundMessagePipeline(
            _db,
            new ReferenceNumberGenerator(_db, clock),
            new NoOpEventRecorder(),
            Substitute.For<IInteractionRecorder>(),
            Substitute.For<ISlaEngine>(),
            Substitute.For<IAssignmentEngine>(),
            attachmentPolicy,
            fileStorage,
            clock,
            NullLogger<InboundMessagePipeline>.Instance);
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private class NoOpEventRecorder : ITicketEventRecorder
    {
        public void Record(Guid ticketId, TicketEventType eventType, string? field = null, string? oldValue = null,
            string? newValue = null, string? oldDisplay = null, string? newDisplay = null, string? metadataJson = null,
            string? triggeredByRule = null)
        {
        }
    }

    private InboundMessage NewMessage(
        string externalId, string from = "customer@example.com", string? subject = "Help please",
        string? inReplyTo = null, IReadOnlyDictionary<string, string>? headers = null) =>
        new(ChannelKey.Email, _channelAccountId, externalId, inReplyTo, [], from, "A Customer", subject,
            "I need help with my order.", null, DateTimeOffset.UtcNow, [], headers ?? new Dictionary<string, string>());

    [Fact]
    public async Task NewMessage_FromUnknownSender_CreatesCustomerAndTicket()
    {
        var ticketId = await _pipeline.IngestAsync(NewMessage("msg-1"));

        ticketId.Should().NotBeNull();
        _db.Customers.Count(c => c.PrimaryEmail == "customer@example.com").Should().Be(1);
        _db.Tickets.Single(t => t.Id == ticketId).CategoryId.Should().Be(_categoryId);
    }

    [Fact]
    public async Task SameExternalId_Redelivered_DoesNotCreateASecondTicketOrMessage()
    {
        var first = await _pipeline.IngestAsync(NewMessage("msg-dup"));
        var second = await _pipeline.IngestAsync(NewMessage("msg-dup"));

        second.Should().BeNull();
        _db.Tickets.Count().Should().Be(1);
        _db.TicketMessages.Count(m => m.ExternalMessageId == "msg-dup").Should().Be(1);
    }

    [Fact]
    public async Task SecondMessage_FromSameSender_LandsOnSameCustomer()
    {
        await _pipeline.IngestAsync(NewMessage("msg-a", subject: "First issue"));
        await _pipeline.IngestAsync(NewMessage("msg-b", subject: "Second issue"));

        _db.Customers.Count(c => c.PrimaryEmail == "customer@example.com").Should().Be(1);
        _db.Tickets.Count().Should().Be(2); // unrelated subjects never merge
    }

    [Fact]
    public async Task ReplyWithInReplyTo_ThreadsOntoTheOriginalTicket()
    {
        var ticketId = await _pipeline.IngestAsync(NewMessage("msg-1", subject: "Order question"));

        var replyId = await _pipeline.IngestAsync(NewMessage("msg-2", subject: "Re: Order question", inReplyTo: "msg-1"));

        replyId.Should().Be(ticketId);
        _db.Tickets.Count().Should().Be(1);
    }

    [Fact]
    public async Task SubjectWithTicketNumber_ThreadsOntoThatTicket_EvenWithoutHeaders()
    {
        var ticketId = await _pipeline.IngestAsync(NewMessage("msg-1"));
        var number = _db.Tickets.Single(t => t.Id == ticketId).Number;

        var secondId = await _pipeline.IngestAsync(NewMessage("msg-2", subject: $"Following up on [{number}]"));

        secondId.Should().Be(ticketId);
    }

    [Fact]
    public async Task GenericReplySubject_WithNoThreadingHeaders_NeverMergesTickets()
    {
        // Two DIFFERENT open tickets, both with a customer who then sends "Re: Question" with no
        // In-Reply-To — subject text alone must never merge them into either.
        await _pipeline.IngestAsync(NewMessage("msg-1", subject: "Question"));
        await _pipeline.IngestAsync(NewMessage("msg-2", from: "other@example.com", subject: "Question"));

        var thirdId = await _pipeline.IngestAsync(NewMessage("msg-3", subject: "Re: Question"));

        _db.Tickets.Count().Should().Be(3);
        thirdId.Should().NotBeNull();
    }

    [Fact]
    public async Task ReplyToAResolvedTicket_ReopensIt()
    {
        var ticketId = await _pipeline.IngestAsync(NewMessage("msg-1"));
        var ticket = _db.Tickets.Single(t => t.Id == ticketId);
        ticket.StatusId = _db.TicketStatuses.Single(s => s.Kind == TicketStatusKind.Resolved).Id;
        await _db.SaveChangesAsync();

        var replyId = await _pipeline.IngestAsync(NewMessage("msg-2", inReplyTo: "msg-1"));

        replyId.Should().Be(ticketId);
        var reloaded = _db.Tickets.Single(t => t.Id == ticketId);
        reloaded.StatusId.Should().Be(_db.TicketStatuses.Single(s => s.Kind == TicketStatusKind.Open).Id);
        reloaded.ReopenCount.Should().Be(1);
    }

    [Fact]
    public async Task ReplyToAClosedTicket_CreatesALinkedFollowUp_RatherThanReopening()
    {
        var ticketId = await _pipeline.IngestAsync(NewMessage("msg-1"));
        var ticket = _db.Tickets.Single(t => t.Id == ticketId);
        ticket.StatusId = _db.TicketStatuses.Single(s => s.Kind == TicketStatusKind.Closed).Id;
        await _db.SaveChangesAsync();

        var followUpId = await _pipeline.IngestAsync(NewMessage("msg-2", inReplyTo: "msg-1"));

        (followUpId != ticketId).Should().BeTrue("a reply to a closed ticket must create a follow-up, not reopen the original");
        _db.Tickets.Count().Should().Be(2);
        _db.Tickets.Single(t => t.Id == ticketId).Status.Kind.Should().Be(TicketStatusKind.Closed); // original untouched
    }

    [Fact]
    public async Task OversizedAttachment_IsSkipped_MessageStillIngested()
    {
        var big = new byte[6 * 1024 * 1024]; // over the 5MB test policy
        var message = NewMessage("msg-1") with
        {
            Attachments = [new InboundAttachment("huge.pdf", "application/pdf", big)],
        };

        var ticketId = await _pipeline.IngestAsync(message);

        ticketId.Should().NotBeNull();
        _db.Attachments.Count().Should().Be(0);
        var stored = _db.TicketMessages.Single(m => m.ExternalMessageId == "msg-1");
        stored.BodyText.Should().Contain("skipped");
    }

    [Fact]
    public async Task AllowedAttachment_IsSaved()
    {
        var message = NewMessage("msg-1") with
        {
            Attachments = [new InboundAttachment("receipt.pdf", "application/pdf", [1, 2, 3])],
        };

        await _pipeline.IngestAsync(message);

        _db.Attachments.Count(a => a.OwnerType == nameof(TicketMessage)).Should().Be(1);
    }

    [Fact]
    public async Task AutomatedSender_NeverGetsAnAutoAcknowledgement()
    {
        var headers = new Dictionary<string, string> { ["Auto-Submitted"] = "auto-replied" };

        var ticketId = await _pipeline.IngestAsync(NewMessage("msg-1", headers: headers));

        ticketId.Should().NotBeNull();
        _db.OutboxMessages.Count().Should().Be(0);
    }

    [Fact]
    public async Task NewTicket_WithAutoReplyEnabled_QueuesAnAcknowledgementThroughTheOutbox()
    {
        await _pipeline.IngestAsync(NewMessage("msg-1"));

        _db.OutboxMessages.Should().ContainSingle(o => o.Type == "channel.email.outbound");
        _db.TicketMessages.Should().Contain(m => m.Direction == MessageDirection.Outbound && m.AuthorType == MessageAuthorType.System);
    }
}
