namespace ModernMediator.Sample.Shared.Domain;

public abstract record Animal(string Name, int AgeYears);

public abstract record Mammal(string Name, int AgeYears) : Animal(Name, AgeYears);

public abstract record Bird(string Name, int AgeYears) : Animal(Name, AgeYears);

public record Dog(string Name, int AgeYears, string Breed) : Mammal(Name, AgeYears);

public record Cat(string Name, int AgeYears, bool IsIndoor) : Mammal(Name, AgeYears);

public record Eagle(string Name, int AgeYears, double WingspanMeters) : Bird(Name, AgeYears);
