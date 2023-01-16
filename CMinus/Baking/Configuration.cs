using CMinus.Baking;
using System;
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

    public static ComponentGenerators Create(Type? methodWrapperType, Type propertyImplementationType, Type? propertyWrapperType, Type eventImplementationType)
        => new ComponentGenerators(
            methodWrapperType is not null ? MethodGenerator.Create(methodWrapperType) : null,
            PropertyGenerator.Create(propertyImplementationType),
            propertyWrapperType is not null ? PropertyGenerator.Create(propertyWrapperType) : null,
            EventGenerator.Create(eventImplementationType)
        );
}

public record BakeryConfiguration(IBakeryComponentGenerators Generators, IDefaultProvider DefaultProvider, Boolean MakeAbstract = false)
{
    public static BakeryConfiguration Create(Type? methodWrapperType = null, Type? propertyImplementationType = null, Type? propertyWrappingType = null, Type? eventImplementationType = null)
        => new BakeryConfiguration(ComponentGenerators.Create(
            methodWrapperType: methodWrapperType,
            propertyImplementationType: propertyImplementationType ?? typeof(SimplePropertyImplementation<>),
            propertyWrapperType: propertyWrappingType,
            eventImplementationType: eventImplementationType ?? typeof(GenericEventImplementation<>)
        ), Defaults.GetDefaultDefaultProvider());

    public static BakeryConfiguration PocGenerationConfiguration
        = new BakeryConfiguration(ComponentGenerators.Create(
            methodWrapperType: null,
            propertyImplementationType: typeof(SimplePropertyImplementation<>),
            propertyWrapperType: null,
            eventImplementationType: typeof(GenericEventImplementation<>)
        ), Defaults.GetDefaultDefaultProvider());

    public AbstractlyBakery CreateBakery(String name) => new Bakery(name, this);
}

public class MixinGenerator { }
