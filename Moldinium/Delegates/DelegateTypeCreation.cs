using System;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Collections.Concurrent;

namespace Moldinium.Delegates;

public class DelegateTypeCreator
{
    ModuleBuilder? moduleBuilder = null;
    TypeBuilder? containerTypeBuilder = null;
    
    ConcurrentDictionary<(MethodInfo baseMethod, MethodInfo? targetMethod), DelegateTypeInfo> delegateTypes
        = new ConcurrentDictionary<(MethodInfo baseMethod, MethodInfo? targetMethod), DelegateTypeInfo>();

    public struct DelegateTypeInfo
    {
        public Type type;
        public ConstructorInfo ctor;
        public MethodInfo? bind;
    }

    public DelegateTypeCreator(ModuleBuilder moduleBuilder)
    {
        this.moduleBuilder = moduleBuilder;
    }

    public DelegateTypeCreator(TypeBuilder containerTypeBuilder)
    {
        this.containerTypeBuilder = containerTypeBuilder;
    }

    public DelegateTypeCreator()
        : this("DelegateTypeCreation")
    {
    }

    public DelegateTypeCreator(String name)
        : this(AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run).DefineDynamicModule(name))
    {
    }

    public DelegateTypeInfo GetDelegateTypeInfo(MethodInfo baseMethod, MethodInfo? targetMethod = null) => delegateTypes.GetOrAdd((baseMethod, targetMethod), CreateDelegateType);

    public Delegate CreateDelegateForSpecificTarget(MethodInfo baseMethod, MethodInfo targetMethod, Object target)
    {
        var delegateTypeInfo = GetDelegateTypeInfo(baseMethod, targetMethod);

        return (Delegate)delegateTypeInfo.bind!.Invoke(null, new[] { target })!;
    }

    public Func<Object, Delegate> CreateDelegateCreatorAsDynamicMethod(MethodInfo targetMethod, Boolean useBind)
    {
        if (useBind)
        {
            return CreateDelegateCreatorAsDynamicMethodUsingCilCallingBind(targetMethod);
        }
        else
        {
            return CreateDelegateCreatorAsDynamicMethodByCilCreatingDelegate(targetMethod);
        }
    }

    public Func<Object, Delegate> CreateDelegateCreatorAsDynamicMethodUsingCilCallingBind(MethodInfo targetMethod)
    {
        var delegateTypeInfo = GetDelegateTypeInfo(targetMethod, targetMethod);

        var cheekyCallerMethod = new DynamicMethod(
            "CreateDelegateForSpecifcTargetMethod",
            typeof(Func<Object, Delegate>),
            new[] { typeof(Object) }
        );

        if (delegateTypeInfo.bind is null) throw new Exception();

        var il = cheekyCallerMethod.GetILGenerator();

        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, delegateTypeInfo.bind);
        il.Emit(OpCodes.Ret);

        return cheekyCallerMethod.CreateDelegate<Func<Object, Delegate>>();
    }

    public Func<Object, Delegate> CreateDelegateCreatorAsDynamicMethodByCilCreatingDelegate(MethodInfo targetMethod)
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
        TypeBuilder typeBuilder;

        if (moduleBuilder is not null)
        {
            typeBuilder = moduleBuilder.DefineType(
                baseMethod.Name,
                TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.AutoClass | TypeAttributes.Sealed,
                typeof(MulticastDelegate)
            );
        }
        else if (containerTypeBuilder is not null)
        {
            typeBuilder = containerTypeBuilder.DefineNestedType(
                baseMethod.Name,
                TypeAttributes.NestedPublic | TypeAttributes.Class | TypeAttributes.AnsiClass | TypeAttributes.AutoClass | TypeAttributes.Sealed,
                typeof(MulticastDelegate)
            );
        }
        else
        {
            throw new Exception("InternalError: No module or type builder");
        }

        var ctorBuilder = typeBuilder.DefineConstructor(
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
            CallingConventions.HasThis,
            new[] { typeof(Object), typeof(IntPtr) }
        );
        ctorBuilder.SetImplementationFlags(MethodImplAttributes.Runtime | MethodImplAttributes.Managed);

        var parameters = baseMethod.GetParameters();

        var parameterTypes = parameters.Select(p => p.ParameterType).ToArray();
        var parameterReqMods = parameters.Select(p => p.GetRequiredCustomModifiers()).ToArray();
        var parameterOptMods = parameters.Select(p => p.GetOptionalCustomModifiers()).ToArray();

        var invokeMethodBuilder = typeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
            baseMethod.CallingConvention, // this right?
            baseMethod.ReturnType,
            baseMethod.ReturnParameter.GetRequiredCustomModifiers(),
            baseMethod.ReturnParameter.GetOptionalCustomModifiers(),
            parameterTypes,
            parameterReqMods,
            parameterOptMods
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
