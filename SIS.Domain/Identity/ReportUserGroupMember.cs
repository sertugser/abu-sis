namespace SIS.Domain.Identity;

public class ReportUserGroupMember
{
    public int GroupId { get; set; }
    public int UserId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;

    public ReportUserGroup? Group { get; set; }
}
