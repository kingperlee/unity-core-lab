using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityCoreLab.Samples.AssetLifecycle
{
    /// <summary>
    /// Owns acquired leases for one GameObject lifetime. Destroying the GameObject
    /// releases every tracked lease exactly once.
    /// </summary>
    public sealed class AssetLeaseScope : MonoBehaviour
    {
        private readonly List<IDisposable> _leases = new List<IDisposable>();
        private bool _destroyed;

        public int TrackedLeaseCount => _leases.Count;

        internal bool TryTrack(IDisposable lease)
        {
            if (lease == null)
            {
                throw new ArgumentNullException(nameof(lease));
            }

            if (_destroyed)
            {
                return false;
            }

            _leases.Add(lease);
            return true;
        }

        private void OnDestroy()
        {
            _destroyed = true;

            for (int index = _leases.Count - 1; index >= 0; index--)
            {
                try
                {
                    _leases[index].Dispose();
                }
                catch (Exception exception)
                {
                    // Unity lifecycle callbacks must finish releasing the remaining
                    // leases even when one provider reports a cleanup failure.
                    Debug.LogException(exception, this);
                }
            }

            _leases.Clear();
        }
    }
}
