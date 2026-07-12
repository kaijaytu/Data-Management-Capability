namespace DMC.Common.DataElements
{
    /// <summary>
    /// Generic Data Element that accepts any type name and arbitrary key-value properties.
    /// Key is auto-generated from Type + first property value (or user-specified key property).
    /// Supports the Open-Closed Principle: no code change needed for new types.
    /// </summary>
    public class GenericDataElement : IDataElement
    {
        public string Type { get; }
        public Dictionary<string, string> Properties { get; }

        /// <summary>
        /// The property name used as the unique identifier within this type.
        /// If not specified, the first property value is used.
        /// </summary>
        public string KeyProperty { get; }

        public GenericDataElement(string type, Dictionary<string, string> properties, string keyProperty = "")
        {
            Type = type;
            Properties = properties;
            KeyProperty = keyProperty;
        }

        /// <summary>
        /// Returns a unique key based on Type + key property value.
        /// Example: "Car:Toyota" or "Person:John"
        /// </summary>
        public string GetKey()
        {
            string keyValue;

            if (!string.IsNullOrEmpty(KeyProperty) && Properties.ContainsKey(KeyProperty))
            {
                keyValue = Properties[KeyProperty];
            }
            else
            {
                // Default: use first property value
                keyValue = Properties.Values.FirstOrDefault() ?? "unknown";
            }

            return $"{Type}:{keyValue}";
        }

        public void Print()
        {
            Console.WriteLine(ToDisplayString());
        }

        public string ToDisplayString()
        {
            var pairs = Properties.Select(p => $"{p.Key}={p.Value}");
            return $"[{Type}] {string.Join(", ", pairs)}  (Key: {GetKey()})";
        }
    }
}
