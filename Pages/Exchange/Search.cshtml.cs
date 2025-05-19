using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
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

        [BindProperty(SupportsGet = true)]
        public string CategoryId { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string Tags { get; set; } = string.Empty;

        [BindProperty(SupportsGet = true)]
        public string SortBy { get; set; } = "relevance";

        [BindProperty(SupportsGet = true)]
        public int Page { get; set; } = 1;

        public List<SearchResult> Results { get; private set; } = new List<SearchResult>();
        public string? ErrorMessage { get; private set; }
        public List<SelectListItem> Categories { get; private set; } = new List<SelectListItem>();
        public int CurrentPage { get; private set; }
        public int TotalPages { get; private set; }
        private const int PageSize = 9;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                ErrorMessage = "Không tìm thấy người dùng.";
                return Page();
            }

            // Load categories for dropdown
            Categories = await _context.ItemCategories
                .Select(c => new SelectListItem { Value = c.Id, Text = c.Name })
                .ToListAsync();

            var userOrgId = user.OrganizationId;
            var query = _context.ExchangeItems
                .Include(i => i.Tags)
                .Include(i => i.Category)
                .Include(i => i.MediaItems)
                .Where(i => i.QuantityAvailable > 0 && (!i.IsPrivate || i.OrganizationId == userOrgId));

            

            // Apply filters
            if (!string.IsNullOrWhiteSpace(Query))
            {
                var cleanedQuery = Query.Trim().ToLower();
                query = query.Where(i => i.Title.ToLower().Contains(cleanedQuery)
                                     || i.Description.ToLower().Contains(cleanedQuery)
                                     || i.Tags.Any(t => t.Tag.ToLower().Contains(cleanedQuery)));
            }

            if (!string.IsNullOrWhiteSpace(CategoryId))
            {
                query = query.Where(i => i.CategoryId == CategoryId);
            }

            if (!string.IsNullOrWhiteSpace(Tags))
            {
                var tagList = Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(t => t.Trim().ToLower())
                    .ToList();
                query = query.Where(i => i.Tags.Any(t => tagList.Contains(t.Tag.ToLower())));
            }

            // Get total count for pagination
            var totalItems = await query.CountAsync();
            TotalPages = (int)Math.Ceiling((double)totalItems / PageSize);
            CurrentPage = Math.Max(1, Math.Min(Page, TotalPages));

            // Apply pagination
            query = query.Skip((CurrentPage - 1) * PageSize).Take(PageSize);

            var items = await query.ToListAsync();
            Results = await ProcessSearchResults(items, Query?.Trim().ToLower());

            // Apply sorting
            Results = SortBy switch
            {
                "rating" => Results.OrderByDescending(r => r.AvgRating)
                    .ThenByDescending(r => r.Item.OrganizationId != "Không Tổ Chức")
                    .ThenByDescending(r => r.Item.QuantityAvailable)
                    .ToList(),
                "quantity" => Results.OrderByDescending(r => r.Item.QuantityAvailable)
                    .ThenByDescending(r => r.AvgRating)
                    .ThenByDescending(r => r.Item.OrganizationId != "Không Tổ Chức")
                    .ToList(),
                _ => Results.OrderByDescending(r => r.RelevanceScore)
                    .ThenByDescending(r => r.AvgRating)
                    .ThenByDescending(r => r.Item.OrganizationId != "Không Tổ Chức")
                    .ThenByDescending(r => r.Item.QuantityAvailable)
                    .ToList()
            };

            return Page();
        }

        private async Task<List<SearchResult>> ProcessSearchResults(List<ExchangeItem> items, string cleanedQuery)
        {
            var results = new List<SearchResult>();

            foreach (var item in items)
            {
                double relevanceScore = 0;
                if (!string.IsNullOrWhiteSpace(cleanedQuery))
                {
                    if (item.Title.ToLower().Contains(cleanedQuery)) relevanceScore += 0.6; // Higher weight for title
                    if (item.Description.ToLower().Contains(cleanedQuery)) relevanceScore += 0.3;
                    if (item.Tags.Any(t => t.Tag.ToLower().Contains(cleanedQuery))) relevanceScore += 0.2;
                }

                // Boost for organization items
                if (item.OrganizationId != "Không Tổ Chức") relevanceScore += 0.1;
                // Boost for higher quantity
                if (item.QuantityAvailable > 10) relevanceScore += 0.05;

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

            return results;
        }

        public class SearchResult
        {
            public ExchangeItem Item { get; set; } = null!;
            public double RelevanceScore { get; set; }
            public double AvgRating { get; set; }
        }
    }
}