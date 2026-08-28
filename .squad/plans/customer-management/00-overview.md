# customer-management — plan overview

Entry point for the **Section 1 — Customer Management** feature. Stories execute in order by their `NN` prefix.

The customer record everything else hangs off. Story 10 is **already implemented end to end in the
skeleton** and is the reference pattern the rest of the product copies — read its code before writing any
other feature. Stories 11–13 complete the profile with contacts, the unified timeline and notes with files.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 10 | [10-story-customer-profiles-CS-101-customer-profiles.md](10-story-customer-profiles-CS-101-customer-profiles.md) | Customer profiles (reference vertical slice) | CS-101-customer-profiles | 01 |
| 11 | [11-story-contact-details-CS-102-contact-details.md](11-story-contact-details-CS-102-contact-details.md) | Contact details management | CS-102-contact-details | 10 |
| 12 | [12-story-interaction-history-CS-103-interaction-history.md](12-story-interaction-history-CS-103-interaction-history.md) | Unified interaction timeline | CS-103-interaction-history | 10 |
| 13 | [13-story-notes-attachments-CS-104-notes-attachments.md](13-story-notes-attachments-CS-104-notes-attachments.md) | Customer notes and the shared attachment store | CS-104-notes-attachments | 10 |

## Dependency notes

- **Story 10 is largely built.** `Customer`, `CustomersService`, `CustomerListPage`, `CustomerFormPage`, `CustomerDetailPage` and the API slice all exist. Its plan covers finishing it (branch scoping, export) and confirms it as the reference.
- Stories 11–13 each fill one tab on the customer detail page. The tab shell and the placeholder state already exist in `customer-detail.page.html`.
- **Story 13 (notes and attachments) builds the attachment store the whole product reuses** — ticket messages, KB articles and branding logos all depend on `IFileStorage` and the `(OwnerType, OwnerId)` model. Do not build a second file mechanism later.
- Story 12 (interaction history) is a projection written by other features. Until Section 3 lands, only ticket-sourced entries appear; that is expected, not a bug.
- All four depend on security-administration 01 for permissions and platform 08 for branch scoping.
