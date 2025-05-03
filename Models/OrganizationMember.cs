namespace LoginSystem.Models
{
    public class OrganizationMember
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Role { get; set; } = "Member"; // Vai trò: Member, Admin
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}