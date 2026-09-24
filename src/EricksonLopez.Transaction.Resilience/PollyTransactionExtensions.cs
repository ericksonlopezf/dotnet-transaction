// Copyright © Erickson Lopez. MIT License.
using System;
using EricksonLopez.Transaction.Exceptions;
using Polly;
using Polly.Retry;

namespace EricksonLopez.Transaction.Resilience;

/// <summary>
/// Provides Polly resilience extensions specifically designed for the transaction ecosystem.
/// </summary>
public static class PollyTransactionExtensions
{
    /// <summary>
    /// Configures a <see cref="PolicyBuilder"/> to handle ambiguous transaction commit outcomes.
    /// </summary>
    /// <param name="policyBuilder">The base policy builder to configure.</param>
    /// <returns>The configured <see cref="PolicyBuilder"/> instance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="policyBuilder"/> is <see langword="null"/></exception>
    public static PolicyBuilder HandleAmbiguousCommit(this PolicyBuilder policyBuilder)
    {
        ArgumentNullException.ThrowIfNull(policyBuilder);

        return policyBuilder.Or<TransactionCommitException>(ex => ex.IsAmbiguous);
    }

    /// <summary>
    /// Creates a base policy builder that triggers on ambiguous transaction commit outcomes.
    /// </summary>
    /// <returns>A <see cref="PolicyBuilder"/> configured to handle ambiguous transaction commits.</returns>
    public static PolicyBuilder HandleAmbiguousCommit()
    {
        return Policy.Handle<TransactionCommitException>(ex => ex.IsAmbiguous);
    }
}
