# Story 27 — Quick replies with placeholders and shortcuts (Story: CS-404-quick-replies)

## Prerequisites

- CS-201 must be complete for the composer.
- **CS-702 (AI suggested replies) will reuse the placeholder resolver built here.** Put it in Application, not in the UI.

## Story Goal

Agents answer common questions in one keystroke, in the customer's language, with the customer's details
already filled in — and a placeholder that cannot be resolved is impossible to miss before sending.

## Context — Read These Files First

1. `.squad/stories/agent-dashboard/CS-404-quick-replies/intake.md`.
2. [backend/src/CustomerSupport.Domain/Workspace/QuickReply.cs](backend/src/CustomerSupport.Domain/Workspace/QuickReply.cs) — note the placeholder example in the remark, `Scope`, `Shortcut` and `UsageCount`.
3. [backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs](backend/src/CustomerSupport.Infrastructure/Persistence/Configurations/ChannelAndContentConfigurations.cs) — the filtered shortcut-uniqueness index.
4. [backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs](backend/src/CustomerSupport.Domain/Tickets/TicketMessage.cs) — `QuickReplyId` records attribution.

## Product rules (from story)

- **Insert the body in the ticket's language, not the agent's interface language.** This is what makes the feature work in a bilingual team.
- **An unresolved placeholder renders as a visible marker** such as `[[customer.tier — not set]]`, never a raw token and never silently blank. The agent must notice before sending.
- **Placeholder resolution is server-side**, so the token vocabulary lives in one place and CS-702 can reuse it.
- **Shortcuts are unique within a scope**, enforced by the filtered index; ambiguity is refused at save.
- **Global replies require `workspace.quickreplies.manage.global`.**
- **Insertion increments `UsageCount` and stamps `TicketMessage.QuickReplyId`**, so the most-used surface first and effectiveness is measurable.

## Data model

_No schema changes: this story reads the existing model._

## API contract

| Method | Route | Permission | Notes |
|---|---|---|---|
| GET | `/api/quick-replies` | `tickets.reply` | Visible to the caller across scopes, ordered by usage. |
| POST | `/api/quick-replies/{id}/render` | `tickets.reply` | Body: `{ ticketId }`. Returns the resolved body plus any unresolved tokens. |
| POST/PUT/DELETE | `/api/quick-replies` | `workspace.quickreplies.manage` | Global scope also needs the global permission. |

## Backend Tasks

### 1 — Placeholder resolver

**File:** `backend/src/CustomerSupport.Application/Workspace/QuickReplies/PlaceholderResolver.cs`

```csharp
public record RenderResult(string Body, IReadOnlyList<string> UnresolvedTokens);

private static readonly Regex TokenPattern =
    new(@"\{\{\s*([a-zA-Z.]+)\s*\}\}", RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));
```

Supported tokens: `customer.displayName`, `customer.firstName`, `customer.code`, `customer.tier`, `ticket.number`, `ticket.subject`, `ticket.status`, `ticket.priority`, `ticket.category`, `agent.displayName`, `agent.jobTitle`, `org.supportEmail`, `org.supportPhone`.

Resolve every localized value in the **ticket** language:

```csharp
var lang = ticket.Language;   // not the agent's UI language

var replaced = TokenPattern.Replace(template, match =>
{
    var token = match.Groups[1].Value;
    var value = Resolve(token, ticket, customer, agent, branding, lang);

    if (string.IsNullOrWhiteSpace(value))
    {
        unresolved.Add(token);
        // A visible marker, so the agent cannot send a half-filled template by accident.
        return $"[[{token} — not set]]";
    }

    return value;
});
```

Return `UnresolvedTokens` so the UI can warn explicitly rather than relying on the agent spotting the marker.

### 2 — Scope-aware listing and shortcut validation

`GetQuickRepliesQuery` returns personal replies owned by the caller, team replies for their teams, and all global replies, ordered by `UsageCount` descending. Optional filters for category and channel.

On save, check shortcut ambiguity within the visible scope:

```csharp
var clashes = await db.QuickReplies.AnyAsync(q =>
    q.Shortcut == request.Shortcut && q.Id != request.Id && q.IsActive &&
    (q.Scope == "Global"
     || (q.Scope == "Team" && teamIds.Contains(q.TeamId!.Value))
     || (q.Scope == "Personal" && q.OwnerId == currentUser.UserId)), ct);

if (clashes) throw new ConflictException($"The shortcut '{request.Shortcut}' is already in use in your scope.");
```

The database index enforces uniqueness within an owner scope; this check catches cross-scope ambiguity the index cannot express.

## Frontend Tasks

### 3 — Quick reply picker

A button in the composer opening a searchable list grouped by scope, showing title, category and a body preview, ordered by usage. Keyboard navigable — arrow keys and Enter — because this is a speed feature and reaching for the mouse defeats it.

Selecting one calls the render endpoint and inserts the resolved body at the cursor. When `unresolvedTokens` is non-empty, show an inline warning listing them above the composer.

### 4 — Shortcut expansion

Watch the composer for `/word` followed by space or Tab. On a match, replace the shortcut text with the rendered body:

```ts
// Match only at a word boundary, so a URL path in the middle of a sentence is not expanded.
const match = /(?:^|\s)(\/[a-z0-9_-]+)\s$/i.exec(textBeforeCursor);
```

Debounce the render call and cache rendered bodies per ticket so repeated use of the same shortcut is instant.

### 5 — Management screen

**File:** `frontend/src/app/features/agent/quick-replies/`

List with scope filter, usage count and shortcut columns. The form takes both language bodies side by side with a token palette that inserts at the cursor, plus scope, shortcut, category and channel.

Show a live preview against a sample ticket so the author sees the resolved output before saving. Hide the Global scope option without the global permission.

## Verification Steps

1. Insert a quick reply on an Arabic-language ticket while the interface is English: the Arabic body is inserted.
2. Insert one containing `{{customer.displayName}}`: the real name appears, in the ticket language.
3. Insert one referencing `{{customer.tier}}` for a customer with no tier: the visible marker appears and a warning lists the unresolved token.
4. Type `/refund` then space: the reply expands.
5. Type a URL containing a slash mid-sentence: nothing expands.
6. Create a personal reply with a shortcut that clashes with a global one: refused.
7. Confirm the picker orders by usage and that inserting increments the count.
8. Confirm the sent message records `QuickReplyId`.
9. Try to create a global reply without the global permission: the option is hidden and the API refuses.
10. Navigate the picker with the keyboard only.

## Done Criteria

- [ ] `PlaceholderResolver` lives in Application and is reusable by CS-702.
- [ ] Bodies resolve in the ticket language, and unresolved tokens are visibly marked and reported.
- [ ] Scope-aware listing, shortcut ambiguity checks and the global permission all work.
- [ ] Shortcut expansion triggers only at word boundaries.
- [ ] Usage counting and message attribution work.
- [ ] The management screen previews against a sample ticket.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
