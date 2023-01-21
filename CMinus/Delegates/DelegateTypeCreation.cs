using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Concurrent;

namespace CMinus.Delegates;

public class DelegateTypeCreator
{
    ModuleBuilder moduleBuilder;

    public DelegateTypeCreator()
    {
        var name = "Delegates";
        delegateTypes = new ConcurrentDictionary<MethodInfo, Type>();
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
    }

    ConcurrentDictionary<MethodInfo, Type> delegateTypes;

    public Type GetDelegateType(MethodInfo method) => delegateTypes.GetOrAdd(method, CreateDelegateType);

    public Func<Object, Delegate> CreateDelegateCreatorForSpecificTargetMethod(MethodInfo targetMethod)
    {
        var delegateType = GetDelegateType(targetMethod);

        var delegateConstructor = delegateType.GetConstructors().Single();

        var cheekyCallerMethod = new DynamicMethod(
            "CreateDelegateForSpecifcTargetMethod",
            typeof(Func<Object, Delegate>),
            new[] { typeof(Object) }
        );

        var il = cheekyCallerMethod.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldftn, targetMethod);
        il.Emit(OpCodes.Newobj, delegateConstructor);
        il.Emit(OpCodes.Ret);

        return cheekyCallerMethod.CreateDelegate<Func<Object, Delegate>>();
    }

    Type CreateDelegateType(MethodInfo method)
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
        var invokeMethodBuilder = typeBuilder.DefineMethod(
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
        invokeMethodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        var delegateType = typeBuilder.CreateType() ?? throw new Exception($"TypeBuilder didn't build a type");

        return delegateType;
    }
}
