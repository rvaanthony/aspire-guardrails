#if (!UseEfCore)
namespace Template.Data.Storage;

/// <summary>
/// Connection names shared with the AppHost resource graph. When running under
/// Aspire these resolve automatically; when deployed standalone they map to
/// ConnectionStrings:{name} configuration entries.
/// </summary>
public static class StorageConnectionNames
{
    public const string Tables = "tables";
    public const string Blobs = "blobs";
    public const string Queues = "queues";
}
#endif
