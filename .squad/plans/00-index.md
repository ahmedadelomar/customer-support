# Plans index

One row per feature folder under `.squad/plans/`. `NN` continues as a global execution sequence across all features when `naming.globalSequence` is `true` in `config.yaml`.

Features are ordered by **execution order**, not by the section numbering in the source document. Security and platform foundations come first because every later story depends on their permission model, bilingual conventions and branch scoping — retrofitting those after the business features exist means touching every screen twice. Each feature's display name records its original section number.

| Feature | Overview | NN range | Source section |
|---------|----------|----------|----------------|
| security-administration | [security-administration/00-overview.md](security-administration/00-overview.md) | 01–04 | 10 |
| platform | [platform/00-overview.md](platform/00-overview.md) | 05–09 | 12 |
| customer-management | [customer-management/00-overview.md](customer-management/00-overview.md) | 10–13 | 1 |
| ticket-management | [ticket-management/00-overview.md](ticket-management/00-overview.md) | 14–18 | 2 |
| communication-channels | [communication-channels/00-overview.md](communication-channels/00-overview.md) | 19–23 | 3 |
| agent-dashboard | [agent-dashboard/00-overview.md](agent-dashboard/00-overview.md) | 24–28 | 4 |
| sla-automation | [sla-automation/00-overview.md](sla-automation/00-overview.md) | 29–32 | 5 |
| knowledge-base | [knowledge-base/00-overview.md](knowledge-base/00-overview.md) | 33–36 | 6 |
| customer-portal | [customer-portal/00-overview.md](customer-portal/00-overview.md) | 37–41 | 8 |
| ai-features | [ai-features/00-overview.md](ai-features/00-overview.md) | 42–46 | 7 |
| reports-management | [reports-management/00-overview.md](reports-management/00-overview.md) | 47–51 | 9 |
| integrations | [integrations/00-overview.md](integrations/00-overview.md) | 52–55 | 11 |

## Critical path

Not every story is equally blocking. These are the ones that unblock the most work:

| NN | Story | Unblocks |
|----|-------|----------|
| 01 | Users, roles and sign-in | Everything. Also creates the `InitialCreate` migration for the whole schema. |
| 02 | Permission catalogue and role editor | Every route guard and `RequirePermission` attribute. |
| 05 | Bilingual UI and data | Every screen. Retrofitting RTL is far more expensive than building with it. |
| 10 | Customer profiles | Tickets, portal, reporting. Already implemented as the reference slice. |
| 14 | Ticket creation, list and detail | Sections 3, 4, 5, 7 and 9 in their entirety. |
| 29 | SLA policies and clocks | Escalation, SLA reporting, dashboard urgency ordering. |
| 32 | Notifications | Tasks, collaboration, escalation and reporting all dispatch through it. |

Stories 32 (notifications) and 05 (i18n) have no dependency on the features that need them, so both can be pulled forward if other work is blocked.

## Recurring invariants

These constraints appear across many plans. Breaking one in a single story tends to break the product quietly:

- **Every ticket mutation appends a `TicketEvent`** in the same transaction as the change (18).
- **Every customer-visible exchange appends an `Interaction`** in the same transaction (12).
- **Portal endpoints use their own DTOs.** Reusing an agent DTO leaks the next field someone adds to it (38).
- **Averages come from stored sums and counts**, never from averaging averages (47).
- **SLA clocks resume, never restart**, after a pause or a reopen (17, 29).
- **Branch scoping is opt-in per query** (`WhereBranchAccessible`), because jobs and cross-branch reports need the whole table (08).
- **Bilingual strings go in both `en.json` and `ar.json`**, enforced by a key-parity check (05).
- **AI is advisory and must degrade gracefully.** A failed model call never breaks a screen (42).

## Working the plans

1. Read the feature's `00-overview.md` for sequence and dependency notes.
2. Open **one** `NN-story-*.md` in a fresh, scoped agent session — attach only that file.
3. Follow its **Context — Read These Files First** section before writing code.
4. Work the Backend Tasks, then the Frontend Tasks, in order.
5. Run the Verification Steps and tick the Done Criteria.
6. Stop at the end of the story and report, as each plan instructs.
