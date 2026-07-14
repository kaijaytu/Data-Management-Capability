using DMC.Common;
using DMC.Common.DataElements;

namespace DMC.Core
{
    public enum SetAction { Created, Updated, AlreadyExists }
    public record SetResult(SetAction Action, string Key);

    /// <summary>
    /// Singleton DMC Server — centralized data registry for all Data Elements.
    /// V2: Schema layer (DefineType) + Data layer (Set/Search/Print).
    /// Kafka-style: CommitLog is source of truth, _registry is derived view.
    /// </summary>
    public class DMCServer
    {
        private static DMCServer? _instance;
        private static readonly object _lock = new object();

        private readonly Dictionary<string, IDataElement> _registry;
        private readonly Dictionary<string, List<string>> _typeSchemas;
        private readonly Dictionary<string, int> _typeCounters;
        private readonly Dictionary<string, string> _identityIndex;
        private CommitLog? _commitLog;

        private DMCServer()
        {
            _registry = new Dictionary<string, IDataElement>();
            _typeSchemas = new Dictionary<string, List<string>>();
            _typeCounters = new Dictionary<string, int>();
            _identityIndex = new Dictionary<string, string>();
        }

        // =================================================================
        // Commit Log — Kafka-style persistence
        // =================================================================

        /// <summary>
        /// Enable commit log persistence. Replays existing log to rebuild state.
        /// Must be called after Instance is created, before any operations.
        /// </summary>
        public int EnableCommitLog(string logPath)
        {
            lock (_lock)
            {
                _commitLog = new CommitLog(logPath);
                return ReplayLog();
            }
        }

        /// <summary>
        /// Replay all entries from the commit log to rebuild in-memory state.
        /// Called on startup — the log is the source of truth.
        /// </summary>
        private int ReplayLog()
        {
            if (_commitLog == null) return 0;

            int replayed = 0;
            foreach (var entry in _commitLog.ReadAll())
            {
                switch (entry.Operation)
                {
                    case LogOperation.DefineType:
                        if (entry.IdentityKeys != null && !_typeSchemas.ContainsKey(entry.Type))
                            _typeSchemas[entry.Type] = new List<string>(entry.IdentityKeys);
                        break;

                    case LogOperation.Set:
                        if (entry.Properties != null && _typeSchemas.TryGetValue(entry.Type, out var idKeys))
                        {
                            string identityKey = BuildIdentityKey(entry.Type, entry.Properties, idKeys);
                            if (_identityIndex.TryGetValue(identityKey, out var existingKey))
                            {
                                UpdateProperties(existingKey, entry.Properties, entry.Merge);
                            }
                            else
                            {
                                string key = entry.Key ?? GenerateKey(entry.Type);
                                var element = new GenericDataElement(
                                    entry.Type,
                                    new Dictionary<string, string>(entry.Properties),
                                    key,
                                    entry.Owner ?? "");
                                _registry[key] = element;
                                _identityIndex[identityKey] = key;
                                // Sync counter to avoid key collision after replay
                                SyncCounter(entry.Type, key);
                            }
                        }
                        break;

                    case LogOperation.Update:
                        if (entry.Key != null && entry.Properties != null && _registry.ContainsKey(entry.Key))
                            UpdateProperties(entry.Key, entry.Properties, entry.Merge);
                        break;

                    case LogOperation.UpdateSchema:
                        if (entry.IdentityKeys != null && _typeSchemas.ContainsKey(entry.Type))
                        {
                            _typeSchemas[entry.Type] = new List<string>(entry.IdentityKeys);
                            RebuildIdentityIndexForType(entry.Type, entry.IdentityKeys);
                        }
                        break;
                }
                replayed++;
            }
            return replayed;
        }

        /// <summary>
        /// Sync the auto-increment counter after replaying a key like "Car:5".
        /// </summary>
        private void SyncCounter(string type, string key)
        {
            var prefix = type + ":";
            if (key.StartsWith(prefix) && int.TryParse(key[prefix.Length..], out int num))
            {
                if (!_typeCounters.ContainsKey(type) || _typeCounters[type] < num)
                    _typeCounters[type] = num;
            }
        }

        /// <summary>
        /// Rebuild identity index for a specific type (used during schema replay).
        /// </summary>
        private void RebuildIdentityIndexForType(string type, List<string> identityKeys)
        {
            var oldEntries = _identityIndex
                .Where(kv => kv.Key.StartsWith(type + "\x1F"))
                .Select(kv => kv.Key).ToList();
            foreach (var k in oldEntries) _identityIndex.Remove(k);

            foreach (var elem in _registry.Values.Where(e => e.Type == type))
            {
                if (elem is GenericDataElement g)
                {
                    string idKey = BuildIdentityKey(type, g.Properties, identityKeys);
                    _identityIndex[idKey] = g.Key;
                }
            }
        }

        /// <summary>
        /// Compact the commit log (Kafka-style log compaction).
        /// Keeps only the latest entry per key, reducing log size.
        /// </summary>
        public int CompactLog()
        {
            lock (_lock)
            {
                return _commitLog?.Compact() ?? 0;
            }
        }

        /// <summary>
        /// Get commit log info (offset count, file path).
        /// </summary>
        public (long Offset, string? Path) GetLogInfo()
        {
            return (_commitLog?.CurrentOffset ?? 0, _commitLog?.LogPath);
        }

        public static DMCServer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new DMCServer();
                        }
                    }
                }
                return _instance;
            }
        }

        // =================================================================
        // Schema Layer
        // =================================================================

        /// <summary>
        /// Define the IdentityKeys schema for a Type. Must be called before Set().
        /// </summary>
        public bool DefineType(string type, List<string> identityKeys)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type cannot be empty.", nameof(type));
            if (identityKeys == null || identityKeys.Count == 0)
                throw new ArgumentException("IdentityKeys is required.", nameof(identityKeys));

            lock (_lock)
            {
                if (_typeSchemas.ContainsKey(type))
                    return false;

                _commitLog?.Append(LogOperation.DefineType, type, identityKeys: identityKeys);
                _typeSchemas[type] = new List<string>(identityKeys);
                return true;
            }
        }

        /// <summary>
        /// Get the IdentityKeys schema for a Type. Returns null if not defined.
        /// </summary>
        public List<string>? GetTypeSchema(string type)
        {
            lock (_lock)
            {
                return _typeSchemas.TryGetValue(type, out var schema)
                    ? new List<string>(schema) : null;
            }
        }

        /// <summary>
        /// Update the IdentityKeys schema for an existing Type.
        /// Validates all existing elements for collisions under the new schema.
        /// Returns (success, list of conflicting key pairs).
        /// </summary>
        public (bool Success, List<(string KeyA, string KeyB)> Conflicts) UpdateTypeSchema(string type, List<string> newIdentityKeys)
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type cannot be empty.", nameof(type));
            if (newIdentityKeys == null || newIdentityKeys.Count == 0)
                throw new ArgumentException("IdentityKeys is required.", nameof(newIdentityKeys));

            lock (_lock)
            {
                if (!_typeSchemas.ContainsKey(type))
                    throw new InvalidOperationException($"Type '{type}' not defined.");

                // Get all elements of this Type
                var elements = _registry.Values
                    .Where(e => e.Type == type)
                    .Cast<GenericDataElement>()
                    .ToList();

                // Validate: all elements must have the new identity keys
                foreach (var elem in elements)
                {
                    foreach (var idKey in newIdentityKeys)
                    {
                        if (!elem.Properties.ContainsKey(idKey))
                            throw new ArgumentException(
                                $"Element '{elem.Key}' is missing property '{idKey}' required by new IdentityKeys.");
                    }
                }

                // Check for collisions under new IdentityKeys
                var newIndex = new Dictionary<string, string>();
                var conflicts = new List<(string, string)>();

                foreach (var elem in elements)
                {
                    string newIdKey = BuildIdentityKey(type, elem.Properties, newIdentityKeys);
                    if (newIndex.TryGetValue(newIdKey, out var existingKey))
                    {
                        conflicts.Add((existingKey, elem.Key));
                    }
                    else
                    {
                        newIndex[newIdKey] = elem.Key;
                    }
                }

                if (conflicts.Count > 0)
                    return (false, conflicts);

                // No collisions → update schema + rebuild identity index
                _commitLog?.Append(LogOperation.UpdateSchema, type, identityKeys: newIdentityKeys);
                _typeSchemas[type] = new List<string>(newIdentityKeys);

                // Remove old identity index entries for this Type
                var oldEntries = _identityIndex
                    .Where(kv => kv.Key.StartsWith(type + "\x1F"))
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var key in oldEntries)
                    _identityIndex.Remove(key);

                // Add new identity index entries
                foreach (var kv in newIndex)
                    _identityIndex[kv.Key] = kv.Value;

                return (true, new List<(string, string)>());
            }
        }

        // =================================================================
        // Data Layer
        // =================================================================

        /// <summary>
        /// Set a Data Element. Server decides Create or Update based on identity matching.
        /// - No key: check identity index → match found = Update, no match = Create
        /// - With key: explicit update by key
        /// Owner is recorded on creation for tracking purposes.
        /// </summary>
        public SetResult Set(string type, Dictionary<string, string> properties, string? existingKey = null, bool merge = true, string owner = "")
        {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentException("Type cannot be empty.", nameof(type));

            if (!_typeSchemas.TryGetValue(type, out var identityKeys))
                throw new InvalidOperationException($"Type '{type}' has no schema. Call DefineType first.");

            foreach (var idKey in identityKeys)
            {
                if (!properties.ContainsKey(idKey))
                    throw new ArgumentException($"IdentityKey '{idKey}' not found in properties.");
            }

            lock (_lock)
            {
                // Case 1: Explicit update by key
                if (!string.IsNullOrEmpty(existingKey))
                {
                    if (!_registry.ContainsKey(existingKey))
                        return new SetResult(SetAction.Updated, existingKey);

                    _commitLog?.Append(LogOperation.Update, type, key: existingKey,
                        properties: properties, merge: merge);
                    UpdateProperties(existingKey, properties, merge);
                    UpdateIdentityIndex(existingKey, type, properties, identityKeys);
                    return new SetResult(SetAction.Updated, existingKey);
                }

                // Case 2: Check identity index for match
                string identityKey = BuildIdentityKey(type, properties, identityKeys);

                if (_identityIndex.TryGetValue(identityKey, out var matchedKey))
                {
                    _commitLog?.Append(LogOperation.Set, type, key: matchedKey,
                        owner: owner, properties: properties, merge: merge);
                    UpdateProperties(matchedKey, properties, merge);
                    return new SetResult(SetAction.Updated, matchedKey);
                }

                // Case 3: No match → Create new element with owner
                string newKey = GenerateKey(type);
                _commitLog?.Append(LogOperation.Set, type, key: newKey,
                    owner: owner, properties: properties, merge: merge);
                var element = new GenericDataElement(type, new Dictionary<string, string>(properties), newKey, owner);
                _registry.Add(newKey, element);
                _identityIndex[identityKey] = newKey;
                return new SetResult(SetAction.Created, newKey);
            }
        }

        /// <summary>
        /// Update properties of an existing element by key.
        /// </summary>
        public bool Update(string key, Dictionary<string, string> properties, bool merge = true)
        {
            lock (_lock)
            {
                if (!_registry.ContainsKey(key))
                    return false;

                _commitLog?.Append(LogOperation.Update, _registry[key].Type, key: key,
                    properties: properties, merge: merge);
                UpdateProperties(key, properties, merge);

                var element = _registry[key];
                if (_typeSchemas.TryGetValue(element.Type, out var identityKeys))
                {
                    UpdateIdentityIndex(key, element.Type, properties, identityKeys);
                }
                return true;
            }
        }

        /// <summary>
        /// Search for elements matching a filter (subset match via ISearchable).
        /// Optionally filter by owner.
        /// </summary>
        public IEnumerable<IDataElement> Search(string? type, Dictionary<string, string> filters, string? owner = null)
        {
            lock (_lock)
            {
                var query = _registry.Values.Where(e => e.Matches(type, filters));

                if (!string.IsNullOrEmpty(owner))
                    query = query.Where(e => e is GenericDataElement g && g.Owner == owner);

                return query.Select(CloneElement).ToList();
            }
        }

        public bool Print(string key)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(key, out var element))
                {
                    element.Print();
                    return true;
                }
                return false;
            }
        }

        public void PrintAll()
        {
            lock (_lock)
            {
                foreach (var element in _registry.Values)
                    element.Print();
            }
        }

        public bool Contains(string key) { lock (_lock) { return _registry.ContainsKey(key); } }
        public int Count { get { lock (_lock) { return _registry.Count; } } }

        public IDataElement? Get(string key)
        {
            lock (_lock)
            {
                if (_registry.TryGetValue(key, out var element))
                    return CloneElement(element);
                return null;
            }
        }

        public IEnumerable<IDataElement> GetAll() { lock (_lock) { return _registry.Values.Select(CloneElement).ToList(); } }

        // =================================================================
        // Internal Helpers
        // =================================================================

        /// <summary>
        /// Create a deep copy of an element so callers can safely iterate
        /// Properties without holding the lock.
        /// </summary>
        private IDataElement CloneElement(IDataElement element)
        {
            if (element is GenericDataElement g)
            {
                return new GenericDataElement(
                    g.Type,
                    new Dictionary<string, string>(g.Properties),
                    g.Key,
                    g.Owner);
            }
            return element;
        }

        private string GenerateKey(string type)
        {
            if (!_typeCounters.ContainsKey(type))
                _typeCounters[type] = 0;
            _typeCounters[type]++;
            return $"{type}:{_typeCounters[type]}";
        }

        /// <summary>
        /// Build identity index key using Unit Separator (\x1F) as delimiter.
        /// This avoids collisions when property values contain ':' or other common characters.
        /// </summary>
        private string BuildIdentityKey(string type, Dictionary<string, string> properties, List<string> identityKeys)
        {
            const char sep = '\x1F'; // ASCII Unit Separator — never appears in user input
            var values = identityKeys.Select(k => properties[k]);
            return $"{type}{sep}{string.Join(sep, values)}";
        }

        private void UpdateProperties(string key, Dictionary<string, string> newProps, bool merge)
        {
            if (_registry[key] is GenericDataElement generic)
            {
                if (merge)
                {
                    foreach (var kvp in newProps)
                        generic.Properties[kvp.Key] = kvp.Value;
                }
                else
                {
                    generic.Properties = new Dictionary<string, string>(newProps);
                }
            }
        }

        private void UpdateIdentityIndex(string key, string type, Dictionary<string, string> properties, List<string> identityKeys)
        {
            var oldEntry = _identityIndex.FirstOrDefault(kv => kv.Value == key);
            if (oldEntry.Key != null)
                _identityIndex.Remove(oldEntry.Key);

            if (_registry.ContainsKey(key) && _registry[key] is GenericDataElement generic)
            {
                string identityKey = BuildIdentityKey(type, generic.Properties, identityKeys);
                _identityIndex[identityKey] = key;
            }
        }

        internal static void ResetInstance()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }
    }
}
