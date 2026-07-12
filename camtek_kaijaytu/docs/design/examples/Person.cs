namespace DMC.Common.DataElements
{
    public class Person : IDataElement
    {
        public string Id { get; }
        public string Type => "Person";
        public string Name { get; set; }
        public int Age { get; set; }
        public string Role { get; set; }

        public Person(string id, string name, int age, string role)
        {
            Id = id;
            Name = name;
            Age = age;
            Role = role;
        }

        public void Print()
        {
            Console.WriteLine($"Person: {Name}, {Age}, {Role}");
        }
    }
}
