# ai-features — plan overview

Entry point for the **Section 7 — AI Features** feature. Stories execute in order by their `NN` prefix.

AI as an assistant to agents, never as an unattended decision-maker. Every suggestion is advisory
until a human accepts it, every generation is logged with its cost, and every feature degrades gracefully when
the model is slow or unavailable — a support desk must keep working when the AI does not.

The one exception is the chatbot, which talks to customers directly; it is therefore held to a stricter rule:
answer from published knowledge base content or hand over, never improvise.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 42 | [42-story-ticket-summaries-CS-701-ticket-summaries.md](42-story-ticket-summaries-CS-701-ticket-summaries.md) | AI service foundation and ticket summaries | CS-701-ticket-summaries | 14 |
| 43 | [43-story-suggested-replies-CS-702-suggested-replies.md](43-story-suggested-replies-CS-702-suggested-replies.md) | Grounded suggested replies | CS-702-suggested-replies | 42 |
| 44 | [44-story-automatic-categorization-CS-703-automatic-categorization.md](44-story-automatic-categorization-CS-703-automatic-categorization.md) | Automatic ticket categorisation and priority suggestion | CS-703-automatic-categorization | 42 |
| 45 | [45-story-suggested-solutions-CS-704-suggested-solutions.md](45-story-suggested-solutions-CS-704-suggested-solutions.md) | Semantic solution retrieval | CS-704-suggested-solutions | 44 |
| 46 | [46-story-ai-chatbot-CS-705-ai-chatbot.md](46-story-ai-chatbot-CS-705-ai-chatbot.md) | Customer-facing AI chatbot with handover | CS-705-ai-chatbot | 45 |

## Dependency notes

- **Story 42 builds `IAiCompletionService` and the review flow.** Stories 43–46 use it. Do not let any of them call the model directly.
- **Everything must degrade gracefully.** A failed or slow model call must never break the ticket screen, block ticket creation, or empty the solutions panel. Each story states its fallback explicitly.
- Story 45 extends the CS-603 suggestion panel through its `Source` field rather than replacing it. If CS-603 has not landed, build the rule-based panel first — an AI-only panel with no fallback violates the graceful-degradation rule.
- Story 46 (chatbot) is customer-facing and carries the highest risk in the product: prompt injection, hallucinated policy, and leaking internal articles. Its plan treats those as first-class requirements, not caveats.
- `ai.enabled` (CS-1004) is the master switch. When off, no generation occurs and the UI hides AI controls entirely rather than showing dead buttons.
- All spend is tracked on `AiSuggestion` and `ChatbotConversation`. The AI usage report (CS-905) reads them.
