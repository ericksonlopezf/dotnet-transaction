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
        int current = Volatile.Read(ref _state);
        if (current == (int)TransactionState.RolledBack || current == (int)TransactionState.Disposed)
        {
            return;
        }

        if (current == (int)TransactionState.Committed)
        {
            throw new TransactionStateException(TransactionState.Committed, "Rollback");
        }

        Interlocked.CompareExchange(ref _state, (int)TransactionState.RolledBack, current);
    }

    public void TransitionToFailed()
    {
        int current = Volatile.Read(ref _state);
        if (current == (int)TransactionState.Committed || current == (int)TransactionState.Failed || current == (int)TransactionState.RolledBack || current == (int)TransactionState.Disposed)
        {
            return;
        }

        Interlocked.CompareExchange(ref _state, (int)TransactionState.Failed, current);
    }

    public void TransitionToDisposed()
    {
        Interlocked.Exchange(ref _state, (int)TransactionState.Disposed);
    }
}
