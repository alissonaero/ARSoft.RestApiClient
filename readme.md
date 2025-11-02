# ApiClient Library for .NET Core and .NET 5+ applications

A modern, resilient HTTP API client library for .NET Core and .NET 5+ applications, providing clean, async HTTP operations with built-in retry policies using Polly v8, comprehensive error handling, and structured logging support. Optimized for high-performance API consumption in cross-platform and cloud-native environments.

## 🎯 Features

- **Modern .NET Support**: Built for .NET 6+ with nullable reference types
- **Polly v8 Integration**: Built-in resilience pipelines with exponential backoff and jitter
- **Structured Logging**: ILogger integration for comprehensive request/response logging
- **Multiple Auth Types**: Bearer, Basic, and API Key authentication support
- **Clean Architecture**: SOLID principles with dependency injection support
- **Comprehensive Error Handling**: Detailed error responses with HTTP status codes
- **Async/Await**: Full asynchronous operation support with cancellation tokens
- **Resource Management**: Proper disposal support with optional HttpClient disposal

## 🚀 Quick Start

### Installation

Add to your project via NuGet Package Manager or .csproj:

```xml
<PackageReference Include="Polly.Core" Version="8.6.2" />
<PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.8" />
```

## 📖 Dependency Injection Setup

### ASP.NET Core Integration

```csharp
// Program.cs
builder.Services.AddHttpClient<IApiClient, ApiClient>();

// Custom configuration
builder.Services.AddScoped<IApiClient>(provider =>
{
    var httpClient = provider.GetRequiredService<HttpClient>();
    var logger = provider.GetRequiredService<ILogger<ApiClient>>();
    
    // Optional: Custom JSON options
    var jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    // Optional: Custom retry pipeline
    var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
    {
        MaxRetryAttempts = 5,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true
    };
    
    var retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
        .AddRetry(retryOptions)
        .Build();
    
    return new ApiClient(httpClient, logger, jsonOptions, retryPipeline);
});
```

### Console Application Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();

// Add logging
services.AddLogging(builder => builder.AddConsole());

// Add HttpClient
services.AddHttpClient();

// Add ApiClient
services.AddScoped<IApiClient, ApiClient>();

var serviceProvider = services.BuildServiceProvider();
var apiClient = serviceProvider.GetRequiredService<IApiClient>();
```

## 📋 Detailed Examples

### 1. GET Request with Authentication

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
        var response = await _apiClient.GetAsync<List<User>>(
            new Uri("https://api.example.com/users"),
            bearerToken,
            AuthType.Bearer,
            cancellationToken);

        if (response.Success)
        {
            _logger.LogInformation("Retrieved {Count} users", response.Data?.Count ?? 0);
            return response.Data ?? new List<User>();
        }

        _logger.LogError("Failed to retrieve users: {Error}", response.ErrorMessage);
        throw new HttpRequestException($"Failed to get users: {response.ErrorMessage}");
    }
}
```

### 2. POST Request with Custom Configuration

```csharp
public class ProductService
{
    private readonly IApiClient _apiClient;

    public ProductService(ILogger<ApiClient> logger)
    {
        var httpClient = new HttpClient();
        
        // Custom JSON options
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        // Custom retry pipeline with more aggressive retries
        var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(r => !r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.TooManyRequests),
            MaxRetryAttempts = 5,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true
        };

        var retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retryOptions)
            .Build();

        _apiClient = new ApiClient(httpClient, logger, jsonOptions, retryPipeline, disposeHttpClient: true);
    }

    public async Task<Product> CreateProductAsync(CreateProductRequest request, string apiKey)
    {
        var response = await _apiClient.PostAsync<CreateProductRequest, Product>(
            new Uri("https://api.inventory.com/products"),
            request,
            apiKey,
            AuthType.ApiKey);

        if (!response.Success)
        {
            throw new InvalidOperationException($"Product creation failed: {response.ErrorMessage}");
        }

        return response.Data!;
    }
}

public record CreateProductRequest(string Name, decimal Price, string Category);
public record Product(int Id, string Name, decimal Price, string Category, DateTime CreatedAt);
```

### 3. Advanced Configuration with Multiple Resilience Strategies

```csharp
public class ConfigurableApiClient
{
    public static IApiClient CreateClient(ILogger<ApiClient> logger, ApiClientConfig config)
    {
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
            BaseAddress = new Uri(config.BaseUrl)
        };

        // Add default headers
        httpClient.DefaultRequestHeaders.Add("User-Agent", "MyApp/1.0");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US");

        // Build comprehensive resilience pipeline
        var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .Handle<TaskCanceledException>()
                .HandleResult(r => !r.IsSuccessStatusCode && 
                    (r.StatusCode == HttpStatusCode.TooManyRequests || 
                     r.StatusCode >= HttpStatusCode.InternalServerError)),
            MaxRetryAttempts = config.RetryCount,
            Delay = TimeSpan.FromSeconds(1),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = true,
            OnRetry = args =>
            {
                logger.LogWarning("Retry {AttemptNumber} after {Delay}ms due to {Exception}", 
                    args.AttemptNumber, 
                    args.RetryDelay.TotalMilliseconds,
                    args.Outcome.Exception?.GetType().Name ?? args.Outcome.Result?.StatusCode.ToString());
                return ValueTask.CompletedTask;
            }
        };

        var circuitBreakerOptions = new CircuitBreakerStrategyOptions<HttpResponseMessage>
        {
            ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                .Handle<HttpRequestException>()
                .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError),
            FailureRatio = 0.5,
            SamplingDuration = TimeSpan.FromSeconds(30),
            MinimumThroughput = 10,
            BreakDuration = TimeSpan.FromSeconds(60),
            OnOpened = args =>
            {
                logger.LogWarning("Circuit breaker opened");
                return ValueTask.CompletedTask;
            },
            OnClosed = args =>
            {
                logger.LogInformation("Circuit breaker closed");
                return ValueTask.CompletedTask;
            }
        };

        var resiliencePipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(retryOptions)
            .AddCircuitBreaker(circuitBreakerOptions)
            .Build();

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = config.UseSnakeCase ? JsonNamingPolicy.SnakeCaseLower : JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        return new ApiClient(httpClient, logger, jsonOptions, resiliencePipeline, disposeHttpClient: true);
    }
}

public record ApiClientConfig
{
    public string BaseUrl { get; init; } = "";
    public int TimeoutSeconds { get; init; } = 30;
    public int RetryCount { get; init; } = 3;
    public bool UseSnakeCase { get; init; } = false;
}
```

### 4. ASP.NET Core Web API Integration

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IApiClient apiClient, ILogger<UsersController> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<User>>> GetUsers(CancellationToken cancellationToken)
    {
        try
        {
            var authToken = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            
            var response = await _apiClient.GetAsync<List<User>>(
                new Uri("https://external-api.com/users"),
                authToken,
                AuthType.Bearer,
                cancellationToken);

            if (response.Success)
            {
                return Ok(response.Data);
            }

            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => Unauthorized(),
                HttpStatusCode.Forbidden => Forbid(),
                HttpStatusCode.NotFound => NotFound(),
                _ => StatusCode(500, response.ErrorMessage)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching users");
            return StatusCode(500, "An error occurred while fetching users");
        }
    }

    [HttpPost]
    public async Task<ActionResult<User>> CreateUser([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var authToken = Request.Headers.Authorization.ToString().Replace("Bearer ", "");
            
            var response = await _apiClient.PostAsync<CreateUserRequest, User>(
                new Uri("https://external-api.com/users"),
                request,
                authToken,
                AuthType.Bearer,
                cancellationToken);

            if (response.Success)
            {
                return CreatedAtAction(nameof(GetUsers), new { id = response.Data!.Id }, response.Data);
            }

            return StatusCode((int)response.StatusCode, response.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user");
            return StatusCode(500, "An error occurred while creating the user");
        }
    }
}
```

### 5. Background Service with Periodic API Calls

```csharp
public class DataSyncBackgroundService : BackgroundService
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<DataSyncBackgroundService> _logger;
    private readonly IConfiguration _configuration;

    public DataSyncBackgroundService(
        IApiClient apiClient,
        ILogger<DataSyncBackgroundService> logger,
        IConfiguration configuration)
    {
        _apiClient = apiClient;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue<int>("DataSync:IntervalMinutes", 5);
        var apiKey = _configuration["DataSync:ApiKey"];

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting data synchronization");

                await SyncDataAsync(apiKey!, stoppingToken);

                _logger.LogInformation("Data synchronization completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during data synchronization");
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task SyncDataAsync(string apiKey, CancellationToken cancellationToken)
    {
        // Sync multiple endpoints in parallel
        var tasks = new[]
        {
            SyncEndpointAsync<Customer>("customers", apiKey, cancellationToken),
            SyncEndpointAsync<Product>("products", apiKey, cancellationToken),
            SyncEndpointAsync<Order>("orders", apiKey, cancellationToken)
        };

        await Task.WhenAll(tasks);
    }

    private async Task SyncEndpointAsync<T>(string endpoint, string apiKey, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _apiClient.GetAsync<List<T>>(
                new Uri($"https://api.example.com/{endpoint}"),
                apiKey,
                AuthType.ApiKey,
                cancellationToken);

            if (response.Success)
            {
                _logger.LogInformation("Synced {Count} {Endpoint} records", response.Data?.Count ?? 0, endpoint);
                // Process and save data here
            }
            else
            {
                _logger.LogWarning("Failed to sync {Endpoint}: {Error}", endpoint, response.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing {Endpoint}", endpoint);
        }
    }
}

// Register in Program.cs
builder.Services.AddHostedService<DataSyncBackgroundService>();
```

## 🔧 Configuration Options

### Authentication Types

```csharp
// Bearer Token (JWT)
await apiClient.GetAsync<User>(url, "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...", AuthType.Bearer);

// Basic Authentication (Base64 encoded username:password)
var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
await apiClient.GetAsync<User>(url, credentials, AuthType.Basic);

// API Key (X-API-Key header)
await apiClient.GetAsync<User>(url, "your-secret-api-key", AuthType.ApiKey);

// No authentication
await apiClient.GetAsync<User>(url);
```

### Custom JSON Serialization

```csharp
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Default behavior
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    WriteIndented = false, // Compact JSON for better performance
    Converters = { new JsonStringEnumConverter() } // Enum as strings
};

var apiClient = new ApiClient(httpClient, logger, jsonOptions);
```

### Advanced Resilience Pipelines with Polly v8

```csharp
// Simple retry with exponential backoff
var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
{
    MaxRetryAttempts = 3,
    Delay = TimeSpan.FromSeconds(1),
    BackoffType = DelayBackoffType.Exponential,
    UseJitter = true
};

var retryPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(retryOptions)
    .Build();

// Advanced retry with custom conditions
var advancedRetryOptions = new RetryStrategyOptions<HttpResponseMessage>
{
    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
        .Handle<HttpRequestException>()
        .Handle<TaskCanceledException>()
        .HandleResult(r => !r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.TooManyRequests),
    MaxRetryAttempts = 5,
    Delay = TimeSpan.FromSeconds(2),
    BackoffType = DelayBackoffType.Exponential,
    UseJitter = true,
    OnRetry = args =>
    {
        Console.WriteLine($"Retry {args.AttemptNumber} after {args.RetryDelay}");
        return ValueTask.CompletedTask;
    }
};

// Circuit breaker pattern
var circuitBreakerOptions = new CircuitBreakerStrategyOptions<HttpResponseMessage>
{
    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
        .Handle<HttpRequestException>()
        .HandleResult(r => r.StatusCode >= HttpStatusCode.InternalServerError),
    FailureRatio = 0.3, // 30% failure rate
    SamplingDuration = TimeSpan.FromSeconds(30),
    MinimumThroughput = 10,
    BreakDuration = TimeSpan.FromSeconds(60)
};

// Combine multiple strategies
var combinedPipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
    .AddRetry(advancedRetryOptions)
    .AddCircuitBreaker(circuitBreakerOptions)
    .AddTimeout(TimeSpan.FromSeconds(30)) // Overall timeout
    .Build();

var apiClient = new ApiClient(httpClient, logger, jsonOptions, combinedPipeline);
```

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
var response = await apiClient.GetAsync<User>(userUrl);

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
        // Implement backoff (handled by retry policy)
        break;
    default:
        _logger.LogError("Unexpected error {StatusCode}: {Error}", 
            response.StatusCode, response.ErrorMessage);
        break;
}

// Pattern 3: Exception-based handling
public async Task<User> GetUserOrThrowAsync(int userId)
{
    var response = await apiClient.GetAsync<User>(new Uri($"https://api.example.com/users/{userId}"));
    
    return response.Success 
        ? response.Data! 
        : throw new HttpRequestException($"Failed to get user {userId}: {response.ErrorMessage}");
}
```

## 📝 Logging Integration

The ApiClient integrates with Microsoft.Extensions.Logging for comprehensive request/response logging:

```csharp
// The ApiClient automatically logs errors during HTTP requests
// Configure logging levels in appsettings.json
{
    "Logging": {
        "LogLevel": {
            "Default": "Information",
            "ARSoft.ApiClient.ApiClient": "Debug"
        }
    }
}
```

## 🧪 Testing

### Unit Testing with Mock

```csharp
[Test]
public async Task GetAsync_Success_ReturnsData()
{
    // Arrange
    var mockHandler = new Mock<HttpMessageHandler>();
    var mockLogger = new Mock<ILogger<ApiClient>>();
    
    var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new User { Id = 1, Name = "Test User" }))
    };
    
    mockHandler.Protected()
        .Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
        .ReturnsAsync(expectedResponse);
    
    var httpClient = new HttpClient(mockHandler.Object);
    var apiClient = new ApiClient(httpClient, mockLogger.Object, disposeHttpClient: true);
    
    // Act
    var result = await apiClient.GetAsync<User>(new Uri("https://api.test.com/users/1"));
    
    // Assert
    Assert.IsTrue(result.Success);
    Assert.AreEqual("Test User", result.Data!.Name);
}
```

### Integration Testing

```csharp
[TestFixture]
public class ApiClientIntegrationTests
{
    private IApiClient _apiClient = null!;

    [SetUp]
    public void Setup()
    {
        var httpClient = new HttpClient();
        var logger = new Mock<ILogger<ApiClient>>().Object;
        _apiClient = new ApiClient(httpClient, logger, disposeHttpClient: true);
    }

    [Test]
    public async Task GetAsync_RealApi_Success()
    {
        // Test against a real API endpoint
        var response = await _apiClient.GetAsync<JsonElement>(
            new Uri("https://jsonplaceholder.typicode.com/posts/1"));

        Assert.IsTrue(response.Success);
        Assert.IsNotNull(response.Data);
    }

    [TearDown]
    public void TearDown()
    {
        _apiClient?.Dispose();
    }
}
```

## 📦 Package Information

### Dependencies

- **.NET 6+** (with nullable reference types support)
- **System.Text.Json 8.0.0+**
- **Polly 8.6.2+**
- **Polly.Core 8.6.2+**
- **Microsoft.Extensions.Logging.Abstractions 9.0.0+**

### Performance Benefits

- **System.Text.Json**: High-performance JSON serialization
- **HTTP Connection Pooling**: Efficient connection reuse through HttpClient
- **Built-in Resilience**: Reduces transient failure impact with Polly v8
- **Memory Efficiency**: Minimal allocations with modern .NET patterns

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 📄 Version History

### v3.0.4 (Current - .NET 6+)
- JsonSerializerOptions default settings improved to suit most use cases
- Constructor summary comments added for better IntelliSense support

### v3.0.0  
- **Breaking Changes**: Migrated to Polly v8 with ResiliencePipeline
- Updated to use modern Polly.Core instead of legacy Polly extensions
- Improved nullable reference types support
- Enhanced error handling and logging
- Added proper resource disposal support
- Simplified configuration with default retry strategies
- Updated to latest System.Text.Json patterns

### Migration from v2.0 (Polly v7)
- Replace `IAsyncPolicy<HttpResponseMessage>` with `ResiliencePipeline<HttpResponseMessage>`
- Update policy creation to use `ResiliencePipelineBuilder<T>` instead of `HttpPolicyExtensions`
- Use new `RetryStrategyOptions` and `CircuitBreakerStrategyOptions` configuration
- Replace callback delegates with `ValueTask`-returning delegates
- Update exception handling predicates using `PredicateBuilder<T>`

### Key Improvements in v3.0
- **Better Performance**: Polly v8 offers significant performance improvements
- **Modern Patterns**: Aligned with current .NET and Polly best practices
- **Enhanced Configurability**: More granular control over resilience strategies
- **Improved Diagnostics**: Better error reporting and logging integration