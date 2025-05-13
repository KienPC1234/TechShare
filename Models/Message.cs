using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoginSystem.Models
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [ForeignKey("Sender")]
        public string SenderId { get; set; } = string.Empty;

        [Required]
        [ForeignKey("Receiver")]
        public string ReceiverId { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public string ContentType { get; set; } = "text";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ApplicationUser Sender { get; set; } = null!;
        public ApplicationUser Receiver { get; set; } = null!;
    }
}