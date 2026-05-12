namespace Spydersoft.Audit;

/// <summary>
/// Configuration for the Spydersoft.Audit publish side.
/// Bound from the "Audit" configuration section by <c>AddSpydersoftAudit</c>.
/// </summary>
public class AuditOptions
{
    /// <summary>
    /// Configuration section name (<c>Audit</c>).
    /// </summary>
    public const string SectionName = "Audit";

    /// <summary>
    /// Name of this application, stamped on every AuditRecord as <see cref="AuditRecord.Source"/>.
    /// Required — configuration is invalid if empty.
    /// </summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>
    /// RabbitMQ topic to publish audit records to. Defaults to "audit.events".
    /// </summary>
    public string Topic { get; set; } = "audit.events";
}
