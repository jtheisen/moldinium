using System;
using System.Reflection;

namespace CMinus;

public struct Dummy { }

public interface IBakeryComponentGenerators
{
    MixInGenerator[] GetMixInGenerators(Type type);

    AbstractPropertyGenerator GetPropertyGenerator(PropertyInfo property);
}

public class ComponentGenerator : IBakeryComponentGenerators
{
    private readonly AbstractPropertyGenerator propertyGenerator;

    public ComponentGenerator(AbstractPropertyGenerator propertyGenerator)
    {
        this.propertyGenerator = propertyGenerator;
    }

    public MixInGenerator[] GetMixInGenerators(Type type) => new MixInGenerator[] { };

    public AbstractPropertyGenerator GetPropertyGenerator(PropertyInfo property) => propertyGenerator;

    public static ComponentGenerator Create(Type propertyImplementationType)
        => new ComponentGenerator(PropertyGenerator.Create(propertyImplementationType));
}

public record BakeryConfiguration(IBakeryComponentGenerators Generators, Boolean MakeAbstract = false)
{
    public static BakeryConfiguration Create(Type propertyImplementationType)
        => new BakeryConfiguration(ComponentGenerator.Create(propertyImplementationType));

    public static BakeryConfiguration PocGenerationConfiguration
        = new BakeryConfiguration(new ComponentGenerator(new BasicPropertyGenerator(typeof(GenericPropertyImplementation<>))));

    public Bakery CreateBakery(String name) => new Bakery(name, this);
}

public class MixInGenerator
{
}
