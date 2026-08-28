# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

- Folder: `.squad/stories/ai-features/CS-705-ai-chatbot/intake.md`
- Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.
- Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

- **Feature name (display):** Section 7 — AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CS-705-ai-chatbot`
- **Work item type:** Story
- **Status:** `Ready for planning`
- **Assignee:** ``
- **Labels:** `ai, realtime`

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

*(Paste the work item title verbatim. Prefilled when `squad new-story` fetched from a tracker.)*

```
AI chatbot
```

---

## Description

*(Paste the full work item description. Prefilled when fetched from a tracker.)*

```
As a customer,
I want to ask a question and get an immediate answer,
So that simple problems are solved without waiting for an agent.

Scope:
- A chatbot fronting live chat and the portal, grounded in published knowledge base content.
- Answers with citations to the articles used.
- Handover to a human when the bot is not confident, when asked, or after repeated failure.
- Full transcript carried into the handover, so the customer never repeats themselves.
- Deflection measurement.
- Strict grounding: the bot answers from the knowledge base or hands over, and never invents policy.
```

---

## Acceptance criteria

*(Checklist, bullets, Gherkin, etc. Prefilled for Azure DevOps when the work item has acceptance criteria.)*

```
Given a customer opens chat and the chatbot is enabled,
Then the bot greets them in their language and answers from published public articles.

Given the bot answers,
Then it cites the articles used,
And the citations are recorded on the conversation.

Given the question is not covered by any article,
Then the bot says so plainly and offers a human,
Rather than producing a plausible answer it cannot support.

Given the customer asks for a human at any point,
Then handover happens immediately, without the bot trying again.

Given the bot fails to help twice in a row,
Then it offers handover proactively.

Given handover occurs,
Then the full transcript is visible to the agent,
And the handover reason is recorded.

Given no agent is available at handover,
Then a ticket is created from the conversation.

Given the conversation ends,
Then the outcome is recorded as resolved by bot, escalated, or abandoned,
And token usage is recorded for cost reporting.

Given the deflection report,
Then I see how many conversations the bot resolved without a human.

Given the chatbot is disabled,
Then chat behaves exactly as it did before, routing straight to the queue.

Given any customer input,
Then it is treated as untrusted: it cannot change the bot's instructions or make it reveal internal content.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| — | None. |

*(Add rows per file. If none, write "None.")*

---

## Dependencies

- **Blocked by / related ids:** CS-704-suggested-solutions
- **Depends on code areas or other stories:**

- CS-303 for live chat and the SignalR hub.
- CS-604 for knowledge base retrieval.
- CS-701 for the AI service.
- `ChatbotConversation` and `ChatbotMessage` entities — already defined, including `IsBotHandled` and `HandedOverAt` on `ChatSession`.

## Extra notes (optional)

- The bot must only use published public articles. Internal runbooks reaching a customer through the bot would be a serious leak.
- Prompt injection is a real risk on a customer-facing bot. Customer text is data, never instruction.

## Technical hints (optional)

- APIs, screens, services already discussed. Repos/roots: `frontend, backend`. Primary language: `typescript`.

- Stream the response so the customer sees it forming rather than waiting for a complete answer.

## Out of scope

- What this story explicitly does **not** cover:

- The bot taking actions such as issuing refunds or changing orders.
- Voice.
- The bot replying on email or WhatsApp; this story covers chat and portal only.
