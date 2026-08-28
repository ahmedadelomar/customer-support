# platform — plan overview

Entry point for the **Section 12 — Platform** feature. Stories execute in order by their `NN` prefix.

Cross-cutting capabilities every other feature assumes: bilingual UI and data, responsive layout,
department and branch scoping, and runtime branding. Placed at NN 05–09 because retrofitting RTL, tenancy
or department scoping after the business features exist means touching every screen twice.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 05 | [05-story-arabic-english-i18n-CS-1201-arabic-english-i18n.md](05-story-arabic-english-i18n-CS-1201-arabic-english-i18n.md) | Bilingual UI and data (Arabic and English) | CS-1201-arabic-english-i18n | 01 |
| 06 | [06-story-responsive-web-mobile-CS-1202-responsive-web-mobile.md](06-story-responsive-web-mobile-CS-1202-responsive-web-mobile.md) | Responsive layout and accessibility | CS-1202-responsive-web-mobile | 05 |
| 07 | [07-story-multi-department-CS-1203-multi-department.md](07-story-multi-department-CS-1203-multi-department.md) | Departments, teams and queue scoping | CS-1203-multi-department | 01 |
| 08 | [08-story-multi-branch-CS-1204-multi-branch.md](08-story-multi-branch-CS-1204-multi-branch.md) | Branch scoping and the branch switcher | CS-1204-multi-branch | 07 |
| 09 | [09-story-custom-branding-CS-1205-custom-branding.md](09-story-custom-branding-CS-1205-custom-branding.md) | Runtime branding and theming | CS-1205-custom-branding | 08 |

## Dependency notes

- **Depends on security-administration 01–02.** Branch and department scoping read claims that story 01 issues.
- **Stories 05 and 06 are conventions, not just features.** Once they land, every later story must follow them: bilingual strings in both i18n files, logical CSS properties, and no hard-coded text.
- Stories 07 (multi-department) and 08 (multi-branch) define scoping rules that every list query in the product depends on. Landing them before the ticket features avoids reworking those queries.
- Story 07 references the `Ticket` entity, which is created in CS-201. Implement the department parts of the ticket in CS-201 against the rules defined here; the two stories should be sequenced together if CS-201 is already in flight.
- Much of this feature is already scaffolded — `LanguageService`, `DataTableComponent`, `WhereBranchAccessible` and the CSS custom properties all exist. These stories complete and enforce them rather than starting from nothing.
