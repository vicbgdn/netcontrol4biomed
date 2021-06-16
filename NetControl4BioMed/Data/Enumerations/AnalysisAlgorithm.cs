﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace NetControl4BioMed.Data.Enumerations
{
    /// <summary>
    /// Represents the possible algorithms used for running an analysis.
    /// </summary>
    public enum AnalysisAlgorithm
    {
        /// <summary>
        /// Represents the greedy algorithm.
        /// </summary>
        [Display(Name = "Greedy", Description = "The analysis uses a greedy algorithm.")]
        Greedy,

        /// <summary>
        /// Represents the genetic algorithm.
        /// </summary>
        [Display(Name = "Genetic", Description = "The analysis uses a genetic algorithm.")]
        Genetic
    }
}
