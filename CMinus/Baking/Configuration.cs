using CMinus.Baking;
using CMinus.Misc;
using System;
using System.Linq;
using System.Reflection;

namespace CMinus;

public struct Dummy { }

public interface IBakeryComponentGenerators
{
    MixinGenerator[] GetMixInGenerators(Type type);

    AbstractMethodGenerator? GetMethodGenerator(MethodInfo method);

    AbstractPropertyGenerator? GetPropertyGenerator(PropertyInfo property);
    
    AbstractEventGenerator GetEventGenerator(EventInfo evt);
}

public class ComponentGenerators : IBakeryComponentGenerators
{
    private readonly AbstractMethodGenerator? methodWrapperGenerator;
    private readonly AbstractPropertyGenerator propertyGenerator;
    private readonly AbstractEventGenerator eventGenerator;

    public ComponentGenerators(
        AbstractMethodGenerator? methodWrapperGenerator,
        AbstractPropertyGenerator propertyImplementationGenerator,
        AbstractEventGenerator eventGenerator)
    {
        this.methodWrapperGenerator = methodWrapperGenerator;
        this.propertyGenerator = propertyImplementationGenerator;
        this.eventGenerator = eventGenerator;
    }

    public MixinGenerator[] GetMixInGenerators(Type type) => new MixinGenerator[] { };

    public AbstractPropertyGenerator? GetPropertyGenerator(PropertyInfo property)
    {
        return propertyGenerator;
    }

    public AbstractMethodGenerator? GetMethodGenerator(MethodInfo method)
    {
        //if (!implemented) throw new Exception($"Method {method} on {method.DeclaringType} must have a default implementation to wrap");

        return methodWrapperGenerator;
    }

    public AbstractEventGenerator GetEventGenerator(EventInfo evt) => eventGenerator;

    static ComponentGenerators CreateInternal(Type? methodWrapperType = null, Type? propertyImplementationType = null, Type? propertyWrapperType = null, Type? eventImplementationType = null)
        => new ComponentGenerators(
            MethodGenerator.Create(methodWrapperType ?? typeof(TrivialMethodWrapper)),
            PropertyGenerator.Create(propertyImplementationType ?? typeof(SimplePropertyImplementation<>), propertyWrapperType ?? typeof(TrivialPropertyWrapper)),
            EventGenerator.Create(eventImplementationType ?? typeof(GenericEventImplementation<>), typeof(TrivialEventWrapper))
        );

    public static ComponentGenerators Create(params Type[] implementations)
    {
        foreach (var implementation in implementations)
        {
            CheckedImplementation.PreCheck(implementation);
        }

        var methodWrapperType
            = FindType(implementations, typeof(IMethodWrapperImplementation));
        var propertyImplementationType
            = FindType(implementations, typeof(IPropertyImplementation));
        var propertyWrapperType
            = FindType(implementations, typeof(IPropertyWrapperImplementation));
        var eventImplementationType
            = FindType(implementations, typeof(IEventImplementation));

        return CreateInternal(
            methodWrapperType,
            propertyImplementationType,
            propertyWrapperType,
            eventImplementationType
        );
    }

    static Type? FindType(Type[] types, Type interfaceType)
    {
        var type = types
            .Where(t => TypeInterfaces.Get(t).DoesTypeImplement(interfaceType))
            .SingleOrDefault($"Multiple types of {interfaceType} found in {String.Join(", ", types.Cast<Type>())}");

        return type;
    }
}

public record BakeryConfiguration(IBakeryComponentGenerators Generators, IDefaultProvider DefaultProvider, Boolean MakeAbstract = false)
{
    public static BakeryConfiguration Create(params Type[] implementations)
        => new BakeryConfiguration(ComponentGenerators.Create(implementations), Defaults.GetDefaultDefaultProvider());

    public static BakeryConfiguration PocGenerationConfiguration
        = new BakeryConfiguration(ComponentGenerators.Create(), Defaults.GetDefaultDefaultProvider());

    public AbstractlyBakery CreateBakery(String name) => new Bakery(name, this);
}

public class MixinGenerator { }
