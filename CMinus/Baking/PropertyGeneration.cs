using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace CMinus;

public enum ImplementationTypeArgumentKind
{
    Value,
    Container,
    Mixin
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

public interface IPropertyImplementation<
    [TypeKind(ImplementationTypeArgumentKind.Value)] Value,
    [TypeKind(ImplementationTypeArgumentKind.Mixin)] Mixin
> : IPropertyImplementation
{
    Value Get(Object self, ref Mixin mixin);

    void Set(Object self, ref Mixin mixin, Value value);
}

public struct EmptyMixIn { }

public interface ISimplePropertyImplementation<[TypeKind(ImplementationTypeArgumentKind.Value)] ValueT> : IPropertyImplementation
{
    ValueT Get();

    void Set(ValueT value);
}

public struct SimplePropertyImplementation<T> : ISimplePropertyImplementation<T>
{
    T value;

    public T Get() => value;

    public void Set(T value) => this.value = value;
}

public abstract class AbstractGenerator
{
    protected MethodBuilder Create(TypeBuilder typeBuilder, MethodInfo methodTemplate, Boolean isAbstract = true)
    {
        var attributes = methodTemplate.Attributes | MethodAttributes.Public;

        if (!isAbstract) attributes &= ~MethodAttributes.Abstract;

        var parameters = methodTemplate.GetParameters();

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

        return methodBuilder;
    }
}

public abstract class AbstractPropertyGenerator : AbstractGenerator
{
    public virtual void GenerateProperty(BakingState state, PropertyInfo property)
    {
        var typeBuilder = state.TypeBuilder;

        var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

        var mixinFieldBuilder = EnsureMixin(state);

        var getMethod = property.GetGetMethod();
        var setMethod = property.GetSetMethod();

        var argumentKinds = GetArgumentKinds();

        argumentKinds[property.PropertyType] = ImplementationTypeArgumentKind.Value;

        var (fieldBuilder, backingGetMethod, backingSetMethod) = GetBackings(typeBuilder, property);

        {
            var backingInitMethod = fieldBuilder.FieldType.GetMethod("Init");

            if (backingInitMethod is not null)
            {
                GenerateWrapperCode(state.ConstructorGenerator, fieldBuilder, backingInitMethod, argumentKinds, mixinFieldBuilder);
            }
        }

        if (getMethod is not null)
        {
            var getMethodBuilder = Create(typeBuilder, getMethod, isAbstract: false);
            var generator = getMethodBuilder.GetILGenerator();
            GenerateWrapperCode(generator, fieldBuilder, backingGetMethod, argumentKinds, mixinFieldBuilder);
            propertyBuilder.SetGetMethod(getMethodBuilder);
        }

        if (setMethod is not null)
        {
            var setMethodBuilder = Create(typeBuilder, setMethod, isAbstract: false);
            var generator = setMethodBuilder.GetILGenerator();
            GenerateWrapperCode(generator, fieldBuilder, backingSetMethod, argumentKinds, mixinFieldBuilder);
            propertyBuilder.SetSetMethod(setMethodBuilder);
        }
    }

    void GenerateWrapperCode(ILGenerator generator, FieldBuilder fieldBuilder, MethodInfo backingMethod, IDictionary<Type, ImplementationTypeArgumentKind> argumentKinds, FieldBuilder? mixInFieldBuilder)
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
                    case ImplementationTypeArgumentKind.Container:
                        if (byRef) throw new Exception("Object references must be passed by value");
                        generator.Emit(OpCodes.Ldarg_0);
                        break;
                    case ImplementationTypeArgumentKind.Value:
                        if (byRef) throw new Exception("Values must be passed by value");
                        generator.Emit(OpCodes.Ldarg_1);
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

        generator.Emit(OpCodes.Ret);
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

    protected abstract (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod)
        GetBackings(TypeBuilder typeBuilder, PropertyInfo property);

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

    protected override FieldBuilder? EnsureMixin(BakingState state) => mixinType is not null ? state.EnsureMixin(state, mixinType) : null;

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
                case ImplementationTypeArgumentKind.Container:
                    if (!arg.IsGenericParameter) throw new Exception($"Property implementation type {propertyImplementationType} must be itself be generic in type parameter {p} of interface {propertyImplementationInterfaceTypeDefinition}");

                    typeParameterToKindMapping[arg] = a.Kind;
                    break;
                default:
                    break;
            }

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

    Type[] GetTypeArguments(Type declaringType, Type valueType)
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
                case ImplementationTypeArgumentKind.Container:
                    arguments.Add(declaringType);
                    break;
                default:
                    throw new Exception($"Dont know how to handle type parameter {type} of property implementation type {propertyImplementationType}");
            }
        }

        return arguments.ToArray();
    }

    protected override (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod)
        GetBackings(TypeBuilder typeBuilder, PropertyInfo property)
    {
        var typeArguments = GetTypeArguments(typeof(Object), property.PropertyType);

        var backingEventImplementationType = propertyImplementationType.MakeGenericType(typeArguments);
        var fieldBuilder = typeBuilder.DefineField($"backing_{property.Name}", backingEventImplementationType, FieldAttributes.Private);
        return (fieldBuilder, GetMethod(fieldBuilder, "Get"), GetMethod(fieldBuilder, "Set"));
    }
}

public class UnimplementedPropertyGenerator : AbstractPropertyGenerator
{
    public static readonly UnimplementedPropertyGenerator Instance = new UnimplementedPropertyGenerator();

    public override void GenerateProperty(BakingState state, PropertyInfo property) => throw new NotImplementedException();

    protected override (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod)
        GetBackings(TypeBuilder typeBuilder, PropertyInfo property)
        => throw new NotImplementedException();
}

public class DelegatingPropertyGenerator : AbstractPropertyGenerator
{
    private readonly FieldBuilder fieldBuilder;

    public DelegatingPropertyGenerator(FieldBuilder targetFieldBuilder)
    {
        this.fieldBuilder = targetFieldBuilder;
    }

    protected override (FieldBuilder fieldBuilder, MethodInfo backingGetMethod, MethodInfo backingSetMethod)
        GetBackings(TypeBuilder typeBuilder, PropertyInfo property)
    {
        var propertyOnImplementation = fieldBuilder.FieldType.GetProperty(property.Name);

        if (propertyOnImplementation is null) throw new Exception($"Implementing type {fieldBuilder.FieldType} unexpectedly has no property named {property.Name}");

        return (fieldBuilder, GetPropertyMethod(propertyOnImplementation, false), GetPropertyMethod(propertyOnImplementation, true));
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

