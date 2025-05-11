using System;
using System.ComponentModel.DataAnnotations;

namespace LoginSystem.Models
{
    public class Notification
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? ItemId { get; set; } // For exchange item-related notifications

        public string? OrderId { get; set; } // For borrow order-related notifications

        public string? OrganizationId { get; set; } // For organization-related notifications

        public string? Type { get; set; } // e.g., "ItemComment", "Order", "OrganizationJoin", "OrganizationRoleChange"
    }
}