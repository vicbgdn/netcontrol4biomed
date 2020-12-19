using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using NetControl4BioMed.Data;
using NetControl4BioMed.Data.Enumerations;
using NetControl4BioMed.Data.Models;
using NetControl4BioMed.Helpers.Extensions;
using NetControl4BioMed.Helpers.InputModels;
using NetControl4BioMed.Helpers.Interfaces;
using NetControl4BioMed.Helpers.Tasks;
using Algorithms = NetControl4BioMed.Helpers.Algorithms;

namespace NetControl4BioMed.Pages.Content.DatabaseTypes.PPI.Created.Analyses
{
    [RequestFormLimits(ValueLengthLimit = 16 * 1024 * 1024)]
    public class CreateModel : PageModel
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly IReCaptchaChecker _reCaptchaChecker;

        public CreateModel(IServiceProvider serviceProvider, UserManager<User> userManager, ApplicationDbContext context, IReCaptchaChecker reCaptchaChecker)
        {
            _serviceProvider = serviceProvider;
            _userManager = userManager;
            _context = context;
            _reCaptchaChecker = reCaptchaChecker;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [DataType(DataType.Text)]
            [Required(ErrorMessage = "This field is required.")]
            public string Name { get; set; }

            [DataType(DataType.MultilineText)]
            public string Description { get; set; }

            [DataType(DataType.Text)]
            [Required(ErrorMessage = "This field is required.")]
            public bool IsPublic { get; set; }

            [DataType(DataType.Text)]
            [Required(ErrorMessage = "This field is required.")]
            public string Algorithm { get; set; }

            [DataType(DataType.MultilineText)]
            [Required(ErrorMessage = "This field is required.")]
            public string NetworkData { get; set; }

            [DataType(DataType.MultilineText)]
            [Required(ErrorMessage = "This field is required.")]
            public string SourceData { get; set; }

            [DataType(DataType.MultilineText)]
            [Required(ErrorMessage = "This field is required.")]
            public string SourceNodeCollectionData { get; set; }

            [DataType(DataType.MultilineText)]
            [Required(ErrorMessage = "This field is required.")]
            public string TargetData { get; set; }

            [DataType(DataType.MultilineText)]
            [Required(ErrorMessage = "This field is required.")]
            public string TargetNodeCollectionData { get; set; }

            [Range(0, 10000, ErrorMessage = "The value must be a positive integer lower than 10000.")]
            [Required(ErrorMessage = "This field is required.")]
            public int MaximumIterations { get; set; }

            [Range(0, 1000, ErrorMessage = "The value must be a positive integer lower than 1000.")]
            [Required(ErrorMessage = "This field is required.")]
            public int MaximumIterationsWithoutImprovement { get; set; }

            public Algorithms.Analyses.Greedy.Parameters GreedyAlgorithmParameters { get; set; }

            public Algorithms.Analyses.Genetic.Parameters GeneticAlgorithmParameters { get; set; }

            [DataType(DataType.Text)]
            [Required(ErrorMessage = "This field is required.")]
            public string ReCaptchaToken { get; set; }
        }

        public ViewModel View { get; set; }

        public class ViewModel
        {
            public bool IsUserAuthenticated { get; set; }

            public string Algorithm { get; set; }

            public IEnumerable<Sample> Samples { get; set; }

            public IEnumerable<NodeCollection> SourceNodeCollections { get; set; }

            public IEnumerable<NodeCollection> TargetNodeCollections { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(string algorithm = null, string analysisId = null, string sampleId = null)
        {
            // Get the current user.
            var user = await _userManager.GetUserAsync(User);
            // Check if there wasn't any algorithm provided.
            if (algorithm == null)
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: An algorithm is required for creating an analysis.";
                // Redirect to the index page.
                return RedirectToPage("/Content/DatabaseTypes/PPI/Created/Analyses/Index");
            }
            // Try to get the algorithm.
            try
            {
                // Get the algorithm.
                _ = EnumerationExtensions.GetEnumerationValue<AnalysisAlgorithm>(algorithm);
            }
            catch (Exception)
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: The provided algorithm is not valid.";
                // Redirect to the index page.
                return RedirectToPage("/Content/DatabaseTypes/PPI/Created/Analyses/Index");
            }
            // Try to get the analysis with the provided ID.
            var analyses = _context.Analyses
                .Where(item => item.AnalysisDatabases.Any(item1 => item1.Database.DatabaseType.Name == "PPI"))
                .Where(item => item.IsPublic || item.AnalysisUsers.Any(item1 => item1.User == user))
                .Where(item => item.Id == analysisId);
            // Check if there was an ID provided, but there was no analysis found.
            if (!string.IsNullOrEmpty(analysisId) && (analyses == null || !analyses.Any()))
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: No analysis could be found with the provided ID, or you don't have access to it.";
                // Redirect to the index page.
                return RedirectToPage("/Content/DatabaseTypes/PPI/Created/Analyses/Index");
            }
            // Define the view.
            View = new ViewModel
            {
                IsUserAuthenticated = user != null,
                Algorithm = algorithm,
                Samples = _context.Samples
                    .Where(item => item.SampleDatabases.Any(item1 => item1.Database.DatabaseType.Name == "PPI")),
                SourceNodeCollections = _context.NodeCollections
                    .Where(item => item.NodeCollectionDatabases.Any(item1 => item1.Database.DatabaseType.Name == "PPI"))
                    .Where(item => item.NodeCollectionDatabases.Any(item1 => item1.Database.IsPublic || item1.Database.DatabaseUsers.Any(item2 => item2.User == user))),
                TargetNodeCollections = _context.NodeCollections
                    .Where(item => item.NodeCollectionDatabases.Any(item1 => item1.Database.DatabaseType.Name == "PPI"))
                    .Where(item => item.NodeCollectionDatabases.Any(item1 => item1.Database.IsPublic || item1.Database.DatabaseUsers.Any(item2 => item2.User == user)))
            };
            // Try to get the sample with the provided ID.
            var sample = View.Samples?
                .FirstOrDefault(item => item.Id == sampleId);
            // Check if there was an ID provided, but there was no sample found.
            if (!string.IsNullOrEmpty(sampleId) && sample == null)
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: No sample could be found with the provided ID.";
                // Redirect to the index page.
                return RedirectToPage("/Content/DatabaseTypes/PPI/Created/Analyses/Index");
            }
            // Check if there was an analysis provided.
            if (!string.IsNullOrEmpty(analysisId))
            {
                // Define the input.
                Input = new InputModel
                {
                    Name = analyses
                        .Select(item => item.Name)
                        .FirstOrDefault(),
                    Description = analyses
                        .Select(item => item.Description)
                        .FirstOrDefault(),
                    IsPublic = analyses
                        .Select(item => item.IsPublic)
                        .FirstOrDefault(),
                    Algorithm = algorithm,
                    NetworkData = JsonSerializer.Serialize(analyses
                        .Select(item => item.AnalysisNetworks)
                        .SelectMany(item => item)
                        .Select(item => item.Network)
                        .Where(item => item.IsPublic || item.NetworkUsers.Any(item1 => item1.User == user))
                        .Select(item => item.Id)),
                    SourceData = JsonSerializer.Serialize(analyses
                        .Select(item => item.AnalysisNodes)
                        .SelectMany(item => item)
                        .Where(item => item.Type == AnalysisNodeType.Source)
                        .Select(item => item.Node.Name)),
                    SourceNodeCollectionData = JsonSerializer.Serialize(analyses
                        .Select(item => item.AnalysisNodeCollections)
                        .SelectMany(item => item)
                        .Where(item => item.Type == AnalysisNodeCollectionType.Source)
                        .Select(item => item.NodeCollection)
                        .Intersect(View.SourceNodeCollections)
                        .Select(item => item.Id)),
                    TargetData = JsonSerializer.Serialize(analyses
                        .Select(item => item.AnalysisNodes)
                        .SelectMany(item => item)
                        .Where(item => item.Type == AnalysisNodeType.Target)
                        .Select(item => item.Node.Name)),
                    TargetNodeCollectionData = JsonSerializer.Serialize(analyses
                        .Select(item => item.AnalysisNodeCollections)
                        .SelectMany(item => item)
                        .Where(item => item.Type == AnalysisNodeCollectionType.Target)
                        .Select(item => item.NodeCollection)
                        .Intersect(View.TargetNodeCollections)
                        .Select(item => item.Id)),
                    MaximumIterations = analyses
                        .Select(item => item.MaximumIterations)
                        .FirstOrDefault(),
                    MaximumIterationsWithoutImprovement = analyses
                        .Select(item => item.MaximumIterationsWithoutImprovement)
                        .FirstOrDefault(),
                    GreedyAlgorithmParameters = algorithm == AnalysisAlgorithm.Greedy.ToString() ? JsonSerializer.Deserialize<Algorithms.Analyses.Greedy.Parameters>(analyses.Select(item => item.Parameters).FirstOrDefault()) : null,
                    GeneticAlgorithmParameters = algorithm == AnalysisAlgorithm.Genetic.ToString() ? JsonSerializer.Deserialize<Algorithms.Analyses.Genetic.Parameters>(analyses.Select(item => item.Parameters).FirstOrDefault()) : null
                };
            }
            // Check if there was a sample provided.
            if (!string.IsNullOrEmpty(sampleId))
            {
                // Define the input.
                Input = new InputModel
                {
                    Name = sample.Name,
                    Description = sample.Description,
                    IsPublic = !View.IsUserAuthenticated,
                    Algorithm = sample.AnalysisAlgorithm.ToString(),
                    NetworkData = sample.AnalysisNetworkData,
                    SourceData = sample.AnalysisSourceData,
                    SourceNodeCollectionData = sample.AnalysisSourceNodeCollectionData,
                    TargetData = sample.AnalysisTargetData,
                    TargetNodeCollectionData = sample.AnalysisTargetNodeCollectionData
                };
            }
            else
            {
                // Define the input.
                Input = new InputModel
                {
                    IsPublic = !View.IsUserAuthenticated,
                    Algorithm = View.Algorithm,
                    NetworkData = JsonSerializer.Serialize(Enumerable.Empty<string>()),
                    SourceData = JsonSerializer.Serialize(Enumerable.Empty<string>()),
                    SourceNodeCollectionData = JsonSerializer.Serialize(Enumerable.Empty<string>()),
                    TargetData = JsonSerializer.Serialize(Enumerable.Empty<string>()),
                    TargetNodeCollectionData = JsonSerializer.Serialize(Enumerable.Empty<string>()),
                    MaximumIterations = 100,
                    MaximumIterationsWithoutImprovement = 25,
                    GreedyAlgorithmParameters = View.Algorithm == AnalysisAlgorithm.Greedy.ToString() ? new Algorithms.Analyses.Greedy.Parameters() : null,
                    GeneticAlgorithmParameters = View.Algorithm == AnalysisAlgorithm.Genetic.ToString() ? new Algorithms.Analyses.Genetic.Parameters() : null
                };
            }
            // Return the page.
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Get the current user.
            var user = await _userManager.GetUserAsync(User);
            // Check if there wasn't any algorithm provided.
            if (Input.Algorithm == null)
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: An algorithm is required for creating an analysis.";
                // Redirect to the index page.
                return RedirectToPage("/Content/DatabaseTypes/PPI/Created/Analyses/Index");
            }
            // Try to get the algorithm.
            try
            {
                // Get the algorithm.
                _ = EnumerationExtensions.GetEnumerationValue<AnalysisAlgorithm>(Input.Algorithm);
            }
            catch (Exception)
            {
                // Display a message.
                TempData["StatusMessage"] = "Error: The provided algorithm is not valid.";
                // Redirect to the index page.
                return RedirectToPage("/Content/DatabaseTypes/PPI/Created/Analyses/Index");
            }
            // Define the view.
            View = new ViewModel
            {
                IsUserAuthenticated = user != null,
                Algorithm = Input.Algorithm,
                Samples = _context.Samples
                    .Where(item => item.SampleDatabases.Any(item1 => item1.Database.DatabaseType.Name == "PPI")),
                SourceNodeCollections = _context.NodeCollections
                    .Where(item => item.NodeCollectionDatabases.Any(item1 => item1.Database.DatabaseType.Name == "PPI"))
                    .Where(item => item.NodeCollectionDatabases.Any(item1 => item1.Database.IsPublic || item1.Database.DatabaseUsers.Any(item2 => item2.User == user))),
                TargetNodeCollections = _context.NodeCollections
                    .Where(item => item.NodeCollectionDatabases.Any(item1 => item1.Database.DatabaseType.Name == "PPI"))
                    .Where(item => item.NodeCollectionDatabases.Any(item1 => item1.Database.IsPublic || item1.Database.DatabaseUsers.Any(item2 => item2.User == user)))
            };
            // Check if the reCaptcha is valid.
            if (!await _reCaptchaChecker.IsValid(Input.ReCaptchaToken))
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The reCaptcha verification failed.");
                // Return the page.
                return Page();
            }
            // Check if the provided model isn't valid.
            if (!ModelState.IsValid)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "An error has been encountered. Please check again the input fields.");
                // Redisplay the page.
                return Page();
            }
            // Check if the public availability isn't valid.
            if (!View.IsUserAuthenticated && !Input.IsPublic)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "You are not logged in, so the analysis must be set as public.");
                // Redisplay the page.
                return Page();
            }
            // Check if the parameters match the given algorithm.
            if ((Input.Algorithm == AnalysisAlgorithm.Greedy.ToString() && Input.GreedyAlgorithmParameters == null) || (Input.Algorithm == AnalysisAlgorithm.Genetic.ToString() && Input.GeneticAlgorithmParameters == null))
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The parameter values are not valid for the chosen algorithm.");
                // Redisplay the page.
                return Page();
            }
            // Try to deserialize the network data.
            if (!Input.NetworkData.TryDeserializeJsonObject<IEnumerable<string>>(out var networkIds) || networkIds == null)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The provided network data could not be deserialized.");
                // Redisplay the page.
                return Page();
            }
            // Check if there weren't any network IDs provided.
            if (!networkIds.Any())
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "At least one network ID must be specified.");
                // Redisplay the page.
                return Page();
            }
            // Try to get the networks with the provided IDs.
            var networks = _context.Networks
                .Where(item => item.IsPublic || item.NetworkUsers.Any(item1 => item1.User == user))
                .Where(item => item.NetworkDatabases.Any(item1 => item1.Database.DatabaseType.Name == "PPI"))
                .Where(item => item.NetworkDatabases.Any(item1 => item1.Database.IsPublic || item1.Database.DatabaseUsers.Any(item2 => item2.User == user)))
                .Where(item => networkIds.Contains(item.Id));
            // Check if there weren't any networks found.
            if (!networks.Any())
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "No networks could be found with the provided IDs.");
                // Redisplay the page.
                return Page();
            }
            // Try to deserialize the seed data.
            if (!Input.SourceData.TryDeserializeJsonObject<IEnumerable<string>>(out var sourceItems) || sourceItems == null)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The provided source data could not be deserialized.");
                // Redisplay the page.
                return Page();
            }
            // Try to deserialize the source node collection data.
            if (!Input.SourceNodeCollectionData.TryDeserializeJsonObject<IEnumerable<string>>(out var sourceNodeCollectionIds) || sourceNodeCollectionIds == null)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The provided source protein collection data could not be deserialized.");
                // Redisplay the page.
                return Page();
            }
            // Try to get the source node collections with the provided IDs.
            var sourceNodeCollections = View.SourceNodeCollections
                .Where(item => sourceNodeCollectionIds.Contains(item.Id));
            // Try to deserialize the target data.
            if (!Input.TargetData.TryDeserializeJsonObject<IEnumerable<string>>(out var targetItems) || targetItems == null)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The provided target data could not be deserialized.");
                // Redisplay the page.
                return Page();
            }
            // Try to deserialize the target node collection data.
            if (!Input.TargetNodeCollectionData.TryDeserializeJsonObject<IEnumerable<string>>(out var targetNodeCollectionIds) || targetNodeCollectionIds == null)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "The provided target protein collection data could not be deserialized.");
                // Redisplay the page.
                return Page();
            }
            // Try to get the target node collections with the provided IDs.
            var targetNodeCollections = View.TargetNodeCollections
                .Where(item => targetNodeCollectionIds.Contains(item.Id));
            // Check if there wasn't any target data found.
            if (!targetItems.Any() && !targetNodeCollections.Any())
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, "No target data has been provided or no target protein collections could be found with the provided IDs.");
                // Redisplay the page.
                return Page();
            }
            // Define the generation data.
            var data = JsonSerializer.Serialize(sourceItems
                .Select(item => new AnalysisNodeInputModel
                {
                    Node = new NodeInputModel
                    {
                        Id = item
                    },
                    Type = "Source"
                })
                .Concat(targetItems
                    .Select(item => new AnalysisNodeInputModel
                    {
                        Node = new NodeInputModel
                        {
                            Id = item
                        },
                        Type = "Target"
                    })));
            // Define a new task.
            var task = new AnalysesTask
            {
                Scheme = HttpContext.Request.Scheme,
                HostValue = HttpContext.Request.Host.Value,
                Items = new List<AnalysisInputModel>
                {
                    new AnalysisInputModel
                    {
                        Name = Input.Name,
                        Description = Input.Description,
                        IsPublic = Input.IsPublic,
                        Data = data,
                        MaximumIterations = Input.MaximumIterations,
                        MaximumIterationsWithoutImprovement = Input.MaximumIterationsWithoutImprovement,
                        Algorithm = Input.Algorithm,
                        Parameters = Input.Algorithm == AnalysisAlgorithm.Greedy.ToString() ? JsonSerializer.Serialize(Input.GreedyAlgorithmParameters, new JsonSerializerOptions { IgnoreReadOnlyProperties = true }) :
                            Input.Algorithm == AnalysisAlgorithm.Genetic.ToString() ? JsonSerializer.Serialize(Input.GeneticAlgorithmParameters, new JsonSerializerOptions { IgnoreReadOnlyProperties = true }) :
                            null,
                        AnalysisUsers = View.IsUserAuthenticated ?
                            new List<AnalysisUserInputModel>
                            {
                                new AnalysisUserInputModel
                                {
                                    User = new UserInputModel
                                    {
                                        Id = user.Id
                                    }
                                }
                            } :
                            new List<AnalysisUserInputModel>(),
                        AnalysisNodeCollections = sourceNodeCollections
                            .Select(item => item.Id)
                            .Select(item => new AnalysisNodeCollectionInputModel
                            {
                                NodeCollection = new NodeCollectionInputModel
                                {
                                    Id = item
                                },
                                Type = "Source"
                            })
                            .Concat(targetNodeCollections
                                .Select(item => item.Id)
                                .Select(item => new AnalysisNodeCollectionInputModel
                                {
                                    NodeCollection = new NodeCollectionInputModel
                                    {
                                        Id = item
                                    },
                                    Type = "Target"
                                })),
                        AnalysisNetworks = networks
                            .Select(item => item.Id)
                            .Select(item => new AnalysisNetworkInputModel
                            {
                                Network = new NetworkInputModel
                                {
                                    Id = item
                                }
                            })
                    }
                }
            };
            // Define the IDs of the created items.
            var ids = Enumerable.Empty<string>();
            // Try to run the task.
            try
            {
                // Run the task.
                ids = await task.CreateAsync(_serviceProvider, CancellationToken.None);
            }
            catch (Exception exception)
            {
                // Add an error to the model.
                ModelState.AddModelError(string.Empty, exception.Message);
                // Redisplay the page.
                return Page();
            }
            // Check if there wasn't any ID returned.
            if (ids != null && ids.Any())
            {
                // Display a message.
                TempData["StatusMessage"] = $"Success: 1 PPI analysis with the algorithm \"{Input.Algorithm}\" defined successfully with the ID \"{ids.First()}\" and scheduled for generation.";
                // Redirect to the index page.
                return RedirectToPage("/Content/DatabaseTypes/PPI/Created/Analyses/Details/Index", new { id = ids.First() });
            }
            // Display a message.
            TempData["StatusMessage"] = $"Success: 1 PPI analysis with the algorithm \"{Input.Algorithm}\" defined successfully and scheduled for generation.";
            // Redirect to the index page.
            return RedirectToPage("/Content/DatabaseTypes/PPI/Created/Analyses/Index");
        }
    }
}
