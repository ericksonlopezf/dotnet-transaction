// Copyright © Erickson Lopez. MIT License.

using System;
using EricksonLopez.Transaction.Exceptions;
using EricksonLopez.Transaction.Resilience;
using Polly;
using Xunit;

namespace EricksonLopez.Transaction.Resilience.Tests;

public sealed class PollyTransactionExtensionsTests
{
    [Fact]
    public void HandleAmbiguousCommit_WithNullPolicyBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        PolicyBuilder builder = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.HandleAmbiguousCommit());
    }

    [Fact]
    public void HandleAmbiguousCommit_Parameterless_CreatesPolicyBuilderHandlingAmbiguousException()
    {
        // Act
        var builder = PollyTransactionExtensions.HandleAmbiguousCommit();

        // Assert
        Assert.NotNull(builder);
        var policy = builder.Retry(1);
        var ambiguousEx = new TransactionCommitException("Commit failed ambiguously", isAmbiguous: true);

        var executed = false;
        policy.Execute(() =>
        {
            if (!executed)
            {
                executed = true;
                throw ambiguousEx;
            }
        });

        Assert.True(executed);
    }

    [Fact]
    public void HandleAmbiguousCommit_Parameterless_DoesNotHandleNonAmbiguousException()
    {
        // Act
        var builder = PollyTransactionExtensions.HandleAmbiguousCommit();
        var policy = builder.Retry(1);
        var nonAmbiguousEx = new TransactionCommitException("Direct commit failed", isAmbiguous: false);

        // Assert
        Assert.Throws<TransactionCommitException>(() =>
        {
            policy.Execute(() => throw nonAmbiguousEx);
        });
    }

    [Fact]
    public void HandleAmbiguousCommit_ExtendedPolicyBuilder_ChainsHandlingAmbiguousException()
    {
        // Arrange
        var baseBuilder = Policy.Handle<InvalidOperationException>();

        // Act
        var chainedBuilder = baseBuilder.HandleAmbiguousCommit();

        // Assert
        Assert.NotNull(chainedBuilder);
        var policy = chainedBuilder.Retry(1);
        var ambiguousEx = new TransactionCommitException("Commit failed ambiguously", isAmbiguous: true);

        var executed = false;
        policy.Execute(() =>
        {
            if (!executed)
            {
                executed = true;
                throw ambiguousEx;
            }
        });

        Assert.True(executed);
    }

    [Fact]
    public void HandleAmbiguousCommit_ExtendedPolicyBuilder_DoesNotHandleNonAmbiguousException()
    {
        // Arrange
        var baseBuilder = Policy.Handle<InvalidOperationException>();

        // Act
        var chainedBuilder = baseBuilder.HandleAmbiguousCommit();
        var policy = chainedBuilder.Retry(1);
        var nonAmbiguousEx = new TransactionCommitException("Direct commit failed", isAmbiguous: false);

        // Assert
        Assert.Throws<TransactionCommitException>(() =>
        {
            policy.Execute(() => throw nonAmbiguousEx);
        });
    }
}
