using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMinus.Construction
{
    public interface IComplexPropertyImplementation
            <ValueInterface, ContainerInterface, Value, Container, MixIn, Accessor>
            where Value : ValueInterface
            where Container : ContainerInterface
            where MixIn : struct
            where Accessor : IMysteriousAccessor<Value, Container>
    {
        Value Get(
            Container self,
            ref MixIn mixIn
            );

        void Set(
            Container self,
            ref MixIn mixIn,
            Value value
            );
    }

    public interface IMysteriousAccessor<Value, Container>
    {
        String GetPropertyName();
        Int32 GetIndex();
        Boolean IsVariable();

        Value Get(Container container);
        void Set(Container container, Value value);
    }

    public struct EmptyMixIn { }

    public struct GenericComplexPropertyImplementation<Value, Container> : IComplexPropertyImplementation<Value, Container, Value, Container, EmptyMixIn, IMysteriousAccessor<Value, Container>>
    {
        Value value;

        public Value Get(Container self, ref EmptyMixIn mixIn) => value;

        public void Set(Container self, ref EmptyMixIn mixIn, Value value) => this.value = value;
    }

    public interface IPropertyImplementation<T>
    {
        T Value { get; set; }
    }

    public struct GenericPropertyImplementation<T>
    {
        public T Value { get; set; }
    }



    public interface IReadonlyProperty<T>
    {
        T Value { get; }
    }

    public interface IWrappedReadonlyProperty<T, B> : IReadonlyProperty<T>
        where B : struct
    {
    }

    public interface ICachingReadonlyProperty<T, B> : IWrappedReadonlyProperty<T, B>
        where B : struct
    {
        void Invalidate();
    }

    public struct CachedReadonlyProperty<T, B> : ICachingReadonlyProperty<T, B>
        where B : struct, IReadonlyProperty<T>
    {
        T cached;
        Boolean isCached;
        B nested;

        public void Invalidate() => isCached = false;

        public T Value
        {
            get
            {
                if (!isCached)
                {
                    cached = nested.Value;

                    isCached = true;
                }

                return cached;
            }
        }
    }
}
