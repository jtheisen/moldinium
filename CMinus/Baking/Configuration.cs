using CMinus.Baking;
using CMinus.Injection;
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
    private readonly AbstractPropertyGenerator propertyImplementationGenerator;
    private readonly AbstractPropertyGenerator? propertyWrapperGenerator;
    private readonly AbstractEventGenerator eventGenerator;

    public ComponentGenerators(
        AbstractMethodGenerator? methodWrapperGenerator,
        AbstractPropertyGenerator propertyImplementationGenerator,
        AbstractPropertyGenerator? propertyWrapperGenerator,
        AbstractEventGenerator eventGenerator)
    {
        this.methodWrapperGenerator = methodWrapperGenerator;
        this.propertyImplementationGenerator = propertyImplementationGenerator;
        this.propertyWrapperGenerator = propertyWrapperGenerator;
        this.eventGenerator = eventGenerator;
    }

    public MixinGenerator[] GetMixInGenerators(Type type) => new MixinGenerator[] { };

    public AbstractPropertyGenerator? GetPropertyGenerator(PropertyInfo property)
    {
        var getter = CheckMethod(property.GetGetMethod());
        var setter = CheckMethod(property.GetSetMethod());

        if (!getter.exists)
        {
            if (!setter.exists)
            {
                throw new Exception($"The property {property.Name} on {property.DeclaringType} has neither a setter nor a getter");
            }
            else
            {
                throw new Exception($"The property {property.Name} on {property.DeclaringType} has a setter but no getter");
            }
        }

        if (setter.exists)
        {
            if (getter.implemented != setter.implemented)
            {
                throw new Exception($"The property {property.Name} on {property.DeclaringType} should implement either both getter and setter or neither");
            }
        }

        if (getter.implemented)
        {
            if (propertyWrapperGenerator is not null)
            {
                return propertyWrapperGenerator;
            }
            else
            {
                return null;
            }
        }
        else
        {
            return propertyImplementationGenerator;
        }
    }

    public AbstractMethodGenerator? GetMethodGenerator(MethodInfo method)
    {
        var check = CheckMethod(method);

        if (!check.implemented) throw new Exception($"Method {method} on {method.DeclaringType} must have a default implementation to wrap");

        return methodWrapperGenerator;
    }

    (Boolean exists, Boolean implemented) CheckMethod(MethodInfo? method)
    {
        return (exists: method is not null, implemented: !method?.IsAbstract ?? false);
    }


    public AbstractEventGenerator GetEventGenerator(EventInfo evt) => eventGenerator;

    static ComponentGenerators CreateInternal(Type? methodWrapperType = null, Type? propertyImplementationType = null, Type? propertyWrapperType = null, Type? eventImplementationType = null)
        => new ComponentGenerators(
            methodWrapperType is not null ? MethodGenerator.Create(methodWrapperType) : null,
            PropertyGenerator.Create(propertyImplementationType ?? typeof(SimplePropertyImplementation<>)),
            propertyWrapperType is not null ? PropertyGenerator.Create(propertyWrapperType) : null,
            EventGenerator.Create(eventImplementationType ?? typeof(GenericEventImplementation<>))
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
