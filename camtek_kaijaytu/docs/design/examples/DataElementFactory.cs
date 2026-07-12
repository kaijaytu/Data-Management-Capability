using DMC.Common;
using DMC.Common.DataElements;

namespace DMC.Core
{
    /// <summary>
    /// Factory for creating Data Element instances by type name.
    /// New types can be registered without modifying existing code.
    /// </summary>
    public class DataElementFactory
    {
        private readonly Dictionary<string, Func<string, Dictionary<string, object>, IDataElement>> _creators;

        public DataElementFactory()
        {
            _creators = new Dictionary<string, Func<string, Dictionary<string, object>, IDataElement>>(
                StringComparer.OrdinalIgnoreCase);

            RegisterDefaultTypes();
        }

        private void RegisterDefaultTypes()
        {
            _creators["MobilePhone"] = (id, props) => new MobilePhone(
                id,
                props.GetValueOrDefault("Brand")?.ToString() ?? "",
                props.GetValueOrDefault("Model")?.ToString() ?? "",
                Convert.ToDecimal(props.GetValueOrDefault("Price", 0m))
            );

            _creators["Car"] = (id, props) => new Car(
                id,
                props.GetValueOrDefault("Make")?.ToString() ?? "",
                Convert.ToInt32(props.GetValueOrDefault("Year", 0)),
                props.GetValueOrDefault("Color")?.ToString() ?? ""
            );

            _creators["Person"] = (id, props) => new Person(
                id,
                props.GetValueOrDefault("Name")?.ToString() ?? "",
                Convert.ToInt32(props.GetValueOrDefault("Age", 0)),
                props.GetValueOrDefault("Role")?.ToString() ?? ""
            );

            _creators["TV"] = (id, props) => new TV(
                id,
                props.GetValueOrDefault("Brand")?.ToString() ?? "",
                Convert.ToInt32(props.GetValueOrDefault("Size", 0)),
                props.GetValueOrDefault("Resolution")?.ToString() ?? ""
            );
        }

        /// <summary>
        /// Register a new Data Element type creator.
        /// </summary>
        public void RegisterType(string typeName, Func<string, Dictionary<string, object>, IDataElement> creator)
        {
            _creators[typeName] = creator;
        }

        /// <summary>
        /// Create a Data Element by type name with given properties.
        /// </summary>
        public IDataElement Create(string typeName, string id, Dictionary<string, object> properties)
        {
            if (!_creators.ContainsKey(typeName))
            {
                throw new ArgumentException($"Unknown Data Element type: {typeName}");
            }

            return _creators[typeName](id, properties);
        }

        /// <summary>
        /// Check if a type is registered in the factory.
        /// </summary>
        public bool IsTypeRegistered(string typeName)
        {
            return _creators.ContainsKey(typeName);
        }
    }
}
