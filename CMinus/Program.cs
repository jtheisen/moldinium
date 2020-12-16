using Castle.DynamicProxy;
using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace CMinus
{


    class Program
    {
        static void Main(string[] args)
        {
            var container = new Container();

            container.Register(typeof(DependencyMetadataHelper<>));

            container.Register<IPreacher, Preacher>();

            //var impl = new ImplementedManualPlayground(new Preacher());

            //(impl as Playground).MethodWithImplementation();


            IResolver resolver = new CheckingResolver(new ReflectingResolver(new ContainerResolver(container)));



            var result = resolver.Create<Playground>();

            result.MethodWithImplementation();


        }
    }

    public record ResolverResult(Type type, Func<Object> get);

    interface IResolver
    {
        ResolverResult Resolve(Type type);

        T Create<T>() => (T)Resolve(typeof(T)).get();
    }

    class TrivialResolver : IResolver
    {
        public ResolverResult Resolve(Type type)
        {
            throw new NotImplementedException();
        }
    }

    class ContainerResolver : IResolver
    {
        private readonly Container container;

        public ContainerResolver(Container container)
        {
            this.container = container;
        }

        public ResolverResult Resolve(Type type)
        {
            var dependencyMetdataType = typeof(DependencyMetadataHelper<>).MakeGenericType(type);

            var helper = (DependencyMetadataHelper)container.GetInstance(dependencyMetdataType);

            return helper.ResolverResult;
        }
    }

    public abstract class DependencyMetadataHelper
    {
        public abstract ResolverResult ResolverResult { get; }
    }

    public class DependencyMetadataHelper<T> : DependencyMetadataHelper
        where T : class
    {
        private readonly DependencyMetadata<T> metadata;

        public DependencyMetadataHelper(DependencyMetadata<T> metadata)
        {
            this.metadata = metadata;
        }

        public override ResolverResult ResolverResult => new ResolverResult(metadata.ImplementationType, metadata.Dependency.GetInstance);
    }

    class AbstractClassForInterfacesFactory
    {
        ModuleBuilder moduleBuilder;

        public AbstractClassForInterfacesFactory()
        {
            var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("MyCoolAssembly"), AssemblyBuilderAccess.Run);
            moduleBuilder = assemblyBuilder.DefineDynamicModule("MyAwesomeModule");
        }

        public Type Resolve(Type interfaceType)
        {
            var name = "C" + interfaceType.Name;
            return moduleBuilder.GetType(name) ?? Create(name, interfaceType);
        }

        Type Create(String name, Type interfaceType)
        {
            var typeBuilder = moduleBuilder.DefineType(name, TypeAttributes.Public | TypeAttributes.Abstract);
            typeBuilder.AddInterfaceImplementation(interfaceType);

            foreach (var property in interfaceType.GetProperties())
            {
                var propertyBuilder = typeBuilder.DefineProperty(property.Name, property.Attributes, property.PropertyType, null);

                var getMethod = property.GetGetMethod();

                if (getMethod != null)
                {
                    propertyBuilder.SetGetMethod(Create(typeBuilder, getMethod));
                }
            }

            foreach (var method in interfaceType.GetMethods())
            {
                if (!method.IsAbstract || method.IsSpecialName) continue;

                typeBuilder.DefineMethod(method.Name, method.Attributes | MethodAttributes.Public, method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());
            }

            return typeBuilder.CreateType() ?? throw new Exception("no type?");
        }

        MethodBuilder Create(TypeBuilder typeBuilder, MethodInfo method)
        {
            return typeBuilder.DefineMethod(method.Name, method.Attributes | MethodAttributes.Public, method.ReturnType, method.GetParameters().Select(p => p.ParameterType).ToArray());
        }
    }

    class CheckingResolver : IResolver
    {
        private readonly IResolver nested;

        public CheckingResolver(IResolver nested)
        {
            this.nested = nested;
        }

        public ResolverResult Resolve(Type type)
        {
            var result = nested.Resolve(type);

            return result with
            {
                get = (() =>
                {
                    var instance = result.get();

                    if (!instance.GetType().IsAssignableTo(result.type))
                        throw new Exception($"Created type {instance.GetType().Name} is not assignable to resolved type {result.type.Name}");

                    return instance;
                })
            };
        }
    }

    class GetterInterceptor : IInterceptor
    {
        private Object? cache;
        private readonly ResolverResult instance;

        public GetterInterceptor(ResolverResult instance)
        {
            this.instance = instance;
        }

        public void Intercept(IInvocation invocation)
        {
            invocation.ReturnValue = cache ??= instance.get();
        }
    }

    class GenerationOptionsForType : ProxyGenerationOptions, IInterceptor, IProxyGenerationHook
    {
        private readonly Dictionary<MethodInfo, IInterceptor> interceptors = new Dictionary<MethodInfo, IInterceptor>();

        public GenerationOptionsForType(Type type, IResolver memberResolver)
        {
            Selector = new SingletonInterceptorSelector(this);
            Hook = this;

            foreach (var property in type.GetProperties())
            {
                var propertyType = property.PropertyType;

                if (property.SetMethod != null)
                {

                }
                else if (property.GetMethod != null)
                {


                    var resolvedPropertyType = memberResolver.Resolve(propertyType);

                    interceptors[property.GetMethod] = new GetterInterceptor(resolvedPropertyType);
                }
            }
        }

        public void Intercept(IInvocation invocation)
        {
            Console.WriteLine("Intercept");

            interceptors[invocation.Method].Intercept(invocation);
        }

        public void MethodsInspected() { }

        public void NonProxyableMemberNotification(Type type, MemberInfo memberInfo) => throw new Exception("NonProxyableMember");

        public bool ShouldInterceptMethod(Type type, MethodInfo methodInfo) => interceptors.ContainsKey(methodInfo);
    }

    class SingletonInterceptorSelector : IInterceptorSelector
    {
        private readonly IInterceptor[] interceptors;

        public SingletonInterceptorSelector(IInterceptor interceptor)
        {
            this.interceptors = new[] { interceptor };
        }

        public IInterceptor[] SelectInterceptors(Type type, MethodInfo method, IInterceptor[] otherInterceptors)
        {
            return interceptors;
        }
    }

    class ReflectingResolver : IResolver
    {
        private readonly IResolver resolver;

        AbstractClassForInterfacesFactory abstractClassFactory = new AbstractClassForInterfacesFactory();

        ProxyGenerator generator = new ProxyGenerator();

        public ReflectingResolver(IResolver resolver)
        {
            this.resolver = resolver;
        }

        public ResolverResult Resolve(Type type)
        {
            var abstractClassType = abstractClassFactory.Resolve(type);

            var options = new GenerationOptionsForType(abstractClassType, resolver);

            var implementationType = generator.ProxyBuilder.CreateClassProxyType(abstractClassType, new Type[] { }, options);

            return new ResolverResult(implementationType, () => generator.CreateClassProxy(abstractClassType, (ProxyGenerationOptions)options, (IInterceptor)options));
        }
    }


}
