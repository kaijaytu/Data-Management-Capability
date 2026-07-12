namespace DMC.Common.DataElements
{
    public class Car : IDataElement
    {
        public string Id { get; }
        public string Type => "Car";
        public string Make { get; set; }
        public int Year { get; set; }
        public string Color { get; set; }

        public Car(string id, string make, int year, string color)
        {
            Id = id;
            Make = make;
            Year = year;
            Color = color;
        }

        public void Print()
        {
            Console.WriteLine($"Car: {Make} {Year}, {Color}");
        }
    }
}
