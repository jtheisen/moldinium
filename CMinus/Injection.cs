using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CMinus.Injection;

/*
 * There are various IDependencyProviders with different demands and properties
 * 
 * 1. The Bakery
 * 
 * Requires only types (not instances) as dependencies to keep the door open for assembly weaving.
 * It also only provides types, not instances, but the provided types are default activatable.
 * 
 * 2. IServiceProvider
 * 
 * Requires nothing as dependencies as it is only used as a source for types. This is good, as
 * it can't tell you what resolved type you get for a service type until you request an instance.
 * 
 * 3. SimpleInjector
 * 
 * Could in principle be used as both a source and a sink as it allows resolving types without any instances.
 * Whether this can be used as a helper for the implementation remains an open question.
 * 
 * 4. Parent scope
 * 
 * Not sure what to say here.
 */

public record Dependency(Type Type, Boolean RequireInstance);

public record DependencyResolution(Type Type, Dependency[] Dependencies, Type? ResolvedTypeOrNull);

public interface IDependencyProvider
{
    DependencyResolution? Query(Type type);

    Object GetObject(Type type, Func<Dependency, Object> getNestedDependency);
}

public class Scope
{

}
