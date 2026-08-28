using CustomerSupport.Domain.Common;

namespace CustomerSupport.Domain.Identity;

/// <summary>
/// Competency of an agent in a ticket category, consumed by
/// <c>AssignmentStrategy.SkillBased</c> to shortlist candidates.
/// </summary>
public class AgentSkill : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CategoryId { get; set; }
    /// <summary>1 (novice) – 5 (expert). Higher levels win ties during skill-based routing.</summary>
    public int Level { get; set; } = 3;
}
