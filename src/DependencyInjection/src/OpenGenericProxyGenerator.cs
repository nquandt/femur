using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

namespace Femur.DependencyInjection;

/// <summary>
/// Generates proxy types at runtime for open generic interfaces that don't have
/// known proxy implementations.
/// </summary>
internal static class OpenGenericProxyGenerator
{
    private static readonly ModuleBuilder ModuleBuilder = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Femur.DynamicProxies"),
            AssemblyBuilderAccess.Run).DefineDynamicModule("Proxies");
    private static readonly ConcurrentDictionary<Type, Type> Cache = new();

    public static Type GetOrCreateProxyType(Type openGenericInterface)
    {
        return Cache.GetOrAdd(openGenericInterface, CreateProxyType);
    }

    private static Type CreateProxyType(Type openGenericInterface)
    {
        if (!openGenericInterface.IsInterface)
        {
            throw new ArgumentException(
                $"Cannot create dynamic proxy for non-interface type: {openGenericInterface}");
        }

        if (!openGenericInterface.IsGenericTypeDefinition)
        {
            throw new ArgumentException(
                $"Expected open generic type definition, got: {openGenericInterface}");
        }

        var genericArgs = openGenericInterface.GetGenericArguments();
        var proxyName = $"DynamicProxy_{openGenericInterface.Name}_{Guid.NewGuid():N}";

        var typeBuilder = ModuleBuilder.DefineType(
            proxyName,
            TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);

        // Define matching generic parameters
        var genericParams = typeBuilder.DefineGenericParameters(
            genericArgs.Select((_, i) => $"T{i}").ToArray());

        // Copy generic constraints
        CopyGenericConstraints(genericArgs, genericParams);

        // Create the closed interface type using our generic parameters
        var closedInterface = openGenericInterface.MakeGenericType(genericParams);
        typeBuilder.AddInterfaceImplementation(closedInterface);

        // Define the _inner field
        var innerField = typeBuilder.DefineField(
            "_inner",
            closedInterface,
            FieldAttributes.Private | FieldAttributes.InitOnly);

        // Generate constructor
        GenerateConstructor(typeBuilder, closedInterface, innerField);

        // Generate interface implementation
        ImplementInterface(typeBuilder, openGenericInterface, closedInterface, innerField);

#if NETSTANDARD2_0
        return typeBuilder.CreateTypeInfo()!.AsType();
#else
        return typeBuilder.CreateType()!;
#endif
    }

    private static void CopyGenericConstraints(
        Type[] sourceParams,
        GenericTypeParameterBuilder[] targetParams)
    {
        for (var i = 0; i < sourceParams.Length; i++)
        {
            var src = sourceParams[i];
            var dst = targetParams[i];

            dst.SetGenericParameterAttributes(src.GenericParameterAttributes);

            var constraints = src.GetGenericParameterConstraints();
            var baseConstraint = constraints.FirstOrDefault(c => !c.IsInterface);
            var interfaceConstraints = constraints.Where(c => c.IsInterface).ToArray();

            if (baseConstraint != null)
            {
                dst.SetBaseTypeConstraint(baseConstraint);
            }

            if (interfaceConstraints.Length > 0)
            {
                dst.SetInterfaceConstraints(interfaceConstraints);
            }
        }
    }

    private static void GenerateConstructor(
        TypeBuilder typeBuilder,
        Type closedInterface,
        FieldBuilder innerField)
    {
        var ctor = typeBuilder.DefineConstructor(
            MethodAttributes.Public,
            CallingConventions.Standard,
            new[] { typeof(SourceProviderAccessor) });

        var il = ctor.GetILGenerator();

        // base()
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Call, typeof(object).GetConstructor(Type.EmptyTypes)!);

        // _inner = accessor.GetRequiredService(typeof(TInterface));
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldarg_1);
        il.Emit(OpCodes.Ldtoken, closedInterface);
        il.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))!);

        var getServiceMethod = typeof(SourceProviderAccessor)
            .GetMethod(nameof(SourceProviderAccessor.GetRequiredService), new[] { typeof(Type) })!;
        il.Emit(OpCodes.Callvirt, getServiceMethod);
        il.Emit(OpCodes.Castclass, closedInterface);
        il.Emit(OpCodes.Stfld, innerField);

        il.Emit(OpCodes.Ret);
    }

    private static void ImplementInterface(
        TypeBuilder typeBuilder,
        Type openGenericInterface,
        Type closedInterface,
        FieldBuilder innerField)
    {
        // Implement all methods (including property accessors)
        // Use the open generic definition to get methods, as TypeBuilderInstantiation doesn't support GetMethods
        foreach (var method in openGenericInterface.GetMethods())
        {
            ImplementMethod(typeBuilder, method, closedInterface, innerField);
        }
    }

    private static void ImplementMethod(
        TypeBuilder typeBuilder,
        MethodInfo openInterfaceMethod,
        Type closedInterface,
        FieldBuilder innerField)
    {
        // Get the corresponding method from the closed interface for proper type substitution
        var parameters = openInterfaceMethod.GetParameters();
        var paramTypes = parameters.Select(p => p.ParameterType).ToArray();

        var methodBuilder = typeBuilder.DefineMethod(
            openInterfaceMethod.Name,
            MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.Final,
            openInterfaceMethod.ReturnType,
            paramTypes);

        // Handle generic methods
        if (openInterfaceMethod.IsGenericMethodDefinition)
        {
            var methodGenericArgs = openInterfaceMethod.GetGenericArguments();
            var methodGenericParams = methodBuilder.DefineGenericParameters(
                methodGenericArgs.Select(a => a.Name).ToArray());

            for (var i = 0; i < methodGenericArgs.Length; i++)
            {
                methodGenericParams[i].SetGenericParameterAttributes(
                    methodGenericArgs[i].GenericParameterAttributes);
            }
        }

        var il = methodBuilder.GetILGenerator();

        // this._inner
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Ldfld, innerField);

        // Load all arguments
        for (var i = 0; i < parameters.Length; i++)
        {
            il.Emit(OpCodes.Ldarg, i + 1);
        }

        // Find the corresponding method in the closed interface
        // Use TypeBuilder.GetMethod to map from the open generic method to the closed generic method
        var closedMethod = TypeBuilder.GetMethod(closedInterface, openInterfaceMethod);

        // Call interface method
        il.Emit(OpCodes.Callvirt, closedMethod);
        il.Emit(OpCodes.Ret);

        typeBuilder.DefineMethodOverride(methodBuilder, closedMethod);
    }
}
