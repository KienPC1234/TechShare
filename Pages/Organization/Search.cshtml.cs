using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using LoginSystem.Data;
using LoginSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using HtmlAgilityPack;

namespace LoginSystem.Pages.Organization
{
    public class SearchModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<SearchModel> _logger;

        public SearchModel(ApplicationDbContext dbContext, ILogger<SearchModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
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
            public string ShortDescription { get; set; } = string.Empty;
        }

        public async Task OnGetAsync()
        {
            await SearchOrganizationsAsync();
        }

        public async Task<IActionResult> OnGetSearchOrganizationsAsync()
        {
            try
            {
                var result = await SearchOrganizationsAsync();
                return new JsonResult(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in SearchOrganizations: {Message}", ex.Message);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        private async Task<object> SearchOrganizationsAsync()
        {
            const int pageSize = 9;
            var query = _dbContext.Organizations.AsNoTracking().AsQueryable();

            try
            {
                if (!string.IsNullOrWhiteSpace(SearchTerm))
                {
                    var searchLower = SearchTerm.ToLower();
                    query = query.Where(o => o.Name.ToLower().Contains(searchLower) ||
                                            (o.Description != null && o.Description.ToLower().Contains(searchLower)));
                }

                if (FilterType == "Public")
                    query = query.Where(o => !o.IsPrivate);
                else if (FilterType == "Private")
                    query = query.Where(o => o.IsPrivate);

                var totalOrgs = await query.CountAsync();
                TotalPages = (int)Math.Ceiling(totalOrgs / (double)pageSize);

                // Validate PageIndex
                if (PageIndex < 1) PageIndex = 1;
                if (PageIndex > TotalPages && TotalPages > 0) PageIndex = TotalPages;

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

                    var shortDescription = string.Empty;
                    if (!string.IsNullOrEmpty(org.Description))
                    {
                        var htmlDoc = new HtmlDocument();
                        htmlDoc.LoadHtml(org.Description);
                        var plainText = htmlDoc.DocumentNode.InnerText;
                        if (!string.IsNullOrWhiteSpace(SearchTerm))
                        {
                            var searchLower = SearchTerm.ToLower();
                            var index = plainText.ToLower().IndexOf(searchLower);
                            if (index >= 0)
                            {
                                var start = Math.Max(0, index - 50);
                                var length = Math.Min(120, plainText.Length - start);
                                shortDescription = plainText.Substring(start, length);
                                if (length == 120) shortDescription += "...";
                                var escapedSearch = System.Text.RegularExpressions.Regex.Escape(SearchTerm);
                                shortDescription = System.Text.RegularExpressions.Regex.Replace(shortDescription, $"(?i){escapedSearch}", $"<mark>$0</mark>");
                            }
                        }
                        if (string.IsNullOrEmpty(shortDescription))
                        {
                            shortDescription = plainText.Length > 120 ? plainText.Substring(0, 120) + "..." : plainText;
                        }
                    }

                    result.Add(new OrganizationInfo
                    {
                        Organization = org,
                        AverageRating = ratings.Any() ? ratings.Average() : 0,
                        MemberCount = memberCount,
                        ShortDescription = shortDescription
                    });
                }

                Organizations = SortBy switch
                {
                    "MembersDesc" => result.OrderByDescending(o => o.MemberCount).ToList(),
                    "RatingDesc" => result.OrderByDescending(o => o.AverageRating).ToList(),
                    "NameAsc" => result.OrderBy(o => o.Organization?.Name).ToList(),
                    "NameDesc" => result.OrderByDescending(o => o.Organization?.Name).ToList(),
                    _ => result.OrderByDescending(o => o.AverageRating * 0.7 + o.MemberCount * 0.3).ToList(),
                };

                return new
                {
                    organizations = Organizations.Select(o => new
                    {
                        organization = new
                        {
                            o.Organization.Id,
                            o.Organization.Name,
                            o.Organization.Slug,
                            o.Organization.AvatarUrl,
                            o.Organization.IsPrivate
                        },
                        o.AverageRating,
                        o.MemberCount,
                        o.ShortDescription
                    }).ToList(),
                    totalPages = TotalPages,
                    pageIndex = PageIndex
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching organizations: {Message}", ex.Message);
                throw;
            }
        }
    }
}