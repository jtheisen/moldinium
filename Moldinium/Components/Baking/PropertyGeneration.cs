using System.Reflection.Emit;
using System.Reflection;
using Moldinium.Common.Misc;
using Moldinium.Common.Defaulting;

namespace Moldinium.Baking;

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

public interface ITrivialPropertyWrapper : IPropertyWrapperImplementation { }

public class TrivialPropertyWrapper : ITrivialPropertyWrapper, IEmptyImplementation { }

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

public abstract class AbstractPropertyGenerator : AbstractGenerator
{
    public virtual void GenerateProperty(IBuildingContext state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var valueType = property.PropertyType;

        var mixinFieldBuilder = EnsureMixin(state);

        var getMethod = property.GetGetMethod();
        var setMethod = property.GetSetMethod();

        var argumentKinds = GetArgumentKinds();

        argumentKinds[valueType] = ImplementationTypeArgumentKind.Value;

        var isImplementedByInterface = getMethod is not null ? state.GetOuterImplementationInfo(getMethod).IsImplememtedByInterface : false;

        var propertyImplementation = GetPropertyImplementation(state, property, isImplementedByInterface);

        if (propertyImplementation is null) return;

        var declaringType = property.DeclaringType ?? throw new Exception("Unexpectedly not having a declaring type");

        var (fieldBuilder, (getImplementation, setImplementation)) = propertyImplementation.Value;

        var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, valueType, null);

        if (declaringType.IsInterface)
        {
            NullabilityHelper.SetNullableAttributes(
                propertyBuilder.SetCustomAttribute,
                property.GetCustomAttributesData(),
                state.GetNullableFlagForInterface(declaringType)
            );
        }

        var codeCreator = new CodeCreation(typeBuilder, argumentKinds, fieldBuilder, mixinFieldBuilder);

        if (fieldBuilder is not null)
        {
            var backingInitMethod = fieldBuilder.FieldType.GetMethod("Init");

            if (backingInitMethod is not null)
            {
                var info = TypeProperties.Get(declaringType);

                var requiresDefault = !isImplementedByInterface && info.Properties.Single(p => p.info == property).requiresDefault;

                var takesDefaultValue = backingInitMethod.GetParameters().Select(p => p.ParameterType).Any(t => t == valueType);

                // If there's an init method taking a default value, we need to pass something even if the property doesn't actually
                // need a default. In that case, we use a dummy default provider that at least doesn't do any allocation.
                Type? genericDefaultImplementationType = requiresDefault ? state.DefaultProvider.GetDefaultType(valueType) : typeof(DummyDefault<>);

                if (genericDefaultImplementationType is null) throw new Exception($"Default provider can't handle type {valueType}, required for property {property}");

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
                getMethod,
                getImplementation,
                valueType,
                toRemove: MethodAttributes.Abstract
            );

            propertyBuilder.SetGetMethod(getMethodBuilder);
        }

        if (setMethod is not null)
        {
            var setMethodBuilder = codeCreator.CreateMethod(
                setMethod,
                setImplementation,
                valueType,
                toRemove: MethodAttributes.Abstract
            );

            propertyBuilder.SetSetMethod(setMethodBuilder);
        }
    }

    protected abstract (FieldBuilder?, PropertyImplementation)? GetPropertyImplementation(IBuildingContext state, PropertyInfo property, Boolean wrap);
}

public class GenericPropertyGenerator : AbstractPropertyGenerator
{
    private readonly CheckedImplementation? implementation, wrapper;

    public GenericPropertyGenerator(CheckedImplementation? implementation, CheckedImplementation? wrapper)
    {
        this.implementation = implementation;
        this.wrapper = wrapper;

        implementation?.AssertWrapperOrNot(false);
        wrapper?.AssertWrapperOrNot(true);
    }

    public override IEnumerable<Type?> GetMixinTypes()
    {
        yield return implementation?.MixinType;
        yield return wrapper?.MixinType;
    }

    protected override void AddArgumentKinds(Dictionary<Type, ImplementationTypeArgumentKind> argumentKinds)
    {
        implementation?.AddArgumentKinds(argumentKinds);
        wrapper?.AddArgumentKinds(argumentKinds);
    }

    protected override (FieldBuilder?, PropertyImplementation)? GetPropertyImplementation(IBuildingContext state, PropertyInfo property, Boolean wrap)
    {
        var outerGetImplementation = state.GetOuterImplementationInfo(property.GetGetMethod());
        var outerSetImplementation = state.GetOuterImplementationInfo(property.GetSetMethod());

        if (outerGetImplementation.Exists && outerSetImplementation.Exists)
        {
            if (outerGetImplementation.Kind != outerSetImplementation.Kind)
            {
                throw new Exception($"The property {property.Name} has an get method of {outerGetImplementation.Kind} and a set method of {outerSetImplementation.Kind}, but they should be the same");
            }

            if (outerGetImplementation.MixinFieldBuilder != outerSetImplementation.MixinFieldBuilder)
            {
                throw new Exception($"The property {property.Name} has an should have their get and set methods implemented by the same mixin");
            }
        }

        if (outerGetImplementation.Kind == MethodImplementationKind.ImplementedByMixin)
        {
            return (null, new PropertyImplementation(
                new OuterMethodImplemention(outerGetImplementation),
                new OuterMethodImplemention(outerSetImplementation)
            ));
        }

        var typeBuilder = state.TypeBuilder;

        var implementation = wrap ? wrapper : this.implementation;

        if (implementation is null)
        {
            // In this case, we can create the type without defining an implementation
            if (wrap && outerGetImplementation.IsMissingOrImplementedByInterface && outerSetImplementation.IsMissingOrImplementedByInterface) return null;

            throw new Exception($"Property {property} needs to be {(wrap ? "wrapped" : "implemented")}, but there is no corresponding property implementation type");
        }

        var propertyImplementationType = implementation.MakeImplementationType(propertyOrHandlerType: property.PropertyType);

        var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", propertyImplementationType, FieldAttributes.Private);

        var propertyImplementation = new PropertyImplementation(
            GetMethodImplementation(fieldBuilder, "Get", outerGetImplementation),
            GetMethodImplementation(fieldBuilder, "Set", outerSetImplementation)
        );

        return (fieldBuilder, propertyImplementation);
    }
}

public class UnimplementedPropertyGenerator : AbstractPropertyGenerator
{
    public static readonly UnimplementedPropertyGenerator Instance = new UnimplementedPropertyGenerator();

    public override void GenerateProperty(IBuildingContext state, PropertyInfo property) => throw new NotImplementedException();

    protected override (FieldBuilder, PropertyImplementation)?
        GetPropertyImplementation(IBuildingContext state, PropertyInfo property, Boolean wrap)
        => throw new NotImplementedException();
}

public static class PropertyGenerator
{
    public static AbstractPropertyGenerator Create(Type? propertyImplementationType, Type? propertyWrapperType)
    {
        var ignoredInterfaces = new[] {
            typeof(IPropertyImplementation),
            typeof(IPropertyWrapperImplementation),
        };

        var checkedImplementation
            = propertyImplementationType?.Apply(t => new CheckedImplementation(t, ignoredInterfaces));
        var checkedWrapper
            = propertyWrapperType?.Apply(t => new CheckedImplementation(t, ignoredInterfaces));

        var instance = new GenericPropertyGenerator(checkedImplementation, checkedWrapper);

        return instance;
    }
}

