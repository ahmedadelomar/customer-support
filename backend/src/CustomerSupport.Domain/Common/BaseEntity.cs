namespace CustomerSupport.Domain.Common;

/// <summary>Base for every persisted aggregate/entity. Uses sequential GUID keys assigned by the database.</summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; }
}
