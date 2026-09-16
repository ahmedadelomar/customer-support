using System.Text.Json;
using CustomerSupport.Application.Channels.Outbound;
using CustomerSupport.Application.Channels.Sms;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Application.Tickets.Commands;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Channels.Sms;
using CustomerSupport.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;
using ConflictException = CustomerSupport.Application.Common.Exceptions.ConflictException;

namespace CustomerSupport.UnitTests.Channels;

/// <summary>
/// SMS channel (CS-304) — opt-out/opt-in, the outbound segment/cost recording, and the reply-time
/// segment cap. An InMemory database, like the other ticket-command tests in this suite: nothing
/// here depends on a real unique-index violation.
/// </summary>
public class SmsChannelTests
{
    private static AppDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static IDateTimeProvider FixedClock()
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        return clock;
    }

    private static ICurrentUser AgentUser(Guid? branchId = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(Guid.NewGuid());
        user.UserName.Returns("Test Agent");
        user.BranchId.Returns(branchId);
        user.AccessibleBranchIds.Returns(Array.Empty<Guid>());
        user.DepartmentIds.Returns(Array.Empty<Guid>());
        user.IsAuthenticated.Returns(true);
        user.HasPermission(Arg.Any<string>()).Returns(true);
        return user;
    }

    [Fact]
    public async Task ApplySmsOpt_Stop_SetsAllowNotificationsFalse_AndSendsOneConfirmation()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("A", "أ"), PreferredLanguage = "en" };
        var contact = new CustomerContact
        {
            Customer = customer, Type = ContactType.Mobile, Value = "+15551234567",
            NormalizedValue = "+15551234567", AllowNotifications = true,
        };
        customer.Contacts.Add(contact);
        var account = new ChannelAccount { Name = "SMS", Identifier = "12345" };
        db.AddRange(customer, account);
        await db.SaveChangesAsync();

        var sender = Substitute.For<ISmsChannelSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelSendResult(true, "provider-1", null, null));

        var handler = new ApplySmsOptCommandHandler(db, sender);
        await handler.Handle(new ApplySmsOptCommand(account.Id, "+15551234567", AllowNotifications: false), CancellationToken.None);

        (await db.CustomerContacts.FindAsync(contact.Id))!.AllowNotifications.Should().BeFalse();
        await sender.Received(1).SendAsync("+15551234567", "12345", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ApplySmsOpt_Start_SetsAllowNotificationsTrue()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("A", "أ"), PreferredLanguage = "en" };
        var contact = new CustomerContact
        {
            Customer = customer, Type = ContactType.Mobile, Value = "+15551234567",
            NormalizedValue = "+15551234567", AllowNotifications = false,
        };
        customer.Contacts.Add(contact);
        var account = new ChannelAccount { Name = "SMS", Identifier = "12345" };
        db.AddRange(customer, account);
        await db.SaveChangesAsync();

        var sender = Substitute.For<ISmsChannelSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelSendResult(true, "provider-1", null, null));

        var handler = new ApplySmsOptCommandHandler(db, sender);
        await handler.Handle(new ApplySmsOptCommand(account.Id, "+15551234567", AllowNotifications: true), CancellationToken.None);

        (await db.CustomerContacts.FindAsync(contact.Id))!.AllowNotifications.Should().BeTrue();
    }

    [Fact]
    public async Task ApplySmsOpt_NoMatchingContact_DoesNotThrow_StillAcknowledges()
    {
        await using var db = CreateContext();
        var account = new ChannelAccount { Name = "SMS", Identifier = "12345" };
        db.Add(account);
        await db.SaveChangesAsync();

        var sender = Substitute.For<ISmsChannelSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelSendResult(true, "provider-1", null, null));

        var handler = new ApplySmsOptCommandHandler(db, sender);
        var act = async () => await handler.Handle(
            new ApplySmsOptCommand(account.Id, "+19999999999", AllowNotifications: false), CancellationToken.None);

        await act.Should().NotThrowAsync();
        await sender.Received(1).SendAsync("+19999999999", "12345", Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OutboxHandler_RecordsSegmentCountAndEstimatedCost()
    {
        await using var db = CreateContext();
        var ticket = new Ticket
        {
            Number = "TCK-2026-000001", CustomerId = Guid.NewGuid(), Subject = "s", Description = "d",
            Language = "en", CategoryId = Guid.NewGuid(), PriorityId = Guid.NewGuid(), StatusId = Guid.NewGuid(),
        };
        var message = new TicketMessage
        {
            TicketId = ticket.Id, Channel = ChannelKey.Sms, Direction = MessageDirection.Outbound,
            AuthorType = MessageAuthorType.Agent, BodyText = "Your order has shipped.", SentAt = DateTimeOffset.UtcNow,
        };
        db.AddRange(ticket, message);
        await db.SaveChangesAsync();

        var sender = Substitute.For<ISmsChannelSender>();
        sender.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new ChannelSendResult(true, "provider-msg-1", null, null));

        var settings = Substitute.For<ISettingsProvider>();
        settings.GetAsync(SettingKeys.SmsCostPerSegment, Arg.Any<double>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(0.05);

        var handler = new SmsChannelOutboxHandler(db, sender, settings, FixedClock());

        var payload = new SmsOutboundPayload(message.Id, ticket.Id, null, null, "+15551234567", "12345", "Your order has shipped.");
        var outboxMessage = new Domain.Integrations.OutboxMessage
        {
            Type = SmsChannelOutbox.OutboxType, PayloadJson = JsonSerializer.Serialize(payload),
            AggregateType = nameof(TicketMessage), AggregateId = message.Id, OccurredAt = DateTimeOffset.UtcNow,
        };

        await handler.HandleAsync(outboxMessage, CancellationToken.None);
        await db.SaveChangesAsync();

        var log = db.MessageDeliveryLogs.Single(l => l.TicketMessageId == message.Id);
        log.SegmentCount.Should().Be(1);
        log.EstimatedCost.Should().Be(0.05m);
        log.Status.Should().Be(MessageDeliveryStatus.Sent);
        log.ProviderMessageId.Should().Be("provider-msg-1");
    }

    [Fact]
    public async Task ReplyToTicket_OnSmsChannel_AboveSegmentCap_IsBlocked()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("A", "أ"), PreferredLanguage = "en", PrimaryPhone = "+15551234567" };
        var status = new TicketStatus { Code = "open", Name = new LocalizedText("Open", "مفتوحة"), Kind = TicketStatusKind.Open };
        var account = new ChannelAccount { Name = "SMS", Identifier = "12345" };
        db.AddRange(customer, status, account);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Number = "TCK-2026-000002", CustomerId = customer.Id, Subject = "s", Description = "d",
            Language = "en", CategoryId = Guid.NewGuid(), PriorityId = Guid.NewGuid(), StatusId = status.Id,
            Channel = ChannelKey.Sms, ChannelAccountId = account.Id,
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        var settings = Substitute.For<ISettingsProvider>();
        settings.GetAsync(SettingKeys.SmsMaxSegments, Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(1);

        var handler = new ReplyToTicketCommandHandler(
            db, AgentUser(), Substitute.For<ITicketEventRecorder>(), Substitute.For<IInteractionRecorder>(),
            Substitute.For<ISlaEngine>(), Substitute.For<INotificationDispatcher>(), settings, FixedClock());

        // 161 GSM characters needs 2 segments, above the 1-segment cap configured above.
        var longBody = new string('a', 161);

        var act = async () => await handler.Handle(
            new ReplyToTicketCommand { TicketId = ticket.Id, BodyText = longBody }, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
        db.TicketMessages.Should().BeEmpty();
        db.OutboxMessages.Should().BeEmpty();
    }

    [Fact]
    public async Task ReplyToTicket_OnSmsChannel_WithinCap_QueuesOutboundSms()
    {
        await using var db = CreateContext();
        var customer = new Customer { Code = "CUS-1", DisplayName = new LocalizedText("A", "أ"), PreferredLanguage = "en", PrimaryPhone = "+15551234567" };
        var status = new TicketStatus { Code = "open", Name = new LocalizedText("Open", "مفتوحة"), Kind = TicketStatusKind.Open };
        var account = new ChannelAccount { Name = "SMS", Identifier = "12345" };
        db.AddRange(customer, status, account);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            Number = "TCK-2026-000003", CustomerId = customer.Id, Subject = "s", Description = "d",
            Language = "en", CategoryId = Guid.NewGuid(), PriorityId = Guid.NewGuid(), StatusId = status.Id,
            Channel = ChannelKey.Sms, ChannelAccountId = account.Id,
        };
        db.Add(ticket);
        await db.SaveChangesAsync();

        var settings = Substitute.For<ISettingsProvider>();
        settings.GetAsync(SettingKeys.SmsMaxSegments, Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(3);

        var handler = new ReplyToTicketCommandHandler(
            db, AgentUser(), Substitute.For<ITicketEventRecorder>(), Substitute.For<IInteractionRecorder>(),
            Substitute.For<ISlaEngine>(), Substitute.For<INotificationDispatcher>(), settings, FixedClock());

        await handler.Handle(new ReplyToTicketCommand { TicketId = ticket.Id, BodyText = "Thanks, on it!" }, CancellationToken.None);

        db.OutboxMessages.Should().ContainSingle(o => o.Type == SmsChannelOutbox.OutboxType);
    }
}
