using System;

namespace UnityCoreLab.Diagnostics
{
    /// <summary>
    /// Stable environment metadata captured with a diagnostic report.
    /// Unity-specific APIs are adapted by the sample layer.
    /// </summary>
    public sealed class RuntimeDiagnosticContext
    {
        public RuntimeDiagnosticContext(
            string sessionId,
            string applicationName,
            string applicationVersion,
            string platform,
            string deviceModel,
            string operatingSystem,
            string graphicsDeviceName)
        {
            SessionId = Normalize(sessionId);
            ApplicationName = Normalize(applicationName);
            ApplicationVersion = Normalize(applicationVersion);
            Platform = Normalize(platform);
            DeviceModel = Normalize(deviceModel);
            OperatingSystem = Normalize(operatingSystem);
            GraphicsDeviceName = Normalize(graphicsDeviceName);
        }

        public string SessionId { get; }

        public string ApplicationName { get; }

        public string ApplicationVersion { get; }

        public string Platform { get; }

        public string DeviceModel { get; }

        public string OperatingSystem { get; }

        public string GraphicsDeviceName { get; }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "Unknown" : value;
        }
    }
}
