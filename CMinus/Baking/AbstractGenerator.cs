using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using CMinus.Injection;
using System.Collections.Generic;
using System.Resources;

namespace CMinus;

public abstract class AbstractGenerator
{
    public virtual IEnumerable<Type?> GetMixinTypes() => Enumerable.Empty<Type?>();

    public Type? GetMixinType()
    {
        return GetMixinTypes().OfType<Type>().Distinct().SingleOrDefault("Multiple mixins are not supported");
    }

    protected FieldBuilder? EnsureMixin(IBuildingContext state) => GetMixinType()?.Apply(t => state.EnsureMixin(t, false));

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

    protected MethodImplementation GetMethodImplementation(FieldBuilder fieldBuilder, String name, MethodImplementationInfo? wrappedMethod)
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
            if (haveBeforeOrAfter) throw new Exception($"Implementation type can't define both a {name} method and the respective Before and After methods");

            if (wrappedMethod?.IsImplememtedByInterface ?? false) throw new Exception($"Internal error: Can't wrap outer implementation with a direct method implementation");

            return method;
        }
        else if (haveBeforeOrAfter || TypeInterfaces.Get(type).DoesTypeImplement(typeof(IEmptyImplementation)))
        {
            if (haveAfterMethod != haveAfterOnErrorMethod) throw new Exception($"Implementation {type} must define either neither or both of the After methods");

            AssertReturnType(beforeMethod, typeof(Boolean));
            AssertReturnType(afterMethod, typeof(void));
            AssertReturnType(afterOnErrorMethod, typeof(Boolean));

            return new WrappingMethodImplementation(wrappedMethod, beforeMethod, afterMethod, afterOnErrorMethod);
        }
        else
        {
            throw new Exception($"Implementation {type} must define either {name}, one of the respective Before and After methods or implement {nameof(IEmptyImplementation)}");
        }
    }

    void AssertReturnType(MethodInfo? method, Type type)
    {
        if (method is null) return;

        if (method.ReturnType != type) throw new Exception($"Implementation {method.DeclaringType} must define the method {method} with a return type of {type}");
    }

    protected virtual void AddArgumentKinds(Dictionary<Type, ImplementationTypeArgumentKind> argumentKinds) { }

    protected Dictionary<Type, ImplementationTypeArgumentKind> GetArgumentKinds()
    {
        var argumentKinds = new Dictionary<Type, ImplementationTypeArgumentKind>();

        AddArgumentKinds(argumentKinds);

        return argumentKinds;
    }

    protected MethodInfo GetMethod(FieldBuilder fieldBuilder, String name)
        => fieldBuilder.FieldType.GetMethod(name)
        ?? throw new Exception($"Implementation type {fieldBuilder.FieldType} must have a '{name}' method");
}

