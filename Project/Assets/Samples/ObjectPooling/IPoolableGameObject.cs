namespace UnityCoreLab.Samples.ObjectPooling
{
    /// <summary>
    /// Defines the state transition hooks for components on a pooled hierarchy.
    /// Implementations should restore reusable runtime state without allocating.
    /// </summary>
    public interface IPoolableGameObject
    {
        void OnRentFromPool();

        void OnReturnToPool();
    }
}
