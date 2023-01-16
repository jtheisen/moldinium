using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace CMinus;

public class CheckedImplementation
{
    readonly TypeArgumentInfo[] typeArgumentsInfos;

    struct TypeArgumentInfo
    {
        public Type argumentType;
        public Type parameterType;
        public ImplementationTypeArgumentKind parameterKind;
    }

    Dictionary<Type, ImplementationTypeArgumentKind> typeArgumentsToKindMapping;

    public IReadOnlyDictionary<Type, ImplementationTypeArgumentKind> TypeArgumentsToKindMapping
        => typeArgumentsToKindMapping;

    public IDictionary<Type, ImplementationTypeArgumentKind> GetArgumentKinds()
        => typeArgumentsInfos.ToDictionary(i => i.argumentType, i => i.parameterKind);

    public Type Type { get; }

    public Type? MixinType { get; }

    public CheckedImplementation(Type implementationType, params Type[] interfaceTypesToIgnore)
    {
        Type = implementationType;

        var implementationInterfaceType = implementationType
            .GetInterfaces()
            .Where(i => !interfaceTypesToIgnore.Contains(i))
            .Single($"Expected implementation type {implementationType} to implement only a single interface besides {String.Join(", ", interfaceTypesToIgnore.Cast<Type>())}")
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
            MixinType = mixinArgumentInfo.Single().argumentType;
        }
    }

    Type[] GetTypeArguments(Type implementationType, Type? valueType, Type? returnType)
    {
        var arguments = new List<Type>();

        foreach (var type in Type.GetGenericArguments())
        {
            var kind = typeArgumentsToKindMapping[type];

            Type Throw() => throw new Exception($"{implementationType} can't have a {kind} type in this context");

            switch (kind)
            {
                case ImplementationTypeArgumentKind.Value:
                    arguments.Add(valueType ?? Throw());
                    break;
                case ImplementationTypeArgumentKind.Return:
                    arguments.Add(returnType ?? Throw());
                    break;
                default:
                    throw new Exception($"Dont know how to handle type parameter {type} of implementation type {implementationType}");
            }
        }

        return arguments.ToArray();
    }

    public Type MakeImplementationType(Type? valueType = null, Type? returnType = null)
    {
        var typeArguments = GetTypeArguments(Type, valueType, returnType);

        var implementationType = Type.IsGenericTypeDefinition ? Type.MakeGenericType(typeArguments) : Type;

        return implementationType;
    }
}

