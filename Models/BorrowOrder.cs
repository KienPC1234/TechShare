// BorrowOrder.cs
using LoginSystem.Models;
using System.ComponentModel.DataAnnotations;

public class BorrowOrder
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Required]
    public string ItemId { get; set; } = string.Empty;
    [Required]
    public string BorrowerId { get; set; } = string.Empty;
    public string? DeliveryAgentId { get; set; }
    [Required]
    [MaxLength(500)]
    public string ShippingAddress { get; set; } = string.Empty;
    public string? PaymentInfo { get; set; }
    [Required]
    public bool TermsAccepted { get; set; }
    [Required]
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public ExchangeItem Item { get; set; } = null!;
    public ApplicationUser Borrower { get; set; } = null!;
    public ApplicationUser? DeliveryAgent { get; set; }
    public List<OrderStatusHistory> StatusHistory { get; set; } = new List<OrderStatusHistory>();
}

// OrderStatusHistory.cs
public class OrderStatusHistory
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    [Required]
    public string OrderId { get; set; } = string.Empty;
    [Required]
    public string Status { get; set; } = string.Empty;
    [Required]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? ChangedByUserId { get; set; }
    public string? Note { get; set; }
    public BorrowOrder Order { get; set; } = null!;
}