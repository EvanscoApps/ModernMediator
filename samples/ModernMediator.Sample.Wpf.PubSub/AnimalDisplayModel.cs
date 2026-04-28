using ModernMediator.Sample.Shared.Domain;

namespace ModernMediator.Sample.Wpf.PubSub;

public class AnimalDisplayModel
{
    private readonly Animal _animal;

    public AnimalDisplayModel(Animal animal)
    {
        _animal = animal;
        Name = animal.Name;
        AgeYears = animal.AgeYears;
        Type = animal switch { Dog => "Dog", Cat => "Cat", Eagle => "Eagle", _ => animal.GetType().Name };
        Details = animal switch
        {
            Dog d => $"Breed: {d.Breed}",
            Cat c => $"Indoor: {c.IsIndoor}",
            Eagle e => $"Wingspan: {e.WingspanMeters}m",
            _ => ""
        };
    }

    public string Name { get; }
    public string Type { get; }
    public int AgeYears { get; }
    public string Details { get; }

    public Animal ToAnimal() => _animal;
}
