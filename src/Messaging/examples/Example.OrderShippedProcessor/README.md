# OrderShipped Processor Microservice Example

A complete example demonstrating how to build a production-ready message processor microservice using **Femur.Hosting** and **Femur.Messaging.ServiceBus**.

## Overview

This example implements a real-world scenario: processing order shipment notifications and sending customer emails. When an order ships from a warehouse, an `OrderShippedMessage` is published to a Service Bus queue. This microservice processes those messages and sends shipping notification emails to customers.

## What This Example Demonstrates

### Femur.Hosting Integration
- **ApplicationBuilder**: Structured application lifecycle with type-safe configuration stages
- **Bootstrap Logging**: Early logging before host initialization for diagnostics
- **Error Handling**: Distinct error handlers for builder, build, startup, runtime, and shutdown phases
- **Exit Codes**: Standardized exit codes for operational monitoring and orchestration

### Femur.Messaging Patterns
- **Message Processing**: Automatic background processing with `IMessageHandler<T>`
- **ServiceBus Transport**: Azure Service Bus integration with configurable options
- **Error Handling**:
  - Validation failures → Dead-letter with reason
  - Transient errors → Retry with exponential backoff
  - Permanent failures → Dead-letter immediately
- **Dependency Injection**: Handler dependencies resolved from DI container per message

### Best Practices
- **Configuration Management**: appsettings.json with environment-specific overrides
- **Structured Logging**: Comprehensive logging with message context
- **Validation**: Input validation with explicit dead-lettering
- **Retry Strategy**: Automatic retry for transient failures, dead-letter for permanent failures

## Architecture

```
Azure Service Bus Queue: "order-shipped"
           ↓
ServiceBusMessageClient (receives message)
           ↓
MessageProcessor (deserializes JSON)
           ↓
[Deserialization valid?]
    ├─ No → Dead-letter with "DeserializationFailed"
    └─ Yes → Continue
           ↓
Create DI Scope (OrderShippedHandler, IEmailService, ILogger)
           ↓
OrderShippedHandler.HandleAsync()
           ↓
[Validate required fields]
    ├─ Invalid → throw DeadLetterException("MissingField", reason)
    └─ Valid → Continue
           ↓
EmailService.SendOrderShippedEmailAsync()
           ↓
[Email sent successfully?]
    ├─ InvalidEmailException → Dead-letter
    ├─ EmailServiceException → Abandon (retry)
    └─ Success → Complete message
```

## Project Structure

```
Example.OrderShippedProcessor/
├── Program.cs                          # Entry point with ApplicationBuilder
├── OrderShippedMessage.cs              # Message definition
├── OrderShippedHandler.cs              # Message handler with validation
├── IEmailService.cs                    # Email service interface
├── MockEmailService.cs                 # Stubbed email implementation
├── appsettings.json                    # Production configuration
├── appsettings.Development.json        # Development configuration (in-memory)
└── README.md                           # This file
```

## Prerequisites

### For Local Development (In-Memory Transport)
- .NET 8.0 or later
- No external dependencies needed

### For Azure Service Bus Testing
- .NET 8.0 or later
- Azure subscription with Service Bus namespace
- Queue named `order-shipped` created in the namespace

## Configuration

### Transport Selection

The example automatically selects the transport based on environment:
- **Development mode**: Uses in-memory transport (no external dependencies)
- **Production mode**: Uses Azure Service Bus (requires connection string)

This is controlled by environment variables (proper DI pattern, avoiding temporary service providers):

```bash
# Use in-memory transport (automatic in Development)
export ASPNETCORE_ENVIRONMENT=Development
dotnet run

# Or explicitly set the flag
export USE_INMEMORY_TRANSPORT=true
dotnet run
```

### Local Development (In-Memory Transport)

No setup required - just run in Development mode:

```bash
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

### Azure Service Bus (Production)

1. **Create Service Bus Queue**:
   ```bash
   # Using Azure CLI
   az servicebus queue create \
     --resource-group <resource-group> \
     --namespace-name <namespace> \
     --name order-shipped
   ```

2. **Configure Connection String**:

   **Option A: appsettings.json** (not recommended for production)
   ```json
   {
     "ConnectionStrings": {
       "ServiceBus": "Endpoint=sb://your-namespace.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=your-key"
     }
   }
   ```

   **Option B: Environment Variable** (recommended)
   ```bash
   export ConnectionStrings__ServiceBus="Endpoint=sb://..."
   export USE_INMEMORY_TRANSPORT=false  # Or omit to use Service Bus
   dotnet run
   ```

   **Option C: User Secrets** (recommended for local development)
   ```bash
   dotnet user-secrets set "ConnectionStrings:ServiceBus" "Endpoint=sb://..."
   ```

## Running the Example

### 1. Run with In-Memory Transport (Local Testing)

```bash
cd src/Messaging/examples/Example.OrderShippedProcessor
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
```

You should see output like:
```
info: Femur.Hosting.ApplicationBuilder[0]
      Building console application host
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Femur.Messaging.MessageProcessor[0]
      Starting message processor for OrderShippedMessage on queue 'order-shipped'
```

### 2. Send Test Messages

To test message processing, you'll need to send messages to the queue. Create a simple test sender:

**TestSender.cs**:
```csharp
using Femur.Messaging;
using Femur.Messaging.InMemory;
using Femur.Messaging.Example.OrderShippedProcessor;

var services = new ServiceCollection();
services.AddFemurInMemory();
services.AddMessageClient<OrderShippedMessage>();

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IMessageClient<OrderShippedMessage>>();

// Send test message
await client.SendAsync(new OrderShippedMessage
{
    OrderId = "ORD-12345",
    CustomerId = "CUST-67890",
    CustomerEmail = "customer@example.com",
    CustomerName = "John Doe",
    OrderDate = DateTimeOffset.UtcNow.AddDays(-2),
    ShippingTrackingNumber = "1Z999AA10123456784",
    CarrierName = "UPS",
    EstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3),
    OrderAmount = 127.99m
});

Console.WriteLine("Test message sent!");
```

### 3. Run with Azure Service Bus

```bash
cd src/Messaging/examples/Example.OrderShippedProcessor
export ASPNETCORE_ENVIRONMENT=Production
export ConnectionStrings__ServiceBus="Endpoint=sb://your-namespace..."
export UseInMemoryTransport=false
dotnet run
```

Send messages using Azure Portal, Service Bus Explorer, or the Azure SDK.

## Testing Error Scenarios

### 1. Test Validation Failure (Dead-Letter)

Send a message with missing email:
```csharp
await client.SendAsync(new OrderShippedMessage
{
    OrderId = "ORD-99999",
    CustomerId = "CUST-11111",
    CustomerEmail = "",  // Missing - will be dead-lettered
    CustomerName = "Test User",
    ShippingTrackingNumber = "TEST123",
    EstimatedDeliveryDate = DateTimeOffset.UtcNow.AddDays(3),
    OrderAmount = 50.00m
});
```

Expected behavior:
- Handler validates and throws `DeadLetterException("MissingCustomerEmail", ...)`
- Message moved to dead-letter queue with reason "MissingCustomerEmail"
- Logs show: `"OrderShipped message missing CustomerEmail for Order ORD-99999"`

### 2. Test Transient Failure (Retry)

Configure email service to simulate failure:
```json
{
  "EmailService": {
    "SimulateFailure": true
  }
}
```

Expected behavior:
- Handler throws `EmailServiceException`
- Message is abandoned and retried (up to `MaxDeliveryCount = 5`)
- After 5 attempts, message is dead-lettered with exception details
- Logs show: `"Email service error while processing Order X. Message will be retried."`

### 3. Test Invalid Email (Dead-Letter)

Send a message with invalid email format:
```csharp
await client.SendAsync(new OrderShippedMessage
{
    CustomerEmail = "not-an-email",  // Invalid format
    // ... other fields
});
```

Expected behavior:
- Email service validates and throws `InvalidEmailException`
- Handler catches and rethrows as `DeadLetterException("InvalidEmailAddress", ...)`
- Message immediately dead-lettered (no retry)

## Code Walkthrough

### Program.cs - ApplicationBuilder Pattern

```csharp
return await ApplicationBuilder.Create(args)
    .UseDefaultConsoleLogging()              // Bootstrap logging
    .ConfigureConfiguration(config => { })    // Load configuration
    .ConfigureServices(services => {          // Register services
        services.AddFemurServiceBus(...);
        services.AddMessageHandler<OrderShippedMessage, OrderShippedHandler>();
    })
    .SkipConfigureErrorHandlers()            // Use default error handling
    .RunAsync();                              // Start hosting
```

**Key Features**:
- Type-safe builder stages prevent configuration mistakes
- Bootstrap logging available before host initialization
- Proper DI patterns (no temporary service providers)
- Standardized exit codes for operational integration

### Proper Dependency Injection Patterns

This example demonstrates **correct DI patterns** to avoid common anti-patterns. See [DI-PATTERNS.md](DI-PATTERNS.md) for comprehensive documentation.

**✅ Options Pattern** (Configuration binding):
```csharp
services.AddOptions<MockEmailServiceOptions>()
    .Configure<IConfiguration>((options, config) =>
    {
        config.GetSection("EmailService").Bind(options);
    });
```
IConfiguration is resolved at runtime, not during service registration.

**✅ Factory Pattern** (Connection strings):
```csharp
services.AddFemurServiceBus(
    sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("ServiceBus")
        ?? throw new InvalidOperationException("Connection string required"));
```
Factory receives IConfiguration when the transport is created.

**✅ Environment Variables** (Registration-time decisions):
```csharp
var useInMemory = Environment.GetEnvironmentVariable("USE_INMEMORY_TRANSPORT")?.ToLowerInvariant() == "true"
    || Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

if (useInMemory)
    services.AddFemurInMemory();
else
    services.AddFemurServiceBus(...);
```
Direct environment variable access avoids building temporary service providers.

**❌ NEVER Do This** (Anti-pattern):
```csharp
// DON'T build temporary service providers during registration!
using var tempProvider = services.BuildServiceProvider();
var config = tempProvider.GetRequiredService<IConfiguration>();
```
See [DI-PATTERNS.md](DI-PATTERNS.md) for why this is problematic and better alternatives.

### OrderShippedMessage.cs - Message Definition

```csharp
public class OrderShippedMessage : IMessage
{
    public static string MessageName => "order-shipped";  // Queue name

    public required string OrderId { get; init; }
    public required string CustomerEmail { get; init; }
    // ... other properties
}
```

**Key Features**:
- `IMessage` interface with `MessageName` property
- Init-only properties for immutability
- `required` modifier for compile-time validation

### OrderShippedHandler.cs - Message Processing

```csharp
public class OrderShippedHandler : IMessageHandler<OrderShippedMessage>
{
    public async Task HandleAsync(OrderShippedMessage message, CancellationToken ct)
    {
        // Validation - permanent failures
        if (string.IsNullOrWhiteSpace(message.CustomerEmail))
            throw new DeadLetterException("MissingEmail", "...");

        try
        {
            await _emailService.SendOrderShippedEmailAsync(...);
        }
        catch (InvalidEmailException ex)
        {
            // Permanent failure - dead-letter
            throw new DeadLetterException("InvalidEmail", ex.Message);
        }
        catch (EmailServiceException)
        {
            // Transient failure - retry
            throw;
        }
    }
}
```

**Key Patterns**:
- Validate input and throw `DeadLetterException` for data quality issues
- Catch specific exceptions and decide: dead-letter vs. retry
- Let transient exceptions bubble up for automatic retry
- Comprehensive logging with message context

### Error Handling Strategy

| Error Type | Exception | Behavior | Retry? |
|------------|-----------|----------|--------|
| Deserialization failure | (automatic) | Dead-letter with "DeserializationFailed" | No |
| Missing required field | `DeadLetterException` | Dead-letter with reason | No |
| Invalid email format | `InvalidEmailException` → `DeadLetterException` | Dead-letter with "InvalidEmailAddress" | No |
| Email service transient | `EmailServiceException` | Abandon message | Yes (up to MaxDeliveryCount) |
| Max retries exceeded | (automatic) | Dead-letter with exception details | No |

## Production Considerations

### 1. Replace Mock Email Service

Replace `MockEmailService` with a real implementation:

```csharp
public class SendGridEmailService : IEmailService
{
    private readonly SendGridClient _client;

    public async Task SendOrderShippedEmailAsync(...)
    {
        var msg = new SendGridMessage
        {
            From = new EmailAddress("noreply@example.com"),
            Subject = $"Your Order #{orderId} Has Shipped!",
            PlainTextContent = $"Hi {customerName}, ..."
        };
        msg.AddTo(to);

        var response = await _client.SendEmailAsync(msg, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Body.ReadAsStringAsync();
            throw new EmailServiceException($"SendGrid error: {body}");
        }
    }
}
```

### 2. Secure Configuration

- Use Azure Key Vault for connection strings
- Use Managed Identity instead of connection strings
- Never commit secrets to source control

```csharp
services.AddFemurServiceBusWithManagedIdentity(
    "your-namespace.servicebus.windows.net",
    new DefaultAzureCredential());
```

### 3. Monitoring and Observability

- Enable Application Insights or equivalent
- Monitor dead-letter queues
- Set up alerts for:
  - High dead-letter queue depth
  - Processing latency spikes
  - Error rate increases

### 4. Scaling Considerations

- Service Bus Standard/Premium tier for auto-scaling
- Multiple processor instances for high throughput
- Consider partitioned queues for >80MB/s throughput
- Adjust `MaxDeliveryCount` based on error patterns

### 5. Testing Strategy

- Unit tests: Test handler logic with mocked dependencies
- Integration tests: Test with in-memory transport
- E2E tests: Test with real Service Bus namespace (dev environment)

## Exit Codes

The application returns standardized exit codes for operational integration:

| Code | Meaning | When It Occurs |
|------|---------|----------------|
| 0 | Success | Normal shutdown (Ctrl+C) |
| 1 | Builder Creation Failed | Error creating ApplicationBuilder |
| 2 | Build Failed | Configuration or service registration error |
| 3 | Pre-Startup Error | Error during host initialization |
| 4 | Runtime Error | Unhandled exception during message processing |
| 5 | Post-Shutdown Error | Error during graceful shutdown |

These exit codes enable:
- Container orchestration systems to detect failures
- Monitoring systems to alert on specific failure types
- Restart policies based on exit code patterns

## Troubleshooting

### Issue: "ServiceBus connection string not configured"

**Solution**: Set `UseInMemoryTransport=true` for local testing, or configure connection string.

### Issue: Messages not being processed

**Checks**:
1. Verify queue name matches `MessageName` in message definition
2. Check Service Bus connection string is valid
3. Verify queue exists and is not disabled
4. Check application logs for startup errors

### Issue: All messages going to dead-letter queue

**Checks**:
1. Review dead-letter reason and description
2. Check message format matches `OrderShippedMessage` schema
3. Verify required fields are populated
4. Review handler validation logic

### Issue: High latency / slow processing

**Checks**:
1. Check `EmailService.DelayMilliseconds` setting
2. Monitor email service response times
3. Consider increasing `MaxLockDuration` if processing takes >30 seconds
4. Scale out by running multiple instances

## Learn More

- [Femur.Hosting Documentation](../../../Hosting/src/README.md)
- [Femur.Messaging Documentation](../../src/README.md)
- [Azure Service Bus Best Practices](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-performance-improvements)

## License

This example is part of the Femur library and is licensed under the MIT License.
