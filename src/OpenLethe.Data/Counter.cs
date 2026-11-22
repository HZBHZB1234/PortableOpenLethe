namespace OpenLethe.Data;

/// SQLite has no sequences and AUTOINCREMENT only applies to INTEGER PRIMARY KEY.
/// IngameId is a unique non-key column, so it is allocated from this counter table
/// inside a transaction (see AccountStore.CreateAsync).
public sealed class Counter
{
    public string Key { get; set; } = "";
    public long Value { get; set; }
}
