namespace DMC.Common.DataElements
{
    /// <summary>
    /// Generic Data Element that accepts any type name and arbitrary key-value properties.
    /// Key is assigned by the Server (auto-increment). Identity is determined by IdentityKeys (per-Type schema).
    /// Supports the Open-Closed Principle: no code change needed for new types.
    /// </summary>
    public class GenericDataElement : IDataElement
    {
        public string Type { get; }
        public string Key { get; set; }
        public Dictionary<string, string> Properties { get; set; }

        public GenericDataElement(string type, Dictionary<string, string> properties, string key = "")
        {
            Type = type;
            Properties = properties;
            Key = key;
        }

        /// <summary>
        /// Determines if incoming data matches this element's identity.
        /// Only compares properties specified in identityKeys (provided by Server from Type schema).
        /// </summary>
        public bool IsIdenticalTo(string type, Dictionary<string, string> properties, List<string> identityKeys)
        {
            if (Type != type) return false;

            foreach (var idKey in identityKeys)
            {
                if (!Properties.TryGetValue(idKey, out var myValue)) return false;
                if (!properties.TryGetValue(idKey, out var theirValue)) return false;
                if (myValue != theirValue) return false;
            }
            return true;
        }

        /// <summary>
        /// Determines if this element matches a search filter (subset match).
        /// </summary>
        public bool Matches(string? type, Dictionary<string, string> filters)
        {
            if (!string.IsNullOrEmpty(type) && Type != type)
                return false;

            foreach (var kvp in filters)
            {
                if (!Properties.TryGetValue(kvp.Key, out var value) || value != kvp.Value)
                    return false;
            }
            return true;
        }

        public void Print()
        {
            Console.WriteLine(ToDisplayString());
        }

        public string ToDisplayString()
        {
            var pairs = Properties.Select(p => $"{p.Key}={p.Value}");
            return $"[{Type}] {string.Join(", ", pairs)}  (Key: {Key})";
        }
    }
}
