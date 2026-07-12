namespace DMC.Common.DataElements
{
    public class MobilePhone : IDataElement
    {
        public string Id { get; }
        public string Type => "MobilePhone";
        public string Brand { get; set; }
        public string Model { get; set; }
        public decimal Price { get; set; }

        public MobilePhone(string id, string brand, string model, decimal price)
        {
            Id = id;
            Brand = brand;
            Model = model;
            Price = price;
        }

        public void Print()
        {
            Console.WriteLine($"MobilePhone: {Brand} {Model}, ${Price}");
        }
    }
}
