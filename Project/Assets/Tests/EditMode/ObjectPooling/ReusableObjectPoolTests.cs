using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityCoreLab.ObjectPooling;

namespace UnityCoreLab.Tests.EditMode.ObjectPooling
{
    public sealed class ReusableObjectPoolTests
    {
        [Test]
        public void Prewarm_CreatesInactiveItemsWithoutRecordingRentals()
        {
            var callbacks = new RecordingCallbacks();
            using (ReusableObjectPool<PooledItem> pool = callbacks.CreatePool(3))
            {
                pool.Prewarm(2);

                PoolSnapshot snapshot = pool.Snapshot;
                Assert.That(snapshot.AvailableCount, Is.EqualTo(2));
                Assert.That(snapshot.ActiveCount, Is.Zero);
                Assert.That(snapshot.CreatedCount, Is.EqualTo(2));
                Assert.That(snapshot.RentCount, Is.Zero);
                Assert.That(callbacks.Returned.Count, Is.EqualTo(2));
            }
        }

        [Test]
        public void ReturnedItem_IsReusedAndUpdatesTheSnapshot()
        {
            var callbacks = new RecordingCallbacks();
            using (ReusableObjectPool<PooledItem> pool = callbacks.CreatePool(2))
            {
                PooledItem first = pool.Rent();
                pool.Return(first);
                PooledItem second = pool.Rent();

                Assert.That(second, Is.SameAs(first));
                Assert.That(callbacks.Rented.Count, Is.EqualTo(2));

                PoolSnapshot snapshot = pool.Snapshot;
                Assert.That(snapshot.CreatedCount, Is.EqualTo(1));
                Assert.That(snapshot.RentCount, Is.EqualTo(2));
                Assert.That(snapshot.ReusedCount, Is.EqualTo(1));
                Assert.That(snapshot.ReturnCount, Is.EqualTo(1));
                Assert.That(snapshot.ReuseRatio, Is.EqualTo(0.5d));
                Assert.That(snapshot.PeakActiveCount, Is.EqualTo(1));

                pool.Return(second);
            }
        }

        [Test]
        public void ReturningBeyondCapacity_DiscardsOverflow()
        {
            var callbacks = new RecordingCallbacks();
            using (ReusableObjectPool<PooledItem> pool = callbacks.CreatePool(1))
            {
                PooledItem first = pool.Rent();
                PooledItem second = pool.Rent();

                pool.Return(first);
                pool.Return(second);

                Assert.That(pool.AvailableCount, Is.EqualTo(1));
                Assert.That(pool.ActiveCount, Is.Zero);
                Assert.That(callbacks.Discarded, Has.Count.EqualTo(1));
                Assert.That(callbacks.Discarded[0], Is.SameAs(second));
                Assert.That(pool.Snapshot.PeakActiveCount, Is.EqualTo(2));
            }
        }

        [Test]
        public void DuplicateAndForeignReturns_AreRejected()
        {
            var callbacks = new RecordingCallbacks();
            using (ReusableObjectPool<PooledItem> pool = callbacks.CreatePool(2))
            {
                PooledItem item = pool.Rent();
                pool.Return(item);

                Assert.Throws<InvalidOperationException>(() => pool.Return(item));
                Assert.Throws<InvalidOperationException>(
                    () => pool.Return(new PooledItem(999)));
                Assert.Throws<ArgumentNullException>(() => pool.Return(null));
                Assert.That(pool.AvailableCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void Dispose_DiscardsAvailableItemsAndLateReturns()
        {
            var callbacks = new RecordingCallbacks();
            var pool = callbacks.CreatePool(2);
            pool.Prewarm(2);
            PooledItem active = pool.Rent();

            pool.Dispose();

            Assert.That(pool.Snapshot.IsDisposed, Is.True);
            Assert.That(pool.AvailableCount, Is.Zero);
            Assert.That(pool.ActiveCount, Is.EqualTo(1));
            Assert.That(callbacks.Discarded, Has.Count.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => pool.Rent());

            pool.Return(active);

            Assert.That(pool.ActiveCount, Is.Zero);
            Assert.That(pool.Snapshot.OwnedCount, Is.Zero);
            Assert.That(pool.Snapshot.DiscardedCount, Is.EqualTo(2));
            pool.Dispose();
        }

        [Test]
        public void RentCallbackFailure_DiscardsTheItemAndKeepsStateConsistent()
        {
            var discarded = new List<PooledItem>();
            int nextId = 0;
            using (var pool = new ReusableObjectPool<PooledItem>(
                () => new PooledItem(++nextId),
                _ => throw new InvalidOperationException("activation failed"),
                onDiscard: discarded.Add,
                maxRetained: 1))
            {
                InvalidOperationException failure =
                    Assert.Throws<InvalidOperationException>(() => pool.Rent());

                Assert.That(failure.Message, Is.EqualTo("activation failed"));
                Assert.That(pool.ActiveCount, Is.Zero);
                Assert.That(pool.AvailableCount, Is.Zero);
                Assert.That(pool.Snapshot.DiscardedCount, Is.EqualTo(1));
                Assert.That(discarded, Has.Count.EqualTo(1));
            }
        }

        [Test]
        public void InvalidConfigurationAndFactoryResults_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(
                () => new ReusableObjectPool<PooledItem>(null));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ReusableObjectPool<PooledItem>(
                    () => new PooledItem(1),
                    maxRetained: -1));

            using (var pool = new ReusableObjectPool<PooledItem>(
                () => null,
                maxRetained: 1))
            {
                Assert.Throws<InvalidOperationException>(() => pool.Rent());
            }

            using (var pool = new ReusableObjectPool<PooledItem>(
                () => new PooledItem(1),
                maxRetained: 1))
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => pool.Prewarm(2));
            }
        }

        [Test]
        public void Dispose_AttemptsEveryAvailableDiscardBeforeAggregatingFailures()
        {
            int discardAttempts = 0;
            var pool = new ReusableObjectPool<PooledItem>(
                () => new PooledItem(discardAttempts + 1),
                onDiscard: _ =>
                {
                    discardAttempts++;
                    throw new InvalidOperationException("discard failed");
                },
                maxRetained: 2);
            pool.Prewarm(2);

            AggregateException failure =
                Assert.Throws<AggregateException>(() => pool.Dispose());

            Assert.That(discardAttempts, Is.EqualTo(2));
            Assert.That(failure.InnerExceptions, Has.Count.EqualTo(2));
            Assert.That(pool.AvailableCount, Is.Zero);
            Assert.That(pool.Snapshot.DiscardedCount, Is.EqualTo(2));
        }

        private sealed class RecordingCallbacks
        {
            private int _nextId;

            public List<PooledItem> Rented { get; } = new List<PooledItem>();

            public List<PooledItem> Returned { get; } = new List<PooledItem>();

            public List<PooledItem> Discarded { get; } = new List<PooledItem>();

            public ReusableObjectPool<PooledItem> CreatePool(int maxRetained)
            {
                return new ReusableObjectPool<PooledItem>(
                    () => new PooledItem(++_nextId),
                    Rented.Add,
                    Returned.Add,
                    Discarded.Add,
                    maxRetained);
            }
        }

        private sealed class PooledItem
        {
            public PooledItem(int id)
            {
                Id = id;
            }

            public int Id { get; }
        }
    }
}
