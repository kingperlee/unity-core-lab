using UnityEngine;

namespace UnityCoreLab.Samples.ObjectPooling
{
    /// <summary>
    /// Small visible component used to demonstrate reusable runtime state.
    /// </summary>
    public sealed class PooledDemoActor : MonoBehaviour, IPoolableGameObject
    {
        public int RentCount { get; private set; }

        public int ResetCount { get; private set; }

        public float ActiveTime { get; private set; }

        public Vector3 Velocity { get; private set; }

        public void Configure(Vector3 velocity)
        {
            Velocity = velocity;
        }

        public void OnRentFromPool()
        {
            RentCount++;
            ActiveTime = 0f;
            Velocity = Vector3.zero;
        }

        public void OnReturnToPool()
        {
            ResetCount++;
            ActiveTime = 0f;
            Velocity = Vector3.zero;
        }

        private void Update()
        {
            ActiveTime += Time.deltaTime;
            transform.localPosition += Velocity * Time.deltaTime;

            Vector3 position = transform.localPosition;
            if (Mathf.Abs(position.x) > 4.5f)
            {
                Velocity = new Vector3(-Velocity.x, Velocity.y, Velocity.z);
            }

            if (Mathf.Abs(position.y) > 3f)
            {
                Velocity = new Vector3(Velocity.x, -Velocity.y, Velocity.z);
            }
        }
    }
}
