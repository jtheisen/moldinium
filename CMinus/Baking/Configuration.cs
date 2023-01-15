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
    private readonly AbstractPropertyGenerator propertyImplementationGenerator;
    private readonly AbstractPropertyGenerator propertyWrapperGenerator;
    private readonly AbstractEventGenerator eventGenerator;

    public ComponentGenerators(
        AbstractPropertyGenerator propertyImplementationGenerator,
        AbstractPropertyGenerator propertyWrapperGenerator,
        AbstractEventGenerator eventGenerator)
    {
        this.propertyImplementationGenerator = propertyImplementationGenerator;
        this.propertyWrapperGenerator = propertyWrapperGenerator;
        this.eventGenerator = eventGenerator;
    }

    public MixInGenerator[] GetMixInGenerators(Type type) => new MixInGenerator[] { };

    public AbstractPropertyGenerator GetPropertyGenerator(PropertyInfo property)
    {
        var getter = CheckMethod(property.GetGetMethod());
        var setter = CheckMethod(property.GetSetMethod());

        if (!getter.exists)
        {
            if (!setter.exists)
            {
                throw new Exception($"The property {property.Name} has neither a setter nor a getter");
            }
            else
            {
                throw new Exception($"The property {property.Name} has a setter but no getter");
            }
        }

        if (setter.exists)
        {
            if (getter.implemented != setter.implemented)
            {
                throw new Exception($"The property {property.Name} should implement either both getter and setter or neither");
            }
        }

        if (getter.implemented)
        {
            return propertyWrapperGenerator;
        }
        else
        {
            return propertyImplementationGenerator;
        }
    }

    (Boolean exists, Boolean implemented) CheckMethod(MethodInfo? method)
    {
        return (exists: method is not null, implemented: !method?.IsAbstract ?? false);
    }


    public AbstractEventGenerator GetEventGenerator(EventInfo evt) => eventGenerator;

    public static ComponentGenerators Create(Type propertyImplementationType, Type eventImplementationType)
        => Create(propertyImplementationType, typeof(TrivialPropertyWrapper), eventImplementationType);

    public static ComponentGenerators Create(Type propertyImplementationType, Type propertyWrapperType, Type eventImplementationType)
        => new ComponentGenerators(
            PropertyGenerator.Create(propertyImplementationType),
            PropertyGenerator.Create(propertyWrapperType),
            EventGenerator.Create(eventImplementationType)
        );
}

public record BakeryConfiguration(IBakeryComponentGenerators Generators, IDefaultProvider DefaultProvider, Boolean MakeAbstract = false)
{
    public static BakeryConfiguration Create(Type? propertyImplementationType = null, Type? propertyWrappingType = null, Type? eventImplementationType = null)
        => new BakeryConfiguration(ComponentGenerators.Create(
            propertyImplementationType ?? typeof(SimplePropertyImplementation<>),
            propertyWrappingType ?? typeof(TrivialPropertyWrapper),
            eventImplementationType ?? typeof(GenericEventImplementation<>)), Defaults.GetDefaultDefaultProvider());

    public static BakeryConfiguration PocGenerationConfiguration
        = new BakeryConfiguration(ComponentGenerators.Create(typeof(SimplePropertyImplementation<>), typeof(GenericEventImplementation<>)), Defaults.GetDefaultDefaultProvider());

    public AbstractlyBakery CreateBakery(String name) => new ConcretelyBakery(name, this);

    public AbstractBakery CreateDoubleBakery(String name) => new DoubleBakery(name, this);
}

public class MixInGenerator
{
}

public enum CodeGenerationContextType
{
    Other,
    Constructor,
    Wrapper,
    Nested,
    Get,
    Set,
    Add,
    Remove
}
