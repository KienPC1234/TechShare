using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Models;

namespace LoginSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Organization> Organizations { get; set; } = null!;
        public DbSet<OrganizationMember> OrganizationMembers { get; set; } = null!;
        public DbSet<OrganizationJoinRequest> OrganizationJoinRequests { get; set; } = null!;
        public DbSet<OrganizationComment> OrganizationComments { get; set; } = null!;
        public DbSet<OrganizationReport> OrganizationReports { get; set; } = null!;
        public DbSet<OrganizationRating> OrganizationRatings { get; set; } = null!;
        public DbSet<ExchangeItem> ExchangeItems { get; set; } = null!;
        public DbSet<ItemComment> ItemComments { get; set; } = null!;
        public DbSet<ItemRating> ItemRatings { get; set; } = null!;
        public DbSet<ItemReport> ItemReports { get; set; } = null!;
        public DbSet<BorrowOrder> BorrowOrders { get; set; } = null!;
        public DbSet<ItemTag> ItemTags { get; set; } = null!;
        public DbSet<ItemCategory> ItemCategories { get; set; } = null!;
        public DbSet<ItemMedia> ItemMedia { get; set; } = null!;
        public DbSet<Notification> Notifications { get; set; } = null!;
        public DbSet<Message> Messages { get; set; } = null!;
        public DbSet<OrderStatusHistory> StatusHistory { get; set; } = null!;
        public DbSet<OrganizationNews> OrganizationNews { get; set; } = null!;
        public DbSet<OrganizationNewsComment> OrganizationNewsComments { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // ===== ORGANIZATION RELATIONS =====

            // Organization - Member (1:N)
            builder.Entity<OrganizationMember>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(m => m.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Organization - JoinRequest (1:N)
            builder.Entity<OrganizationJoinRequest>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(j => j.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Organization - Comment (1:N)
            builder.Entity<OrganizationComment>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(c => c.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Organization - Report (1:N)
            builder.Entity<OrganizationReport>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(r => r.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // Organization - Rating (1:N)
            builder.Entity<OrganizationRating>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(r => r.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // ExchangeItem - Organization (optional)
            builder.Entity<ExchangeItem>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(e => e.OrganizationId)
                .IsRequired(false) // Make OrganizationId nullable
                .OnDelete(DeleteBehavior.SetNull);

            // OrganizationMember - User
            builder.Entity<OrganizationMember>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(m => m.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrganizationJoinRequest - User
            builder.Entity<OrganizationJoinRequest>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(j => j.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrganizationComment - User
            builder.Entity<OrganizationComment>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrganizationReport - User
            builder.Entity<OrganizationReport>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrganizationRating - User
            builder.Entity<OrganizationRating>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Organization - News (1:N)
            builder.Entity<OrganizationNews>()
                .HasOne<Organization>()
                .WithMany()
                .HasForeignKey(n => n.OrganizationId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrganizationNews - Comment (1:N)
            builder.Entity<OrganizationNewsComment>()
                .HasOne<OrganizationNews>()
                .WithMany()
                .HasForeignKey(c => c.NewsId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrganizationNews - Author
            builder.Entity<OrganizationNews>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(n => n.AuthorId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrganizationNewsComment - User
            builder.Entity<OrganizationNewsComment>()
                .HasOne<ApplicationUser>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== EXCHANGE ITEM RELATIONS =====

            // ItemMedia
            builder.Entity<ItemMedia>()
                .HasOne(m => m.Item)
                .WithMany(e => e.MediaItems)
                .HasForeignKey(m => m.ItemId)
                .OnDelete(DeleteBehavior.NoAction);

            // ExchangeItem
            builder.Entity<ExchangeItem>()
                .HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerId)
                .HasForeignKey(e => e.OwnerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ExchangeItem>()
    .HasOne(e => e.Category)
    .WithMany()
    .HasForeignKey(e => e.CategoryId)
    .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ExchangeItem>()
                .HasIndex(e => e.OwnerId);
            builder.Entity<ExchangeItem>()
                .HasIndex(e => e.OrganizationId);
            builder.Entity<ExchangeItem>()
                .HasIndex(e => e.CategoryId);
            builder.Entity<ExchangeItem>()
                .HasIndex(e => e.CreatedAt);

            // ItemComment
            builder.Entity<ItemComment>()
                .HasOne(c => c.Item)
                .WithMany()
                .HasForeignKey(c => c.ItemId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ItemComment>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ItemComment>()
                .HasIndex(c => c.ItemId);
            builder.Entity<ItemComment>()
                .HasIndex(c => c.CreatedAt);

            // ItemRating
            builder.Entity<ItemRating>()
                .HasOne(r => r.Item)
                .WithMany()
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ItemRating>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ItemRating>()
                .HasIndex(r => r.ItemId);

            // ItemReport
            builder.Entity<ItemReport>()
                .HasOne(r => r.Item)
                .WithMany(i => i.Reports)
                .HasForeignKey(r => r.ItemId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<ItemReport>()
                .HasOne(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ItemReport>()
                .HasIndex(r => r.ItemId);
            builder.Entity<ItemReport>()
                .HasIndex(r => r.ReportedAt);

            // BorrowOrder
            builder.Entity<BorrowOrder>()
                .HasOne(o => o.Item)
                .WithMany()
                .HasForeignKey(o => o.ItemId)
               .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<BorrowOrder>()
                .HasOne(o => o.Borrower)
                .WithMany()
                .HasForeignKey(o => o.BorrowerId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<BorrowOrder>()
                .HasOne(o => o.DeliveryAgent)
                .WithMany()
                .HasForeignKey(o => o.DeliveryAgentId)
                .OnDelete(DeleteBehavior.NoAction);

            builder.Entity<BorrowOrder>()
                .HasIndex(o => o.ItemId);
            builder.Entity<BorrowOrder>()
                .HasIndex(o => o.BorrowerId);
            builder.Entity<BorrowOrder>()
                .HasIndex(o => o.DeliveryAgentId);
            builder.Entity<BorrowOrder>()
                .HasIndex(o => o.Status);

            // ItemTag
            builder.Entity<ItemTag>()
                .HasOne(t => t.Item)
                .WithMany(t => t.Tags)
                .HasForeignKey(t => t.ItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ItemTag>()
                .HasIndex(t => t.Tag);

            // Notification
            builder.Entity<Notification>()
                .HasIndex(n => n.UserId);
            builder.Entity<Notification>()
                .HasIndex(n => n.CreatedAt);
            builder.Entity<Notification>()
                .HasIndex(n => n.OrganizationId);

            // Message
            builder.Entity<Message>()
                .HasIndex(m => m.SenderId)
                .HasDatabaseName("IX_Messages_SenderId");

            builder.Entity<Message>()
                .HasIndex(m => m.ReceiverId)
                .HasDatabaseName("IX_Messages_ReceiverId");

            builder.Entity<Message>()
                .HasIndex(m => m.CreatedAt)
                .HasDatabaseName("IX_Messages_CreatedAt");

            builder.Entity<Message>()
                .HasOne(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<Message>()
                .HasOne(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .OnDelete(DeleteBehavior.Cascade);

            // OrderStatusHistory
            builder.Entity<OrderStatusHistory>()
                .HasOne(o => o.Order)
                .WithMany(o => o.StatusHistory)
                .HasForeignKey(o => o.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderStatusHistory>()
                .HasIndex(o => o.OrderId);
            builder.Entity<OrderStatusHistory>()
                .HasIndex(o => o.ChangedAt);
        }
    }
}