namespace LoginSystem.Models
{
    public class OrganizationJoinRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    }
}