namespace OpenLethe.Data;

/// A JavaScript script installed by one user. It always executes against the
/// installing account; cross-account access is never available to user scripts.
public sealed class UserScript
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Name { get; set; } = "";
    public string Source { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public long Revision { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// Admin-controlled grant: which capabilities a specific user may use with a
/// specific script. Absence of a row means only the baseline capabilities.
public sealed class ScriptGrant
{
    public Guid Id { get; set; }
    public Guid ScriptId { get; set; }
    public Guid GranteeAccountId { get; set; }
    /// JSON array of capability strings, e.g. ["packet.modify","timer"].
    public string Capabilities { get; set; } = "[]";
    public DateTime GrantedAt { get; set; }
}
