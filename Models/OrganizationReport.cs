namespace LoginSystem.Models
{
    public class OrganizationReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
    }
}