using System;
using System.Reflection;

namespace CMinus;

public struct Dummy { }

public interface IBakeryComponentGenerators
{
    MixInGenerator[] GetMixInGenerators(Type type);

    AbstractPropertyGenerator GetPropertyGenerator(PropertyInfo property);

    AbstractEventGenerator GetEventGenerator(EventInfo evt);
}

public class ComponentGenerators : IBakeryComponentGenerators
{
    private readonly AbstractPropertyGenerator propertyGenerator;
    private readonly AbstractEventGenerator eventGenerator;

    public ComponentGenerators(AbstractPropertyGenerator propertyGenerator, AbstractEventGenerator eventGenerator)
    {
        this.propertyGenerator = propertyGenerator;
        this.eventGenerator = eventGenerator;
    }

    public MixInGenerator[] GetMixInGenerators(Type type) => new MixInGenerator[] { };

    public AbstractPropertyGenerator GetPropertyGenerator(PropertyInfo property) => propertyGenerator;

    public AbstractEventGenerator GetEventGenerator(EventInfo evt) => eventGenerator;

    public static ComponentGenerators Create(Type propertyImplementationType, Type eventImplementationType)
        => new ComponentGenerators(
            PropertyGenerator.Create(propertyImplementationType),
            EventGenerator.Create(eventImplementationType)
        );
}

public record BakeryConfiguration(IBakeryComponentGenerators Generators, IDefaultProvider DefaultProvider, Boolean MakeAbstract = false)
{
    public static BakeryConfiguration Create(Type? propertyImplementationType = null, Type? eventImplementationType = null)
        => new BakeryConfiguration(ComponentGenerators.Create(
            propertyImplementationType ?? typeof(SimplePropertyImplementation<,>),
            eventImplementationType ?? typeof(GenericEventImplementation<>)), Defaults.GetDefaultDefaultProvider());

    public static BakeryConfiguration PocGenerationConfiguration
        = new BakeryConfiguration(ComponentGenerators.Create(typeof(SimplePropertyImplementation<,>), typeof(GenericEventImplementation<>)), Defaults.GetDefaultDefaultProvider());

    public Bakery CreateBakery(String name) => new Bakery(name, this);
}

public class MixInGenerator
{
}
