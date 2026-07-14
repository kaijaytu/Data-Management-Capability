using DMC.Common;
using DMC.Common.DataElements;

namespace DMC.Core
{
    public enum SetAction { Created, Updated, AlreadyExists }
    public record SetResult(SetAction Action, string Key);

    /// <summary>
    /// Singleton DMC Server — centralized data registry for all Data Elements.
    /// V2: Schema layer (DefineType) + Data layer (Set/Search/Print).
    /// Server is responsible for: key generation, identity detection, register/update decisions.
    /// </summary>
    public class DMCServer
    {
        private static DMCServer? _instance;
        private static readonly object _lock = new object();

        private readonly Dictionary<string, IDataElement> _registry;
        private readonly Dictionary<string, List<string>> _typeSchemas;
        private readonly Dictionary<string, int> _typeCounters;
        private readonly Dictionary<string, string> _identityIndex;

        private DMCServer()
        {
            _registry = new Dictionary<string, IDataElement>();
            _typeSchemas = new Dictionary<string, List<string>>();
            _typeCounters = new Dictionary<string, int>();
            _identityIndex = new Dictionary<string, string>();
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

            if (_typeSchemas.ContainsKey(type))
                return false;

            _typeSchemas[type] = new List<string>(identityKeys);
            return true;
        }

        /// <summary>
        /// Get the IdentityKeys schema for a Type. Returns null if not defined.
        /// </summary>
        public List<string>? GetTypeSchema(string type)
        {
            return _typeSchemas.TryGetValue(type, out var schema) ? schema : null;
        }

        // =================================================================
        // Data Layer
        // =================================================================

        /// <summary>
        /// Set a Data Element. Server decides Create or Update based on identity matching.
        /// - No key: check identity index → match found = Update, no match = Create
        /// - With key: explicit update by key
        /// </summary>
        public SetResult Set(string type, Dictionary<string, string> properties, string? existingKey = null, bool merge = true)
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

            // Case 1: Explicit update by key
            if (!string.IsNullOrEmpty(existingKey))
            {
                if (!_registry.ContainsKey(existingKey))
                    return new SetResult(SetAction.Updated, existingKey);

                UpdateProperties(existingKey, properties, merge);
                UpdateIdentityIndex(existingKey, type, properties, identityKeys);
                return new SetResult(SetAction.Updated, existingKey);
            }

            // Case 2: Check identity index for match
            string identityKey = BuildIdentityKey(type, properties, identityKeys);

            if (_identityIndex.TryGetValue(identityKey, out var matchedKey))
            {
                UpdateProperties(matchedKey, properties, merge);
                return new SetResult(SetAction.Updated, matchedKey);
            }

            // Case 3: No match → Create new element
            string newKey = GenerateKey(type);
            var element = new GenericDataElement(type, new Dictionary<string, string>(properties), newKey);
            _registry.Add(newKey, element);
            _identityIndex[identityKey] = newKey;
            return new SetResult(SetAction.Created, newKey);
        }

        /// <summary>
        /// Update properties of an existing element by key.
        /// </summary>
        public bool Update(string key, Dictionary<string, string> properties, bool merge = true)
        {
            if (!_registry.ContainsKey(key))
                return false;

            UpdateProperties(key, properties, merge);

            var element = _registry[key];
            if (_typeSchemas.TryGetValue(element.Type, out var identityKeys))
            {
                UpdateIdentityIndex(key, element.Type, properties, identityKeys);
            }
            return true;
        }

        /// <summary>
        /// Search for elements matching a filter (subset match via ISearchable).
        /// </summary>
        public IEnumerable<IDataElement> Search(string? type, Dictionary<string, string> filters)
        {
            return _registry.Values
                .Where(e => e.Matches(type, filters))
                .ToList();
        }

        public bool Print(string key)
        {
            if (_registry.TryGetValue(key, out var element))
            {
                element.Print();
                return true;
            }
            return false;
        }

        public void PrintAll()
        {
            foreach (var element in _registry.Values)
                element.Print();
        }

        public bool Contains(string key) => _registry.ContainsKey(key);
        public int Count => _registry.Count;

        public IDataElement? Get(string key)
        {
            _registry.TryGetValue(key, out var element);
            return element;
        }

        public IEnumerable<IDataElement> GetAll() => _registry.Values.ToList();

        // =================================================================
        // Internal Helpers
        // =================================================================

        private string GenerateKey(string type)
        {
            if (!_typeCounters.ContainsKey(type))
                _typeCounters[type] = 0;
            _typeCounters[type]++;
            return $"{type}:{_typeCounters[type]}";
        }

        private string BuildIdentityKey(string type, Dictionary<string, string> properties, List<string> identityKeys)
        {
            var values = identityKeys.Select(k => properties[k]);
            return $"{type}:{string.Join(":", values)}";
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
