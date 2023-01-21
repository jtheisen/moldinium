using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace CMinus.Delegates.TestStuff;

public class BoxedString
{
    public String? Value { get; set; }
}

public interface IBase
{
    private String OwnName => nameof(IBase);

    String FooProp => OwnName;

    String FooS() => OwnName;

    void FooVB(BoxedString boxedString) => boxedString.Value = OwnName;

    String FooSS(String s) => $"{OwnName} ({s})";
}

public interface IDerived : IBase
{
    private String OwnName => nameof(IDerived);

    String IBase.FooProp => OwnName;

    String IBase.FooS() => OwnName;

    void IBase.FooVB(BoxedString boxedString) => boxedString.Value = OwnName;

    String IBase.FooSS(String s) => $"{OwnName} ({s})";
}

public class CInnocent : IDerived { }

public class CWithImpls : IDerived
{
    private String OwnName => nameof(CWithImpls);

    private String GetSecret() => "secret";

    String IBase.FooProp => OwnName;

    String IBase.FooS() => OwnName;

    void IBase.FooVB(BoxedString boxedString) => boxedString.Value = OwnName;

    String IBase.FooSS(String s) => $"{OwnName} ({s})";
}

public static class Cheeky
{
    public static D CreateCalliCaller<D, T>(Type type, String methodName)
        where D : Delegate
    {
        return CreateCalliCaller<D, T>(type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == methodName || m.Name.EndsWith($".{methodName}")).Single());
    }

    // Broken attempt to call the method directly; it worked under specific circumstances
    public static D CreateCalliCaller<D, T>(MethodInfo implementation)
        where D : Delegate
    {
        var cheekyCallerMethod = new DynamicMethod(
            "ProxyDelegate",
            implementation.ReturnType,
            new Type[] { typeof(T) }.Concat(implementation.GetParameters().Select(p => p.ParameterType)).ToArray()
        );

        var il = cheekyCallerMethod.GetILGenerator();

        // something must be still wrong here, there's no this pointer
        il.Emit(OpCodes.Ldftn, implementation);
        il.EmitCalli(OpCodes.Calli, CallingConventions.Any, implementation.ReturnType, implementation.GetParameters().Select(p => p.ParameterType).ToArray(), null);
        il.Emit(OpCodes.Ret);

        return (D)cheekyCallerMethod.CreateDelegate(typeof(D));
    }

    public static MethodInfo GetSingleMethod(this Type type, String methodName)
    {
        return type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == methodName || m.Name.EndsWith($".{methodName}")).Single();
    }
}
