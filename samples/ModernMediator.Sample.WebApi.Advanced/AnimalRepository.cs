namespace ModernMediator.Sample.WebApi.Advanced;

public class AnimalRepository
{
    private readonly List<Animal> _animals =
    [
        new Dog("Rex", 5, "German Shepherd"),
        new Dog("Bella", 3, "Labrador"),
        new Cat("Whiskers", 7, true),
        new Cat("Shadow", 2, false),
        new Eagle("Aquila", 12, 2.1)
    ];

    private readonly object _lock = new();

    public Animal[] GetAll()
    {
        lock (_lock) return _animals.ToArray();
    }

    public Animal? GetByName(string name)
    {
        lock (_lock)
            return _animals.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public bool Exists(string name)
    {
        lock (_lock)
            return _animals.Any(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public void Add(Animal animal)
    {
        lock (_lock) _animals.Add(animal);
    }

    public bool Remove(string name)
    {
        lock (_lock)
        {
            var animal = _animals.FirstOrDefault(a =>
                string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            return animal is not null && _animals.Remove(animal);
        }
    }
}
