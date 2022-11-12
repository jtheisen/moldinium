using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CMinus
{
    public interface Constructor<T>
    {
        T Construct();
        T Construct(Expression<Predicate<T>> expression);
    }

    public interface Requires<T>
    {
        T Resolve<S>() where S : T;
    }

    public interface RequiresConstructor<T> : Requires<Constructor<T>>
    {
        T Construct<S>() where S : T
            => Resolve<Constructor<T>>().Construct();
    }


    public interface Requires<T, P>
    {
        T Resolve<S>(P arg) where S : T;
    }

    public interface Implementation<T>
    {
    }

    // provider don't resolve types
    public interface Provider<T>
    {
        T Get();
    }

    public record DependencyRecord(Type interfaceType, Type implementationType, Func<Object> constructor);

    public interface Resolver
    {
        IEnumerable<DependencyRecord> GetRecords();
    }



    //public interface AssemblyTypeResolver<TypeInAssembly> : Resolver
    //{
    //    IEnumerable<DependencyRecord> GetRecords()
    //    {
    //        return typeof(TypeInAssembly).Assembly
    //    }
    //}


    public interface DefaultConstructingResolver<T> : Resolver
        where T : class, new()
    {
        new IEnumerable<DependencyRecord> GetRecords()
        {
            yield return new DependencyRecord(typeof(T), typeof(T), () => new T());
        }
    }


    //public interface IPropertyImplementation<Value, Container, MixIn>
    //{
    //    Value Get(Container self, MixIn mixIn);

    //    void Set(Container self, MixIn mixIn, Value value);
    //}

    //public interface ReadonlyProperty<T>
    //{
    //    T Value { get; }
    //}

    public interface Property<T>
    {
        T Value { get; set; }
    }
}
