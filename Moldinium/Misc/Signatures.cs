﻿using System.Reflection.Emit;
using System.Reflection;
using System.Security.Cryptography;
using System.Collections.Concurrent;
using System.Reflection.Metadata;
using System.Text;

namespace Moldinium;

public record EquatableMethodNameAndSignature(String MethodName, EquatableMethodSignature Signature);

public class ExtraMethodInfo
{
    public EquatableMethodNameAndSignature MethodNameAndSignature { get; }
    public MethodInfo Method { get; }
    public Type DeclaringType { get; }
    public String? ContainerName { get; }
    public String UnqualifiedName { get; }
    public String QualifiedName { get; }
    public Boolean IsPublicOnInterface { get; }
    public Boolean IsPrivateImplementation { get; }
    public Boolean IsImplemented { get; }
    public Boolean IsImplementable { get; }

    private String GetMetaCode() => this switch
    {
        { IsPrivateImplementation: true } => "p",
        { IsImplementable: true } and { IsImplemented: true } => "v",
        { IsImplementable: true} => "a",
        { IsImplemented: true } => "c",
        _ => "?"
    };

    public override string ToString()
    {
        return $"{GetMetaCode()};{QualifiedName}";
    }

    public ExtraMethodInfo(MethodInfo method)
    {
        var signature = new EquatableMethodSignature(method);

        IsImplemented = method.GetMethodBody() is not null;
        IsImplementable = !method.IsPrivate && !method.IsStatic && method.IsVirtual;
        Method = method;
        DeclaringType = method.DeclaringType ?? throw new Exception($"Method {method} has no declaring type");

        IsPublicOnInterface = DeclaringType.IsInterface && method.IsPublic;

        if (TryParsePrivateName(method.Name, out var containerName, out var baseMethodName))
        {
            UnqualifiedName = baseMethodName;
            ContainerName = containerName;
            QualifiedName = $"{containerName}.{baseMethodName}";
            IsPrivateImplementation = true;
        }
        else
        {
            containerName = method.DeclaringType?.FullName?.Replace('+', '.') ?? "";

            UnqualifiedName = method.Name;
            ContainerName = containerName;
            QualifiedName = $"{containerName}.{method.Name}";
            IsPrivateImplementation = false;
        }

        MethodNameAndSignature = new EquatableMethodNameAndSignature(UnqualifiedName, signature);
    }

    static Boolean TryParsePrivateName(String methodName, out String containerName, out String baseMethodName)
    {
        var lastDotAt = methodName.LastIndexOf('.');

        if (lastDotAt >= 0)
        {
            containerName = methodName[..lastDotAt];
            baseMethodName = methodName[(lastDotAt + 1)..];

            return true;
        }
        else
        {
            containerName = String.Empty;
            baseMethodName = String.Empty;

            return false;
        }
    }

}

public record EquatableMethodSignature(String SignatureToken, Int32 GenericParameterCount)
{
    public EquatableMethodSignature(MethodInfo method)
        : this(CustomSignatureHelper.GetSignatureToken(method), GetGenericTypeParameterCount(method))
    {
    }

    static Int32 GetGenericTypeParameterCount(MethodInfo method)
    {
        var genericParameters = method.GetGenericArguments();

        if (genericParameters.Any(t => !t.IsGenericMethodParameter)) throw new Exception($"Expected {method} to have only generic type parameters");

        return genericParameters.Length;
    }
}

public static class MethodSignatures
{
    static ConcurrentDictionary<MethodInfo, ExtraMethodInfo> signatures
        = new ConcurrentDictionary<MethodInfo, ExtraMethodInfo>();

    public static ExtraMethodInfo GetExtraMethodInfo(MethodInfo method)
        => signatures.GetOrAdd(method, m => new ExtraMethodInfo(m));

    public static EquatableMethodSignature GetSignature(MethodInfo method)
        => GetExtraMethodInfo(method).MethodNameAndSignature.Signature;
}

static class CustomSignatureHelper
{
    class SignatureEncoder
    {
        StringWriter writer = new StringWriter();

        public String GetString() => writer.ToString();

        public void EncodeMethod(MethodInfo method)
        {
            if (method.ReturnParameter is ParameterInfo returnParameter)
            {
                EncodeParameter(returnParameter, 0);
            }

            var paramters = method.GetParameters();

            for (var i = 0; i < paramters.Length; ++i)
            {
                EncodeParameter(paramters[i], i + 1);
            }
        }

        void EncodeParameter(ParameterInfo parameter, Int32 position)
        {
            writer.Write(position);
            writer.Write('[');
            EncodeType(parameter.ParameterType);
            foreach (var m in parameter.GetRequiredCustomModifiers())
            {
                EncodeModifier(m, true);
            }
            foreach (var m in parameter.GetOptionalCustomModifiers())
            {
                EncodeModifier(m, false);
            }
            EncodeType(parameter.ParameterType);

            writer.Write(']');
        }

        void EncodeModifier(Type modifier, Boolean isRequired)
        {
            writer.Write(isRequired ? 'r' : 'o');
            EncodeType(modifier);
        }

        void EncodeType(Type type)
        {
            writer.Write('(');
            writer.Write(type.Name);
            writer.Write('|');
            writer.Write(type.TypeHandle.Value.ToInt64());
            writer.Write(')');
        }
    }

    public static String GetSignatureToken(MethodInfo method)
    {
        var encoder = new SignatureEncoder();

        encoder.EncodeMethod(method);

        return encoder.GetString();
    }
}
