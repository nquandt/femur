
## Table Of Contents:

- [Status Quo](#status-quo)
- [Transient Instance Via Delegate Magic](#transient-instances-via-delegate-magic)
  - Skip to HERE if you are just interested in my implentation of MinimalApi as transient instances resolved by DI.
- [How Does This Work](#how-does-this-work)
- [Potential Gotchyas](#gotchyas)
- [How I am going to use this](#actual-usage-remarks)

## Status Quo

Minimal APIs in .NET provide a more concise way to define APIs while maintaining performance. To keep larger projects maintainable, it’s often suggested to organize endpoints using extension methods, separate files per feature, or grouped routing via IEndpointRouteBuilder.

This leads to something that typically looks like this:

```csharp
// Program.cs

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var weatherGroup = app.MapGroup("/weather");

weatherGroup.MapGet("/", () => Results.Ok("Weather API Root"));
weatherGroup.MapGet("/{city}", (string city) => Results.Ok($"Weather for {city}"));

app.Run();
```

or in seperate files

```csharp
// Routes/WeatherRoutes.cs
public static class WeatherRoutes
{
    public static void RegisterRoutes(IEndpointRouteBuilder endpoints)
    {
        var weatherGroup = endpoints.MapGroup("/weather");

        weatherGroup.MapGet("/", () => Results.Ok("Weather API Root"));
        weatherGroup.MapGet("/{city}", (string city) => Results.Ok($"Weather for {city}"));
    }
}

// Program.cs

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

WeatherRoutes.RegisterRoutes(app);

app.Run();
```

or with extension methods

```csharp
// Routes/WeatherRoutes.cs
public static class WeatherRoutes
{
    public static void MapWeatherRoutes(this IEndpointRouteBuilder endpoints)
    {
        var weatherGroup = endpoints.MapGroup("/weather");

        weatherGroup.MapGet("/", () => Results.Ok("Weather API Root"));
        weatherGroup.MapGet("/{city}", (string city) => Results.Ok($"Weather for {city}"));
    }
}

// Program.cs

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapWeatherRoutes();

app.Run();

```

This are already much easier than controllers when it comes to "simple" endpoints or "CQRS" style patterns. But as an endpoint grows, I have found the complexity of writing/maintaining extension methods does as well.

A pattern I find myself often using is something like

```csharp
// Endpoints/MyEndpoint.cs
public class MyEndpoint
{
    private readonly ILogger _logger;
    private readonly IWeatherService _weatherService;

    public MyEndpoint(ILogger<MyEndpoint> logger, IWeatherService weatherService)
    {
        _logger = logger;
        _weatherService = weatherService;
    }

    public async Task HandleAsync(string city, HttpContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Asking for temperature of {city}", city);

        var temp = await _weatherService.GetTemperatureAsync(city);

        await context.Response.WriteAsync($"The temperature is {temp}");
    }
}

// Program.cs
app.MapGet("/weather/{city}", async ([FromServices] MyEndpoint instance, [FromRoute] string city, HttpContext httpContext, CancellationToken cancellationToken) =>
{
    await instance.HandleAsync(city, httpContext, cancellationToken);
});
```

From this example note that all the attributes I need in `MyEndpoint` are registered in the delegate passsed to `MapGet` AND written in the method of my class (obviously). Now if I update my endpoint class I need to also change the endpoint registration(i.e. not DRY).

## Transient Instances via Delegate Magic

I didn't want to jump to far out of the dotnet box, but also wanted to find a solution to allow for more DRY code when building endpoints. I also find too often that I'd like for my Endpoint logic to live inside the lifecycle of a transient dependency that is resolved from DI. This doesn't actually remove the need for endpoint parameter setup it just moves it to the instance class. I'd rather has setup here and prefer ctor injection or even top-level ctors over setting up service injection as parameters. Functional code feels funny in C#. Controllers have been around so long and the ctor injection patterns are very widely understood, so in the spirit of keeping familar and to mitigate maintaing delegate definition in two places I've come up with the following pattern:

```csharp
// Endpoints/MyEndpoint.cs
public class MyEndpoint
{
    private readonly ILogger _logger;
    private readonly IWeatherService _weatherService;

    public MyEndpoint(ILogger<MyEndpoint> logger, IWeatherService weatherService)
    {
        _logger = logger;
        _weatherService = weatherService;
    }

    public async Task HandleAsync([FromRoute] string city, HttpContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Asking for temperature of {city}", city);

        var temp = await _weatherService.GetTemperatureAsync(city);

        await context.Response.WriteAsync($"The temperature is {temp}");
    }
}

// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<MyEndpoint>();

var app = builder.Build();

app.MapEndpoint<MyEndpoint>("/weather/{city}", [HttpMethod.Get], i => i.HandleAsync);

app.Run();
```

Here I am adding the `MyEndpoint` type to DI, and then calling a new extension method that generates a new static delegate that includes the `[FromServices] MyEndpoint instance` parameter as well as all the rest of the `HandleAsync` methods parameters. Note in this second copy of `MyEndpoint` I have added `[FromRoute]` on the argument in the `HandleAsync` method signature. And thats all I need to now use a DI resolved endpoint for a MinimalApi.

The code to generate the static delegate is a point of potential optimization. I'm not sure based on the internal implementation of delegate parsing if my strategy will experience any edge case issues.

Benchmarks wise I could not find a difference in execution time of this endpoint strategy vs the statically typed lambda delegate equivalent.

| Method                | Mean     | Error   | StdDev  |
|---------------------- |---------:|--------:|--------:|
| MapEndpoint_Http_Call | 209.0 us | 3.94 us | 8.49 us |
| MapGet_Http_Call      | 211.9 us | 4.20 us | 8.76 us |

```csharp
public class Bench
{
	private readonly HttpClient httpClient = new HttpClient();


	[Benchmark]
	public HttpStatusCode MapEndpoint_Http_Call() => httpClient.GetAsync($"http://localhost:5000/instance").GetAwaiter().GetResult().StatusCode;

    [Benchmark]
	public HttpStatusCode MapGet_Http_Call() => httpClient.GetAsync($"http://localhost:5000/standard").GetAwaiter().GetResult().StatusCode;

	[GlobalCleanup]
	public void CloseHost()
	{

		httpClient.Dispose();
	}
}
```

Here I had a release build of an example application running, and just ran BenchmarkDotNet against that.. It's probably not the ideal test, but it shows that from an integrations standpoint there is no statistically relevant difference between the two implementations.

## How Does This Work?

After and exhaustive amount of reading, I think I finally understand what is happening when you pass a `Delegate` to the `MapGet` function (not to be confused with the option to pass a `RequestDelegate`). Long story short, AspNetCore checks a large variety of details of the reflection definition of the function you pass, such as the input and output types as well as if there are attributes. It makes a series of decisions as to where to get parameters from (i.e. route, headers, query, body, di services etc.) and COMPILES a new `RequestDelegate` that wraps your original `Delegate`. So knowing that I figured that if I could compile a static method `Delegate` dynamically based on the parameters of my instance classes method, that I could pass that to the standard `MapMethods` method and let AspNetCore work exactly as expected to resolve DI and setup all the parameters.

i.e. given the signature 

```csharp
Task HandleAsync([FromRoute] string city, HttpContext context, CancellationToken cancellationToken);
```

I generate

```csharp
Task Invoke([FromServices] MyEndpoint instance, [FromRoute] string city, HttpContext context, CancellationToken cancellationToken)
{
    return instance.HandleAsync(city, context, cancellationToken);
}
```

and this new `Invoke` method is what gets passed to AspNetCore.

```csharp
// FemurEndpointsExtensions.cs
public static IEndpointRouteBuilder MapEndpoint<T>(this IEndpointRouteBuilder endpoints,
    [StringSyntax("Route")] string routePattern,
    IEnumerable<HttpMethod> httpMethods,
    Expression<Func<T, Delegate>> expression)
        where T : class
{
    // Gets the MethodInfo from our "fake lambda"
    var methodInfo = GetMethodInfoOfEndpoint(expression);

    // This is the magic that adds the [FromServices] "instance" parameter and then proxies the parameters
    var del = DelegateGenerator.CreateStaticDelegate(typeof(T), methodInfo);

    endpoints.MapMethods(routePattern, GetVerbStrings(httpMethods), del);

    return endpoints;
}

// DelegateGenerator.cs
public class DelegateGenerator
{
    // This holds an assembly in memory.. eek    
    private static readonly ModuleBuilder ModuleBuilder;

    static DelegateGenerator()
    {
        var assemblyName = new AssemblyName("DynamicDelegatesAssembly");
        var assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        ModuleBuilder = assemblyBuilder.DefineDynamicModule("MainModule");
    }

    /// <summary>
    /// A helper to create a delegate Type from a given methodInfo.    
    /// </summary>
    /// <param name="methodInfo"></param>
    /// <returns></returns>
    /// <exception cref="NullReferenceException"></exception>
    public static Type CreateDelegateType(MethodInfo methodInfo)
    {
        if (methodInfo.DeclaringType == null) { throw new NullReferenceException("Method DeclaringType cannot be null"); }

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

        for (int i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            invokeMethod.DefineParameter(i + 1, ParameterAttributes.None, parameter.Name);
        }

        var returnType = typeBuilder.CreateType();

        if (returnType == null) { throw new NullReferenceException("Something went wrong creating delegate type"); }

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
        ParameterInfo[] parameters = methodInfo.GetParameters();
        Type[] methodParams = new Type[parameters.Length + 1];

        // First parameter is the instance
        methodParams[0] = targetType;
        for (int i = 0; i < parameters.Length; i++)
            methodParams[i + 1] = parameters[i].ParameterType;

        // Define a new type that will hold the static method
        var typeBuilder = ModuleBuilder.DefineType($"{targetType.Name}_Invoker", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.Abstract);

        // Define the static method
        var methodBuilder = typeBuilder.DefineMethod(
            "Invoke",
            MethodAttributes.Public | MethodAttributes.Static,
            methodInfo.ReturnType,
            methodParams);

        // Set parameter name for endpoint instance
        var instanceParamBuilder = methodBuilder.DefineParameter(1, ParameterAttributes.None, "instance");

        // Apply [FromServices] attribute to the 'instance' parameter
        ConstructorInfo fromServicesCtor = typeof(FromServicesAttribute).GetConstructor(Type.EmptyTypes)!;
        if (fromServicesCtor != null)
        {
            var fromServicesAttr = new CustomAttributeBuilder(fromServicesCtor, Array.Empty<object>());
            instanceParamBuilder.SetCustomAttribute(fromServicesAttr);
        }

        // set parameter names
        for (int i = 0; i < parameters.Length; i++)
        {
            var paramBuilder = methodBuilder.DefineParameter(i + 2, ParameterAttributes.None, parameters[i].Name);

            //copy the attributes over for argumentsFromQuery, FromRoute, FromHeaders etc.
            foreach (var customAttribute in parameters[i].CustomAttributes)
            {
                ConstructorInfo customCtor = customAttribute.Constructor!;
                var customAttr = new CustomAttributeBuilder(customCtor, customAttribute.ConstructorArguments.Select(y => y.Value).ToArray());
                paramBuilder.SetCustomAttribute(customAttr);
            }
        }


        // I don't know reflection.emit very well so i ripped this part from GPT.
        // Generate IL to call instance method
        ILGenerator il = methodBuilder.GetILGenerator();

        // Load the instance onto the stack
        il.Emit(OpCodes.Ldarg_0);

        // Load method arguments onto the stack
        for (int i = 0; i < parameters.Length; i++)
            il.Emit(OpCodes.Ldarg, i + 1);

        // Callvirt to invoke instance method
        il.Emit(OpCodes.Callvirt, methodInfo);

        // Return the Task result
        il.Emit(OpCodes.Ret);

        // Create the new type
        Type generatedType = typeBuilder.CreateType()!;

        // Get reference to the generated static method
        MethodInfo generatedMethod = generatedType.GetMethod("Invoke")!;

        // Create delegate type dynamically
        Type delegateType = CreateDelegateType(generatedMethod);

        // Return delegate pointing to generated static method
        return Delegate.CreateDelegate(delegateType, generatedMethod);
    }
}
```

## Gotchyas

Couple gotchya I haven't figured out yet:
- I don't believe my implementation accounts for async/await when it generates the new method via Reflection.Emit, so I'm just passing the Task from the other method... While this is a "no-no" I don't believe it will harm anything since its just a "Here is the actual task you should be concerned with".
- I don't think there is a limit to C# method parameter length.. but if there is now I only support `MaxLength - 1` since the compiled method needs to add a parameter.
- Is there a reason AspNetCore doesn't have this option already? I think I might be second guessing myself, but this pattern seems like a natural progression to me but I haven't seen a 99% similar solution to this as a built-in.
- Is there an performance hit for a dynamic assembly like this?
- How does that dynamic assembly scale for 100+ endpoints (memory footprint, naming collisions, etc.)?

## Actual Usage Remarks

In actual practice I would probably use `[Route]` and `[HttpVERB]` attributes to annotate the `MyEndpoint` class similar to a controller, and then grab those via Expressions/Reflection to register my endpoint.

```csharp
[Route("/weather/{city}")]
public class MyEndpoint
{
    private readonly ILogger _logger;
    private readonly IWeatherService _weatherService;

    public MyEndpoint(ILogger<MyEndpoint> logger, IWeatherService weatherService)
    {
        _logger = logger;
        _weatherService = weatherService;
    }

    [HttpGet]
    public async Task HandleAsync([FromRoute] string city, HttpContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Asking for temperature of {city}", city);

        var temp = await _weatherService.GetTemperatureAsync(city);

        await context.Response.WriteAsync($"The temperature is {temp}");
    }
}

// MvcAttributesMapEndpointExtensions.cs

public static IEndpointRouteBuilder MapEndpoint<T>(this IEndpointRouteBuilder endpoints,
    Expression<Func<T, Delegate>> expression)
        where T : class
{
    var endpointType = typeof(T);
    var atts = endpointType.GetCustomAttributes(typeof(RouteAttribute), true)
                .Select(x => (RouteAttribute)x);

    if (!atts.Any())
    {
        throw ...;
    }

    var routeAtt = atts.First()!;

    var methodInfo = GetMethodInfoOfEndpoint(expression);

    var httpMethodsAtts = methodInfo.GetCustomAttributes(typeof(HttpMethodAttribute), true)
        .Select(x => x.GetType());

    var httpMethods = httpMethodsAtts.Select(x => GetMethodStringFromAttr(x));

    endpoints.MapEndpoint<T>(routeAtt.Template, httpMethods, expression);

    return endpoints;
}

// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransient<MyEndpoint>();

var app = builder.Build();

app.MapEndpoint<MyEndpoint>(i => i.HandleAsync);

app.Run();
```

The last step to improve could be to add a more generic `MapEndpoints` call for `app` that adds all endpoints from the `IServiceCollection`. We would need a marker of some sort, whether an attribute/interface on the class, or a custom `IServiceCollection` extension method that tracks the necessary information. Dealers choice.

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpoint<MyEndpoint1>(i => i.HandleAsync);
builder.Services.AddEndpoint<MyEndpoint2>(i => i.DoThingyAsync);
builder.Services.AddEndpoint<MyEndpoint3>(i => i.NameDoesntMatterAsync);

var app = builder.Build();

app.MapEndpoints();

app.Run();
```