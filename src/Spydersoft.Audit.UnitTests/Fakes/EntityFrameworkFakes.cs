namespace Spydersoft.Audit.UnitTests.Fakes;

/// <summary>
/// Test-only fakes that mirror the public shape of Audit.EntityFramework.Core's
/// EntityFrameworkEvent / EventEntry / EventEntryChange types. The production
/// MessagingAuditDataProvider reads these via <c>dynamic</c>, which respects
/// accessibility — so these have to be <c>public</c> so the production assembly's
/// dynamic dispatch can resolve their members.
/// </summary>
public sealed class FakeEntityFrameworkEvent
{
    public List<FakeEventEntry> Entries { get; set; } = new();
}

public sealed class FakeEventEntry
{
    public string EntityType { get; set; } = "";
    public string Action { get; set; } = "";
    public Dictionary<string, object> PrimaryKey { get; set; } = new();
    public Dictionary<string, object>? ColumnValues { get; set; }
    public List<FakeEventEntryChange>? Changes { get; set; }

    public static FakeEventEntry Insert(string entityType, object primaryKey, Dictionary<string, object> values) =>
        new()
        {
            EntityType = entityType,
            Action = "Insert",
            PrimaryKey = new() { ["Id"] = primaryKey },
            ColumnValues = values,
        };

    public static FakeEventEntry Delete(string entityType, object primaryKey) =>
        new()
        {
            EntityType = entityType,
            Action = "Delete",
            PrimaryKey = new() { ["Id"] = primaryKey },
        };

    public static FakeEventEntry Update(
        string entityType,
        object primaryKey,
        Dictionary<string, object> columnValues,
        List<FakeEventEntryChange> changes) =>
        new()
        {
            EntityType = entityType,
            Action = "Update",
            PrimaryKey = new() { ["Id"] = primaryKey },
            ColumnValues = columnValues,
            Changes = changes,
        };
}

public sealed class FakeEventEntryChange
{
    public string ColumnName { get; set; } = "";
    public object? OriginalValue { get; set; }
    public object? NewValue { get; set; }
}
