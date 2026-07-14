namespace DMC.Common
{
    /// <summary>
    /// Each Data Element knows how to determine if incoming data matches its identity.
    /// Identity is defined by a subset of properties (IdentityKeys), not all properties.
    /// The Server provides the IdentityKeys from the Type schema.
    /// </summary>
    public interface IIdentifiable
    {
        bool IsIdenticalTo(string type, Dictionary<string, string> properties, List<string> identityKeys);
    }

    /// <summary>
    /// Each Data Element knows how to determine if it matches a search filter.
    /// Filters are a subset of properties; element matches if it contains all filter key-values.
    /// </summary>
    public interface ISearchable
    {
        bool Matches(string? type, Dictionary<string, string> filters);
    }

    /// <summary>
    /// Each Data Element knows how to print/display itself.
    /// The server calls Print() without knowing the concrete type.
    /// </summary>
    public interface IPrintable
    {
        void Print();
        string ToDisplayString();
    }

    /// <summary>
    /// Combined interface for all Data Elements managed by the DMC Server.
    /// Inherits IIdentifiable (identity matching), ISearchable (query filtering), and IPrintable (self-printing).
    /// </summary>
    public interface IDataElement : IIdentifiable, ISearchable, IPrintable
    {
        string Type { get; }
        string Key { get; set; }
    }
}
