namespace CustomerSupport.Application.Common.Security;

/// <summary>
/// The single source of truth for permission keys (Security and Administration / Permissions).
/// Seeding reads this registry, the API authorises against it, and the Angular
/// <c>hasPermission</c> directive mirrors the same strings, so a key exists in exactly one place.
/// </summary>
public static class Permissions
{
    public static class Customers
    {
        public const string View = "customers.view";
        public const string Create = "customers.create";
        public const string Update = "customers.update";
        public const string Delete = "customers.delete";
        public const string ManageContacts = "customers.contacts.manage";
        public const string ViewNotes = "customers.notes.view";
        public const string ManageNotes = "customers.notes.manage";
        public const string ViewHistory = "customers.history.view";
        public const string Export = "customers.export";
    }

    public static class Tickets
    {
        public const string View = "tickets.view";
        public const string ViewAll = "tickets.view.all";
        public const string Create = "tickets.create";
        public const string Update = "tickets.update";
        public const string Delete = "tickets.delete";
        public const string Assign = "tickets.assign";
        public const string ChangeStatus = "tickets.status.change";
        public const string Escalate = "tickets.escalate";
        public const string Reply = "tickets.reply";
        public const string InternalNote = "tickets.note.internal";
        public const string Merge = "tickets.merge";
        public const string ViewHistory = "tickets.history.view";
        public const string ManageCategories = "tickets.categories.manage";
        public const string ManagePriorities = "tickets.priorities.manage";
        public const string ManageStatuses = "tickets.statuses.manage";
    }

    public static class Channels
    {
        public const string View = "channels.view";
        public const string Manage = "channels.manage";
        public const string ManageWebForms = "channels.webforms.manage";
        public const string HandleLiveChat = "channels.livechat.handle";
    }

    public static class Workspace
    {
        public const string ViewOwnTasks = "workspace.tasks.view";
        public const string ManageTasks = "workspace.tasks.manage";
        public const string AssignTasks = "workspace.tasks.assign";
        public const string ManageQuickReplies = "workspace.quickreplies.manage";
        public const string ManageGlobalQuickReplies = "workspace.quickreplies.manage.global";
        public const string Collaborate = "workspace.collaborate";
    }

    public static class Sla
    {
        public const string View = "sla.view";
        public const string ManagePolicies = "sla.policies.manage";
        public const string ManageCalendars = "sla.calendars.manage";
        public const string ManageAssignmentRules = "sla.assignment.manage";
        public const string ManageEscalationRules = "sla.escalation.manage";
    }

    public static class KnowledgeBase
    {
        public const string View = "kb.view";
        public const string Author = "kb.author";
        public const string Publish = "kb.publish";
        public const string Delete = "kb.delete";
        public const string ManageCategories = "kb.categories.manage";
        public const string ViewAnalytics = "kb.analytics.view";
    }

    public static class Ai
    {
        public const string UseSuggestions = "ai.suggestions.use";
        public const string ViewUsage = "ai.usage.view";
        public const string Configure = "ai.configure";
        public const string ManageChatbot = "ai.chatbot.manage";
    }

    public static class Reports
    {
        public const string ViewTickets = "reports.tickets.view";
        public const string ViewSla = "reports.sla.view";
        public const string ViewAgentPerformance = "reports.agents.view";
        public const string ViewSatisfaction = "reports.satisfaction.view";
        public const string ViewDashboards = "reports.dashboards.view";
        public const string ManageDashboards = "reports.dashboards.manage";
        public const string Schedule = "reports.schedule";
        public const string Export = "reports.export";
    }

    public static class Administration
    {
        public const string ViewUsers = "admin.users.view";
        public const string ManageUsers = "admin.users.manage";
        public const string ManageRoles = "admin.roles.manage";
        public const string ManagePermissions = "admin.permissions.manage";
        public const string ViewAuditLogs = "admin.audit.view";
        public const string ManageSettings = "admin.settings.manage";
        public const string ManageBranches = "admin.branches.manage";
        public const string ManageDepartments = "admin.departments.manage";
        public const string ManageTeams = "admin.teams.manage";
        public const string ManageBranding = "admin.branding.manage";
    }

    public static class Integrations
    {
        public const string View = "integrations.view";
        public const string Manage = "integrations.manage";
        public const string ManageApiClients = "integrations.apiclients.manage";
        public const string ManageWebhooks = "integrations.webhooks.manage";
        public const string ViewSyncLogs = "integrations.synclogs.view";
    }

    /// <summary>Every declared key, discovered by reflection. Used by the seeder and the admin UI.</summary>
    public static IReadOnlyList<(string Category, string Key)> All { get; } = Discover();

    private static List<(string, string)> Discover()
    {
        var result = new List<(string, string)>();
        foreach (var group in typeof(Permissions).GetNestedTypes())
        {
            foreach (var field in group.GetFields())
            {
                if (field is { IsLiteral: true, IsInitOnly: false } && field.GetRawConstantValue() is string key)
                {
                    result.Add((group.Name, key));
                }
            }
        }

        return result;
    }
}
