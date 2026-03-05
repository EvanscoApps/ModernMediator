namespace ModernMediator
{
    /// <summary>
    /// Configuration options for ModernMediator telemetry instrumentation.
    /// Reserved for future runtime toggling; the generated instrumentation is always
    /// active but incurs zero cost when no listener is attached.
    /// </summary>
    public class TelemetryOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether telemetry instrumentation is enabled.
        /// Defaults to <c>true</c>.
        /// </summary>
        public bool Enabled { get; set; } = true;
    }
}
