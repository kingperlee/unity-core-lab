using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnityCoreLab.ObjectPooling
{
    /// <summary>
    /// A single-threaded pool that makes retention, overflow, and ownership
    /// policies observable. Unity-specific activation and reset behavior belongs
    /// in an adapter rather than this core type.
    /// </summary>
    public sealed class ReusableObjectPool<T> : IDisposable
        where T : class
    {
        private readonly Func<T> _create;
        private readonly Action<T> _onRent;
        private readonly Action<T> _onReturn;
        private readonly Action<T> _onDiscard;
        private readonly Stack<T> _available = new Stack<T>();
        private readonly HashSet<T> _active =
            new HashSet<T>(ReferenceComparer.Instance);
        private readonly HashSet<T> _owned =
            new HashSet<T>(ReferenceComparer.Instance);
        private readonly int _maxRetained;

        private int _createdCount;
        private int _rentCount;
        private int _reusedCount;
        private int _returnCount;
        private int _discardedCount;
        private int _peakActiveCount;
        private bool _disposed;

        public ReusableObjectPool(
            Func<T> create,
            Action<T> onRent = null,
            Action<T> onReturn = null,
            Action<T> onDiscard = null,
            int maxRetained = 32)
        {
            _create = create ?? throw new ArgumentNullException(nameof(create));
            if (maxRetained < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxRetained),
                    "The retained capacity cannot be negative.");
            }

            _onRent = onRent;
            _onReturn = onReturn;
            _onDiscard = onDiscard;
            _maxRetained = maxRetained;
        }

        public int MaxRetained => _maxRetained;

        public int AvailableCount => _available.Count;

        public int ActiveCount => _active.Count;

        public PoolSnapshot Snapshot => new PoolSnapshot(
            _available.Count,
            _active.Count,
            _createdCount,
            _rentCount,
            _reusedCount,
            _returnCount,
            _discardedCount,
            _peakActiveCount,
            _disposed);

        public void Prewarm(int targetAvailableCount)
        {
            ThrowIfDisposed();
            if (targetAvailableCount < 0 || targetAvailableCount > _maxRetained)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(targetAvailableCount),
                    $"Prewarm target must be between 0 and {_maxRetained}.");
            }

            while (_available.Count < targetAvailableCount)
            {
                T item = CreateOwnedItem();
                try
                {
                    _onReturn?.Invoke(item);
                }
                catch (Exception returnFailure)
                {
                    ThrowAfterDiscard(
                        item,
                        returnFailure,
                        "Pool prewarm reset and discard both failed.");
                }

                _available.Push(item);
            }
        }

        public T Rent()
        {
            ThrowIfDisposed();

            T item;
            if (_available.Count > 0)
            {
                item = _available.Pop();
                _reusedCount++;
            }
            else
            {
                item = CreateOwnedItem();
            }

            if (!_active.Add(item))
            {
                throw new InvalidOperationException(
                    "The pool attempted to rent an item that is already active.");
            }

            _rentCount++;
            _peakActiveCount = Math.Max(_peakActiveCount, _active.Count);

            try
            {
                _onRent?.Invoke(item);
            }
            catch (Exception rentFailure)
            {
                _active.Remove(item);
                ThrowAfterDiscard(
                    item,
                    rentFailure,
                    "Pool rent activation and discard both failed.");
            }

            return item;
        }

        public void Return(T item)
        {
            if (item == null)
            {
                throw new ArgumentNullException(nameof(item));
            }

            if (!_active.Remove(item))
            {
                throw new InvalidOperationException(
                    "The item is not an active rental from this pool. " +
                    "It may belong to another pool or have already been returned.");
            }

            _returnCount++;
            try
            {
                _onReturn?.Invoke(item);
            }
            catch (Exception returnFailure)
            {
                ThrowAfterDiscard(
                    item,
                    returnFailure,
                    "Pool return reset and discard both failed.");
            }

            if (!_disposed && _available.Count < _maxRetained)
            {
                _available.Push(item);
                return;
            }

            Discard(item);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            List<Exception> failures = null;
            while (_available.Count > 0)
            {
                T item = _available.Pop();
                try
                {
                    Discard(item);
                }
                catch (Exception exception)
                {
                    if (failures == null)
                    {
                        failures = new List<Exception>();
                    }

                    failures.Add(exception);
                }
            }

            if (failures != null)
            {
                throw new AggregateException(
                    "One or more pooled items failed to discard.",
                    failures);
            }
        }

        private T CreateOwnedItem()
        {
            T item = _create();
            if (item == null)
            {
                throw new InvalidOperationException(
                    "The pool factory returned null.");
            }

            if (!_owned.Add(item))
            {
                throw new InvalidOperationException(
                    "The pool factory returned an item that this pool already owns.");
            }

            _createdCount++;
            return item;
        }

        private void ThrowAfterDiscard(
            T item,
            Exception operationFailure,
            string aggregateMessage)
        {
            try
            {
                Discard(item);
            }
            catch (Exception discardFailure)
            {
                throw new AggregateException(
                    aggregateMessage,
                    operationFailure,
                    discardFailure);
            }

            throw operationFailure;
        }

        private void Discard(T item)
        {
            _owned.Remove(item);
            try
            {
                _onDiscard?.Invoke(item);
            }
            finally
            {
                _discardedCount++;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(
                    nameof(ReusableObjectPool<T>));
            }
        }

        private sealed class ReferenceComparer : IEqualityComparer<T>
        {
            public static readonly ReferenceComparer Instance =
                new ReferenceComparer();

            public bool Equals(T x, T y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
