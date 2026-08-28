using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Identity;

/// <summary>Join row granting a <see cref="Permission"/> to an ASP.NET Identity role.</summary>
public class RolePermission : BaseEntity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = null!;

    public DateTimeOffset GrantedAt { get; set; }
    public Guid? GrantedById { get; set; }
}
