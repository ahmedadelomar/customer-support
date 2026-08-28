namespace CustomerSupport.Domain.Common;

/// <summary>Stamped automatically by <c>AppDbContext.SaveChangesAsync</c> from the current user + clock.</summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; set; }
    Guid? CreatedById { get; set; }
    DateTimeOffset? ModifiedAt { get; set; }
    Guid? ModifiedById { get; set; }
}
