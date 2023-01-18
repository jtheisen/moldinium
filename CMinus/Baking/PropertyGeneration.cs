using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using CMinus.Injection;

namespace CMinus;

public interface IStandardPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Container)] Container,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyImplementation
{
    void Init(Value def);

    Value Get(Container self, ref Mixin mixin);

    void Set(Container self, ref Mixin mixin, Value value);
}

public struct EmptyMixIn { }

public interface ISimplePropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] ValueT
> : IPropertyImplementation
{
    void Init(ValueT def);

    ValueT Get();

    void Set(ValueT value);
}

public struct SimplePropertyImplementation<T> : ISimplePropertyImplementation<T>
{
    T value;

    public void Init(T def)
    {
        value = def;
    }

    public T Get() => value;

    public void Set(T value) => this.value = value;
}

public interface ITrivialPropertyWrapper : IPropertyWrapperImplementation { }

public struct TrivialPropertyWrapper : ITrivialPropertyWrapper { }

public abstract class AbstractPropertyGenerator : AbstractGenerator
{
    public virtual void GenerateProperty(IBuildingContext state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var valueType = property.PropertyType;

        var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, valueType, null);

        var mixinFieldBuilder = EnsureMixin(state);

        var getMethod = property.GetGetMethod();
        var setMethod = property.GetSetMethod();

        var argumentKinds = GetArgumentKinds();

        argumentKinds[valueType] = ImplementationTypeArgumentKind.Value;

        var (fieldBuilder, (getImplementation, setImplementation)) = GetPropertyImplementation(state, property);

        var codeCreator = new CodeCreation(typeBuilder, argumentKinds, fieldBuilder, mixinFieldBuilder);

        {
            var backingInitMethod = fieldBuilder.FieldType.GetMethod("Init");

            if (backingInitMethod is not null)
            {
                var info = TypeProperties.Get(property.DeclaringType ?? throw new Exception("Unexpectedly not having a declaring type"));

                var requiresDefault = info.Properties.Single(p => p.info == property).requiresDefault;

                var takesDefaultValue = backingInitMethod.GetParameters().Select(p => p.ParameterType).Any(t => t == valueType);

                // If there's an init method taking a default value, we need to pass something even if the property doesn't actually
                // need a default. In that case, we use a dummy default provider that at least doesn't do any allocation.
                Type? genericDefaultImplementationType = requiresDefault ? state.DefaultProvider.GetDefaultType(valueType) : typeof(DummyDefault<>);

                if (genericDefaultImplementationType is null) throw new Exception($"Default provider can't handle type {valueType}");

                var defaultType = Defaults.CreateConcreteDefaultImplementationType(genericDefaultImplementationType, valueType);

                var defaultImplementationGetMethod = defaultType.GetProperty(nameof(IDefault<Dummy>.Default))?.GetGetMethod();

                if (defaultImplementationGetMethod is null) throw new Exception("Can't find getter on default value implementation");

                var defaultImplementationFieldBuilder = state.EnsureMixin(defaultType, true);

                codeCreator.GenerateImplementationCode(
                    state.ConstructorGenerator,
                    fieldBuilder, backingInitMethod,
                    CodeCreation.ValueOrReturnAt.GetFromDefaultImplementationPassedByValue,
                    haveExceptionAtLocal1: false,
                    defaultImplementationFieldBuilder,
                    defaultImplementationGetMethod
                );
            }
        }

        if (getMethod is not null)
        {
            var getMethodBuilder = codeCreator.CreateMethod(
                getMethod, getMethod.GetBaseDefinition(),
                getImplementation,
                valueType,
                toRemove: MethodAttributes.Abstract
            );

            propertyBuilder.SetGetMethod(getMethodBuilder);
        }

        if (setMethod is not null)
        {
            var setMethodBuilder = codeCreator.CreateMethod(
                setMethod, setMethod.GetBaseDefinition(),
                setImplementation,
                valueType,
                toRemove: MethodAttributes.Abstract
            );

            propertyBuilder.SetSetMethod(setMethodBuilder);
        }
    }

    protected virtual FieldBuilder? EnsureMixin(IBuildingContext state) => null;

    protected abstract (FieldBuilder, PropertyImplementation) GetPropertyImplementation(IBuildingContext state, PropertyInfo property);

    protected MethodInfo GetPropertyMethod(PropertyInfo property, Boolean setter)
        => (setter ? property.GetSetMethod() : property.GetGetMethod())
        ?? throw new Exception($"Property {property.Name} on implementation type {property.DeclaringType} must have a {(setter ? "setter" : "getter")} method");
}

public class GenericPropertyGenerator : AbstractPropertyGenerator
{
    private readonly CheckedImplementation implementation;

    public GenericPropertyGenerator(CheckedImplementation implementation)
    {
        this.implementation = implementation;
    }

    public override Type? GetMixinType() => implementation.MixinType;

    protected override IDictionary<Type, ImplementationTypeArgumentKind> GetArgumentKinds()
        => implementation.GetArgumentKinds();

    protected override FieldBuilder? EnsureMixin(IBuildingContext state) => implementation.MixinType is not null ? state.EnsureMixin(implementation.MixinType, false) : null;

    protected override (FieldBuilder, PropertyImplementation) GetPropertyImplementation(IBuildingContext state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var propertyImplementationType = implementation.MakeImplementationType(valueType: property.PropertyType);

        var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", propertyImplementationType, FieldAttributes.Private);

        var propertyImplementation = new PropertyImplementation(
            GetMethodImplementation(fieldBuilder, "Get"),
            GetMethodImplementation(fieldBuilder, "Set")
        );

        return (fieldBuilder, propertyImplementation);
    }
}

public class UnimplementedPropertyGenerator : AbstractPropertyGenerator
{
    public static readonly UnimplementedPropertyGenerator Instance = new UnimplementedPropertyGenerator();

    public override void GenerateProperty(IBuildingContext state, PropertyInfo property) => throw new NotImplementedException();

    protected override (FieldBuilder, PropertyImplementation)
        GetPropertyImplementation(IBuildingContext state, PropertyInfo property)
        => throw new NotImplementedException();
}

public class DelegatingPropertyGenerator : AbstractPropertyGenerator
{
    private readonly FieldBuilder fieldBuilder;

    public DelegatingPropertyGenerator(FieldBuilder targetFieldBuilder)
    {
        this.fieldBuilder = targetFieldBuilder;
    }

    protected override (FieldBuilder, PropertyImplementation)
        GetPropertyImplementation(IBuildingContext state, PropertyInfo property)
    {
        var propertyOnImplementation = fieldBuilder.FieldType.GetProperty(property.Name);

        if (propertyOnImplementation is null) throw new Exception($"Implementing type {fieldBuilder.FieldType} unexpectedly has no property named {property.Name}");

        return (fieldBuilder, new PropertyImplementation(GetPropertyMethod(propertyOnImplementation, false), GetPropertyMethod(propertyOnImplementation, true)));
    }
}

public static class PropertyGenerator
{
    public static AbstractPropertyGenerator Create(Type propertyImplementationType)
    {
        var ignoredInterfaces = new[] {
            typeof(IPropertyImplementation),
            typeof(IPropertyWrapperImplementation),
        };

        var instance = new GenericPropertyGenerator(new CheckedImplementation(propertyImplementationType, ignoredInterfaces));

        return instance;
    }
}

