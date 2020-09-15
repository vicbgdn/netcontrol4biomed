﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetControl4BioMed.Data;
using NetControl4BioMed.Data.Enumerations;
using NetControl4BioMed.Data.Models;
using NetControl4BioMed.Helpers.Exceptions;
using NetControl4BioMed.Helpers.Extensions;
using NetControl4BioMed.Helpers.InputModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NetControl4BioMed.Helpers.Tasks
{
    /// <summary>
    /// Implements a task to update database node fields in the database.
    /// </summary>
    public class DatabaseNodeFieldsTask
    {
        /// <summary>
        /// Gets or sets the items to be updated.
        /// </summary>
        public IEnumerable<DatabaseNodeFieldInputModel> Items { get; set; }

        /// <summary>
        /// Creates the items in the database.
        /// </summary>
        /// <param name="serviceProvider">The application service provider.</param>
        /// <param name="token">The cancellation token for the task.</param>
        /// <returns>The created items.</returns>
        public IEnumerable<DatabaseNodeField> Create(IServiceProvider serviceProvider, CancellationToken token)
        {
            // Check if there weren't any valid items found.
            if (Items == null)
            {
                // Throw an exception.
                throw new TaskException("No valid items could be found with the provided data.");
            }
            // Check if the exception item should be shown.
            var showExceptionItem = Items.Count() > 1;
            // Get the total number of batches.
            var count = Math.Ceiling((double)Items.Count() / ApplicationDbContext.BatchSize);
            // Go over each batch.
            for (var index = 0; index < count; index++)
            {
                // Check if the cancellation was requested.
                if (token.IsCancellationRequested)
                {
                    // Break.
                    break;
                }
                // Create a new scope.
                using var scope = serviceProvider.CreateScope();
                // Use a new context instance.
                using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                // Get the items in the current batch.
                var batchItems = Items
                    .Skip(index * ApplicationDbContext.BatchSize)
                    .Take(ApplicationDbContext.BatchSize);
                // Get the IDs of the items in the current batch.
                var batchIds = batchItems
                    .Where(item => !string.IsNullOrEmpty(item.Id))
                    .Select(item => item.Id);
                // Check if any of the IDs are repeating in the list.
                if (batchIds.Distinct().Count() != batchIds.Count())
                {
                    // Throw an exception.
                    throw new TaskException("Two or more of the manually provided IDs are duplicated.");
                }
                // Get the valid IDs, that do not appear in the database.
                var validBatchIds = batchIds
                    .Except(context.DatabaseNodeFields
                        .Where(item => batchIds.Contains(item.Id))
                        .Select(item => item.Id));
                // Get the IDs of the related entities that appear in the current batch.
                var batchDatabaseIds = batchItems
                    .Where(item => item.Database != null)
                    .Select(item => item.Database)
                    .Where(item => !string.IsNullOrEmpty(item.Id))
                    .Select(item => item.Id)
                    .Distinct();
                // Get the related entities that appear in the current batch.
                var batchDatabases = context.Databases
                    .Include(item => item.DatabaseType)
                    .Where(item => batchDatabaseIds.Contains(item.Id));
                // Save the items to add.
                var databaseNodeFieldsToAdd = new List<DatabaseNodeField>();
                // Go over each item in the current batch.
                foreach (var batchItem in batchItems)
                {
                    // Check if the ID of the item is not valid.
                    if (!string.IsNullOrEmpty(batchItem.Id) && !validBatchIds.Contains(batchItem.Id))
                    {
                        // Continue.
                        continue;
                    }
                    // Check if there is another database node field with the same name.
                    if (context.DatabaseNodeFields.Any(item => item.Name == batchItem.Name) || databaseNodeFieldsToAdd.Any(item => item.Name == batchItem.Name))
                    {
                        // Throw an exception.
                        throw new TaskException("A database node field with the same name already exists.", showExceptionItem, batchItem);
                    }
                    // Check if there was no database provided.
                    if (batchItem.Database == null || string.IsNullOrEmpty(batchItem.Database.Id))
                    {
                        // Throw an exception.
                        throw new TaskException("There was no database provided.", showExceptionItem, batchItem);
                    }
                    // Get the database.
                    var database = batchDatabases
                        .FirstOrDefault(item => item.Id == batchItem.Database.Id);
                    // Check if there was no database found.
                    if (database == null)
                    {
                        // Throw an exception.
                        throw new TaskException("There was no database found.", showExceptionItem, batchItem);
                    }
                    // Check if the database is generic.
                    if (database.DatabaseType.Name == "Generic")
                    {
                        // Throw an exception.
                        throw new TaskException("The database node field can't be generic.", showExceptionItem, batchItem);
                    }
                    // Define the new item.
                    var databaseNodeField = new DatabaseNodeField
                    {
                        DateTimeCreated = DateTime.UtcNow,
                        Name = batchItem.Name,
                        Description = batchItem.Description,
                        Url = batchItem.Url,
                        IsSearchable = batchItem.IsSearchable,
                        DatabaseId = database.Id,
                        Database = database
                    };
                    // Check if there is any ID provided.
                    if (!string.IsNullOrEmpty(batchItem.Id))
                    {
                        // Assign it to the item.
                        database.Id = batchItem.Id;
                    }
                    // Add the item to the list.
                    databaseNodeFieldsToAdd.Add(databaseNodeField);
                }
                // Create the items.
                IEnumerableExtensions.Create(databaseNodeFieldsToAdd, context, token);
                // Go over each item.
                foreach (var databaseNodeField in databaseNodeFieldsToAdd)
                {
                    // Yield return it.
                    yield return databaseNodeField;
                }
            }
        }

        /// <summary>
        /// Edits the items in the database.
        /// </summary>
        /// <param name="serviceProvider">The application service provider.</param>
        /// <param name="token">The cancellation token for the task.</param>
        /// <returns>The edited items.</returns>
        public IEnumerable<DatabaseNodeField> Edit(IServiceProvider serviceProvider, CancellationToken token)
        {
            // Check if there weren't any valid items found.
            if (Items == null)
            {
                // Throw an exception.
                throw new TaskException("No valid items could be found with the provided data.");
            }
            // Check if the exception item should be shown.
            var showExceptionItem = Items.Count() > 1;
            // Get the total number of batches.
            var count = Math.Ceiling((double)Items.Count() / ApplicationDbContext.BatchSize);
            // Go over each batch.
            for (var index = 0; index < count; index++)
            {
                // Check if the cancellation was requested.
                if (token.IsCancellationRequested)
                {
                    // Break.
                    break;
                }
                // Create a new scope.
                using var scope = serviceProvider.CreateScope();
                // Use a new context instance.
                using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                // Get the items in the current batch.
                var batchItems = Items
                    .Skip(index * ApplicationDbContext.BatchSize)
                    .Take(ApplicationDbContext.BatchSize);
                // Get the IDs of the items in the current batch.
                var batchIds = batchItems
                    .Where(item => !string.IsNullOrEmpty(item.Id))
                    .Select(item => item.Id)
                    .Distinct();
                // Get the items corresponding to the current batch.
                var databaseNodeFields = context.DatabaseNodeFields
                    .Include(item => item.Database)
                        .ThenInclude(item => item.DatabaseType)
                    .Where(item => batchIds.Contains(item.Id));
                // Save the items to edit.
                var databaseNodeFieldsToEdit = new List<DatabaseNodeField>();
                // Go over each item in the current batch.
                foreach (var batchItem in batchItems)
                {
                    // Get the corresponding items.
                    var databaseNodeField = databaseNodeFields
                        .FirstOrDefault(item => item.Id == batchItem.Id);
                    // Check if there was no item found.
                    if (databaseNodeField == null)
                    {
                        // Continue.
                        continue;
                    }
                    // Check if the database node field is generic.
                    if (databaseNodeField.Database.DatabaseType.Name == "Generic")
                    {
                        // Throw an exception.
                        throw new TaskException("The generic database node field can't be edited.", showExceptionItem, batchItem);
                    }
                    // Check if there is another database node field with the same name.
                    if (context.DatabaseNodeFields.Any(item => item.Id != databaseNodeField.Id && item.Name == batchItem.Name) || databaseNodeFieldsToEdit.Any(item => item.Name == batchItem.Name))
                    {
                        // Throw an exception.
                        throw new TaskException("A database node field with the same name already exists.", showExceptionItem, batchItem);
                    }
                    // Update the item.
                    databaseNodeField.Name = batchItem.Name;
                    databaseNodeField.Description = batchItem.Description;
                    databaseNodeField.Url = batchItem.Url;
                    databaseNodeField.IsSearchable = batchItem.IsSearchable;
                    // Add the item to the list.
                    databaseNodeFieldsToEdit.Add(databaseNodeField);
                }
                // Edit the items.
                IEnumerableExtensions.Edit(databaseNodeFieldsToEdit, context, token);
                // Go over each item.
                foreach (var databaseNodeField in databaseNodeFieldsToEdit)
                {
                    // Yield return it.
                    yield return databaseNodeField;
                }
            }
        }

        /// <summary>
        /// Deletes the items from the database.
        /// </summary>
        /// <param name="serviceProvider">The application service provider.</param>
        /// <param name="token">The cancellation token for the task.</param>
        public void Delete(IServiceProvider serviceProvider, CancellationToken token)
        {
            // Check if there weren't any valid items found.
            if (Items == null)
            {
                // Throw an exception.
                throw new TaskException("No valid items could be found with the provided data.");
            }
            // Get the total number of batches.
            var count = Math.Ceiling((double)Items.Count() / ApplicationDbContext.BatchSize);
            // Go over each batch.
            for (var index = 0; index < count; index++)
            {
                // Check if the cancellation was requested.
                if (token.IsCancellationRequested)
                {
                    // Break.
                    break;
                }
                // Create a new scope.
                using var scope = serviceProvider.CreateScope();
                // Use a new context instance.
                using var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                // Get the items in the current batch.
                var batchItems = Items
                    .Skip(index * ApplicationDbContext.BatchSize)
                    .Take(ApplicationDbContext.BatchSize);
                // Get the IDs of the items in the current batch.
                var batchIds = batchItems.Select(item => item.Id);
                // Get the items with the provided IDs.
                var databaseNodeFields = context.DatabaseNodeFields
                    .Where(item => batchIds.Contains(item.Id));
                // Get the related entities that use the items.
                var nodes = context.Nodes
                    .Where(item => item.DatabaseNodeFieldNodes.Any(item1 => databaseNodeFields.Contains(item1.DatabaseNodeField)));
                var edges = context.Edges
                    .Where(item => item.EdgeNodes.Any(item1 => nodes.Contains(item1.Node)));
                var nodeCollections = context.NodeCollections
                    .Where(item => item.NodeCollectionNodes.Any(item1 => nodes.Contains(item1.Node)));
                var networks = context.Networks
                    .Where(item => item.NetworkNodes.Any(item1 => nodes.Contains(item1.Node)));
                var analyses = context.Analyses
                    .Where(item => item.AnalysisNodes.Any(item1 => nodes.Contains(item1.Node)));
                // Delete the items.
                IQueryableExtensions.Delete(analyses, context, token);
                IQueryableExtensions.Delete(networks, context, token);
                IQueryableExtensions.Delete(nodeCollections, context, token);
                IQueryableExtensions.Delete(edges, context, token);
                IQueryableExtensions.Delete(nodes, context, token);
                IQueryableExtensions.Delete(databaseNodeFields, context, token);
            }
        }
    }
}
