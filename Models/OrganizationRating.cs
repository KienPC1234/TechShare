namespace LoginSystem.Models
{
    public class OrganizationRating
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string OrganizationId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public int Score { get; set; } // Điểm đánh giá từ 1-5
        public DateTime RatedAt { get; set; } = DateTime.UtcNow;
    }
}