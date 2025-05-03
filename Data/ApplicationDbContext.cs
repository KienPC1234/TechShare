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

        public DbSet<Organization> Organizations { get; set; }
        public DbSet<OrganizationMember> OrganizationMembers { get; set; }
        public DbSet<OrganizationJoinRequest> OrganizationJoinRequests { get; set; }
        public DbSet<OrganizationComment> OrganizationComments { get; set; }
        public DbSet<OrganizationReport> OrganizationReports { get; set; }
        public DbSet<OrganizationRating> OrganizationRatings { get; set; }
        public DbSet<Notification> Notifications { get; set; }
    }
}