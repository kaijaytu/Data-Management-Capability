using DMC.Common;

namespace DMC.Core
{
    /// <summary>
    /// Singleton DMC Server — centralized data registry for all Data Elements.
    /// All Clients access the same instance via DMCServer.Instance.
    /// </summary>
    public class DMCServer
    {
        private static DMCServer? _instance;
        private static readonly object _lock = new object();

        private readonly Dictionary<string, IDataElement> _registry;

        private DMCServer()
        {
            _registry = new Dictionary<string, IDataElement>();
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

        /// <summary>
        /// Register a new Data Element. If already exists, routes to Update.
        /// Uses element.GetKey() as the dictionary key.
        /// </summary>
        public bool Register(IDataElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            string key = element.GetKey();
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Element key cannot be null or empty.", nameof(element));

            if (_registry.ContainsKey(key))
            {
                return Update(element);
            }

            _registry.Add(key, element);
            return true;
        }

        /// <summary>
        /// Update an existing Data Element. Returns false if not found.
        /// </summary>
        public bool Update(IDataElement element)
        {
            if (element == null)
                throw new ArgumentNullException(nameof(element));

            string key = element.GetKey();
            if (!_registry.ContainsKey(key))
            {
                return false;
            }

            _registry[key] = element;
            return true;
        }

        /// <summary>
        /// Print a single Data Element by key.
        /// </summary>
        public bool Print(string key)
        {
            if (_registry.TryGetValue(key, out var element))
            {
                element.Print();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Print all Data Elements in the system.
        /// </summary>
        public void PrintAll()
        {
            foreach (var element in _registry.Values)
            {
                element.Print();
            }
        }

        /// <summary>
        /// Check if a Data Element exists in the system by key.
        /// </summary>
        public bool Contains(string key)
        {
            return _registry.ContainsKey(key);
        }

        /// <summary>
        /// Get the total count of registered Data Elements.
        /// </summary>
        public int Count => _registry.Count;

        /// <summary>
        /// Get a single Data Element by key. Returns null if not found.
        /// </summary>
        public IDataElement? Get(string key)
        {
            _registry.TryGetValue(key, out var element);
            return element;
        }

        /// <summary>
        /// Get all Data Elements in the system.
        /// </summary>
        public IEnumerable<IDataElement> GetAll()
        {
            return _registry.Values.ToList();
        }

        /// <summary>
        /// Reset the server instance (for testing purposes only).
        /// </summary>
        internal static void ResetInstance()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }
    }
}
