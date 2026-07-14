using System.Text.Json;

namespace DMC.Core
{
    /// <summary>
    /// Operation types recorded in the commit log.
    /// </summary>
    public enum LogOperation
    {
        DefineType,
        Set,
        Update,
        UpdateSchema
    }

    /// <summary>
    /// A single entry in the commit log. Each entry represents one atomic operation.
    /// </summary>
    public class LogEntry
    {
        public long Offset { get; set; }
        public DateTime Timestamp { get; set; }
        public LogOperation Operation { get; set; }
        public string Type { get; set; } = "";
        public string? Key { get; set; }
        public string? Owner { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
        public List<string>? IdentityKeys { get; set; }
        public bool Merge { get; set; } = true;
    }

    /// <summary>
    /// Kafka-style append-only commit log.
    /// The log is the source of truth — in-memory registry is a derived view.
    /// 
    /// Design principles (from Kafka):
    /// - Append-only: never modify existing entries
    /// - Sequential offsets: each entry has a monotonically increasing offset
    /// - Replay: rebuild state by replaying the entire log
    /// - Compaction: keep only the latest entry per key to control log size
    /// </summary>
    public class CommitLog
    {
        private readonly string _logPath;
        private long _currentOffset;
        private readonly object _fileLock = new object();

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public CommitLog(string logPath)
        {
            _logPath = logPath;
            _currentOffset = 0;

            // Ensure directory exists
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Resume offset from existing log
            if (File.Exists(logPath))
            {
                foreach (var _ in ReadAll())
                    _currentOffset++;
            }
        }

        /// <summary>
        /// Append an operation to the commit log (disk write).
        /// Called BEFORE modifying in-memory state.
        /// </summary>
        public LogEntry Append(LogOperation operation, string type,
            string? key = null, string? owner = null,
            Dictionary<string, string>? properties = null,
            List<string>? identityKeys = null, bool merge = true)
        {
            var entry = new LogEntry
            {
                Offset = _currentOffset++,
                Timestamp = DateTime.UtcNow,
                Operation = operation,
                Type = type,
                Key = key,
                Owner = owner,
                Properties = properties != null ? new Dictionary<string, string>(properties) : null,
                IdentityKeys = identityKeys != null ? new List<string>(identityKeys) : null,
                Merge = merge
            };

            string json = JsonSerializer.Serialize(entry, _jsonOptions);

            lock (_fileLock)
            {
                File.AppendAllText(_logPath, json + Environment.NewLine);
            }

            return entry;
        }

        /// <summary>
        /// Read all entries from the commit log (for replay on startup).
        /// </summary>
        public IEnumerable<LogEntry> ReadAll()
        {
            if (!File.Exists(_logPath))
                yield break;

            foreach (var line in File.ReadLines(_logPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var entry = JsonSerializer.Deserialize<LogEntry>(line, _jsonOptions);
                if (entry != null)
                    yield return entry;
            }
        }

        /// <summary>
        /// Log compaction: keep only the latest entry per key.
        /// Reduces log size while preserving the final state.
        /// Similar to Kafka's log compaction strategy.
        /// </summary>
        public int Compact()
        {
            if (!File.Exists(_logPath))
                return 0;

            var entries = ReadAll().ToList();
            int beforeCount = entries.Count;

            // Keep schema operations (DefineType, UpdateSchema) — they're rare and important
            // For data operations (Set, Update) — keep only the latest per key
            var schemaEntries = entries.Where(e =>
                e.Operation == LogOperation.DefineType ||
                e.Operation == LogOperation.UpdateSchema).ToList();

            var dataEntries = entries.Where(e =>
                e.Operation == LogOperation.Set ||
                e.Operation == LogOperation.Update).ToList();

            // Keep only the latest entry per key
            var latestPerKey = dataEntries
                .GroupBy(e => e.Key ?? "")
                .Select(g => g.OrderByDescending(e => e.Offset).First())
                .ToList();

            // Rebuild log: schemas first, then latest data entries (re-number offsets)
            var compacted = schemaEntries
                .Concat(latestPerKey)
                .OrderBy(e => e.Offset)
                .ToList();

            long newOffset = 0;
            foreach (var entry in compacted)
                entry.Offset = newOffset++;

            // Atomic write: write to temp file, then rename
            string tempPath = _logPath + ".tmp";
            var lines = compacted.Select(e => JsonSerializer.Serialize(e, _jsonOptions));
            File.WriteAllLines(tempPath, lines);
            File.Move(tempPath, _logPath, overwrite: true);

            _currentOffset = newOffset;

            return beforeCount - compacted.Count;
        }

        /// <summary>
        /// Get the current log size (number of entries).
        /// </summary>
        public long CurrentOffset => _currentOffset;

        /// <summary>
        /// Get the log file path.
        /// </summary>
        public string LogPath => _logPath;
    }
}
