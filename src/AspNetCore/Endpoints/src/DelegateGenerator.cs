using System.Reflection;
using System.Reflection.Emit;
using Microsoft.AspNetCore.Mvc;

namespace Femur.AspNetCore.Endpoints;

public static class DelegateGenerator
{
    // This holds an assembly in memory.. eek    
    private static readonly ModuleBuilder ModuleBuilder = CreateModuleBuilder();

    private static ModuleBuilder CreateModuleBuilder()
    {
        var assemblyName = new AssemblyName("DynamicDelegatesAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        return assemblyBuilder.DefineDynamicModule("MainModule");
    }

    /// <summary>
    /// A helper to create a delegate Type from a given methodInfo.    
    /// </summary>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    public static Type CreateDelegateType(MethodInfo methodInfo)
    {
        if (methodInfo.DeclaringType == null)
        {
            throw new NullReferenceException("Method DeclaringType cannot be null");
        }

        var typeBuilder = ModuleBuilder.DefineType(
                $"{methodInfo.DeclaringType.Name}_{methodInfo.Name}_Delegate",
                TypeAttributes.Sealed | TypeAttributes.Public,
                typeof(MulticastDelegate));

        var constructor = typeBuilder.DefineConstructor(
            MethodAttributes.RTSpecialName | MethodAttributes.HideBySig | MethodAttributes.Public,
            CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) });
        constructor.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        var parameters = methodInfo.GetParameters();

        var invokeMethod = typeBuilder.DefineMethod(
            "Invoke", MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Public,
            methodInfo.ReturnType, parameters.Select(p => p.ParameterType).ToArray());
        invokeMethod.SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            _ = invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, parameter.Name);
        }

        var returnType = typeBuilder.CreateType();

        if (returnType == null)
        {
            throw new NullReferenceException("Something went wrong creating delegate type");
        }

        return returnType;
    }


    /// <summary>
    /// This will create our custom static delegate method, that will request the endpoint instance via FromServices and proxy the remaining arguments to the defined call.
    /// </summary>
    /// <param name="targetType">This is the endpoint instance type.</param>
    /// <param name="methodInfo">This is the method on the endpoint we want to use to process a request.</param>
    /// <returns></returns>
    public static Delegate CreateStaticDelegate(Type targetType, MethodInfo methodInfo)
    {
        var parameters = methodInfo.GetParameters();
        var methodParams = new Type[parameters.Length + 1];

        // First parameter is the instance
        methodParams[0] = targetType;
        for (var i = 0; i < parameters.Length; i++)
        {
            methodParams[i + 1] = parameters[i].ParameterType;
        }

        // Define a new type that will hold the static method
        var typeBuilder = ModuleBuilder.DefineType(GetUniqueName($"{targetType.Name}_Invoker"), TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);

        // Define the static method
        var methodBuilder = typeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.Static,
            methodInfo.ReturnType,
            methodParams);

        // Set parameter name for endpoint instance
        var instanceParamBuilder = methodBuilder.DefineParameter(1, ParameterAttributes.None, "instance");

        // Apply [FromServices] attribute to the 'instance' parameter
        var fromServicesCtor = typeof(FromServicesAttribute).GetConstructor(Type.EmptyTypes)!;
        if (fromServicesCtor != null)
        {
            var fromServicesAttr = new CustomAttributeBuilder(fromServicesCtor, Array.Empty<object>());
            instanceParamBuilder.SetCustomAttribute(fromServicesAttr);
        }

        // set parameter names
        for (var i = 0; i < parameters.Length; i++)
        {
            var paramBuilder = methodBuilder.DefineParameter(i + 2, ParameterAttributes.None, parameters[i].Name);

            //copy the attributes over for argumentsFromQuery, FromRoute, FromHeaders etc.
            foreach (var customAttribute in parameters[i].CustomAttributes)
            {
                var customCtor = customAttribute.Constructor!;
                var customAttr = new CustomAttributeBuilder(customCtor, customAttribute.ConstructorArguments.Select(y => y.Value).ToArray());
                paramBuilder.SetCustomAttribute(customAttr);
            }
        }


        foreach (var customAttribute in methodInfo.CustomAttributes)
        {
            var customCtor = customAttribute.Constructor;
            if (customCtor is not null)
            {
                var customAttr = new CustomAttributeBuilder(customCtor, customAttribute.ConstructorArguments.Select(y =>
                {
                    if (y.Value is IEnumerable<CustomAttributeTypedArgument> c)
                    {
                        return c.Select(x => (string)x.Value!).ToArray();
                    }

                    return y.Value;
                }).ToArray());

                methodBuilder.SetCustomAttribute(customAttr);
            }
        }

        // I don't know reflection.emit very well so i ripped this part from GPT.
        // Generate IL to call instance method
        var il = methodBuilder.GetILGenerator();

        // Load the instance onto the stack
        il.Emit(OpCodes.Ldarg_0);

        // Load method arguments onto the stack
        for (var i = 0; i < parameters.Length; i++)
        {
            il.Emit(OpCodes.Ldarg, i + 1);
        }

        // Callvirt to invoke instance method
        il.Emit(OpCodes.Callvirt, methodInfo);

        // Return the Task result
        il.Emit(OpCodes.Ret);

        // Create the new type
        var generatedType = typeBuilder.CreateType()!;

        // Get reference to the generated static method
        var generatedMethod = generatedType.GetMethod("Invoke")!;

        // Create delegate type dynamically
        var delegateType = CreateDelegateType(generatedMethod);

        // Return delegate pointing to generated static method
        return Delegate.CreateDelegate(delegateType, generatedMethod);
    }


    private static string GetUniqueName(string nameBase)
    {
        var number = 2;
        var name = nameBase;
        while (ModuleBuilder.GetType(name) != null)
        {
            name = nameBase + number++;
        }

        return name;
    }
}
