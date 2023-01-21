using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;

namespace CMinus.Baking.CheekyCalling;

//void Main()
//{
//Func<String> a = (new CInnocent() as IBase).FooS;

//// this is required for the below to work - I guess the delegate internally assures
//// that the private implementation is loaded
//a();

//Cheeky.CreateCalliCaller<Func<IBase, String>>(typeof(IDerived), "FooS")(new CWithImpls()).Dump();

//Cheeky.DynamicInvoke<CWithImpls, IDerived>("FooS").Dump();

//var bs = new BoxedString();
//Cheeky.DynamicInvoke<CWithImpls, IDerived>("FooVB", bs);
//bs.Dump();
//}

public class BoxedString
{
    public String? Value { get; set; }
}

public interface IBase
{
    private String OwnName => nameof(IBase);

    String FooS() => OwnName;

    void FooVB(BoxedString boxedString) => boxedString.Value = OwnName;

    String FooSS(String s) => $"{OwnName} ({s})";
}

public interface IDerived : IBase
{
    private String OwnName => nameof(IDerived);

    String IBase.FooS() => OwnName;

    void IBase.FooVB(BoxedString boxedString) => boxedString.Value = OwnName;

    String IBase.FooSS(String s) => $"{OwnName} ({s})";
}

public class CInnocent : IDerived { }

public class CWithImpls : IDerived
{
    private String OwnName => nameof(CWithImpls);

    private String GetSecret() => "secret";

    String IBase.FooS() => OwnName;

    void IBase.FooVB(BoxedString boxedString) => boxedString.Value = OwnName;

    String IBase.FooSS(String s) => $"{OwnName} ({s})";
}

public static class Cheeky
{
    public static Object DynamicInvoke<TConcrete, TTarget>(String methodName, params Object[] arguments)
        where TConcrete : TTarget, new()
    {
        var delegateCreator = typeof(TTarget).GetSingleMethod(methodName).CreateDelegateCreator();
        var cheekyDelegate = delegateCreator(new TConcrete());
        return cheekyDelegate.DynamicInvoke(arguments)!;
    }

    public static D CreateCalliCaller<D, T>(Type type, String methodName)
        where D : Delegate
    {
        return CreateCalliCaller<D, T>(type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).Where(m => m.Name == methodName || m.Name.EndsWith($".{methodName}")).Single());
    }

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

    public static Func<Object, Delegate> CreateDelegateCreator(this MethodInfo target)
    {
        var creator = new DelegateTypeCreator();

        var delegateType = creator.CreateDelegate(target);

        var delegateConstructor = delegateType.GetConstructors().Single();

        var cheekyCallerMethod = new DynamicMethod(
            "DelegateCreator",
            typeof(Func<Object, Delegate>),
            new[] { typeof(Object) }
        );

        var il = cheekyCallerMethod.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldftn, target);
        il.Emit(OpCodes.Newobj, delegateConstructor);
        il.Emit(OpCodes.Ret);

        return cheekyCallerMethod.CreateDelegate<Func<Object, Delegate>>();
    }
}

public class DelegateTypeCreator
{
    ModuleBuilder moduleBuilder;

    public DelegateTypeCreator()
    {
        var name = "Delegates";
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
    }

    public Type CreateDelegate(MethodInfo method)
    {
        var typeBuilder = moduleBuilder.DefineType(
            method.Name,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.AutoClass | TypeAttributes.Sealed,
            typeof(MulticastDelegate)
        );

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.HasThis,
            new[] { typeof(Object), typeof(IntPtr) }
        );
        ctorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        var parameters = method.GetParameters();
        var methodBuilder = typeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            method.CallingConvention, // this right?
            method.ReturnType,
            method.ReturnParameter.GetRequiredCustomModifiers(),
            method.ReturnParameter.GetOptionalCustomModifiers(),
            parameters.Select(p => p.ParameterType).ToArray(),
            parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray(),
            parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray()
        );
        methodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        var delegateType = typeBuilder.CreateType() ?? throw new Exception($"TypeBuilder didn't build a type");

        return delegateType;
    }
}
