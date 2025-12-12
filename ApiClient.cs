using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARSoft.RestApiClient;

/// <summary>
/// Specifies the authentication type for requests.
/// </summary>
public enum AuthType
{
	/// <summary>No authentication.</summary>
	None,
	/// <summary>Bearer token authentication.</summary>
	Bearer,
	/// <summary>Basic authentication.</summary>
	Basic,
	/// <summary>API key authentication.</summary>
	ApiKey
}

/// <summary>
/// Represents a structured response from an API call.
/// </summary>
/// <typeparam name="T">Type of the data expected from the response body.</typeparam>
public class ApiResponse<T>
{
	/// <summary>Indicates whether the API call succeeded.</summary>
	public bool Success { get; set; }
	/// <summary>The deserialized response data if successful.</summary>
	public T? Data { get; set; }
	/// <summary>A message describing the error if the call failed.</summary>
	public string? ErrorMessage { get; set; }
	/// <summary>Raw response content in case of errors.</summary>
	public string? ErrorData { get; set; }
	/// <summary>The HTTP status code returned by the API.</summary>
	public HttpStatusCode StatusCode { get; set; }
}

/// <summary>
/// Defines the interface for REST API clients.
/// </summary>
public interface IApiClient
{
	/// <summary>Performs a GET request against the provided URI.</summary>
	Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	/// <summary>Performs a GET request using the client's base address.</summary>
	Task<ApiResponse<TResponse>> GetAsync<TResponse>(string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	/// <summary>Performs a POST request with a payload.</summary>
	Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	/// <summary>Performs a PUT request with a payload.</summary>
	Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	/// <summary>Performs a DELETE request against the provided URI.</summary>
	Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	/// <summary>Performs a PATCH request with a payload.</summary>
	Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents reasons why configuration changes to ApiClient are not allowed.
/// </summary>
public enum ApiClientConfigurationReason
{
	/// <summary>Modification of DefaultRequestHeaders is not allowed after first request.</summary>
	HeadersModificationNotAllowed,
	/// <summary>BaseAddress modification is not allowed after first request.</summary>
	BaseAddressModificationNotAllowed,
	/// <summary>Timeout modification is not allowed after first request.</summary>
	TimeoutModificationNotAllowed,
	/// <summary>Operation attempted on a disposed client.</summary>
	ClientDisposed
}

/// <summary>
/// Exception thrown when ApiClient configuration changes are attempted after use.
/// </summary>
/// <remarks>Initializes a new instance of the ApiClientConfigurationException class.</remarks>
public class ApiClientConfigurationException(ApiClientConfigurationReason reason, string message) : InvalidOperationException(message)
{
	/// <summary>The reason why configuration is not allowed.</summary>
	public ApiClientConfigurationReason Reason { get; } = reason;
}

/// <summary>
/// A resilient, thread-safe API client that owns and reuses a single internal HttpClient instance.
/// Prevents modification of sensitive configuration after the first request is made.
/// </summary>
public sealed class ApiClient : IApiClient, IDisposable
{
	private static readonly HttpClient _sharedHttpClient = CreateDefaultHttpClient();
	private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ILogger<ApiClient>? _logger;
	private readonly object _stateLock = new object();
	private bool _hasSentRequests = false;
	private bool _disposed = false;

	/// <summary>
	/// Initializes a new instance of the ApiClient with optional configuration parameters.
	/// </summary>
	/// <param name="baseAddress">The base address for HTTP requests (optional).</param>
	/// <param name="timeout">Request timeout duration (optional).</param>
	/// <param name="logger">Optional logger for capturing client activity.</param>
	/// <param name="jsonOptions">JSON serializer options (optional).</param>
	/// <param name="retryPipeline">Optional retry policy pipeline.</param>
	public ApiClient(Uri? baseAddress = null, TimeSpan? timeout = null, ILogger<ApiClient>? logger = null, JsonSerializerOptions? jsonOptions = null, ResiliencePipeline<HttpResponseMessage>? retryPipeline = null)
	{
		_logger = logger;
		_jsonOptions = jsonOptions ?? DefaultJsonOptions();
		_retryPipeline = retryPipeline ?? CreateDefaultRetryPipeline();
		if (baseAddress != null || timeout != null)
		{
			lock (_stateLock)
			{
				if (baseAddress != null)
					_sharedHttpClient.BaseAddress = baseAddress;
				if (timeout != null)
					_sharedHttpClient.Timeout = timeout.Value;
			}
		}
	}

	/// <summary>Adds a default header before the first request is sent.</summary>
	public void AddDefaultRequestHeader(string name, string value)
	{
		EnsureNotDisposed();
		lock (_stateLock)
		{
			if (_hasSentRequests)
				throw new ApiClientConfigurationException(ApiClientConfigurationReason.HeadersModificationNotAllowed, $"Cannot modify DefaultRequestHeaders after sending requests.");

			if (_sharedHttpClient.DefaultRequestHeaders.Contains(name))
				_sharedHttpClient.DefaultRequestHeaders.Remove(name);

			_sharedHttpClient.DefaultRequestHeaders.Add(name, value);
		}
	}

	/// <summary>Sets the base address before any request is sent.</summary>
	public void SetBaseAddress(Uri baseAddress)
	{
		EnsureNotDisposed();
		lock (_stateLock)
		{
			if (_hasSentRequests)
				throw new ApiClientConfigurationException(ApiClientConfigurationReason.BaseAddressModificationNotAllowed, "Cannot change BaseAddress after sending requests.");
			_sharedHttpClient.BaseAddress = baseAddress;
		}
	}

	/// <summary>Sets the timeout before any request is sent.</summary>
	public void SetTimeout(TimeSpan timeout)
	{
		EnsureNotDisposed();
		lock (_stateLock)
		{
			if (_hasSentRequests)
				throw new ApiClientConfigurationException(ApiClientConfigurationReason.TimeoutModificationNotAllowed, "Cannot change Timeout after sending requests.");
			_sharedHttpClient.Timeout = timeout;
		}
	}

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		=> await SendAsync<object, TResponse>(HttpMethod.Get, url, null, authToken, authType, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		=> await SendAsync<object, TResponse>(HttpMethod.Get, _sharedHttpClient.BaseAddress ?? throw new InvalidOperationException("This overload requires BaseAddress configured."), null, authToken, authType, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		=> await SendAsync<TRequest, TResponse>(HttpMethod.Post, url, payload, authToken, authType, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		=> await SendAsync<TRequest, TResponse>(HttpMethod.Put, url, payload, authToken, authType, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		=> await SendAsync<object, TResponse>(HttpMethod.Delete, url, null, authToken, authType, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default)
		=> await SendAsync<TRequest, TResponse>(HttpMethod.Patch, url, payload, authToken, authType, cancellationToken).ConfigureAwait(false);

	/// <summary>Releases resources associated with the ApiClient instance (does not dispose the shared HttpClient).</summary>
	public void Dispose()
	{
		_disposed = true;
	}

	//---- Private methods ----
	private async Task<ApiResponse<TResponse>> SendAsync<TRequest, TResponse>(HttpMethod method, Uri url, TRequest? payload, string? authToken, AuthType authType, CancellationToken cancellationToken)
	{
		EnsureNotDisposed();
		var result = new ApiResponse<TResponse>();
		HttpResponseMessage? httpResponse = null;
		try
		{
			lock (_stateLock)
			{
				_hasSentRequests = true;
			}
			httpResponse = await _retryPipeline.ExecuteAsync(async (ctx, token) =>
			{
				using var request = new HttpRequestMessage(method, url);
				SetHeaders(request, authToken, authType);
				SetContent(request, payload, method);
				return await _sharedHttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
			}, cancellationToken).ConfigureAwait(false);

			result.StatusCode = httpResponse.StatusCode;

			if (!httpResponse.IsSuccessStatusCode)
			{
				result.Success = false;
				result.ErrorMessage = $"HTTP Error {(int)httpResponse.StatusCode}";
				result.ErrorData = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				return result;
			}
			if (typeof(TResponse) == typeof(string))
			{
				var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
				result.Data = (TResponse)(object)responseJson!;
			}
			else
			{
				await using var stream = await httpResponse.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
				var deserialized = await JsonSerializer.DeserializeAsync<TResponse>(stream, _jsonOptions, cancellationToken).ConfigureAwait(false);
				result.Data = deserialized;
			}
			result.Success = true;
		}
		catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
		{
			_logger?.LogDebug(ex, "Request cancelled by caller.");
			result.Success = false;
			result.ErrorMessage = "Request cancelled";
		}
		catch (Exception ex)
		{
			_logger?.LogError(ex, "Error during HTTP request");
			result.Success = false;
			result.ErrorMessage = ex.Message;
		}
		finally
		{
			httpResponse?.Dispose();
		}
		return result;
	}

	private static void SetHeaders(HttpRequestMessage request, string? authToken, AuthType authType)
	{
		request.Headers.Accept.Clear();
		request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		if (!string.IsNullOrWhiteSpace(authToken))
		{
			switch (authType)
			{
				case AuthType.Bearer:
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", authToken);
					break;
				case AuthType.Basic:
					request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authToken);
					break;
				case AuthType.ApiKey:
					request.Headers.Remove("X-API-Key");
					request.Headers.Add("X-API-Key", authToken);
					break;
			}
		}
	}

	private void SetContent<TRequest>(HttpRequestMessage request, TRequest? payload, HttpMethod method)
	{
		if (payload != null && (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
		{
			var json = JsonSerializer.Serialize(payload, _jsonOptions);
			request.Content = new StringContent(json, Encoding.UTF8, "application/json");
		}
	}

	private static ResiliencePipeline<HttpResponseMessage> CreateDefaultRetryPipeline()
	{
		var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
		{
			ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
				.Handle<HttpRequestException>()
				.Handle<TaskCanceledException>()
				.HandleResult(r => r.StatusCode == HttpStatusCode.RequestTimeout || r.StatusCode == HttpStatusCode.TooManyRequests || (int)r.StatusCode >= 500),
			MaxRetryAttempts = 3,
			Delay = TimeSpan.FromSeconds(1),
			BackoffType = DelayBackoffType.Exponential,
			UseJitter = true
		};
		return new ResiliencePipelineBuilder<HttpResponseMessage>().AddRetry(retryOptions).Build();
	}

	private static JsonSerializerOptions DefaultJsonOptions() => new(JsonSerializerDefaults.Web)
	{
		PropertyNamingPolicy = null,
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	private static HttpClient CreateDefaultHttpClient()
	{
		var client = new HttpClient { Timeout = TimeSpan.FromSeconds(100) };
		client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
		return client;
	}

	private void EnsureNotDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, nameof(ApiClient));
	}
}
