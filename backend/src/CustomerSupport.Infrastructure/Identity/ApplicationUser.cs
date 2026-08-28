using CustomerSupport.Domain.Common;
using CustomerSupport.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace CustomerSupport.Infrastructure.Identity;

/// <summary>
/// One Identity table serves agents and customer-portal logins, distinguished by <see cref="UserType"/>.
/// A single table keeps authentication, lockout and MFA behaviour identical for both audiences.
/// </summary>
public class ApplicationUser : IdentityUser<Guid>, IAuditable, ISoftDeletable, ITenantScoped
{
    public UserType UserType { get; set; } = UserType.Agent;

    public LocalizedText DisplayName { get; set; } = new();
    public string? JobTitle { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>Home branch. Access beyond it is granted through <see cref="AccessibleBranchIds"/>.</summary>
    public Guid? BranchId { get; set; }

    /// <summary>Comma-separated additional branch ids. Empty for a single-branch user.</summary>
    public string? AccessibleBranchIds { get; set; }
    public Guid? DepartmentId { get; set; }

    /// <summary>Set only when <see cref="UserType"/> is Customer; links the login to its profile.</summary>
    public Guid? CustomerId { get; set; }

    public string PreferredLanguage { get; set; } = "ar";
    public string? TimeZoneId { get; set; }

    /// <summary>Available, Busy, Away or Offline. Consulted by availability-aware assignment.</summary>
    public string AvailabilityStatus { get; set; } = "Offline";

    /// <summary>Hard cap on concurrently assigned tickets; zero means no limit.</summary>
    public int MaxConcurrentTickets { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset? LastLoginAt { get; set; }
    public string? LastLoginIp { get; set; }
    /// <summary>Forces a password change on next sign-in, used for admin-created accounts.</summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTimeOffset? ModifiedAt { get; set; }
    public Guid? ModifiedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
