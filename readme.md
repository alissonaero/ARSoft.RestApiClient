# ApiClient Library for .NET Core and .NET 5+ applications

A modern, resilient HTTP API client library for .NET Core and .NET 5+ applications, providing clean, async HTTP operations with built-in retry policies using Polly v8, comprehensive error handling, and structured logging support. Optimized for high-performance API consumption in cross-platform and cloud-native environments.

## 🎯 Features

- **Modern .NET Support**: Built for .NET 6+ with nullable reference types
- **Polly v8 Integration**: Flexible resilience pipelines with exponential backoff and jitter
- **Per-Instance HttpClient**: Each ApiClient manages its own HttpClient for isolated configuration
- **Structured Logging**: ILogger integration for comprehensive request/response logging
- **Flexible Authentication**: Bearer, Basic, and API Key authentication support
- **Custom Headers Support**: Add dynamic headers per request or globally
- **Thread-Safe Design**: Proper concurrency handling with internal locking
- **Clean Architecture**: SOLID principles with dependency injection support
- **Comprehensive Error Handling**: Detailed error responses with HTTP status codes and timeout detection
- **Async/Await**: Full asynchronous operation support with cancellation tokens
- **Configuration Validation**: Prevents unsafe configuration changes after initialization
- **Relative Path Support**: Built-in support for relative URLs with BaseAddress configuration
- **Streaming Performance**: Efficient JSON deserialization using Newtonsoft.Json with streaming

## 🚀 Quick Start

### Installation

Add to your project via NuGet Package Manager or .csproj:

```xml
<PackageReference Include="Polly.Core" Version="8.6.2" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
```

## 📖 Basic Usage

### Simple Setup

```csharp
// Create client with base address
var apiClient = new ApiClient(
    baseAddress: new Uri("https://api.example.com"),
    timeout: TimeSpan.FromSeconds(30));

// Make requests using relative paths
var response = await apiClient.GetAsync<User>("users/1");
if (response.Success)
{
    Console.WriteLine($"User: {response.Data.Name}");
}
```

### Dependency Injection Setup

```csharp
// Program.cs - ASP.NET Core
builder.Services.AddSingleton<IApiClient>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ApiClient>>();
    var client = new ApiClient(
        baseAddress: new Uri("https://api.example.com"),
        timeout: TimeSpan.FromSeconds(60),
        logger: logger);
    
    // Add default headers before first use
    client.AddDefaultRequestHeader("User-Agent", "MyApp/1.0");
    client.AddDefaultRequestHeader("Accept-Language", "en-US");
    
    return client;
});
```

## 📋 Detailed Examples

### 1. GET Request with Relative Paths

```csharp
public class UserService
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<UserService> _logger;

    public UserService(IApiClient apiClient, ILogger<UserService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    public async Task<List<User>> GetUsersAsync(string bearerToken, CancellationToken cancellationToken = default)
    {
        // Using relative path with configured BaseAddress
        var response = await _apiClient.GetAsync<List<User>>(
            "users",
            authToken: bearerToken,
            authType: AuthType.Bearer,
            cancellationToken: cancellationToken);

        if (response.Success)
        {
            _logger.LogInformation("Retrieved {Count} users", response.Data?.Count ?? 0);
            return response.Data ?? new List<User>();
        }

        _logger.LogError("Failed to retrieve users: {Error}", response.ErrorMessage);
        throw new HttpRequestException($"Failed to get users: {response.ErrorMessage}");
    }

    public async Task<User> GetUserByIdAsync(int userId)
    {
        // Absolute URI also supported
        var response = await _apiClient.GetAsync<User>(
            new Uri($"https://api.example.com/users/{userId}"));

        return response.Data!;
    }
}
```

### 2. Using Custom Headers Per Request

```csharp
public class ApiService
{
    private readonly IApiClient _apiClient;

    public ApiService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // Example 1: Request-specific tracing header
    public async Task<Order> GetOrderAsync(string orderId, string traceId)
    {
        var customHeaders = new Dictionary<string, string>
        {
            { "X-Trace-Id", traceId },
            { "X-Request-Source", "Mobile-App" }
        };

        var response = await _apiClient.GetAsync<Order>(
            new Uri($"https://api.orders.com/orders/{orderId}"),
            customHeaders: customHeaders);

        return response.Data!;
    }

    // Example 2: API versioning with custom header
    public async Task<Product> GetProductV2Async(int productId)
    {
        var headers = new Dictionary<string, string>
        {
            { "X-API-Version", "2.0" },
            { "X-Feature-Flags", "new-pricing,bulk-discount" }
        };

        var response = await _apiClient.GetAsync<Product>(
            $"products/{productId}",
            customHeaders: headers);

        return response.Data!;
    }

    // Example 3: Combining authentication with custom headers
    public async Task<Report> GenerateReportAsync(string apiKey, string reportType, string format)
    {
        var customHeaders = new Dictionary<string, string>
        {
            { "X-Report-Type", reportType },
            { "X-Output-Format", format },
            { "X-Request-Priority", "high" }
        };

        var response = await _apiClient.GetAsync<Report>(
            new Uri("https://api.analytics.com/reports/generate"),
            authToken: apiKey,
            authType: AuthType.ApiKey,
            customHeaders: customHeaders);

        return response.Data!;
    }
}

public record Order(string Id, decimal Total, DateTime CreatedAt);
public record Product(int Id, string Name, decimal Price);
public record Report(string Id, byte[] Data, string Format);
```

### 3. POST Request with Custom Headers

```csharp
public class PaymentService
{
    private readonly IApiClient _apiClient;

    public PaymentService(IApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task<PaymentResult> ProcessPaymentAsync(PaymentRequest payment, string idempotencyKey)
    {
        // Idempotency key prevents duplicate charges
        var customHeaders = new Dictionary<string, string>
        {
            { "X-Idempotency-Key", idempotencyKey },
            { "X-Client-Version", "1.2.3" },
            { "X-Device-Id", Environment.MachineName }
        };

        var response = await _apiClient.PostAsync<PaymentRequest, PaymentResult>(
            new Uri("https://api.payments.com/v1/charge"),
            payload: payment,
            authToken: "sk_live_xyz123",
            authType: AuthType.ApiKey,
            customHeaders: customHeaders);

        if (!response.Success)
        {
            throw new PaymentException($"Payment failed: {response.ErrorMessage}");
        }

        return response.Data!;
    }
}

public record PaymentRequest(decimal Amount, string Currency, string CardToken);
public record PaymentResult(string TransactionId, string Status, DateTime ProcessedAt);
public class PaymentException : Exception 
{ 
    public PaymentException(string message) : base(message) { } 
}
```

### 4. Advanced Configuration with Default and Custom Headers

```csharp
public class EnterpriseApiClient
{
    private readonly IApiClient _apiClient;

    public EnterpriseApiClient(ILogger<ApiClient> logger, string environment)
    {
        _apiClient = new ApiClient(
            baseAddress: new Uri("https://api.enterprise.com"),
            timeout: TimeSpan.FromSeconds(120),
            logger: logger);

        // Set default headers that apply to ALL requests
        _apiClient.AddDefaultRequestHeader("X-Environment", environment);
        _apiClient.AddDefaultRequestHeader("X-Client-Type", "EnterpriseClient");
        _apiClient.AddDefaultRequestHeader("User-Agent", "EnterpriseApp/2.0");
    }

    public async Task<Customer> CreateCustomerAsync(
        CreateCustomerRequest request, 
        string apiKey, 
        string correlationId)
    {
        // These custom headers are ONLY for this specific request
        var requestHeaders = new Dictionary<string, string>
        {
            { "X-Correlation-Id", correlationId },
            { "X-Operation", "CreateCustomer" },
            { "X-Request-Timestamp", DateTime.UtcNow.ToString("o") }
        };

        var response = await _apiClient.PostAsync<CreateCustomerRequest, Customer>(
            new Uri("customers", UriKind.Relative),
            payload: request,
            authToken: apiKey,
            authType: AuthType.ApiKey,
            customHeaders: requestHeaders);

        return response.Data!;
    }

    // Headers can be built dynamically based on context
    public async Task<List<Transaction>> GetTransactionsAsync(
        string accountId, 
        string userId,
        TransactionQueryOptions options)
    {
        var headers = new Dictionary<string, string>
        {
            { "X-User-Id", userId },
            { "X-Account-Id", accountId }
        };

        // Add conditional headers based on options
        if (options.IncludeMetadata)
        {
            headers["X-Include-Metadata"] = "true";
        }

        if (options.PageSize.HasValue)
        {
            headers["X-Page-Size"] = options.PageSize.Value.ToString();
        }

        var response = await _apiClient.GetAsync<List<Transaction>>(
            $"accounts/{accountId}/transactions",
            customHeaders: headers);

        return response.Data ?? new List<Transaction>();
    }
}

public record CreateCustomerRequest(string Name, string Email);
public record Customer(string Id, string Name, string Email);
public record Transaction(string Id, decimal Amount, DateTime Date);
public record TransactionQueryOptions(bool IncludeMetadata, int? PageSize);
```

## 🔧 Configuration Options

### Authentication Types

```csharp
// Bearer Token (JWT)
await apiClient.GetAsync<User>(url, authToken: "eyJhbGci...", authType: AuthType.Bearer);

// Basic Authentication (Base64 encoded username:password)
var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
await apiClient.GetAsync<User>(url, authToken: credentials, authType: AuthType.Basic);

// API Key (X-API-Key header)
await apiClient.GetAsync<User>(url, authToken: "your-secret-api-key", authType: AuthType.ApiKey);

// No authentication
await apiClient.GetAsync<User>(url);
```

### Custom JSON Serialization with Newtonsoft.Json

```csharp
var jsonSettings = new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    NullValueHandling = NullValueHandling.Ignore,
    Formatting = Formatting.None,
    DateFormatHandling = DateFormatHandling.IsoDateFormat,
    Converters = new List<JsonConverter> 
    { 
        new StringEnumConverter() 
    }
};

var apiClient = new ApiClient(
    baseAddress: new Uri("https://api.example.com"),
    jsonSettings: jsonSettings);
```

### Advanced Resilience Pipelines with Polly v8

```csharp
// Custom retry pipeline with exponential backoff
var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
{
    MaxRetryAttempts = 3,
    Delay = TimeSpan.FromMilliseconds(500),
    BackoffType = DelayBackoffType.Exponential,
    UseJitter = true,
    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
        .Handle<HttpRequestException>()
        .Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
        .HandleResult(r => r.StatusCode is
            HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests or
            HttpStatusCode.ServiceUnavailable or
            HttpStatusCode.GatewayTimeout),
    OnRetry = args =>
    {
        Console.WriteLine($"Retry {args.AttemptNumber} after {args.RetryDelay}");
        return ValueTask.CompletedTask;
    }
};

var resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(retryOptions)
    .Build();

var apiClient = new ApiClient(
    baseAddress: new Uri("https://api.example.com"),
    resiliencePipeline: resiliencePipeline);
```

### No Retry Pipeline (High-Performance Scenarios)

```csharp
// For scenarios where retries are not desired (e.g., idempotent operations handled elsewhere)
var noRetryPipeline = ApiClient.CreateNoRetryPipeline();

var apiClient = new ApiClient(
    baseAddress: new Uri("https://api.example.com"),
    resiliencePipeline: noRetryPipeline);
```

### Default Light Retry Pipeline

The library includes a sensible default retry pipeline that handles transient failures:

- **Max Retry Attempts**: 2
- **Delay**: 300ms with exponential backoff
- **Jitter**: Enabled to prevent thundering herd
- **Retry Conditions**:
  - `HttpRequestException` (network failures)
  - `TaskCanceledException` when NOT cancelled by user (timeout scenarios)
  - HTTP 408 (Request Timeout)
  - HTTP 429 (Too Many Requests)
  - HTTP 503 (Service Unavailable)
  - HTTP 504 (Gateway Timeout)

## 🛡️ Error Handling

### Response Structure

```csharp
public class ApiResponse<T>
{
    public bool Success { get; set; }           // Operation success status
    public T? Data { get; set; }               // Response data (if successful)
    public string? ErrorMessage { get; set; }  // Error description
    public string? ErrorData { get; set; }     // Raw error response
    public HttpStatusCode StatusCode { get; set; } // HTTP status code
}
```

### Error Handling Patterns

```csharp
var response = await apiClient.GetAsync<User>("users/123");

// Pattern 1: Simple success check
if (response.Success)
{
    ProcessUser(response.Data!);
}
else
{
    _logger.LogError("API call failed: {Error}", response.ErrorMessage);
}

// Pattern 2: Status code specific handling
switch (response.StatusCode)
{
    case HttpStatusCode.OK:
        ProcessUser(response.Data!);
        break;
    case HttpStatusCode.NotFound:
        // Handle user not found
        break;
    case HttpStatusCode.Unauthorized:
        // Refresh token or redirect to login
        break;
    case HttpStatusCode.TooManyRequests:
        // Rate limit exceeded
        break;
    default:
        _logger.LogError("Unexpected error {StatusCode}: {Error}", 
            response.StatusCode, response.ErrorMessage);
        break;
}

// Pattern 3: Timeout detection
if (!response.Success && response.ErrorMessage == "Request timeout")
{
    _logger.LogWarning("Request timed out, consider increasing timeout or checking network");
}

// Pattern 4: Exception-based handling
public async Task<User> GetUserOrThrowAsync(int userId)
{
    var response = await apiClient.GetAsync<User>($"users/{userId}");
    
    return response.Success 
        ? response.Data! 
        : throw new HttpRequestException(
            $"Failed to get user {userId}: {response.ErrorMessage}");
}
```

### Timeout Handling

The library distinguishes between user-initiated cancellation and timeout:

```csharp
try
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var response = await apiClient.GetAsync<Data>("data", cancellationToken: cts.Token);
    
    if (!response.Success)
    {
        if (response.ErrorMessage == "Request timeout")
        {
            // HttpClient timeout exceeded
            Console.WriteLine("Server took too long to respond");
        }
        else if (response.ErrorMessage == "Request cancelled")
        {
            // User cancelled via CancellationToken
            Console.WriteLine("Request was cancelled by user");
        }
    }
}
catch (OperationCanceledException)
{
    // This won't normally occur as cancellation is handled internally
    Console.WriteLine("Operation was cancelled");
}
```

## 🔒 Configuration Safety

### Configuration Lock Mechanism

The ApiClient prevents configuration changes after the first request is sent to ensure thread-safety and predictable behavior:

```csharp
var client = new ApiClient(new Uri("https://api.example.com"));

// ✅ OK - Before first request
client.AddDefaultRequestHeader("X-Custom", "value");
client.SetTimeout(TimeSpan.FromSeconds(30));

// Send first request
await client.GetAsync<Data>("data");

// ❌ THROWS ApiClientConfigurationException - After first request
try
{
    client.SetTimeout(TimeSpan.FromSeconds(60));
}
catch (ApiClientConfigurationException ex)
{
    Console.WriteLine($"Configuration error: {ex.Reason}");
    // Output: Configuration error: TimeoutModificationNotAllowed
}
```

### ApiClientConfigurationException

```csharp
public enum ApiClientConfigurationReason
{
    HeadersModificationNotAllowed,
    BaseAddressModificationNotAllowed,
    TimeoutModificationNotAllowed,
    ClientDisposed
}

// Example handling
try
{
    client.AddDefaultRequestHeader("X-New-Header", "value");
}
catch (ApiClientConfigurationException ex) when 
    (ex.Reason == ApiClientConfigurationReason.HeadersModificationNotAllowed)
{
    // Handle configuration lock
    _logger.LogWarning("Cannot modify headers after requests have been sent");
}
```

## 📝 Logging Integration

The ApiClient integrates with Microsoft.Extensions.Logging:

```csharp
// Configure logging in appsettings.json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "ARSoft.RestApiClient.ApiClient": "Debug"
        }
    }
}

// The ApiClient will automatically log:
// - Request cancellations (Debug level)
// - Request timeouts (Warning level)
// - HTTP errors (Error level)
```

## 🧪 Testing

### Unit Testing with Mock

```csharp
[Test]
public async Task GetAsync_WithCustomHeaders_Success()
{
    // Arrange
    var mockHandler = new Mock<HttpMessageHandler>();
    var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonConvert.SerializeObject(
            new User { Id = 1, Name = "Test User" }))
    };
    
    mockHandler.Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.Is<HttpRequestMessage>(req => 
                req.Headers.Contains("X-Custom-Header")),
            ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(expectedResponse);
    
    var apiClient = new ApiClient(new Uri("https://api.test.com"));
    
    // Act
    var headers = new Dictionary<string, string> 
    { 
        { "X-Custom-Header", "test-value" } 
    };
    var result = await apiClient.GetAsync<User>(
        "users/1",
        customHeaders: headers);
    
    // Assert
    Assert.IsTrue(result.Success);
    Assert.AreEqual("Test User", result.Data!.Name);
}
```

## 📦 Package Information

### Dependencies

- **.NET 6+** (with nullable reference types support)
- **Newtonsoft.Json 13.0.3+**
- **Polly 8.6.2+**
- **Polly.Core 8.6.2+**
- **Microsoft.Extensions.Logging.Abstractions 9.0.0+**

### Performance Benefits

- **Newtonsoft.Json with Streaming**: High-performance JSON deserialization using StreamReader and JsonTextReader
- **Per-Instance HttpClient**: Isolated configuration prevents cross-contamination between different client instances
- **HTTP Connection Pooling**: Efficient connection reuse within each client
- **Built-in Resilience**: Reduces transient failure impact with intelligent Polly v8 retry strategies
- **Memory Efficiency**: Streaming deserialization for large responses with `ResponseHeadersRead`
- **Thread-Safe**: Designed for concurrent requests with proper locking mechanisms
- **Timeout Detection**: Distinguishes between user cancellation and HttpClient timeout for better observability

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 📜 Version History

### v3.3.0 (Current - .NET 6+)
- **Breaking Change**: Each `ApiClient` instance now manages its own `HttpClient` instead of sharing a static instance
- **New Feature**: Added relative path support - `GetAsync<T>(string relativePath)` overload for convenience
- **Performance**: Switched to Newtonsoft.Json with streaming (StreamReader/JsonTextReader) for better memory efficiency
- **Enhanced Resilience**: Improved default retry pipeline with better timeout handling
- **Enhanced Error Handling**: Better distinction between user-initiated cancellation and timeout scenarios
- **New Method**: `CreateNoRetryPipeline()` static method for scenarios requiring no retry logic
- **Improved Timeout Detection**: Separate catch block for `TaskCanceledException` when not user-cancelled
- **Better Logging**: Enhanced logging with URL context in error messages
- **Configuration Flexibility**: Full support for custom `ResiliencePipeline<HttpResponseMessage>` injection

### v3.2.0
- **New Feature**: Added `customHeaders` parameter to all HTTP methods for request-specific headers
- Enhanced header flexibility with per-request custom header support
- Improved examples demonstrating custom header usage patterns
- Added comprehensive documentation for multi-tenant, webhook, and rate-limiting scenarios
- Updated all method signatures to support `Dictionary<string, string>? customHeaders`

### v3.1.1
- **Breaking Change**: Removed constructor parameter for external `HttpClient`
- Added internal shared `HttpClient` managed by library
- Added `ApiClientConfigurationException` for post-start configuration changes
- Improved concurrency and memory safety
- Enhanced XML documentation for all public members
- Internal locking for thread-safe configuration state

### v3.1.0
- **New Feature**: Custom authentication type support
- Added `CustomAuthInfo` record for flexible header configuration
- Enhanced authentication flexibility

### v3.0.4
- JsonSerializerOptions default settings improved
- Constructor summary comments added for better IntelliSense support

### v3.0.0
- **Breaking Changes**: Migrated to Polly v8 with ResiliencePipeline
- Updated to use modern Polly.Core
- Improved nullable reference types support
- Enhanced error handling and logging
- Added proper resource disposal support
- Simplified configuration with default retry strategies

### Migration Notes

#### From v3.2.0 to v3.3.0
- **IMPORTANT**: `ApiClient` no longer shares a static `HttpClient`. Each instance has its own.
- **Impact**: If you were relying on shared configuration across instances, you'll need to adjust your code.
- **Recommendation**: Use dependency injection with singleton `ApiClient` instances for each base URL.
- **New Feature**: Can now use relative paths: `GetAsync<T>("users/1")` instead of always using full URIs.
- **JSON Library**: Now uses Newtonsoft.Json instead of System.Text.Json (more stable for complex scenarios).

Example migration:
```csharp
// Before (v3.2.0) - shared static HttpClient
var client1 = new ApiClient(timeout: TimeSpan.FromSeconds(30));
var client2 = new ApiClient(timeout: TimeSpan.FromSeconds(15));
// client2 would affect client1's timeout ❌

// After (v3.3.0) - isolated HttpClients
var client1 = new ApiClient(timeout: TimeSpan.FromSeconds(30));
var client2 = new ApiClient(timeout: TimeSpan.FromSeconds(15));
// Each has independent configuration ✅
```

#### From v3.1.x to v3.2.0
- Replace `CustomAuthInfo` usage with `customHeaders` dictionary
- Old: `new CustomAuthInfo { HeaderName = "token", HeaderValue = "abc" }`
- New: `customHeaders: new Dictionary<string, string> { { "token", "abc" } }`

#### From v3.0.x to v3.1.x
- Remove external `HttpClient` instantiation
- Use new constructor: `new ApiClient(baseAddress, timeout, logger)`
- Configuration methods must be called before first request

#### From v2.x to v3.0.0
- Replace `IAsyncPolicy<HttpResponseMessage>` with `ResiliencePipeline<HttpResponseMessage>`
- Update policy creation to use `ResiliencePipelineBuilder<T>`
- Use new `RetryStrategyOptions` configuration

### Key Improvements in v3.3.0
- **Isolated Configuration**: Each ApiClient instance fully independent with its own HttpClient
- **Better Resource Management**: Proper disposal of per-instance HttpClient
- **Enhanced Performance**: Streaming JSON deserialization with Newtonsoft.Json
- **Improved Developer Experience**: Relative path support for cleaner code
- **Better Observability**: Enhanced timeout detection and logging with request context
- **More Flexible**: Easy to create multiple clients with different configurations without interference