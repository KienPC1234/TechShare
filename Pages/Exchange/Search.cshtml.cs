using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using LoginSystem.Data;
using LoginSystem.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoginSystem.Pages.Exchange
{
    [Authorize]
    public class SearchModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SearchModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        }

        [BindProperty(SupportsGet = true)]
        public string Query { get; set; } = string.Empty;

        public List<SearchResult> Results { get; private set; } = new List<SearchResult>();

        public string? ErrorMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return Page();
            }

            // Get user's organization ID (each user belongs to only one organization)
            var userOrgId = user.OrganizationId;

            if (string.IsNullOrWhiteSpace(Query))
            {
                // Return all available items if no query
                var items = await _context.ExchangeItems
                    .Include(i => i.Tags)
                    .Include(i => i.Category)
                    .Include(i => i.MediaItems)
                    .Where(i => i.QuantityAvailable > 0 && (!i.IsPrivate || i.OrganizationId == userOrgId))
                    .ToListAsync();

                Results = await ProcessSearchResults(items, Query);
            }
            else
            {
                // Clean query for search
                var cleanedQuery = Query.Trim().ToLower();

                // Fetch items matching the query
                var items = await _context.ExchangeItems
                    .Include(i => i.Tags)
                    .Include(i => i.Category)
                    .Include(i => i.MediaItems)
                    .Where(i => i.QuantityAvailable > 0 && (!i.IsPrivate || i.OrganizationId == userOrgId))
                    .Where(i => i.Title.ToLower().Contains(cleanedQuery)
                             || i.Description.ToLower().Contains(cleanedQuery)
                             || i.Tags.Any(t => t.Tag.ToLower().Contains(cleanedQuery)))
                    .ToListAsync();

                Results = await ProcessSearchResults(items, cleanedQuery);
            }

            return Page();
        }

        private async Task<List<SearchResult>> ProcessSearchResults(List<ExchangeItem> items, string cleanedQuery)
        {
            var results = new List<SearchResult>();

            foreach (var item in items)
            {
                // Calculate RelevanceScore on client-side
                double relevanceScore = 0;
                if (!string.IsNullOrWhiteSpace(cleanedQuery))
                {
                    if (item.Title.ToLower().Contains(cleanedQuery)) relevanceScore += 0.5;
                    if (item.Description.ToLower().Contains(cleanedQuery)) relevanceScore += 0.3;
                    if (item.Tags.Any(t => t.Tag.ToLower().Contains(cleanedQuery))) relevanceScore += 0.2;
                }

                // Calculate AvgRating
                var ratings = await _context.ItemRatings
                    .Where(r => r.ItemId == item.Id)
                    .ToListAsync();
                double avgRating = ratings.Any() ? ratings.Average(r => r.Score) : 0;

                results.Add(new SearchResult
                {
                    Item = item,
                    RelevanceScore = relevanceScore,
                    AvgRating = avgRating
                });
            }

            // Sort results by RelevanceScore (descending) and then by AvgRating (descending)
            return results.OrderByDescending(r => r.RelevanceScore)
                         .ThenByDescending(r => r.AvgRating)
                         .ToList();
        }

        public class SearchResult
        {
            public ExchangeItem Item { get; set; } = null!;
            public double RelevanceScore { get; set; }
            public double AvgRating { get; set; }
        }
    }
}