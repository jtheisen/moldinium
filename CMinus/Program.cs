using Castle.DynamicProxy;
using CMinus.Construction;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace CMinus
{


    class Program
    {
        static Bakery classFactory = new Bakery("Funky", new BakeryConfiguration(new PropertyGenerator()));

        static void Main(string[] args)
        {
            //var container = new Container();

            //container.Register(typeof(DependencyMetadataHelper<>));

            //container.Register<IPreacher, Preacher>();

            ////container.Register

            ////var impl = new ImplementedManualPlayground(new Preacher());

            ////(impl as Playground).MethodWithImplementation();

            //var playground1 = classFactory.Create<Playground>();
            //playground1.Name = "Bar";
            //playground1.MethodWithImplementation();

            //IResolver resolver = new CheckingResolver(new ReflectingResolver(new ContainerResolver(container)));



            //var playground2 = resolver.Create<Playground>();

            //playground2.Name = "Foo";


            //playground2.MethodWithImplementation();


        }
    }

    //public record ResolverResult(Type type, Func<Object> get);

    //interface IResolver
    //{
    //    ResolverResult Resolve(Type type);

    //    T Create<T>() => (T)Resolve(typeof(T)).get();
    //}

    //class TrivialResolver : IResolver
    //{
    //    public ResolverResult Resolve(Type type)
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

    //class ContainerResolver : IResolver
    //{
    //    private readonly Container container;

    //    public ContainerResolver(Container container)
    //    {
    //        this.container = container;
    //    }

    //    public ResolverResult Resolve(Type type)
    //    {
    //        var dependencyMetdataType = typeof(DependencyMetadataHelper<>).MakeGenericType(type);

    //        var helper = (DependencyMetadataHelper)container.GetInstance(dependencyMetdataType);

    //        return helper.ResolverResult;
    //    }
    //}

    //public abstract class DependencyMetadataHelper
    //{
    //    public abstract ResolverResult ResolverResult { get; }
    //}

    //public class DependencyMetadataHelper<T> : DependencyMetadataHelper
    //    where T : class
    //{
    //    private readonly DependencyMetadata<T> metadata;

    //    public DependencyMetadataHelper(DependencyMetadata<T> metadata)
    //    {
    //        this.metadata = metadata;
    //    }

    //    public override ResolverResult ResolverResult => new ResolverResult(metadata.ImplementationType, metadata.Dependency.GetInstance);
    //}

    //class CheckingResolver : IResolver
    //{
    //    private readonly IResolver nested;

    //    public CheckingResolver(IResolver nested)
    //    {
    //        this.nested = nested;
    //    }

    //    public ResolverResult Resolve(Type type)
    //    {
    //        var result = nested.Resolve(type);

    //        return result with
    //        {
    //            get = (() =>
    //            {
    //                var instance = result.get();

    //                if (!instance.GetType().IsAssignableTo(result.type))
    //                    throw new Exception($"Created type {instance.GetType().Name} is not assignable to resolved type {result.type.Name}");

    //                return instance;
    //            })
    //        };
    //    }
    //}

    //class GetterInterceptor : IInterceptor
    //{
    //    private Object? cache;
    //    private readonly ResolverResult instance;

    //    public GetterInterceptor(ResolverResult instance)
    //    {
    //        this.instance = instance;
    //    }

    //    public void Intercept(IInvocation invocation)
    //    {
    //        invocation.ReturnValue = cache ??= instance.get();
    //    }
    //}

    //class GenerationOptionsForType : ProxyGenerationOptions, IInterceptor, IProxyGenerationHook
    //{
    //    private readonly Dictionary<MethodInfo, IInterceptor> interceptors = new Dictionary<MethodInfo, IInterceptor>();

    //    public GenerationOptionsForType(Type type, IResolver memberResolver)
    //    {
    //        Selector = new SingletonInterceptorSelector(this);
    //        Hook = this;

    //        foreach (var property in type.GetProperties())
    //        {
    //            var propertyType = property.PropertyType;

    //            if (property.SetMethod != null)
    //            {

    //            }
    //            else if (property.GetMethod != null)
    //            {


    //                var resolvedPropertyType = memberResolver.Resolve(propertyType);

    //                interceptors[property.GetMethod] = new GetterInterceptor(resolvedPropertyType);
    //            }
    //        }
    //    }

    //    public void Intercept(IInvocation invocation)
    //    {
    //        Console.WriteLine("Intercept");

    //        interceptors[invocation.Method].Intercept(invocation);
    //    }

    //    public void MethodsInspected() { }

    //    public void NonProxyableMemberNotification(Type type, MemberInfo memberInfo) => throw new Exception("NonProxyableMember");

    //    public bool ShouldInterceptMethod(Type type, MethodInfo methodInfo) => interceptors.ContainsKey(methodInfo);
    //}

    //class SingletonInterceptorSelector : IInterceptorSelector
    //{
    //    private readonly IInterceptor[] interceptors;

    //    public SingletonInterceptorSelector(IInterceptor interceptor)
    //    {
    //        this.interceptors = new[] { interceptor };
    //    }

    //    public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] otherInterceptors)
    //    {
    //        return interceptors;
    //    }
    //}

    //class ReflectingResolver : IResolver
    //{
    //    private readonly IResolver resolver;

    //    Bakery abstractClassFactory = new Bakery("DynamicTypes");

    //    ProxyGenerator generator = new ProxyGenerator();

    //    public ReflectingResolver(IResolver resolver)
    //    {
    //        this.resolver = resolver;
    //    }

    //    public ResolverResult Resolve(Type type)
    //    {
    //        var abstractClassType = abstractClassFactory.Resolve(type);

    //        var options = new GenerationOptionsForType(abstractClassType, resolver);

    //        var implementationType = generator.ProxyBuilder.CreateClassProxyType(abstractClassType, new Type[] { }, options);

    //        return new ResolverResult(implementationType, () => generator.CreateClassProxy(abstractClassType, (ProxyGenerationOptions)options, (IInterceptor)options));
    //    }
    //}


}
