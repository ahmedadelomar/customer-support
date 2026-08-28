namespace CustomerSupport.Domain.Common;

/// <summary>Entities excluded by a global query filter once <see cref="IsDeleted"/> is set.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTimeOffset? DeletedAt { get; set; }
    Guid? DeletedById { get; set; }
}
