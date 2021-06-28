using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using NetControl4BioMed.Data;
using NetControl4BioMed.Data.Enumerations;
using NetControl4BioMed.Data.Models;
using NetControl4BioMed.Helpers.InputModels;
using NetControl4BioMed.Helpers.Interfaces;
using NetControl4BioMed.Helpers.Tasks;
using NetControl4BioMed.Helpers.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetControl4BioMed.Pages.AvailableData.Created.Networks.Details.Accounts.Users
{
    public class AddModel : PageModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ISendGridEmailSender _emailSender;
        private readonly LinkGenerator _linkGenerator;
        private readonly IReCaptchaChecker _reCaptchaChecker;

        public AddModel(IServiceProvider serviceProvider, UserManager<User> userManager, ApplicationDbContext context, ISendGridEmailSender emailSender, LinkGenerator linkGenerator, IReCaptchaChecker reCaptchaChecker)
        {
            _serviceProvider = serviceProvider;
            _userManager = userManager;
            _context = context;
            _emailSender = emailSender;
            _linkGenerator = linkGenerator;
            _reCaptchaChecker = reCaptchaChecker;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [DataType(DataType.Text)]
            [Required(ErrorMessage = "This field is required.")]
            public string Id { get; set; }

            [DataType(DataType.EmailAddress)]
            [Required(ErrorMessage = "This field is required.")]
            public string Email { get; set; }

            public string ReCaptchaToken { get; set; }
        }

        public ViewModel View { get; set; }

        public class ViewModel
        {
            public bool IsUserOwner { get; set; }

            public Network Network { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string id)
        {
            // Check if there isn't any ID provided.
            if (string.IsNullOrEmpty(id))
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: No ID has been provided.";
                // Redirect to the index page.
                return RedirectToPage("/AvailableData/Created/Networks/Index");
            }
            // Get the current user.
            var user = await _userManager.GetUserAsync(User);
            // Get the items with the provided ID.
            var items = _context.Networks
                .Where(item => item.IsPublic || (user != null && item.NetworkUsers.Any(item1 => item1.Email == user.Email)))
                .Where(item => item.Id == id);
            // Check if there were no items found.
            if (items == null || !items.Any())
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: No item has been found with the provided ID, or you don't have access to it.";
                // Redirect to the index page.
                return RedirectToPage("/AvailableData/Created/Networks/Index");
            }
            // Define the view.
            View = new ViewModel
            {
                Network = items
                    .First()
            };
            // Check if the user is not an owner.
            if (user == null || !items.Any(item => item.NetworkUsers.Any(item1 => item1.Email == user.Email)))
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: You need to be an owner of the network in order to add a user.";
                // Redirect to the index page.
                return RedirectToPage("/AvailableData/Created/Networks/Details/Accounts/Users/Index", new { id = View.Network.Id });
            }
            // Define the input.
            Input = new InputModel
            {
                Id = View.Network.Id
            };
            // Return the page.
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Check if there isn't any ID provided.
            if (string.IsNullOrEmpty(Input.Id))
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: No ID has been provided.";
                // Redirect to the index page.
                return RedirectToPage("/AvailableData/Created/Networks/Index");
            }
            // Get the current user.
            var user = await _userManager.GetUserAsync(User);
            // Get the items with the provided ID.
            var items = _context.Networks
                .Where(item => item.IsPublic || (user != null && item.NetworkUsers.Any(item1 => item1.Email == user.Email)))
                .Where(item => item.Id == Input.Id);
            // Check if there were no items found.
            if (items == null || !items.Any())
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: No item has been found with the provided ID, or you don't have access to it.";
                // Redirect to the index page.
                return RedirectToPage("/AvailableData/Created/Networks/Index");
            }
            // Define the view.
            View = new ViewModel
            {
                Network = items
                    .First()
            };
            // Check if the user is not an owner.
            if (user == null || !items.Any(item => item.NetworkUsers.Any(item1 => item1.Email == user.Email)))
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: You need to be an owner of the network in order to add a user.";
                // Redirect to the index page.
                return RedirectToPage("/AvailableData/Created/Networks/Details/Accounts/Users/Index", new { id = View.Network.Id });
            }
            // Check if the reCaptcha is valid.
            if (!await _reCaptchaChecker.IsValid(Input.ReCaptchaToken))
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The reCaptcha verification failed.");
                // Return the page.
                return Page();
            }
            // Check if the provided model is not valid.
            if (!ModelState.IsValid)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "An error was encountered. Please check again the input fields.");
                // Return the page.
                return Page();
            }
            // Check if the provided e-mail address already has access to the network.
            if (View.Network.NetworkUsers.Any(item => item.User.Email == Input.Email))
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The user with the provided e-mail already has access to the network.");
                // Return the page.
                return Page();
            }
            // Define a new task.
            var task = new NetworkUsersTask
            {
                Items = new List<NetworkUserInputModel>
                {
                    new NetworkUserInputModel
                    {
                        Network = new NetworkInputModel
                        {
                            Id = View.Network.Id
                        },
                        Email = Input.Email
                    }
                }
            };
            // Try to run the task.
            try
            {
                // Run the task.
                await task.CreateAsync(_serviceProvider, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, exception.Message);
                // Redisplay the page.
                return Page();
            }
            // Define the view model for the e-mail.
            var emailAddedToNetworkViewModel = new EmailAddedToNetworkViewModel
            {
                Email = user.Email,
                Name = View.Network.Name,
                Url = _linkGenerator.GetUriByPage(HttpContext, "/AvailableData/Created/Networks/Details/Index", handler: null, values: new { id = View.Network.Id }),
                AddedEmail = Input.Email,
                ApplicationUrl = _linkGenerator.GetUriByPage(HttpContext, "/Index", handler: null, values: null)
            };
            // Send the defined e-mails.
            await _emailSender.SendAddedToNetworkEmailAsync(emailAddedToNetworkViewModel);
            // Define the view model for the e-mail.
            var emailWasAddedToNetworkViewModel = new EmailWasAddedToNetworkViewModel
            {
                Email = Input.Email,
                Name = View.Network.Name,
                Url = _linkGenerator.GetUriByPage(HttpContext, "/AvailableData/Created/Networks/Details/Index", handler: null, values: new { id = View.Network.Id }),
                AddedByEmail = user.Email,
                ApplicationUrl = _linkGenerator.GetUriByPage(HttpContext, "/Index", handler: null, values: null)
            };
            // Send the defined e-mails.
            await _emailSender.SendWasAddedToNetworkEmailAsync(emailWasAddedToNetworkViewModel);
            // Display a message to the user.
            TempData["StatusMessage"] = $"Success: 1 user added successfully to the network.";
            // Redirect to the users page.
            return RedirectToPage("/AvailableData/Created/Networks/Details/Accounts/Users/Index", new { id = View.Network.Id });
        }
    }
}
