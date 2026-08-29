// Copyright © Erickson Lopez. MIT License.
using System.Threading;
using EricksonLopez.Transaction.Exceptions;

namespace EricksonLopez.Transaction.Internal;

/// <summary>
/// Enforces deterministic, atomic state transitions for the transaction lifecycle.
/// </summary>
internal sealed class TransactionStateMachine
{
    private int _state;

    public TransactionStateMachine(TransactionState initialState = TransactionState.Created)
    {
        _state = (int)initialState;
    }

    public TransactionState CurrentState => (TransactionState)Volatile.Read(ref _state);

    public void TransitionToActive()
    {
        int initial = Interlocked.CompareExchange(ref _state, (int)TransactionState.Active, (int)TransactionState.Created);
        if (initial != (int)TransactionState.Created)
        {
            throw new TransactionStateException((TransactionState)initial, "Begin");
        }
    }

    public void TransitionToCommitted()
    {
        int initial = Interlocked.CompareExchange(ref _state, (int)TransactionState.Committed, (int)TransactionState.Active);
        if (initial != (int)TransactionState.Active)
        {
            throw new TransactionStateException((TransactionState)initial, "Commit");
        }
    }

    public void TransitionToRolledBack()
    {
        // Rollback is allowed from Active or Failed
        while (true)
        {
            int current = Volatile.Read(ref _state);
            if (current == (int)TransactionState.RolledBack || current == (int)TransactionState.Disposed)
            {
                return; // Idempotent or already disposed
            }

            if (current != (int)TransactionState.Active && current != (int)TransactionState.Failed && current != (int)TransactionState.Created)
            {
                throw new TransactionStateException((TransactionState)current, "Rollback");
            }

            if (Interlocked.CompareExchange(ref _state, (int)TransactionState.RolledBack, current) == current)
            {
                return;
            }
        }
    }

    public void TransitionToFailed()
    {
        while (true)
        {
            int current = Volatile.Read(ref _state);
            if (current == (int)TransactionState.Failed || current == (int)TransactionState.RolledBack || current == (int)TransactionState.Disposed)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _state, (int)TransactionState.Failed, current) == current)
            {
                return;
            }
        }
    }

    public void TransitionToDisposed()
    {
        Interlocked.Exchange(ref _state, (int)TransactionState.Disposed);
    }
}
