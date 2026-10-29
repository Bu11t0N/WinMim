using System.Text.Json.Serialization;

namespace AeroForge.Core;

public sealed class ValueState
{
    public bool Exists { get; set; }
    public string Kind { get; set; } = "";
    public byte[]? Bytes { get; set; }
    public string? Value { get; set; }
    public string Fingerprint => !Exists ? "absent" : Kind == "File"
        ? "file:" + Hashing.Bytes(Bytes ?? []) : "reg:" + Kind + ":" + Hashing.Text(Kind=="ClassicAppearance"?ClassicAppearance.StableValue(Value!):Value ?? "");
    public static ValueState Desired(Change change) => change.Kind == ChangeKind.File
        ? new() { Exists = change.Content != null, Kind = "File", Bytes = change.Content }
        : new() { Exists = change.Value != null, Kind = change.ValueKind, Value = change.Value };
}
public interface IChangeBackend
{
    void Validate(Change change);
    ValueState Read(Change change);
    void Write(Change change, ValueState state);
}
public sealed class JournalEntry
{
    public Change Change { get; set; } = new();
    public ValueState Before { get; set; } = new();
    public string ExpectedAfter { get; set; } = "";
    public string State { get; set; } = "Pending";
    public string? Error { get; set; }
}
public sealed class Receipt
{
    public int SchemaVersion { get; set; } = 1;
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTimeOffset Created { get; set; } = DateTimeOffset.UtcNow;
    public string ProfileId { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public string Status { get; set; } = "Preparing";
    public string? Error { get; set; }
    public List<JournalEntry> Entries { get; set; } = [];
    [JsonIgnore] public string FilePath { get; set; } = "";
}
public sealed class ApplyException(string message, Receipt receipt, Exception inner) : Exception(message, inner)
{ public Receipt Receipt { get; } = receipt; }

// Listing status must not deserialize every historical DLL backup into memory.
internal sealed class ReceiptMetadata
{
    public int SchemaVersion { get; set; }=1;
    public string Id { get; set; }="";
    public DateTimeOffset Created { get; set; }
    public string ProfileId { get; set; }="";
    public string ProfileName { get; set; }="";
    public string Status { get; set; }="";
    public string? Error { get; set; }
    public Receipt Receipt()=>new(){SchemaVersion=SchemaVersion,Id=Id,Created=Created,ProfileId=ProfileId,ProfileName=ProfileName,Status=Status,Error=Error};
}

/// <summary>Write-ahead journal: snapshot is flushed before every mutation, including a failing operation.</summary>
public sealed class TransactionManager(IChangeBackend backend, string journalRoot)
{
    public string JournalRoot { get; } = Path.GetFullPath(journalRoot);
    private IDisposable Lock()
    {
        Directory.CreateDirectory(JournalRoot);
        var lockPath = SafePath.Under(JournalRoot, "transaction.lock");
        try { return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException("Другой процесс выполняет изменение. Дождитесь его завершения.", ex); }
    }
    public IReadOnlyList<Receipt> History(bool includeEntries=true)
    {
        if (!Directory.Exists(JournalRoot)) return [];
        List<Receipt> result = [];
        foreach (var file in Directory.EnumerateFiles(JournalRoot, "*.json"))
        {
            string safe=SafePath.Under(JournalRoot,Path.GetFileName(file));
            var r = includeEntries?JsonFile.Read<Receipt>(safe):JsonFile.Read<ReceiptMetadata>(safe).Receipt();
            if (r.SchemaVersion != 1) throw new InvalidDataException("Unsupported journal schema");
            r.FilePath = file; result.Add(r);
        }
        return result.OrderByDescending(r => r.Created).ThenByDescending(r => r.Id).ToArray();
    }
    public Receipt Apply(Plan plan, IProgress<string>? progress = null, CancellationToken cancellationToken = default, bool allowProfileUpdate = false)
    {
        if (plan.Blockers.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, plan.Blockers));
        if (plan.Changes.Select(c => c.Identity).Distinct().Count() != plan.Changes.Count)
            throw new InvalidDataException("Duplicate plan targets");
        foreach (var c in plan.Changes) backend.Validate(c);
        using var transactionLock = Lock();
        var history = History(false);
        if (history.Any(r => r.Status is not ("Applied" or "RolledBack")))
            throw new InvalidOperationException("Есть незавершённая операция. Сначала восстановите её в разделе «Восстановление».");
        if (plan.Exclusive && history.FirstOrDefault(r => r.Status == "Applied") is { } active)
        {
            active=ReadReceipt(active.FilePath);
            bool same = active.ProfileId == plan.ProfileId && active.Entries.Count == plan.Changes.Count
                && active.Entries.Zip(plan.Changes).All(pair => pair.First.Change.Identity == pair.Second.Identity
                    && pair.First.ExpectedAfter == ValueState.Desired(pair.Second).Fingerprint
                    && backend.Read(pair.Second).Fingerprint == pair.First.ExpectedAfter);
            if (same) return active;
            // A small preferences layer does not relinquish ownership of settings
            // in older layers. Compare the latest expected value for every target.
            var owned=history.Where(r=>r.Status=="Applied").TakeWhile(r=>r.ProfileId==active.ProfileId)
                .SelectMany(r=>ReadReceipt(r.FilePath).Entries).GroupBy(entry=>entry.Change.Identity).Select(group=>group.First());
            if (!allowProfileUpdate || active.ProfileId != plan.ProfileId
                || owned.Any(entry => !plan.AdoptUserPreferences.Contains(entry.Change.Identity) && backend.Read(entry.Change).Fingerprint != entry.ExpectedAfter))
                throw new InvalidOperationException("Оформление уже применено или изменено после применения. Сначала верните его настройки; чужие изменения будут сохранены.");
            // Append an update layer; never replace the original undo snapshot.
        }
        var receipt = new Receipt { ProfileId = plan.ProfileId, ProfileName = plan.ProfileName, Status = "Applying" };
        receipt.FilePath = SafePath.Under(JournalRoot, receipt.Id + ".json");
        Save(receipt);
        try
        {
            foreach (var c in plan.Changes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(c.Description);
                var desired = ValueState.Desired(c);
                var before=backend.Read(c);
                if(!plan.Exclusive && before.Fingerprint==desired.Fingerprint)continue;
                var entry = new JournalEntry
                {
                    Change = new Change { Kind = c.Kind, Target = c.Target, ValueName = c.ValueName,
                        ValueKind = c.ValueKind, Description = c.Description },
                    Before = before, ExpectedAfter = desired.Fingerprint
                };
                receipt.Entries.Add(entry);
                Save(receipt); // The Pending entry is recoverable even if the process dies immediately after Write.
                if(entry.Before.Fingerprint!=desired.Fingerprint)backend.Write(c, desired);
                if (backend.Read(c).Fingerprint != desired.Fingerprint)
                    throw new IOException("Read-after-write verification failed: " + c.Target);
                entry.State = "Applied"; Save(receipt);
            }
            receipt.Status = "Applied"; Save(receipt);
            return receipt;
        }
        catch (Exception ex)
        {
            receipt.Error = ex.Message;
            RollbackInternal(receipt, progress);
            throw new ApplyException("Применение прервано; состояние отката: " + receipt.Status, receipt, ex);
        }
    }
    public Receipt Rollback(string receiptPath, IProgress<string>? progress = null,IReadOnlySet<string>? preserveUserChanges=null)
    {
        using var transactionLock = Lock();
        string safe = SafePath.Under(JournalRoot, Path.GetFileName(receiptPath));
        if (!Path.GetFullPath(receiptPath).Equals(safe, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Receipt is outside the journal directory");
        var history = History(false);
        var receipt = ReadReceipt(history.Single(r => r.FilePath.Equals(safe, StringComparison.OrdinalIgnoreCase)).FilePath);
        if (receipt.Status == "RolledBack") return receipt;
        var latest = history.FirstOrDefault(r => r.Status != "RolledBack");
        if (latest?.Id != receipt.Id) throw new InvalidOperationException("Сначала откатите более позднюю операцию.");
        foreach (var entry in receipt.Entries) backend.Validate(entry.Change);
        return RollbackInternal(receipt, progress,preserveUserChanges);
    }
    private Receipt RollbackInternal(Receipt receipt, IProgress<string>? progress,IReadOnlySet<string>? preserveUserChanges=null)
    {
        receipt.Status = "RollingBack"; Save(receipt);
        foreach (var entry in receipt.Entries.AsEnumerable().Reverse())
        {
            if (entry.State is "RolledBack" or "Preserved") continue;
            try
            {
                progress?.Report("Восстановление: " + entry.Change.Description);
                string current = backend.Read(entry.Change).Fingerprint;
                if (current != entry.Before.Fingerprint)
                {
                    if (current != entry.ExpectedAfter)
                    {
                        if(preserveUserChanges?.Contains(entry.Change.Identity)==true)
                        {entry.State="Preserved";entry.Error=null;Save(receipt);continue;}
                        throw new IOException("Значение изменено другой программой. Оно сохранено: " + entry.Change.Target);
                    }
                    backend.Write(entry.Change, entry.Before);
                    if (backend.Read(entry.Change).Fingerprint != entry.Before.Fingerprint)
                        throw new IOException("Проверка восстановления не пройдена: " + entry.Change.Target);
                }
                entry.State = "RolledBack"; entry.Error = null;
            }
            catch (Exception ex) { entry.State = "Conflict"; entry.Error = ex.Message; }
            Save(receipt);
        }
        receipt.Status = receipt.Entries.All(e => e.State is "RolledBack" or "Preserved") ? "RolledBack" : "RollbackIncomplete";
        Save(receipt); return receipt;
    }
    private static void Save(Receipt receipt) => JsonFile.Write(receipt.FilePath, receipt);
    private static Receipt ReadReceipt(string path)
    {var receipt=JsonFile.Read<Receipt>(path);receipt.FilePath=path;return receipt;}
}
