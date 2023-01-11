using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace CMinus;

/// <summary>
/// Marks this assembly as containing Moldinium model archetypes. This is required for the code
/// generator to search for <see cref="IModel"/> implementing archetypes to implement proper models from.
/// </summary>
/// <seealso cref="System.Attribute" />
/// <seealso cref="IModel" />
[AttributeUsage(AttributeTargets.Assembly)]
public class MoldiniumArchetypesAttribute : Attribute
{
}

class ModelFactoryInterceptor : IInterceptor
{
    public ModelFactoryInterceptor(ModelFactoryProxyGenerator generator)
    {
        this.generator = generator;
    }

    public void Intercept(IInvocation invocation)
    {
        if (!Models.ShouldIntercept)
        {
            using (Models.SetShouldIntercept(true))
            {
                invocation.Proceed();
            }
            return;
        }

        var method = invocation.Method;

        var parts = method.Name.Split('_');

        if (parts.Length != 2) throw new Exception($"Unexpected method encountered: {method.Name}");

        var type = invocation.Proxy.GetType().BaseType;

        var implementations = GetImplementations(invocation.Proxy, type!);

        switch (parts[0])
        {
            case "get":
                implementations[method].Get(invocation);
                break;
            case "set":
                implementations[method].Set(invocation);
                break;
            case "add":
                foreach (var implementation in implementations.Values)
                    implementation.PropertyChanged += (PropertyChangedEventHandler)invocation.Arguments[0];
                break;
            case "remove":
                foreach (var implementation in implementations.Values)
                    implementation.PropertyChanged -= (PropertyChangedEventHandler)invocation.Arguments[0];
                break;
            default:
                throw new NotImplementedException();
        }
    }

    abstract class PropertyImplementation
    {
        public PropertyImplementation(PropertyInfo property, Object target)
        {
            this.property = property;
            this.target = target;
            eventArgs = new PropertyChangedEventArgs(property.Name);
        }

        public abstract void Get(IInvocation invocation);
        public abstract void Set(IInvocation invocation);

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void Notify()
        {
            PropertyChanged?.Invoke(target, eventArgs);
        }

        protected readonly PropertyInfo property;
        protected readonly Object target;

        private readonly PropertyChangedEventArgs eventArgs;
    }

    class WatchableVariablePropertyImplementation : PropertyImplementation
    {
        IWatchableVariable variable;

        public WatchableVariablePropertyImplementation(PropertyInfo property, Object target)
            : base(property, target)
        {
            variable = Watchable.VarForType(property.PropertyType);

            (variable as WatchableValueBase)!.Name = $"{property.DeclaringType!.Name}.{property.Name}";

            variable.Subscribe(this, Notify);
        }

        public override void Get(IInvocation invocation)
        {
            invocation.ReturnValue = variable.UntypedValue;
        }

        public override void Set(IInvocation invocation)
        {
            variable.UntypedValue = invocation.Arguments[0];
        }
    }

    class WatchableImplementationPropertyImplementation : PropertyImplementation
    {
        IWatchable<Object> watchable;

        public WatchableImplementationPropertyImplementation(PropertyInfo property, Object target)
            : base(property, target)
        {
            watchable = Watchable.Eval(Invoke).watchable;

            (watchable as WatchableValueBase)!.Name = $"{property.DeclaringType!.Name}.{property.Name}";

            watchable.Subscribe(this, Notify);
        }

        public override void Get(IInvocation invocation)
        {
            invocation.ReturnValue = watchable.UntypedValue;
        }

        public override void Set(IInvocation invocation)
        {
            invocation.Proceed();
        }

        Object Invoke()
        {
            using (Models.SetShouldIntercept(false))
            {
                var result = property.GetGetMethod()!.Invoke(target, null);

                return result!;
            }
        }
    }


    static Dictionary<MethodInfo, PropertyImplementation> GetImplementations(Object target, Type type)
    {
        ObjectInfo info = GetInfo(target);

        if (info.PropertyImplementations == null)
        {
            info.PropertyImplementations = new Dictionary<MethodInfo, PropertyImplementation>();

            foreach (var property in type.GetProperties())
            {
                var implementation = MakeImplementation(property, target);

                foreach (var prefix in new[] { "get", "set" })
                {
                    var method = type.GetMethod($"{prefix}_{property.Name}");

                    if (method != null)
                        info.PropertyImplementations[method] = implementation;
                }
            }
        }

        return info.PropertyImplementations;
    }

    static PropertyImplementation MakeImplementation(PropertyInfo property, Object target)
    {
        if (property.GetMethod!.IsAbstract)
        {
            return new WatchableVariablePropertyImplementation(property, target);
        }
        else
        {
            return new WatchableImplementationPropertyImplementation(property, target);
        }
    }

    static ObjectInfo GetInfo(Object target)
    {
        ObjectInfo? info;

        if (!objectInfos.TryGetValue(target, out info))
        {
            info = objectInfos[target] = new ObjectInfo();
        }

        return info;
    }

    class ObjectInfo
    {
        public Dictionary<MethodInfo, PropertyImplementation>? PropertyImplementations;
    }

    static Dictionary<Object, ObjectInfo> objectInfos = new Dictionary<Object, ObjectInfo>();

    ModelFactoryProxyGenerator generator;
}

class ModelFactoryProxyGenerator : ProxyGenerator
{
    public Type GetProxyType(Type modelType)
    {
        return CreateClassProxyType(modelType, new[] { typeof(INotifyPropertyChanged) }, ProxyGenerationOptions.Default);
    }
}

/// <summary>
/// All models need to implement this empty interface.
/// This is for type safety and also serves as a type marker for the code generator.
/// </summary>
public interface IModel { }

/// <summary>
/// Creates models from abstract archetypes.
/// </summary>
public static class Models
{
    /// <summary>
    /// Creates a new instance of the given model type.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The newly created model.</returns>
    public static TModel Create<TModel>()
        where TModel : class, IModel
    {
        CheckType(typeof(TModel));

        var model = (TModel)generator.CreateClassProxy(typeof(TModel), new[] { typeof(INotifyPropertyChanged) }, interceptor);

        return model;
    }

    /// <summary>
    /// Creates a new instance of the given model type.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The newly created model.</returns>
    public static Object Create(Type type)
    {
        CheckType(type);

        var model = generator.CreateClassProxy(type, new[] { typeof(INotifyPropertyChanged) }, interceptor);

        return model;
    }

    /// <summary>
    /// Creates a new instance of the given model type and a customizer function. Example usage: <code>Create&lt;MyModel>(m => m.Parent = this)</code>.
    /// </summary>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>The newly created model.</returns>
    public static TModel Create<TModel>(Action<TModel> customize)
        where TModel : class, IModel
    {
        var model = Create<TModel>();

        customize?.Invoke(model);

        return model;
    }

    /// <summary>
    /// Gets the most derived type for the given archetype.
    /// </summary>
    /// <param name="archetype">The archetype.</param>
    /// <returns>The most derived type.</returns>
    public static Type GetMostDerivedType<TModel>()
        where TModel : class, IModel
    {
        CheckType(typeof(TModel));

        return generator.GetProxyType(typeof(TModel));
    }

    /// <summary>
    /// Gets the most derived type for the given archetype.
    /// </summary>
    /// <param name="archetype">The archetype.</param>
    /// <returns>The most derived type.</returns>
    public static Type GetMostDerivedType(Type archetype)
    {
        CheckType(archetype);

        return generator.GetProxyType(archetype);
    }

    public static Boolean IsLegacyConventionCheckingEnabled { get; set; } = false;

    static void CheckType(Type archetype)
    {
        if (!IsLegacyConventionCheckingEnabled) return;

        CheckAssembly(archetype);

        if (checkedTypes.Contains(archetype)) return;

        if (typeof(IModel).IsAssignableFrom(archetype))
        {
            checkedTypes.Add(archetype);

            return;
        }

        throw new ArgumentException($"The type {archetype} should implement the IModel interface if it is to be used as a Moldinium model archetype.");
    }

    static void CheckAssembly(Type archetype)
    {
        var assembly = archetype.Assembly;

        if (checkedAssemblies.Contains(assembly)) return;

        var attributes = assembly.GetCustomAttributes(typeof(MoldiniumArchetypesAttribute), false);

        if (attributes.Length == 0)
        {
            throw new ArgumentException($"The type {archetype}'s assembly should have a MoldiniumArchetype attribute if any of its types are to be used as Moldinium archetypes.");
        }
        else if (attributes.Length > 1)
        {
            throw new ArgumentException($"The type {archetype}'s assembly has multiple MoldiniumArchetype attributes.");
        }

        checkedAssemblies.Add(assembly);
    }

    public class PopInterceptionMode : IDisposable
    {
        public void Dispose()
        {
            Models.shouldInterceptStack.Pop();
        }
    }

    static PopInterceptionMode? popInterceptionMode;

    static Stack<Boolean> shouldInterceptStack = new Stack<Boolean>(new[] { true });

    static internal Boolean ShouldIntercept => shouldInterceptStack.Peek();

    static internal IDisposable? SetShouldIntercept(Boolean value)
    {
        shouldInterceptStack.Push(value);
        return popInterceptionMode;
    }

    static HashSet<Assembly> checkedAssemblies = new HashSet<Assembly>();

    static HashSet<Type> checkedTypes = new HashSet<Type>();

    static ModelFactoryInterceptor interceptor = new ModelFactoryInterceptor(generator!);

    static ModelFactoryProxyGenerator generator = new ModelFactoryProxyGenerator();
}
