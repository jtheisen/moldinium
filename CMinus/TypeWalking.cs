using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CMinus;

public struct ReflectedTypeDependency
{
    public PropertyInfo Property { get; init; }

    public Type Type => Property.PropertyType;

    public String Name => Property.Name;
}

public class ReflectedType
{
    public Type Type { get; init; }

    public ReflectedTypeDependency[] Dependencies { get; }

    public ReflectedType(Type type)
    {
        Type = type;

        var props =
            from p in type.GetProperties()
            let set = p.SetMethod
            let rpcm = set?.ReturnParameter.GetRequiredCustomModifiers()
            select (prop: p, init: rpcm?.Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)) ?? false);

        Dependencies = (
            from p in props
            where p.init
            select new ReflectedTypeDependency { Property = p.prop }
        ).ToArray();
    }

    public static ConcurrentDictionary<Type, ReflectedType> reflected = new ConcurrentDictionary<Type, ReflectedType>();

    public static ReflectedType Get(Type type) => reflected.GetOrAdd(type, t => new ReflectedType(t));
}

public abstract class ResolvedTypeNode
{
    public Type Type { get; init; } = null!;
}

public class ExternalTypeNode : ResolvedTypeNode
{
    public Func<Object> Get { get; } = null!;
}

public class ReflectedTypeNode : ResolvedTypeNode
{
    public ReflectedType ReflectedType { get; init; } = null!;

    public ResolvedTypeNode[] Children { get; set; } = null!;
}

public class DependencyTree
{
    ResolvedTypeNode rootNode;

    // Maps the interface to a dep node of the implementation
    Dictionary<Type, ResolvedTypeNode> resolved;

    Func<Type, Type> getImplementationType;

    public ResolvedTypeNode RootNode => rootNode;

    public IEnumerable<ResolvedTypeNode> Nodes => resolved.Values;

    public DependencyTree(Type rootType, Func<Type, Type> getImplementationType)
    {
        resolved = new Dictionary<Type, ResolvedTypeNode>();

        rootNode = Resolve(rootType);

        this.getImplementationType = getImplementationType;
    }

    ResolvedTypeNode Resolve(Type type)
    {
        if (!resolved.TryGetValue(type, out var node))
        {
            var implementation = getImplementationType(type);

            var reflected = ReflectedType.Get(implementation);

            var reflectedNode = new ReflectedTypeNode
            {
                Type = type,
                ReflectedType = reflected
            };

            node = reflectedNode;

            resolved.Add(type, node);

            reflectedNode.Children = reflected.Dependencies.Select(d => Resolve(d.Type)).ToArray();
        }

        return node;
    }

    public Object Create(Type type)
    {
        var activator = new TreeActivator(this);

        return activator.Create(rootNode.Type);
    }

    public class TreeActivator
    {
        DependencyTree tree;

        Dictionary<Type, Object> instances;

        public TreeActivator(DependencyTree tree)
        {
            this.tree = tree;

            instances = new Dictionary<Type, Object>();
        }

        internal Object Create(Type type)
        {
            var node = tree.resolved[type];

            if (node is ExternalTypeNode en)
            {
                return en.Get();
            }
            else if (node is ReflectedTypeNode rn)
            {
                if (!instances.TryGetValue(rn.ReflectedType.Type, out var instance))
                {
                    instance = Activator.CreateInstance(rn.ReflectedType.Type)!;

                    instances[rn.Type] = instance;

                    foreach (var dependency in rn.ReflectedType.Dependencies)
                    {
                        dependency.Property.SetValue(instance, Create(dependency.Type));
                    }
                }

                return instance;
            }
            else
            {
                throw new Exception($"Unknown node type {node.GetType().Name}");
            }
        }
    }
}


public class Container
{
    IServiceProvider parent;

    DependencyTree tree;

    public Container(Type rootType, IServiceProvider parent)
    {
        this.parent = parent;

        tree = new DependencyTree(rootType, i => i);
    }

}
