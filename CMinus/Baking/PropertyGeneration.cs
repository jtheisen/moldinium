using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using CMinus.Injection;
using Castle.DynamicProxy.Generators.Emitters.SimpleAST;

namespace CMinus;

public enum ImplementationTypeArgumentKind
{
    Value,
    Mixin,
    NestedPropertyImplementation
}

[AttributeUsage(AttributeTargets.GenericParameter)]
public class TypeKindAttribute : Attribute
{
    public TypeKindAttribute(ImplementationTypeArgumentKind type)
    {
        Kind = type;
    }

    public ImplementationTypeArgumentKind Kind { get; }
}

public interface IPropertyImplementation { }

/* Difficulties with wrapping
 * 
 * - Creating delegates to the nested implementation is possible but difficult if the nested method is virtual
 * - Creating structs that delegate to the nested implementation is also difficult, but likely the way to implement the former
 */

public interface INestedPropertyImplementation<Value>
{
    Value Get();

    void Set(Value value);
}

public interface IPropertyImplementation<
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

                GenerateWrapperCode(state.ConstructorGenerator, MethodType.Constructor, fieldBuilder, backingInitMethod, argumentKinds, mixinFieldBuilder, defaultImplementationFieldBuilder, defaultImplementationGetMethod);
            }
        }

        if (getMethod is not null)
        {
            var getMethodBuilder = Create(typeBuilder, getMethod, toRemove: MethodAttributes.Abstract);
            var generator = getMethodBuilder.GetILGenerator();
            if (backingGetMethod.Method is null) throw new NotImplementedException(); // FIXME
            GenerateWrapperCode(generator, MethodType.Get, fieldBuilder, backingGetMethod.Method, argumentKinds, mixinFieldBuilder);
            propertyBuilder.SetGetMethod(getMethodBuilder);
        }

        if (setMethod is not null)
        {
            var setMethodBuilder = Create(typeBuilder, setMethod, toRemove: MethodAttributes.Abstract);
            var generator = setMethodBuilder.GetILGenerator();
            if (backingSetMethod.Method is null) throw new NotImplementedException();
            GenerateWrapperCode(generator, MethodType.Set, fieldBuilder, backingSetMethod.Method, argumentKinds, mixinFieldBuilder);
            propertyBuilder.SetSetMethod(setMethodBuilder);
        }
    }

    void GenerateWrapperCode(
        ILGenerator generator,
        MethodType methodType,
        FieldBuilder fieldBuilder,
        MethodInfo backingMethod,
        IDictionary<Type, ImplementationTypeArgumentKind> argumentKinds,
        FieldBuilder? mixInFieldBuilder,
        FieldBuilder? defaultImplementationFieldBuilder = null,
        MethodInfo? defaultImplementationGetMethod = null
    )
    {
        if (!backingMethod.IsStatic)
        {
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldflda, fieldBuilder);
        }

        var parameters = backingMethod.GetParameters();

        foreach (var p in parameters)
        {
            if (p.ParameterType == typeof(Object))
            {
                generator.Emit(OpCodes.Ldarg_0);
            }
            else
            {
                var (parameterType, byRef) = GetParameterType(p);

                if (!argumentKinds.TryGetValue(parameterType, out var kind))
                {
                    throw new Exception($"Dont know how to handle argument {p.Name} of type {p.ParameterType} of property implementation method {backingMethod.Name} on property implementation type {backingMethod.DeclaringType}");
                }

                switch (kind)
                {
                    case ImplementationTypeArgumentKind.Value:
                        if (byRef) throw new Exception("Values must be passed by value");

                        switch (methodType)
                        {
                            case MethodType.Constructor:
                                if (defaultImplementationFieldBuilder is null || defaultImplementationGetMethod is null)
                                {
                                    throw new Exception("Expected to have a fieldBuilder with a get method");
                                }
                                generator.Emit(OpCodes.Ldarg_0);
                                generator.Emit(OpCodes.Ldflda, defaultImplementationFieldBuilder);
                                generator.Emit(OpCodes.Call, defaultImplementationGetMethod);
                                break;
                            case MethodType.Set:
                                generator.Emit(OpCodes.Ldarg_1);
                                break;
                            default:
                                break;
                        }

                        break;
                    case ImplementationTypeArgumentKind.Mixin:
                        if (!byRef) throw new Exception("Mixins must be passed by ref");
                        generator.Emit(OpCodes.Ldarg_0);
                        generator.Emit(OpCodes.Ldflda, mixInFieldBuilder!);
                        break;
                    default:
                        throw new Exception($"Unkown argument kind {kind}");
                }
            }
        }

        generator.Emit(OpCodes.Call, backingMethod);

        if (methodType != MethodType.Constructor)
        {
            generator.Emit(OpCodes.Ret);
        }
    }

    (Type type, Boolean byRef) GetParameterType(ParameterInfo p)
    {
        var type = p.ParameterType;

        if (type is null) throw new Exception($"Parameter {p} unexpectedly has no type");

        return type.IsByRef ? ((type.GetElementType() ?? throw new Exception("Can't get element type of ref type")), true) : (type, false);
    }

    protected virtual IDictionary<Type, ImplementationTypeArgumentKind> GetArgumentKinds()
        => new Dictionary<Type, ImplementationTypeArgumentKind>();

    protected virtual FieldBuilder? EnsureMixin(BakingState state) => null;

    protected record MethodImplementation(MethodInfo? Method = null, MethodInfo? BeforeMethod = null, MethodInfo? AfterMethod = null)
    {
        public static implicit operator MethodImplementation(MethodInfo method) => new MethodImplementation(method);
    }

    protected record PropertyImplementation(FieldBuilder FieldBuilder, MethodImplementation Get, MethodImplementation Set);

    protected abstract PropertyImplementation GetPropertyImplementation(BakingState state, PropertyInfo property);

    protected MethodInfo GetMethod(FieldBuilder fieldBuilder, String name)
        => fieldBuilder.FieldType.GetMethod(name)
        ?? throw new Exception($"Property implementation type {fieldBuilder.FieldType} must have a '{name}' method");

    protected MethodInfo GetPropertyMethod(PropertyInfo property, Boolean setter)
        => (setter ? property.GetSetMethod() : property.GetGetMethod())
        ?? throw new Exception($"Property {property.Name} on implementation type {property.DeclaringType} must have a {(setter ? "setter" : "getter")} method");
}

public class GenericPropertyGenerator : AbstractPropertyGenerator
{
    readonly Type propertyImplementationType;

    readonly Type? mixinType;

    readonly TypeArgumentInfo[] typeArgumentsInfos;

    struct TypeArgumentInfo
    {
        public Type argumentType;
        public Type parameterType;
        public ImplementationTypeArgumentKind parameterKind;
    }

    readonly Dictionary<Type, ImplementationTypeArgumentKind> typeParameterToKindMapping;

    protected override IDictionary<Type, ImplementationTypeArgumentKind> GetArgumentKinds()
        => typeArgumentsInfos.ToDictionary(i => i.argumentType, i => i.parameterKind);

    protected override FieldBuilder? EnsureMixin(BakingState state) => mixinType is not null ? state.EnsureMixin(state, mixinType, false) : null;

    public GenericPropertyGenerator(Type propertyImplementationType)
    {
        this.propertyImplementationType = propertyImplementationType;

        var propertyImplementationInterfaceType = propertyImplementationType
            .GetInterfaces()
            .Where(i => i != typeof(IPropertyImplementation))
            .Single($"Expected property implementation type {propertyImplementationType} to implement only a single interface besides {nameof(IPropertyImplementation)}")
            ;

        var propertyImplementationInterfaceTypeDefinition = propertyImplementationInterfaceType.GetGenericTypeDefinition();

        var typeArguments = propertyImplementationInterfaceType.GetGenericArguments();
        var typeParameters = propertyImplementationInterfaceTypeDefinition.GetGenericArguments();

        if (typeArguments.Length != typeParameters.Length) throw new Exception("Unexpected have different numbers of type parameters");

        typeParameterToKindMapping = new Dictionary<Type, ImplementationTypeArgumentKind>();

        typeArgumentsInfos = typeParameters.Select((p, i) =>
        {
            var a = p.GetCustomAttribute<TypeKindAttribute>();

            if (a is null) throw new Exception($"Expected property implementation interface {p} type paramter {p} to have a {nameof(TypeKindAttribute)}");

            var arg = typeArguments[i];

            switch (a.Kind)
            {
                case ImplementationTypeArgumentKind.Value:
                    if (!arg.IsGenericParameter) throw new Exception($"Property implementation type {propertyImplementationType} must be itself be generic in type parameter {p} of interface {propertyImplementationInterfaceTypeDefinition}");
                    break;
                default:
                    break;
            }

            typeParameterToKindMapping[arg] = a.Kind;

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

    Type[] GetTypeArguments(BakingState state, Type declaringType, Type valueType)
    {
        var arguments = new List<Type>();

        foreach (var type in propertyImplementationType.GetGenericArguments())
        {
            var kind = typeParameterToKindMapping[type];

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

        var backingEventImplementationType = propertyImplementationType.MakeGenericType(typeArguments);
        var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingEventImplementationType, FieldAttributes.Private);
        return new PropertyImplementation(fieldBuilder, GetMethod(fieldBuilder, "Get"), GetMethod(fieldBuilder, "Set"));
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

