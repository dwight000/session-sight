namespace SessionSight.Core.Entities;

public class Therapist
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? LicenseNumber { get; set; }
    public string? Credentials { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
