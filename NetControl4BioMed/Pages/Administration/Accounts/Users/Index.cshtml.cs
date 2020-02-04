using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NetControl4BioMed.Data;
using NetControl4BioMed.Data.Models;
using NetControl4BioMed.Helpers.ViewModels;

namespace NetControl4BioMed.Pages.Administration.Accounts.Users
{
    [Authorize(Roles = "Administrator")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly LinkGenerator _linkGenerator;

        public IndexModel(ApplicationDbContext context, LinkGenerator linkGenerator)
        {
            _context = context;
            _linkGenerator = linkGenerator;
        }

        public ViewModel View { get; set; }

        public class ViewModel
        {
            public SearchViewModel<User> Search { get; set; }
        }

        public IActionResult OnGet(string searchString = null, IEnumerable<string> searchIn = null, IEnumerable<string> filter = null, string sortBy = null, string sortDirection = null, int? itemsPerPage = null, int? currentPage = 1)
        {
            // Define the search options.
            var options = new SearchOptionsViewModel
            {
                SearchIn = new Dictionary<string, string>
                {
                    { "Id", "ID" },
                    { "Email", "E-mail" },
                    { "Roles", "Roles" }
                },
                Filter = new Dictionary<string, string>
                {
                    { "HasEmailConfirmed", "Has e-mail confirmed" },
                    { "HasEmailNotConfirmed", "Does not have e-mail confirmed" },
                    { "IsAdministrator", "Is administrator" },
                    { "IsNotAdministrator", "Is not administrator" }
                },
                SortBy = new Dictionary<string, string>
                {
                    { "Id", "ID" },
                    { "DateTimeCreated", "Date created" },
                    { "Email", "E-mail" }
                }
            };
            // Define the search input.
            var input = new SearchInputViewModel(options, null, searchString, searchIn, filter, sortBy, sortDirection, itemsPerPage, currentPage);
            // Check if any of the provided variables was null before the reassignment.
            if (input.NeedsRedirect)
            {
                // Redirect to the page where they are all explicitly defined.
                return RedirectToPage(new { searchString = input.SearchString, searchIn = input.SearchIn, filter = input.Filter, sortBy = input.SortBy, sortDirection = input.SortDirection, itemsPerPage = input.ItemsPerPage, currentPage = input.CurrentPage });
            }
            // Start with all of the items in the database.
            var query = _context.Users
                .Where(item => true);
            // Select the results matching the search string.
            query = query
                .Where(item => !input.SearchIn.Any() ||
                    input.SearchIn.Contains("Id") && item.Id.Contains(input.SearchString) ||
                    input.SearchIn.Contains("Email") && item.Email.Contains(input.SearchString) ||
                    input.SearchIn.Contains("Roles") && item.UserRoles.Any(item1 => item1.Role.Name.Contains(input.SearchString)));
            // Select the results matching the filter parameter.
            query = query
                .Where(item => input.Filter.Contains("HasEmailConfirmed") ? item.EmailConfirmed : true)
                .Where(item => input.Filter.Contains("HasEmailNotConfirmed") ? !item.EmailConfirmed : true)
                .Where(item => input.Filter.Contains("IsAdministrator") ? item.UserRoles.Any(ur => ur.Role.Name == "Administrator") : true)
                .Where(item => input.Filter.Contains("IsNotAdministrator") ? !item.UserRoles.Any(ur => ur.Role.Name == "Administrator") : true);
            // Sort it according to the parameters.
            switch ((input.SortBy, input.SortDirection))
            {
                case var sort when sort == ("Id", "Ascending"):
                    query = query.OrderBy(item => item.Id);
                    break;
                case var sort when sort == ("Id", "Descending"):
                    query = query.OrderByDescending(item => item.Id);
                    break;
                case var sort when sort == ("DateTimeCreated", "Ascending"):
                    query = query.OrderBy(item => item.DateTimeCreated);
                    break;
                case var sort when sort == ("DateTimeCreated", "Descending"):
                    query = query.OrderByDescending(item => item.DateTimeCreated);
                    break;
                case var sort when sort == ("Email", "Ascending"):
                    query = query.OrderBy(item => item.Email);
                    break;
                case var sort when sort == ("Email", "Descending"):
                    query = query.OrderByDescending(item => item.Email);
                    break;
                default:
                    break;
            }
            // Include the related entitites.
            query = query
                .Include(item => item.UserRoles)
                    .ThenInclude(item => item.Role);
            // Define the view.
            View = new ViewModel
            {
                Search = new SearchViewModel<User>(_linkGenerator, HttpContext, input, query)
            };
            // Return the page.
            return Page();
        }
    }
}
