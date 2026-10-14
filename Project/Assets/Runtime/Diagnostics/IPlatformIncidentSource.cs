namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Adapter boundary for platform code or a crash SDK. Peek must leave the
    /// record available until it is explicitly acknowledged.
    /// </summary>
    public interface IPlatformIncidentSource
    {
        bool TryPeek(out PlatformIncidentRecord incident);

        void Acknowledge(string incidentId);
    }
}
