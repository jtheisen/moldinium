using System;
using System.Linq;
using System.Reflection.Emit;

namespace CMinus;

public delegate Object DelegateImplementation(params Object[] args);

public static class DelegateCreation
{
    public static D CreateDelegate<D>(DelegateImplementation implementation)
        where D : Delegate
        => (D)CreateDelegate(typeof(D), implementation);

    public static Object CreateDelegate(Type delegateType, DelegateImplementation implementation)
    {
        var method = delegateType.GetMethod("Invoke");

        if (method is null) throw new Exception("Could not get method for delegate");

        var parameters = method.GetParameters();

        var returnType = method.ReturnParameter.ParameterType;
        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

        if (returnType.IsValueType) throw new Exception("The delegate return type is void or a value type which is unsupported");

        foreach (var p in parameters)
        {
            if (p.ParameterType.IsValueType) throw new ArgumentException($"The delegate parameter {p.Name} is a value type which is unsupported");
        }

        (Boolean hasTarget, DynamicMethod proxy) CreateMethod()
        {
            if (implementation.Target is Object target)
            {
                return (true, new DynamicMethod(
                    "ProxyDelegate",
                    returnType,
                    new[] { target.GetType() }.Concat(parameterTypes).ToArray(),
                    target.GetType(),
                    true));
            }
            else
            {
                return (false, new DynamicMethod(
                    "ProxyDelegate",
                    returnType,
                    parameterTypes));
            }
        }

        var (hasTarget, proxy) = CreateMethod();

        var il = proxy.GetILGenerator();

        var i = 0;

        if (hasTarget)
        {
            il.Emit(OpCodes.Ldarg, i++);
        }

        il.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
        il.Emit(OpCodes.Newarr, typeof(object));
        foreach (var _ in parameterTypes)
        {
            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldc_I4, i++);
            il.Emit(OpCodes.Ldarg, i);
            il.Emit(OpCodes.Stelem, typeof(object));
        }
        il.Emit(OpCodes.Call, implementation.Method);

        il.Emit(OpCodes.Ret);

        return proxy.CreateDelegate(delegateType, implementation.Target);
    }
}
