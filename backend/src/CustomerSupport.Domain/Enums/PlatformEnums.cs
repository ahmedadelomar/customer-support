namespace CustomerSupport.Domain.Enums;

public enum CustomerType
{
    Individual = 0,
    Company = 1,
    Government = 2,
}

/// <summary>Kinds of value stored on <c>CustomerContact</c>.</summary>
public enum ContactType
{
    Email = 0,
    Mobile = 1,
    Phone = 2,
    WhatsApp = 3,
    Address = 4,
    Website = 5,
    Other = 6,
}

/// <summary>Distinguishes agent logins from customer-portal logins on the shared Identity table.</summary>
public enum UserType
{
    Agent = 0,
    Customer = 1,
    ServiceAccount = 2,
}

public enum AgentTaskStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}

public enum NotificationChannel
{
    InApp = 0,
    Email = 1,
    Sms = 2,
    Push = 3,
    WhatsApp = 4,
}

public enum IntegrationType
{
    Erp = 0,
    EmailProvider = 1,
    SmsProvider = 2,
    WhatsAppProvider = 3,
    Crm = 4,
    Sso = 5,
    Storage = 6,
    Other = 7,
}

public enum IntegrationStatus
{
    NotConfigured = 0,
    Connected = 1,
    Degraded = 2,
    Failed = 3,
    Disabled = 4,
}

public enum SyncDirection
{
    Inbound = 0,
    Outbound = 1,
    Bidirectional = 2,
}

/// <summary>Audit trail verbs (Security &amp; Administration / Audit logs).</summary>
public enum AuditAction
{
    Create = 0,
    Update = 1,
    Delete = 2,
    Read = 3,
    Login = 4,
    LoginFailed = 5,
    Logout = 6,
    PermissionChanged = 7,
    Export = 8,
    ConfigChanged = 9,
}
