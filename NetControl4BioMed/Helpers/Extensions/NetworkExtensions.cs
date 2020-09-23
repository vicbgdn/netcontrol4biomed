﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NetControl4BioMed.Data;
using NetControl4BioMed.Data.Enumerations;
using NetControl4BioMed.Data.Models;
using NetControl4BioMed.Helpers.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NetControl4BioMed.Helpers.Extensions
{
    /// <summary>
    /// Provides extensions for the networks.
    /// </summary>
    public static class NetworkExtensions
    {
        /// <summary>
        /// Represents the maximum size for which visualization will be enabled.
        /// </summary>
        public readonly static int MaximumSizeForVisualization = 500;

        /// <summary>
        /// Gets the log of the network.
        /// </summary>
        /// <param name="network">The current network.</param>
        /// <returns>The log of the network.</returns>
        public static List<LogEntryViewModel> GetLog(this Network network)
        {
            // Return the list of log entries.
            return JsonSerializer.Deserialize<List<LogEntryViewModel>>(network.Log);
        }

        /// <summary>
        /// Appends to the list a new entry to the log of the network and returns the updated list.
        /// </summary>
        /// <param name="network">The current network.</param>
        /// <param name="message">The message to add as a new entry to the network log.</param>
        /// <returns>The updated log of the network.</returns>
        public static string AppendToLog(this Network network, string message)
        {
            // Return the log entries with the message appended.
            return JsonSerializer.Serialize(network.GetLog().Append(new LogEntryViewModel { DateTime = DateTime.UtcNow, Message = message }));
        }

        /// <summary>
        /// Gets the Cytoscape view model corresponding to the provided network.
        /// </summary>
        /// <param name="network">The current network.</param>
        /// <param name="linkGenerator">The link generator.</param>
        /// <returns>The Cytoscape view model corresponding to the provided network.</returns>
        public static CytoscapeViewModel GetCytoscapeViewModel(this Network network, HttpContext httpContext, LinkGenerator linkGenerator, ApplicationDbContext context)
        {
            // Get the default values.
            var emptyEnumerable = Enumerable.Empty<string>();
            var interactionType = context.NetworkDatabases
                .Where(item => item.Network == network)
                .Select(item => item.Database.DatabaseType.Name)
                .FirstOrDefault();
            var isGeneric = interactionType == "Generic";
            // Return the view model.
            return new CytoscapeViewModel
            {
                Elements = new CytoscapeViewModel.CytoscapeElements
                {
                    Nodes = context.NetworkNodes
                        .Where(item => item.Network == network)
                        .Where(item => item.Type == NetworkNodeType.None)
                        .Select(item => item.Node)
                        .Select(item => new
                        {
                            Id = item.Id,
                            Name = item.Name,
                            Alias = item.DatabaseNodeFieldNodes
                                .Where(item1 => item1.DatabaseNodeField.IsSearchable)
                                .Select(item1 => item1.Value),
                            Classes = item.NetworkNodes
                                .Where(item1 => item1.Network == network)
                                .Select(item => item.Type.ToString().ToLower())
                        })
                        .AsEnumerable()
                        .Select(item => new CytoscapeViewModel.CytoscapeElements.CytoscapeNode
                        {
                            Data = new CytoscapeViewModel.CytoscapeElements.CytoscapeNode.CytoscapeNodeData
                            {
                                Id = item.Id,
                                Name = item.Name,
                                Href = isGeneric ? string.Empty : linkGenerator.GetUriByPage(httpContext, "/Content/Data/Nodes/Details", handler: null, values: new { id = item.Id }),
                                Alias = item.Alias
                            },
                            Classes = item.Classes
                        }),
                    Edges = context.NetworkEdges
                        .Where(item => item.Network == network)
                        .Select(item => item.Edge)
                        .Select(item => new
                        {
                            Id = item.Id,
                            Name = item.Name,
                            SourceNodeId = item.EdgeNodes
                                .Where(item1 => item1.Type == EdgeNodeType.Source)
                                .Select(item1 => item1.Node)
                                .Where(item1 => item1 != null)
                                .Select(item1 => item1.Id)
                                .FirstOrDefault(),
                            TargetNodeId = item.EdgeNodes
                                .Where(item1 => item1.Type == EdgeNodeType.Target)
                                .Select(item1 => item1.Node)
                                .Where(item1 => item1 != null)
                                .Select(item1 => item1.Id)
                                .FirstOrDefault()
                        })
                        .AsEnumerable()
                        .Select(item => new CytoscapeViewModel.CytoscapeElements.CytoscapeEdge
                        {
                            Data = new CytoscapeViewModel.CytoscapeElements.CytoscapeEdge.CytoscapeEdgeData
                            {
                                Id = item.Id,
                                Name = item.Name,
                                Source = item.SourceNodeId,
                                Target = item.TargetNodeId,
                                Interaction = interactionType
                            },
                            Classes = emptyEnumerable
                        })
                },
                Layout = CytoscapeViewModel.DefaultLayout,
                Styles = CytoscapeViewModel.DefaultStyles.Concat(CytoscapeViewModel.DefaultNetworkStyles)
            };
        }
    }
}
