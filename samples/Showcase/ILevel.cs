// Copyright © Erickson Lopez. MIT License.
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EricksonLopez.Transaction.Showcase;

/// <summary>
/// Defines the executable contract for a progressive educational level in the Showcase.
/// </summary>
public interface ILevel
{
    /// <summary>
    /// Gets the numerical level indicator (e.g. 0 for Conceptual, 1 for QuickStart, etc.).
    /// </summary>
    int LevelNumber { get; }

    /// <summary>
    /// Gets the human-readable title of the level.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a detailed description of the architectural concepts and APIs demonstrated.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the complexity category (e.g., "Foundations", "Configuration", "Integration", "Enterprise").
    /// </summary>
    string Category { get; }

    /// <summary>
    /// Executes the level's demonstration using registered dependencies.
    /// </summary>
    /// <param name="serviceProvider">The root service provider.</param>
    /// <param name="cancellationToken">A token to cancel the demonstration.</param>
    /// <returns>A task representing the asynchronous demonstration execution.</returns>
    Task RunAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}
