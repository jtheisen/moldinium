using Castle.Core.Configuration;
using Castle.DynamicProxy.Generators;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.PortableExecutable;

namespace CMinus;

public delegate FieldBuilder MixinEnsurer(BakingState state, Type type, Boolean isPrivate);

public record BakingState(TypeBuilder TypeBuilder, MixinEnsurer EnsureMixin, ILGenerator ConstructorGenerator, IDefaultProvider DefaultProvider)
{
    public readonly Dictionary<Type, FieldBuilder> Mixins = new Dictionary<Type, FieldBuilder>();
}

public abstract class AbstractBakery
{
    Dictionary<Type, Type> bakedTypes = new Dictionary<Type, Type>();

    public T Create<T>()
    {
        var type = Resolve(typeof(T));

        if (Activator.CreateInstance(type) is T t)
        {
            return t;
        }
        else
        {
            throw new Exception("Unexpectedly got null or the wrong type from activator");
        }
    }

    public Type Resolve(Type interfaceOrBaseType)
    {
        if (!bakedTypes.TryGetValue(interfaceOrBaseType, out var bakedType))
        {
            bakedTypes[interfaceOrBaseType] = bakedType = Create(interfaceOrBaseType);
        }

        return bakedType;
    }

    public abstract Type Create(Type interfaceOrBaseType);
}

public class DoubleBakery : AbstractBakery
{
    AbstractlyBakery abstractlyBakery, concretelyBakery;

    public DoubleBakery(String name)
        : this(name, BakeryConfiguration.PocGenerationConfiguration) { }

    public DoubleBakery(String name, BakeryConfiguration configuration)
    {
        abstractlyBakery = new AbstractlyBakery($"{name} (abstractly)");
        concretelyBakery = new ConcretelyBakery($"{name} (concretely)", configuration);
    }

    public override Type Create(Type interfaceType)
    {
        if (!interfaceType.IsInterface) throw new Exception($"The DoubleBakery creates only types from interfaces");

        var baseType = abstractlyBakery.Create(interfaceType);

        var concreteType = concretelyBakery.Create(baseType);

        return concreteType;
    }
}

public class AbstractlyBakery : AbstractBakery
{
    protected readonly string name;
    protected readonly bool makeAbstract;
    protected readonly ModuleBuilder moduleBuilder;
    protected readonly TypeAttributes typeAttributes;

    public AbstractlyBakery(String name, TypeAttributes typeAttributes = TypeAttributes.Public | TypeAttributes.Abstract)
    {
        this.name = name;
        this.typeAttributes = typeAttributes;
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(name), AssemblyBuilderAccess.Run);
        moduleBuilder = assemblyBuilder.DefineDynamicModule(name);
    }

    protected virtual String GetTypeName(Type interfaceOrBaseType)
        => $"{GetTypePrefix()}{interfaceOrBaseType.Name}";

    String GetTypePrefix()
    {
        if (typeAttributes.HasFlag(TypeAttributes.Abstract))
        {
            return "A";
        }
        else if (typeAttributes.HasFlag(TypeAttributes.SequentialLayout))
        {
            return "S";
        }
        else
        {
            return "C";
        }
    }

    public override Type Create(Type interfaceOrBaseOrMixinType)
    {
        var name = GetTypeName(interfaceOrBaseOrMixinType);
        return moduleBuilder.GetType(name) ?? Create(name, interfaceOrBaseOrMixinType);
    }

    protected virtual Type Create(String name, Type interfaceType)
    {
        var typeBuilder = moduleBuilder.DefineType(name, typeAttributes);

        RedeclareInterface(typeBuilder, interfaceType);

        if (!typeAttributes.HasFlag(TypeAttributes.Abstract))
        {
            //foreach (var method in typeBuilder.metho)
            //{
            //    if (method.IsAbstract) throw new Exception();
            //}
        }

        return typeBuilder.CreateType() ?? throw new Exception("TypeBuilder gave no built type");
    }

    void RedeclareInterface(TypeBuilder typeBuilder, Type type)
    {
        typeBuilder.AddInterfaceImplementation(type);

        foreach (var property in type.GetProperties())
        {
            var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

            if (RedeclareMethodIfApplicable(typeBuilder, property.GetGetMethod(), out var getMethodBuilder))
                propertyBuilder.SetGetMethod(getMethodBuilder);
            if (RedeclareMethodIfApplicable(typeBuilder, property.GetSetMethod(), out var setMethodBuilder))
                propertyBuilder.SetSetMethod(setMethodBuilder);
        }

        foreach (var evt in type.GetEvents())
        {
            var eventBuilder = typeBuilder.DefineEvent(evt.Name, evt.Attributes, evt.EventHandlerType ?? throw new Exception($"No event handler type"));

            if (RedeclareMethodIfApplicable(typeBuilder, evt.GetAddMethod(), out var addMethodBuilder))
                eventBuilder.SetAddOnMethod(addMethodBuilder);
            if (RedeclareMethodIfApplicable(typeBuilder, evt.GetRemoveMethod(), out var removeMethodBuilder))
                eventBuilder.SetRemoveOnMethod(removeMethodBuilder);
        }

        foreach (var method in type.GetMethods())
        {
            if (method.IsSpecialName) continue;

            RedeclareMethodIfApplicable(typeBuilder, method, out _);
        }
    }

    Boolean RedeclareMethodIfApplicable(TypeBuilder typeBuilder, MethodInfo? method, [MaybeNullWhen(false)] out MethodBuilder methodBuilder)
    {
        methodBuilder = null;

        if (method is null || !method.IsAbstract) return false;

        var attributes = method.Attributes & (~MethodAttributes.NewSlot);
        
        attributes &= ~MethodAttributes.NewSlot;
        attributes |= ~MethodAttributes.Public;

        methodBuilder = typeBuilder.DefineMethod(method.Name, attributes, method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());

        return true;
    }
}

public class ConcretelyBakery : AbstractlyBakery
{
    readonly BakeryConfiguration configuration;
    readonly IBakeryComponentGenerators generators;
    readonly IDefaultProvider defaultProvider;

    BakingState? state;

    public String Name => name;

    public ConcretelyBakery(String name)
        : this(name, BakeryConfiguration.PocGenerationConfiguration) { }

    public ConcretelyBakery(String name, BakeryConfiguration configuration)
        : base(name, TypeAttributes.Public)
    {
        this.configuration = BakeryConfiguration.PocGenerationConfiguration;
        generators = this.configuration.Generators;
        defaultProvider = this.configuration.DefaultProvider;
    }

    protected override Type Create(String name, Type interfaceOrBaseOrMixinType)
    {
        if (state is not null) throw new Exception("Already building a type");

        var baseType = interfaceOrBaseOrMixinType.IsClass ? interfaceOrBaseOrMixinType : null;

        var typeBuilder = moduleBuilder.DefineType(name, typeAttributes, baseType);

        var constructorBuilder = typeBuilder.DefineConstructor(MethodAttributes.Public, CallingConventions.Standard, new Type[] { });

        var constructorGenerator = constructorBuilder.GetILGenerator();

        state = new BakingState(typeBuilder, EnsureDelegatingMixin, constructorGenerator, defaultProvider);

        try
        {
            if (interfaceOrBaseOrMixinType.IsValueType)
            {
                EnsureImplementedMixin(state, interfaceOrBaseOrMixinType, false);
            }
            else
            {
                ImplementBaseOrInterface(state, interfaceOrBaseOrMixinType, generators);
            }

            constructorGenerator.Emit(OpCodes.Ret);

            return typeBuilder.CreateType() ?? throw new Exception("no type?");
        }
        finally
        {
            state = null;
        }
    }

    void ImplementBaseOrInterface(BakingState state, Type type, IBakeryComponentGenerators generators)
    {
        var typeBuilder = state.TypeBuilder;

        if (type.IsInterface)
        {
            typeBuilder.AddInterfaceImplementation(type);
        }

        foreach (var property in type.GetProperties())
        {
            generators.GetPropertyGenerator(property).GenerateProperty(state, property);
        }

        foreach (var evt in type.GetEvents())
        {
            generators.GetEventGenerator(evt).GenerateEvent(state, evt);
        }

        foreach (var method in type.GetMethods())
        {
            if (!method.IsAbstract || method.IsSpecialName) continue;

            typeBuilder.DefineMethod(method.Name, method.Attributes | MethodAttributes.Public, method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());
        }
    }

    FieldBuilder EnsureImplementedMixin(BakingState state, Type type, Boolean isPrivate)
        => EnsureMixin(state, type, false, isPrivate);

    FieldBuilder EnsureDelegatingMixin(BakingState state, Type type, Boolean isPrivate)
        => EnsureMixin(state, type, true, isPrivate);

    FieldBuilder EnsureMixin(BakingState state, Type type, Boolean onlyDelegate, Boolean isPrivate)
    {
        var fieldBuilder = state.Mixins.GetValueOrDefault(type);

        if (fieldBuilder is null)
        {
            CreateMixin(state, type, onlyDelegate, isPrivate, out fieldBuilder);
        }

        return fieldBuilder;
    }

    void CreateMixin(BakingState state, Type type, Boolean onlyDelegate, Boolean isPrivate, out FieldBuilder fieldBuilder)
    {
        var typeBuilder = state.TypeBuilder;

        fieldBuilder = typeBuilder.DefineField($"mixin_{(type.FullName ?? "x").Replace('.', '_')}_{Guid.NewGuid()}", type, FieldAttributes.Private);

        state.Mixins[type] = fieldBuilder;

        var propertyImplementationType = typeof(IPropertyImplementation);

        var interfaces = (
            from i in type.GetInterfaces()
            where !propertyImplementationType.IsAssignableFrom(i)
            select i
        ).ToArray();

        if (interfaces.Length == 0) return;

        if (!isPrivate)
        {
            var nestedGenerators = onlyDelegate
                ? new ComponentGenerators(
                    new DelegatingPropertyGenerator(fieldBuilder),
                    new DelegatingEventGenerator(fieldBuilder)
                )
                : generators;

            foreach (var ifc in interfaces)
            {
                ImplementBaseOrInterface(state, ifc, nestedGenerators);
            }
        }
    }

}
