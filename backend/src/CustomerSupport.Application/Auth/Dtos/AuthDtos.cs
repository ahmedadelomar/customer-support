namespace CustomerSupport.Application.Auth.Dtos;

/// <summary>
/// Shape returned by login and refresh. Mirrors <c>AuthResult</c> in
/// <c>frontend/src/app/core/auth/auth.models.ts</c> — the client was written against this contract,
/// so the two must stay in step.
/// </summary>
public record AuthResultDto
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public AuthenticatedUserDto User { get; init; } = new();
}

public record AuthenticatedUserDto
{
    public Guid Id { get; init; }
    public string UserName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DisplayNameEn { get; init; } = string.Empty;
    public string DisplayNameAr { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public int UserType { get; init; }
    public Guid? BranchId { get; init; }
    public IReadOnlyList<Guid> AccessibleBranchIds { get; init; } = Array.Empty<Guid>();
    public Guid? DepartmentId { get; init; }
    public Guid? CustomerId { get; init; }
    public string PreferredLanguage { get; init; } = "ar";
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Permissions { get; init; } = Array.Empty<string>();
    public bool MustChangePassword { get; init; }
}
