#nullable enable
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace LoginSystem.Pages.Organization
{
    public class SearchModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;

        public SearchModel(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public IList<OrganizationInfo> Organizations { get; set; } = new List<OrganizationInfo>();

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public string FilterType { get; set; } = "All";

        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "Featured";

        [BindProperty(SupportsGet = true)]
        public int PageIndex { get; set; } = 1;

        public int TotalPages { get; set; }

        public class OrganizationInfo
        {
            public Models.Organization? Organization { get; set; }
            public double AverageRating { get; set; }
            public int MemberCount { get; set; }
        }

        public async Task OnGetAsync()
        {
            const int pageSize = 9;
            var query = _dbContext.Organizations.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
                query = query.Where(o => o.Name.Contains(SearchTerm));

            if (FilterType == "Public")
                query = query.Where(o => !o.IsPrivate);
            else if (FilterType == "Private")
                query = query.Where(o => o.IsPrivate);

            var totalOrgs = await query.CountAsync();
            TotalPages = (int)Math.Ceiling(totalOrgs / (double)pageSize);

            var orgs = await query
                .Skip((PageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var result = new List<OrganizationInfo>();

            foreach (var org in orgs)
            {
                var ratings = await _dbContext.OrganizationRatings
                    .Where(r => r.OrganizationId == org.Id)
                    .Select(r => r.Score)
                    .ToListAsync();

                var memberCount = await _dbContext.OrganizationMembers
                    .CountAsync(m => m.OrganizationId == org.Id);

                result.Add(new OrganizationInfo
                {
                    Organization = org,
                    AverageRating = ratings.Any() ? ratings.Average() : 0,
                    MemberCount = memberCount
                });
            }

            Organizations = SortBy switch
            {
                "MembersDesc" => result.OrderByDescending(o => o.MemberCount).ToList(),
                "RatingDesc" => result.OrderByDescending(o => o.AverageRating).ToList(),
                "NameAsc" => result.OrderBy(o => o.Organization?.Name).ToList(),
                "NameDesc" => result.OrderByDescending(o => o.Organization?.Name).ToList(),
                _ => result
                    .OrderByDescending(o => o.AverageRating * 0.7 + o.MemberCount * 0.3)
                    .ToList(), // "Featured"
            };
        }
    }
}
