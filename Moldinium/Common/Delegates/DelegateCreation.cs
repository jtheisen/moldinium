using System.Reflection.Emit;

namespace Moldinium.Delegates;

public delegate object DelegateImplementation(params object[] args);

public static class DelegateCreation
{
    public static D CreateDelegate<D>(DelegateImplementation implementation)
        where D : Delegate
        => (D)CreateDelegate(typeof(D), implementation);

    public static object CreateDelegate(Type delegateType, DelegateImplementation implementation)
    {
        var method = delegateType.GetMethod("Invoke");

        if (method is null) throw new Exception("Could not get method for delegate");

        var parameters = method.GetParameters();

        var returnType = method.ReturnParameter.ParameterType;
        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();

        if (returnType.IsValueType) throw new Exception("The delegate return type is void or a value type which is unsupported");

        foreach (var p in parameters)
        {
            var type = p.ParameterType;

            if (type.IsByRef || type.IsByRefLike) throw new ArgumentException($"The delegate parameter {p.Name} is passed by ref which is unsupported");
        }

        (bool hasTarget, DynamicMethod proxy) CreateMethod()
        {
            if (implementation.Target is object target)
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

        var argOffset = 0;

        if (hasTarget)
        {
            il.Emit(OpCodes.Ldarg, 0);
            ++argOffset;
        }

        il.Emit(OpCodes.Ldc_I4, parameterTypes.Length);
        il.Emit(OpCodes.Newarr, typeof(object));
        for (int i = 0; i < parameterTypes.Length; i++)
        {
            var type = parameterTypes[i];

            il.Emit(OpCodes.Dup);
            il.Emit(OpCodes.Ldc_I4, i);
            il.Emit(OpCodes.Ldarg, i + argOffset);
            if (type.IsValueType)
            {
                il.Emit(OpCodes.Box, type);
            }
            il.Emit(OpCodes.Stelem, typeof(object));
        }
        il.Emit(OpCodes.Call, implementation.Method);

        il.Emit(OpCodes.Ret);

        if (hasTarget)
        {
            return proxy.CreateDelegate(delegateType, implementation.Target);
        }
        else
        {
            return proxy.CreateDelegate(delegateType);
        }
    }
}
