namespace DMC.Common
{
    /// <summary>
    /// Each Data Element knows how to identify itself uniquely.
    /// The key is determined by the element, not by the server.
    /// </summary>
    public interface IKeyIdentifiable
    {
        string GetKey();
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
    /// Inherits IKeyIdentifiable (self-identifying) and IPrintable (self-printing).
    /// </summary>
    public interface IDataElement : IKeyIdentifiable, IPrintable
    {
        string Type { get; }
    }
}
