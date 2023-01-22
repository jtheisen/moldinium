using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;

namespace CMinus;

public class ImplementationMapping
{
    Type[] interfaces;
    (ExtraMethodInfo declaration, ExtraMethodInfo? implementation)[] declarationsAndImplementations;

    public Type[] Interfaces => interfaces;

    public IReadOnlyDictionary<MethodInfo, MethodInfo?> DeclarationsToImplementations { get; }

    public MethodInfo? GetImplementationMethod(MethodInfo? method)
        => method is not null ? DeclarationsToImplementations.GetValueOrDefault(method) : null;

    public String ImplementationReport
    {
        get
        {
            var writer = new StringWriter();

            foreach (var (declaration, implementation) in declarationsAndImplementations)
            {
                writer.WriteLine($"{declaration} -> {implementation}");
            }

            return writer.ToString();
        }
    }

    public ImplementationMapping(HashSet<Type> types)
    {
        interfaces = types.Where(t => t.IsInterface).ToArray();

        var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;

        var methodsNamesAndSignatures = (
            from type in types
            from method in type.GetMethods(bindingFlags)
            select MethodSignatures.GetExtraMethodInfo(method)
        ).ToArray();

        ExtraMethodInfo? SelectImplementation(ExtraMethodInfo declaration, ExtraMethodInfo[] candidates, Boolean requiresImplementation)
        {
            if (candidates.Length == 0 && requiresImplementation)
            {
                throw new Exception($"No implementation candidate with the same unqualified name found for method {declaration}");
            }

            var checkedCandidates = candidates.Where(c => declaration.MethodNameAndSignature == c.MethodNameAndSignature).ToArray();

            if (checkedCandidates.Length == 1)
            {
                return checkedCandidates.Single();
            }

            String MakeReport(String name, IEnumerable<ExtraMethodInfo> candidates)
            {
                return $"{name}:\n\t{String.Join("\t\n", candidates)}\n";
            }

            if (checkedCandidates.Length > 1)
            {
                throw new Exception($"Multiple implementations found for method {declaration},\n{MakeReport("candidates", checkedCandidates)}");
            }
            else if (requiresImplementation)
            {
                throw new Exception($"No implementation found for method {declaration},\n"
                    + $"{MakeReport("candidates", checkedCandidates)}\n{MakeReport("all with the same unqualified name", candidates)}");
            }
            else
            {
                return null;
            }
        }

        var implementations = (
            from declaration in methodsNamesAndSignatures
            where declaration.IsImplementable
            join candidate in methodsNamesAndSignatures.Where(m => m.IsImplemented) on declaration.UnqualifiedName equals candidate.UnqualifiedName into candidates
            let requiresImplementation = !declaration.Method.IsSpecialName
            select (declaration, implementation: SelectImplementation(declaration, candidates.ToArray(), requiresImplementation))
        );

        declarationsAndImplementations = implementations.ToArray();

        DeclarationsToImplementations = declarationsAndImplementations.ToDictionary(p => p.declaration.Method, p => p.implementation?.Method);
    }
}