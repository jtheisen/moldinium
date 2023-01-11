using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMinus;

public static class Extensions
{
    public static T Return<T>(this Object _, T value) => value;
}
