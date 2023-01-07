using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CMinus
{
    public interface Provider<T>
    {
        T Provide();
    }

    public interface Property<T>
    {
        T Value { get; set; }
    }
}
