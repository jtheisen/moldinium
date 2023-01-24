using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Moldinium;

public enum ImplementationTypeArgumentKind
{
    Value,
    Return,
    Handler,
    Exception,
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

public interface IImplementation { }

public interface IEmptyImplementation : IImplementation { }

public interface IPropertyImplementation : IImplementation { }

public interface IPropertyWrapperImplementation : IImplementation { }

public interface IMethodWrapperImplementation : IImplementation { }

public interface IEventImplementation : IImplementation { }

public interface IEventWrapperImplementation : IImplementation { }

public struct VoidDummy { }

public static partial class Extensions
{
    public static String GetQualifiedName(this MemberInfo member)
    {
        var name = member.Name;

        var lastDotAt = name.LastIndexOf('.');

        if (lastDotAt >= 0)
        {
            name = name[(lastDotAt + 1)..];
        }

        return $"{member.DeclaringType?.Name ?? "?"}.{name}";
    }
}

public record MethodImplementation
{
    public static implicit operator MethodImplementation(MethodInfo method) => new DirectMethodImplementation(method);
}

public record DirectMethodImplementation(MethodInfo Method) : MethodImplementation
{
    public override string ToString() => $"DirectMethodImplementation {Method.GetQualifiedName()}";
}

public record OuterMethodImplemention(MethodImplementationInfo WrappedMethod) : MethodImplementation;

public record WrappingMethodImplementation(
    MethodImplementationInfo? WrappedMethod,
    MethodInfo? BeforeMethod = null,
    MethodInfo? AfterMethod = null,
    MethodInfo? AfterOnErrorMethod = null
) : MethodImplementation;

public record PropertyImplementation(MethodImplementation Get, MethodImplementation Set);

public record EventImplementation(MethodImplementation GetOrAdd, MethodImplementation SetOrRemove);

public enum MethodImplementationKind
{
    NonExistent,
    NonImplemented,
    ImplementedByInterface,
    ImplementedByMixin
}

public struct MethodImplementationInfo
{
    MethodImplementationKind kind;

    public Boolean IsImplementedPrivately { get; }
    public MethodImplementationKind Kind => kind;

    public Boolean Exists => kind != MethodImplementationKind.NonExistent;
    public MethodInfo? ImplementationMethod { get; }
    public FieldBuilder? MixinFieldBuilder { get; }

    public Boolean IsImplememted => ImplementationMethod is not null;
    public Boolean IsImplememtedByInterface => kind == MethodImplementationKind.ImplementedByInterface;
    public Boolean IsMissingOrImplementedByInterface => kind != MethodImplementationKind.ImplementedByMixin;

    public MethodImplementationInfo(FieldBuilder mixinFieldBuilder, MethodInfo method)
    {
        IsImplementedPrivately = method.IsPrivate;
        kind = MethodImplementationKind.ImplementedByMixin;
        MixinFieldBuilder = mixinFieldBuilder;
        ImplementationMethod = method;
    }

    public MethodImplementationInfo(ImplementationMapping mapping, Dictionary<Type, FieldBuilder> mixins, MethodInfo? method)
    {
        if (method is not null)
        {
            var implementationMethod = mapping.GetImplementationMethod(method);
            ImplementationMethod = implementationMethod;

            IsImplementedPrivately = implementationMethod?.IsPrivate ?? false;

            if (implementationMethod?.DeclaringType is Type type && mixins.TryGetValue(type, out var mixinFieldBuilder))
            {
                kind = MethodImplementationKind.ImplementedByMixin;
                MixinFieldBuilder = mixinFieldBuilder;
            }
            else if (implementationMethod is null)
            {
                kind = MethodImplementationKind.NonImplemented;
                MixinFieldBuilder = null;
            }
            else
            {
                kind = MethodImplementationKind.ImplementedByInterface;
                MixinFieldBuilder = null;
            }
        }
        else
        {
            kind = MethodImplementationKind.NonExistent;
            IsImplementedPrivately = false;
            ImplementationMethod = null;
            MixinFieldBuilder = null;
        }
    }

    public override string ToString() => $"MethodImplementationInfo {ToStringInternal()}";

    String ToStringInternal()
    {
        if (!Exists)
        {
            return "nonexistent";
        }
        else if (ImplementationMethod is MethodInfo implementationMethod)
        {
            if (MixinFieldBuilder is FieldBuilder)
            {
                return $"implemented by mixin method {implementationMethod.GetQualifiedName()}";
            }
            else
            {
                return $"implemented by interface method {implementationMethod.GetQualifiedName()}";
            }
        }
        else
        {
            return "unimplemented";
        }
    }
}
