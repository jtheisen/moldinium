using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using CMinus.Injection;

namespace CMinus;

public interface IPropertyImplementation : IImplementation { }

public interface IPropertyWrapper : IPropertyImplementation { }

public interface IStandardPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyImplementation
{
    void Init(Value def);

    Value Get(Object self, ref Mixin mixin);

    void Set(Object self, ref Mixin mixin, Value value);
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

public interface ITrivialPropertyWrapper : IPropertyWrapper { }

public struct TrivialPropertyWrapper : ITrivialPropertyWrapper { }

public abstract class AbstractGenerator
{
    protected MethodBuilder Create(
        TypeBuilder typeBuilder,
        MethodInfo methodTemplate,
        MethodAttributes toAdd = MethodAttributes.Public,
        MethodAttributes toRemove = MethodAttributes.Abstract
    )
    {
        var attributes = methodTemplate.Attributes;
        attributes |= toAdd;
        attributes &= ~toRemove;

        var parameters = methodTemplate.GetParameters();

        var rcms = methodTemplate.ReturnParameter.GetRequiredCustomModifiers();

        var methodBuilder = typeBuilder.DefineMethod(
            methodTemplate.Name,
            attributes,
            methodTemplate.CallingConvention,
            methodTemplate.ReturnType,
            methodTemplate.ReturnParameter.GetRequiredCustomModifiers(),
            methodTemplate.ReturnParameter.GetOptionalCustomModifiers(),
            parameters.Select(p => p.ParameterType).ToArray(),
            parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray(),
            parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray()
        );

        if (methodTemplate.DeclaringType!.IsClass)
        {
            typeBuilder.DefineMethodOverride(methodBuilder, methodTemplate);
        }

        return methodBuilder;
    }
}

public abstract class AbstractGeneratorWithImplementation : AbstractGenerator
{
    protected readonly Type implementationType;

    readonly TypeArgumentInfo[] typeArgumentsInfos;

    struct TypeArgumentInfo
    {
        public Type argumentType;
        public Type parameterType;
        public ImplementationTypeArgumentKind parameterKind;
    }

    readonly Dictionary<Type, ImplementationTypeArgumentKind> typeArgumentsToKindMapping;

    protected IDictionary<Type, ImplementationTypeArgumentKind> GetArgumentKinds()
        => typeArgumentsInfos.ToDictionary(i => i.argumentType, i => i.parameterKind);

    protected virtual IEnumerable<Type> GetInterfacesToIgnore() => new[] { typeof(IImplementation) };

    protected AbstractGeneratorWithImplementation(Type implementationType)
    {
        this.implementationType = implementationType;

        var interfacesToIgnore = GetInterfacesToIgnore().ToArray();

        var implementationInterfaceType = implementationType
            .GetInterfaces()
            .Where(i => !interfacesToIgnore.Contains(i))
            .Single($"Expected implementation type {implementationType} to implement only a single interface besides {String.Join(", ", GetInterfacesToIgnore())}")
            ;

        var implementationInterfaceTypeDefinition = implementationInterfaceType.IsGenericType
            ? implementationInterfaceType.GetGenericTypeDefinition()
            : implementationInterfaceType;

        var typeArguments = implementationInterfaceType.GetGenericArguments();
        var typeParameters = implementationInterfaceTypeDefinition.GetGenericArguments();

        if (typeArguments.Length != typeParameters.Length) throw new Exception("Unexpected have different numbers of type parameters");

        typeArgumentsToKindMapping = new Dictionary<Type, ImplementationTypeArgumentKind>();

        typeArgumentsInfos = typeParameters.Select((p, i) =>
        {
            var a = p.GetCustomAttribute<TypeKindAttribute>();

            if (a is null) throw new Exception($"Expected implementation interface {p} type paramter {p} to have a {nameof(TypeKindAttribute)}");

            var arg = typeArguments[i];

            switch (a.Kind)
            {
                case ImplementationTypeArgumentKind.Value:
                    if (!arg.IsGenericParameter) throw new Exception($"Implementation type {implementationType} must be itself be generic in type parameter {p} of interface {implementationInterfaceTypeDefinition}");
                    break;
                default:
                    break;
            }

            typeArgumentsToKindMapping[arg] = a.Kind;

            return new TypeArgumentInfo
            {
                parameterType = p,
                argumentType = arg,
                parameterKind = a.Kind
            };
        }).ToArray();

        var mixinArgumentInfo = typeArgumentsInfos
            .Where(i => i.parameterKind == ImplementationTypeArgumentKind.Mixin)
            .ToArray();

        if (mixinArgumentInfo.Length > 1)
        {
            throw new Exception("Multiple mixins are not yet supported");
        }
        else if (mixinArgumentInfo.Length > 0)
        {
            mixinType = mixinArgumentInfo.Single().argumentType;
        }
    }



}

public abstract class AbstractMethodGenerator : AbstractGenerator
{
    public virtual void GenerateMethod(BakingState state, MethodInfo method)
    {
        var typeBuilder = state.TypeBuilder;

        var returnType = method.ReturnType;

        //var propertyBuilder = typeBuilder.defin(property.Name, property.Attributes, returnType, null);

        var mixinFieldBuilder = EnsureMixin(state);

        var argumentKinds = GetArgumentKinds();

        argumentKinds[valueType] = ImplementationTypeArgumentKind.Value;

        var (fieldBuilder, backingGetMethod, backingSetMethod) = GetPropertyImplementation(state, property);

        var methodCreator = new MethodCreation(typeBuilder, argumentKinds, fieldBuilder, mixinFieldBuilder);

    }
}

public abstract class AbstractPropertyGenerator : AbstractGenerator
{
    public virtual void GenerateProperty(BakingState state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var valueType = property.PropertyType;

        var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, valueType, null);

        var mixinFieldBuilder = EnsureMixin(state);

        var getMethod = property.GetGetMethod();
        var setMethod = property.GetSetMethod();

        var argumentKinds = GetArgumentKinds();

        argumentKinds[valueType] = ImplementationTypeArgumentKind.Value;

        var (fieldBuilder, backingGetMethod, backingSetMethod) = GetPropertyImplementation(state, property);

        var methodCreator = new MethodCreation(typeBuilder, argumentKinds, fieldBuilder, mixinFieldBuilder);

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

                var defaultImplementationFieldBuilder = state.EnsureMixin(state, defaultType, true);

                methodCreator.GenerateImplementationCode(
                    state.ConstructorGenerator,
                    fieldBuilder, backingInitMethod,
                    MethodCreation.ValueAt.GetFromDefaultImplementationPassedByValue,
                    defaultImplementationFieldBuilder,
                    defaultImplementationGetMethod
                );
            }
        }

        if (getMethod is not null)
        {
            var getMethodBuilder = methodCreator.CreatePropertyMethod(
                getMethod, getMethod.GetBaseDefinition(),
                backingGetMethod,
                valueType,
                toRemove: MethodAttributes.Abstract
            );

            propertyBuilder.SetGetMethod(getMethodBuilder);
        }

        if (setMethod is not null)
        {
            var setMethodBuilder = methodCreator.CreatePropertyMethod(
                setMethod, setMethod.GetBaseDefinition(),
                backingSetMethod,
                valueType,
                toRemove: MethodAttributes.Abstract
            );

            propertyBuilder.SetSetMethod(setMethodBuilder);
        }
    }

    protected virtual IDictionary<Type, ImplementationTypeArgumentKind> GetArgumentKinds()
        => new Dictionary<Type, ImplementationTypeArgumentKind>();

    protected virtual FieldBuilder? EnsureMixin(BakingState state) => null;

    protected abstract PropertyImplementation GetPropertyImplementation(BakingState state, PropertyInfo property);

    protected MethodInfo GetMethod(FieldBuilder fieldBuilder, String name)
        => fieldBuilder.FieldType.GetMethod(name)
        ?? throw new Exception($"Property implementation type {fieldBuilder.FieldType} must have a '{name}' method");

    protected MethodImplementation GetMethodImplementation(FieldBuilder fieldBuilder, String name)
    {
        var type = fieldBuilder.FieldType;

        var method = type.GetMethod(name);
        var beforeMethod = type.GetMethod("Before" + name);
        var afterMethod = type.GetMethod("After" + name);
        var afterOnErrorMethod = type.GetMethod("AfterError" + name);

        var haveAfterMethod = afterMethod is not null;
        var haveAfterOnErrorMethod = afterOnErrorMethod is not null;


        var haveBeforeOrAfter = beforeMethod is not null || haveAfterMethod || haveAfterOnErrorMethod;

        if (method is not null)
        {
            if (haveBeforeOrAfter) throw new Exception($"Property implementation type can't define both a {name} method and the respective Before and After methods");

            return method;
        }
        else if (haveBeforeOrAfter || TypeInterfaces.Get(type).DoesTypeImplement(typeof(IPropertyWrapper)))
        {
            if (haveAfterMethod != haveAfterOnErrorMethod) throw new Exception($"Property implementation {type} must define either neither or both of the After methods");

            AssertReturnType(beforeMethod, typeof(Boolean));
            AssertReturnType(afterMethod, typeof(void));
            AssertReturnType(afterOnErrorMethod, typeof(Boolean));

            return new WrappingMethodImplementation(beforeMethod, afterMethod, afterOnErrorMethod);
        }
        else
        {
            throw new Exception($"Property implementation {type} must define either {name}, one of the respective Before and After methods or implement {nameof(IPropertyWrapper)}");
        }
    }

    void AssertReturnType(MethodInfo? method, Type type)
    {
        if (method is null) return;

        if (method.ReturnType != type) throw new Exception($"Property implementation {method.DeclaringType} must define the method {method} with a return type of {type}");
    }

    protected MethodInfo GetPropertyMethod(PropertyInfo property, Boolean setter)
        => (setter ? property.GetSetMethod() : property.GetGetMethod())
        ?? throw new Exception($"Property {property.Name} on implementation type {property.DeclaringType} must have a {(setter ? "setter" : "getter")} method");
}

public class GenericPropertyGenerator : AbstractGeneratorWithImplementation
{
    protected override FieldBuilder? EnsureMixin(BakingState state) => mixinType is not null ? state.EnsureMixin(state, mixinType, false) : null;

    Type[] GetTypeArguments(BakingState state, Type declaringType, Type valueType)
    {
        var arguments = new List<Type>();

        foreach (var type in propertyImplementationType.GetGenericArguments())
        {
            var kind = typeArgumentsToKindMapping[type];

            switch (kind)
            {
                case ImplementationTypeArgumentKind.Value:
                    arguments.Add(valueType);
                    break;
                default:
                    throw new Exception($"Dont know how to handle type parameter {type} of property implementation type {propertyImplementationType}");
            }
        }

        return arguments.ToArray();
    }

    protected override PropertyImplementation GetPropertyImplementation(BakingState state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var typeArguments = GetTypeArguments(state, typeof(Object), property.PropertyType);

        var backingPropertyImplementationType = implementationType.IsGenericTypeDefinition
            ? implementationType.MakeGenericType(typeArguments)
            : implementationType;

        var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingPropertyImplementationType, FieldAttributes.Private);

        var implementation = new PropertyImplementation(
            fieldBuilder,
            GetMethodImplementation(fieldBuilder, "Get"),
            GetMethodImplementation(fieldBuilder, "Set")
        );

        return implementation;
    }
}

public class UnimplementedPropertyGenerator : AbstractPropertyGenerator
{
    public static readonly UnimplementedPropertyGenerator Instance = new UnimplementedPropertyGenerator();

    public override void GenerateProperty(BakingState state, PropertyInfo property) => throw new NotImplementedException();

    protected override PropertyImplementation
        GetPropertyImplementation(BakingState state, PropertyInfo property)
        => throw new NotImplementedException();
}

public class DelegatingPropertyGenerator : AbstractPropertyGenerator
{
    private readonly FieldBuilder fieldBuilder;

    public DelegatingPropertyGenerator(FieldBuilder targetFieldBuilder)
    {
        this.fieldBuilder = targetFieldBuilder;
    }

    protected override PropertyImplementation
        GetPropertyImplementation(BakingState state, PropertyInfo property)
    {
        var propertyOnImplementation = fieldBuilder.FieldType.GetProperty(property.Name);

        if (propertyOnImplementation is null) throw new Exception($"Implementing type {fieldBuilder.FieldType} unexpectedly has no property named {property.Name}");

        return new PropertyImplementation(fieldBuilder, GetPropertyMethod(propertyOnImplementation, false), GetPropertyMethod(propertyOnImplementation, true));
    }
}

public static class PropertyGenerator
{
    public static AbstractPropertyGenerator Create(Type propertyImplementationType)
    {
        var instance = new GenericPropertyGenerator(propertyImplementationType);

        return instance;
    }
}

