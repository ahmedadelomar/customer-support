using CustomerSupport.Application.Channels.LiveChat;
using CustomerSupport.Application.Common.Exceptions;
using CustomerSupport.Application.Common.Interfaces;
using CustomerSupport.Application.Common.Settings;
using CustomerSupport.Application.Tickets.Commands;
using CustomerSupport.Domain.Channels;
using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Customers;
using CustomerSupport.Domain.Enums;
using CustomerSupport.Domain.Organization;
using CustomerSupport.Domain.Tickets;
using CustomerSupport.Infrastructure.Persistence;
using CustomerSupport.Infrastructure.Services;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace CustomerSupport.UnitTests.Channels;

/// <summary>
/// Live chat (CS-303) — session start/routing, race-safe accept, identification, the offline path
/// on end, and promotion. A real Sqlite connection so the unique-index-backed idempotency other
/// chat-adjacent code relies on behaves the same way it does in <c>InboundMessagePipelineTests</c>.
/// </summary>
public class LiveChatTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private AppDbContext _db = null!;
    private Guid _channelAccountId;
    private Guid _teamId;
    private Guid _agentId;
    private IDateTimeProvider _clock = null!;
    private IChatRealtimeNotifier _realtime = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        var channel = new Channel { Key = ChannelKey.LiveChat, Name = new LocalizedText("Live Chat", "دردشة") };
        _db.Channels.Add(channel);

        var department = new Department { Name = new LocalizedText("Support", "الدعم"), Code = "sup", IsActive = true };
        _db.Departments.Add(department);

        var team = new Team { Department = department, Name = new LocalizedText("Chat Team", "فريق الدردشة"), IsActive = true };
        _db.Teams.Add(team);

        var account = new ChannelAccount
        {
            Channel = channel,
            Name = "Website chat",
            Identifier = "widget-1",
            DefaultDepartmentId = department.Id,
            IsActive = true,
        };
        _db.ChannelAccounts.Add(account);

        _db.TicketCategories.Add(new TicketCategory { Code = "general", Name = new LocalizedText("General", "عام"), IsActive = true });
        _db.TicketPriorities.Add(new TicketPriority { Code = "normal", Name = new LocalizedText("Normal", "عادي"), IsDefault = true });
        _db.TicketStatuses.Add(new TicketStatus { Code = "new", Name = new LocalizedText("New", "جديد"), Kind = TicketStatusKind.New, IsDefault = true });

        await _db.SaveChangesAsync();

        _channelAccountId = account.Id;
        _teamId = team.Id;
        _agentId = Guid.NewGuid();
        _db.TeamMembers.Add(new TeamMember { TeamId = team.Id, UserId = _agentId, IsActive = true, JoinedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        _clock = Substitute.For<IDateTimeProvider>();
        _clock.UtcNow.Returns(_ => DateTimeOffset.UtcNow);

        _realtime = Substitute.For<IChatRealtimeNotifier>();
    }

    public async Task DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private IChatVisitorTokenService FakeTokens() =>
        new FakeTokenService();

    private sealed class FakeTokenService : IChatVisitorTokenService
    {
        public string IssueSessionToken(Guid sessionId) => $"token-{sessionId}";
    }

    private ICurrentUser AgentUser(Guid? id = null)
    {
        var user = Substitute.For<ICurrentUser>();
        user.UserId.Returns(id ?? _agentId);
        user.UserName.Returns("agent1");
        user.HasPermission(Arg.Any<string>()).Returns(true);
        return user;
    }

    [Fact]
    public async Task StartSession_QueuesToTheAccountsDepartmentTeam()
    {
        var handler = new StartChatSessionCommandHandler(_db, FakeTokens(), _realtime, _clock);

        var result = await handler.Handle(
            new StartChatSessionCommand(new StartChatSessionRequest(_channelAccountId, null, "Visitor A", "/pricing", "en", "Hi, I have a question"), "1.2.3.4", "test-agent"),
            CancellationToken.None);

        result.Session.QueuedForTeamId.Should().Be(_teamId);
        result.Session.Status.Should().Be("Waiting");
        result.Token.Should().NotBeNullOrEmpty();
        _db.ChatMessages.Count(m => m.ChatSessionId == result.SessionId).Should().Be(1);
    }

    [Fact]
    public async Task Accept_TwoAgentsRaceForTheSameSession_OnlyOneWins()
    {
        var session = await SeedWaitingSessionAsync();
        var settings = FixedSettings(maxConcurrent: 3);

        var otherAgentId = Guid.NewGuid();
        _db.TeamMembers.Add(new TeamMember { TeamId = _teamId, UserId = otherAgentId, IsActive = true, JoinedAt = DateTimeOffset.UtcNow });
        await _db.SaveChangesAsync();

        var firstHandler = new AcceptChatSessionCommandHandler(_db, AgentUser(_agentId), settings, _realtime);
        var secondHandler = new AcceptChatSessionCommandHandler(_db, AgentUser(otherAgentId), settings, _realtime);

        var firstResult = await firstHandler.Handle(new AcceptChatSessionCommand(session.Id), CancellationToken.None);
        firstResult.AssignedAgentId.Should().Be(_agentId);

        var act = async () => await secondHandler.Handle(new AcceptChatSessionCommand(session.Id), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Accept_AtConcurrentChatLimit_Throws()
    {
        var settings = FixedSettings(maxConcurrent: 1);

        // The agent already has one active chat, at the configured limit.
        _db.ChatSessions.Add(new ChatSession
        {
            ChannelAccountId = _channelAccountId, VisitorKey = "v-existing", QueuedForTeamId = _teamId,
            Status = "Active", AssignedAgentId = _agentId, StartedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        var session = await SeedWaitingSessionAsync();
        var handler = new AcceptChatSessionCommandHandler(_db, AgentUser(), settings, _realtime);

        var act = async () => await handler.Handle(new AcceptChatSessionCommand(session.Id), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Identify_UnknownEmail_CreatesMinimalCustomerAndLinksSession()
    {
        var session = await SeedWaitingSessionAsync();
        var handler = new IdentifyChatSessionCommandHandler(_db, new ReferenceNumberGenerator(_db, _clock), _realtime);

        var dto = await handler.Handle(
            new IdentifyChatSessionCommand(session.Id, new IdentifyChatSessionRequest("Visitor A", "visitor@example.com", null)),
            CancellationToken.None);

        dto.CustomerId.Should().NotBeNull();
        _db.Customers.Single(c => c.Id == dto.CustomerId).PrimaryEmail.Should().Be("visitor@example.com");
    }

    [Fact]
    public async Task Identify_MatchingExistingContact_LinksToTheSameCustomer()
    {
        var existingCustomer = new Customer
        {
            Code = "CUST-1", DisplayName = new LocalizedText("Existing", "موجود"), PreferredLanguage = "en",
        };
        existingCustomer.Contacts.Add(new CustomerContact
        {
            Type = ContactType.Email, Value = "known@example.com", NormalizedValue = "known@example.com", IsPrimary = true,
        });
        _db.Customers.Add(existingCustomer);
        await _db.SaveChangesAsync();

        var session = await SeedWaitingSessionAsync();
        var handler = new IdentifyChatSessionCommandHandler(_db, new ReferenceNumberGenerator(_db, _clock), _realtime);

        var dto = await handler.Handle(
            new IdentifyChatSessionCommand(session.Id, new IdentifyChatSessionRequest(null, "known@example.com", null)),
            CancellationToken.None);

        dto.CustomerId.Should().Be(existingCustomer.Id);
        _db.Customers.Count().Should().Be(1);
    }

    [Fact]
    public async Task End_NeverAcceptedWithIdentifiedVisitorAndAMessage_CreatesOfflineTicket()
    {
        var session = await SeedWaitingSessionAsync();
        var customerId = await IdentifyAsync(session.Id, "offline@example.com");

        var handler = new EndChatSessionCommandHandler(
            _db, new ReferenceNumberGenerator(_db, _clock), new NoOpEventRecorder(),
            Substitute.For<IInteractionRecorder>(), Substitute.For<ISlaEngine>(), Substitute.For<IAssignmentEngine>(),
            _realtime, _clock, NullLogger<EndChatSessionCommandHandler>.Instance);

        var dto = await handler.Handle(new EndChatSessionCommand(session.Id), CancellationToken.None);

        dto.Status.Should().Be("Ended");
        dto.TicketId.Should().NotBeNull();
        _db.Tickets.Single(t => t.Id == dto.TicketId).CustomerId.Should().Be(customerId);
        _db.Tickets.Single(t => t.Id == dto.TicketId).Channel.Should().Be(ChannelKey.LiveChat);
    }

    [Fact]
    public async Task End_AlreadyAcceptedSession_EndsWithoutCreatingATicket()
    {
        var session = await SeedWaitingSessionAsync();
        session.Status = "Active";
        session.AssignedAgentId = _agentId;
        await _db.SaveChangesAsync();

        var handler = new EndChatSessionCommandHandler(
            _db, new ReferenceNumberGenerator(_db, _clock), new NoOpEventRecorder(),
            Substitute.For<IInteractionRecorder>(), Substitute.For<ISlaEngine>(), Substitute.For<IAssignmentEngine>(),
            _realtime, _clock, NullLogger<EndChatSessionCommandHandler>.Instance);

        var dto = await handler.Handle(new EndChatSessionCommand(session.Id), CancellationToken.None);

        dto.Status.Should().Be("Ended");
        dto.TicketId.Should().BeNull();
        _db.Tickets.Count().Should().Be(0);
    }

    [Fact]
    public async Task Promote_WithoutIdentification_Throws()
    {
        var session = await SeedWaitingSessionAsync();
        var handler = new PromoteChatToTicketCommandHandler(
            _db, Substitute.For<ISender>(), Substitute.For<IInteractionRecorder>(), _realtime, _clock);

        var act = async () => await handler.Handle(
            new PromoteChatToTicketCommand(session.Id, Guid.NewGuid(), null, null), CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Promote_Identified_DispatchesCreateTicketAndLinksSession()
    {
        var session = await SeedWaitingSessionAsync();
        var customerId = await IdentifyAsync(session.Id, "promote@example.com");

        var expectedTicketId = Guid.NewGuid();
        var sender = Substitute.For<ISender>();
        sender.Send(Arg.Any<CreateTicketCommand>(), Arg.Any<CancellationToken>()).Returns(expectedTicketId);

        var handler = new PromoteChatToTicketCommandHandler(_db, sender, Substitute.For<IInteractionRecorder>(), _realtime, _clock);
        var categoryId = Guid.NewGuid();

        var ticketId = await handler.Handle(
            new PromoteChatToTicketCommand(session.Id, categoryId, null, null), CancellationToken.None);

        ticketId.Should().Be(expectedTicketId);
        (await _db.ChatSessions.FindAsync(session.Id))!.TicketId.Should().Be(expectedTicketId);
        await sender.Received(1).Send(
            Arg.Is<CreateTicketCommand>(c => c.CustomerId == customerId && c.CategoryId == categoryId && c.Channel == ChannelKey.LiveChat),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rate_BeforeEnded_Throws()
    {
        var session = await SeedWaitingSessionAsync();
        var handler = new RateChatSessionCommandHandler(_db);

        var act = async () => await handler.Handle(new RateChatSessionCommand(session.Id, new RateChatSessionRequest(5)), CancellationToken.None);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task Rate_AfterEnded_Succeeds()
    {
        var session = await SeedWaitingSessionAsync();
        session.Status = "Ended";
        session.EndedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync();

        var handler = new RateChatSessionCommandHandler(_db);
        await handler.Handle(new RateChatSessionCommand(session.Id, new RateChatSessionRequest(4)), CancellationToken.None);

        (await _db.ChatSessions.FindAsync(session.Id))!.Rating.Should().Be(4);
    }

    private ISettingsProvider FixedSettings(int maxConcurrent)
    {
        var settings = Substitute.For<ISettingsProvider>();
        settings.GetAsync(SettingKeys.LiveChatMaxConcurrentSessions, Arg.Any<int>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(maxConcurrent);
        return settings;
    }

    private async Task<ChatSession> SeedWaitingSessionAsync()
    {
        var session = new ChatSession
        {
            ChannelAccountId = _channelAccountId,
            VisitorKey = Guid.NewGuid().ToString("N"),
            QueuedForTeamId = _teamId,
            Status = "Waiting",
            Language = "en",
            StartedAt = DateTimeOffset.UtcNow,
        };
        session.Messages.Add(new ChatMessage
        {
            ChatSessionId = session.Id, AuthorType = MessageAuthorType.Customer,
            Body = "Hello, I need help", SentAt = DateTimeOffset.UtcNow,
        });
        session.MessageCount = 1;

        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync();
        return session;
    }

    private async Task<Guid> IdentifyAsync(Guid sessionId, string email)
    {
        var handler = new IdentifyChatSessionCommandHandler(_db, new ReferenceNumberGenerator(_db, _clock), _realtime);
        var dto = await handler.Handle(
            new IdentifyChatSessionCommand(sessionId, new IdentifyChatSessionRequest("Visitor", email, null)), CancellationToken.None);
        return dto.CustomerId!.Value;
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
