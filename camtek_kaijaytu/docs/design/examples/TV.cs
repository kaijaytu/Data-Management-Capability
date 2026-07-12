namespace DMC.Common.DataElements
{
    public class TV : IDataElement
    {
        public string Id { get; }
        public string Type => "TV";
        public string Brand { get; set; }
        public int Size { get; set; }
        public string Resolution { get; set; }

        public TV(string id, string brand, int size, string resolution)
        {
            Id = id;
            Brand = brand;
            Size = size;
            Resolution = resolution;
        }

        public void Print()
        {
            Console.WriteLine($"TV: {Brand} {Size}in, {Resolution}");
        }
    }
}
