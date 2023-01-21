using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Concurrent;

namespace CMinus.Delegates;

public class DelegateTypeCreator
{
    ModuleBuilder moduleBuilder;

    public struct DelegateTypeInfo
    {
        public Type type;
        public ConstructorInfo ctor;
        public MethodInfo? bind;
    }

    public DelegateTypeCreator()
    {
        var name = "Delegates";
        delegateTypes = new ConcurrentDictionary<(MethodInfo, MethodInfo?), DelegateTypeInfo>();
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
    }

    ConcurrentDictionary<(MethodInfo baseMethod, MethodInfo? targetMethod), DelegateTypeInfo> delegateTypes;

    public DelegateTypeInfo GetDelegateTypeInfo(MethodInfo baseMethod, MethodInfo? targetMethod = null) => delegateTypes.GetOrAdd((baseMethod, targetMethod), CreateDelegateType);

    public Delegate CreateDelegateForSpecificTarget(MethodInfo baseMethod, MethodInfo targetMethod, Object target)
    {
        var delegateTypeInfo = GetDelegateTypeInfo(baseMethod, targetMethod);

        return (Delegate)delegateTypeInfo.bind!.Invoke(null, new[] { target })!;
    }

    public Func<Object, Delegate> CreateDelegateCreatorAsDynamicMethod(MethodInfo targetMethod)
    {
        var delegateTypeInfo = GetDelegateTypeInfo(targetMethod);

        var cheekyCallerMethod = new DynamicMethod(
            "CreateDelegateForSpecifcTargetMethod",
            typeof(Func<Object, Delegate>),
            new[] { typeof(Object) }
        );

        var il = cheekyCallerMethod.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldftn, targetMethod);
        il.Emit(OpCodes.Newobj, delegateTypeInfo.ctor);
        il.Emit(OpCodes.Ret);

        return cheekyCallerMethod.CreateDelegate<Func<Object, Delegate>>();
    }

    DelegateTypeInfo CreateDelegateType((MethodInfo baseMethod, MethodInfo? targetMethod) parameters) => CreateDelegateType(parameters.baseMethod, parameters.targetMethod);

    DelegateTypeInfo CreateDelegateType(MethodInfo baseMethod, MethodInfo? targetMethod)
    {
        var typeBuilder = moduleBuilder.DefineType(
            baseMethod.Name,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.AutoClass | TypeAttributes.Sealed,
            typeof(MulticastDelegate)
        );

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.HasThis,
            new[] { typeof(Object), typeof(IntPtr) }
        );
        ctorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        var parameters = baseMethod.GetParameters();
        var invokeMethodBuilder = typeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            baseMethod.CallingConvention, // this right?
            baseMethod.ReturnType,
            baseMethod.ReturnParameter.GetRequiredCustomModifiers(),
            baseMethod.ReturnParameter.GetOptionalCustomModifiers(),
            parameters.Select(p => p.ParameterType).ToArray(),
            parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray(),
            parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray()
        );
        invokeMethodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        if (targetMethod is not null)
        {
            var bindingMethod = typeBuilder.DefineMethod(
                "Bind",
                MethodAttributes.Public | MethodAttributes.Static,
                typeof(Func<Object, Delegate>),
                new[] { typeof(Object) }
            );

            var il = bindingMethod.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Ldftn, targetMethod);
            il.Emit(OpCodes.Newobj, ctorBuilder);
            il.Emit(OpCodes.Ret);
        }

        var delegateType = typeBuilder.CreateType() ?? throw new Exception($"TypeBuilder didn't build a type");

        var delegateConstructor = delegateType.GetConstructors().Single();

        var bindMethod = delegateType.GetMethod("Bind");

        return new DelegateTypeInfo { type = delegateType, ctor = delegateConstructor, bind = bindMethod };
    }
}
