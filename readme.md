# ApiClient Library for .NET Core

A modern, resilient HTTP API client library for .NET Core/5+ applications, providing clean async HTTP operations with built-in retry policies, comprehensive error handling, and structured logging support.

## 🎯 Features

- **Modern .NET Support**: Built for .NET Core 3.1+ and .NET 5+
- **System.Text.Json**: High-performance JSON serialization (no Newtonsoft.Json dependency)
- **Polly Integration**: Built-in retry policies with exponential backoff using modern Polly extensions
- **Structured Logging**: ILogger integration for comprehensive request/response logging
- **Multiple Auth Types**: Bearer, Basic, and API Key authentication support
- **Clean Architecture**: SOLID principles with dependency injection support
- **Comprehensive Error Handling**: Detailed error responses with HTTP status codes
- **Async/Await**: Full asynchronous operation support with cancellation tokens

## 🚀 Quick Start

### Installation

Add to your project via NuGet Package Manager or .csproj:

```xml
<PackageReference Include="System.Text.Json" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Http.Polly" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
```

### Basic Usage

```csharp
using ARSoft.ApiClient;

// Simple usage with default configuration
using var httpClient = new HttpClient();
using var apiClient = new ApiClient(httpClient);

var response = await apiClient.GetAsync<User>(new Uri("https://api.example.com/users/1"));

if (response.Success)
{
    Console.WriteLine($"User: {response.Data.Name}");
}
else
{
    Console.WriteLine($"Error {(int)response.StatusCode}: {response.ErrorMessage}");
}
```

## 📖 Dependency Injection Setup

### ASP.NET Core Integration

```csharp
// Program.cs or Startup.cs
builder.Services.AddHttpClient<IApiClient, ApiClient>()
    .AddPolicyHandler(GetRetryPolicy());

// Custom retry policy
static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .OrResult(msg => (int)msg.StatusCode == 429)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (outcome, timespan, retryCount, context) =>
            {
                Console.WriteLine($"Retry {retryCount} after {timespan}s");
            });
}

// Service registration
builder.Services.AddScoped<IApiClient>(provider =>
{
    var httpClient = provider.GetRequiredService<HttpClient>();
    var logger = provider.GetRequiredService<ILogger<ApiClient>>();
    var retryPolicy = GetRetryPolicy();
    
    return new ApiClient(httpClient, logger, null, retryPolicy);
});
```

### Console Application Setup

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Http;

var services = new ServiceCollection();

// Add logging
services.AddLogging(builder => builder.AddConsole());

// Add HttpClient with Polly
services.AddHttpClient<IApiClient, ApiClient>()
    .AddPolicyHandler(HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

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

### 2. POST Request with Custom JSON Options

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

        _apiClient = new ApiClient(httpClient, logger, jsonOptions);
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

### 3. Advanced Configuration with Custom Policies

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

        // Custom retry policy with circuit breaker
        var retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 3,
                durationOfBreak: TimeSpan.FromSeconds(30))
            .WrapAsync(
                HttpPolicyExtensions
                    .HandleTransientHttpError()
                    .WaitAndRetryAsync(
                        retryCount: config.RetryCount,
                        sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        onRetry: (outcome, timespan, retryCount, context) =>
                        {
                            logger.LogWarning("Retry {RetryCount} after {Delay}s for {Url}",
                                retryCount, timespan.TotalSeconds, context.OperationKey);
                        }));

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = config.UseSnakeCase ? JsonNamingPolicy.SnakeCaseLower : JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        return new ApiClient(httpClient, logger, jsonOptions, retryPolicy, true);
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
```

### Custom JSON Serialization

```csharp
var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // or SnakeCaseLower
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true,
    WriteIndented = true, // For debugging
    Converters = { new JsonStringEnumConverter() } // Enum as strings
};

var apiClient = new ApiClient(httpClient, logger, jsonOptions);
```

### Advanced Retry Policies

```csharp
// Exponential backoff with jitter
var retryPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(msg => (int)msg.StatusCode == 429)
    .WaitAndRetryAsync(
        retryCount: 5,
        sleepDurationProvider: retryAttempt =>
        {
            var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt));
            var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 1000));
            return baseDelay + jitter;
        });

// Circuit breaker pattern
var circuitBreakerPolicy = HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 3,
        durationOfBreak: TimeSpan.FromSeconds(30),
        onBreak: (exception, duration) => 
            Console.WriteLine($"Circuit breaker opened for {duration}"),
        onReset: () => 
            Console.WriteLine("Circuit breaker closed"));

// Combine policies
var combinedPolicy = Policy.WrapAsync(retryPolicy, circuitBreakerPolicy);
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
        // Implement backoff
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

## 🔍 Logging Integration

The ApiClient integrates with Microsoft.Extensions.Logging for comprehensive request/response logging:

```csharp
// Structured logging examples
_logger.LogInformation("API request started for {Method} {Url}", method, url);
_logger.LogWarning("API request failed with status {StatusCode}", response.StatusCode);
_logger.LogError(exception, "Unexpected error during API request");

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
    var mockHttpClient = new Mock<HttpClient>();
    var mockLogger = new Mock<ILogger<ApiClient>>();
    var apiClient = new ApiClient(mockHttpClient.Object, mockLogger.Object);
    
    // Setup mock response
    var expectedUser = new User { Id = 1, Name = "Test User" };
    // ... setup mock behavior
    
    // Act
    var result = await apiClient.GetAsync<User>(new Uri("https://api.test.com/users/1"));
    
    // Assert
    Assert.IsTrue(result.Success);
    Assert.AreEqual(expectedUser.Name, result.Data!.Name);
}
```

### Integration Testing

```csharp
[TestFixture]
public class ApiClientIntegrationTests
{
    private IApiClient _apiClient = null!;
    private HttpClient _httpClient = null!;

    [SetUp]
    public void Setup()
    {
        _httpClient = new HttpClient();
        var logger = new Mock<ILogger<ApiClient>>().Object;
        _apiClient = new ApiClient(_httpClient, logger, disposeHttpClient: true);
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

- **.NET Core 3.1+** or **.NET 5+**
- **System.Text.Json 8.0.0+**
- **Microsoft.Extensions.Http.Polly 8.0.0+**
- **Microsoft.Extensions.Logging 8.0.0+**

### Performance Benefits

- **System.Text.Json**: Up to 2x faster serialization compared to Newtonsoft.Json
- **HTTP Connection Pooling**: Efficient connection reuse
- **Async/Await**: Non-blocking I/O operations
- **Built-in Retry Logic**: Reduces transient failure impact

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📝 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🔄 Version History

### v2.0.0 (.NET Core/5+)
- Migrated to System.Text.Json for improved performance
- Added structured logging support with ILogger
- Updated to modern Polly extensions
- Improved error handling and status code reporting
- Added comprehensive documentation and examples
- Full async/await support with CancellationToken

### Migration from v1.0 (.NET Framework)
- Replace Newtonsoft.Json with System.Text.Json
- Update Polly policy creation using HttpPolicyExtensions
- Add ILogger dependency injection support
- Update JSON serialization options syntax