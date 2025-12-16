using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace ARSoft.RestApiClient;

/// <summary>
/// Specifies the authentication type for API requests.
/// </summary>
public enum AuthType
{
	/// <summary>No authentication.</summary>
	None,
	/// <summary>Bearer token authentication (JWT).</summary>
	Bearer,
	/// <summary>Basic authentication (Base64 encoded credentials).</summary>
	Basic,
	/// <summary>API key authentication (X-API-Key header).</summary>
	ApiKey
}

/// <summary>
/// Represents a structured response from an API call.
/// </summary>
/// <typeparam name="T">The type of data expected in the response body.</typeparam>
public class ApiResponse<T>
{
	/// <summary>Indicates whether the API call succeeded.</summary>
	public bool Success { get; set; }

	/// <summary>The deserialized response data if the request was successful.</summary>
	public T? Data { get; set; }

	/// <summary>A message describing the error if the request failed.</summary>
	public string? ErrorMessage { get; set; }

	/// <summary>Raw response content in case of errors.</summary>
	public string? ErrorData { get; set; }

	/// <summary>The HTTP status code returned by the API.</summary>
	public HttpStatusCode StatusCode { get; set; }
}

/// <summary>
/// Defines the contract for REST API client operations.
/// </summary>
public interface IApiClient
{
	/// <summary>
	/// Performs a GET request against the provided absolute URI.
	/// </summary>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute URI to request.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The type of authentication to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a GET request using a relative path with the client's configured base address.
	/// </summary>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="relativePath">The relative path to append to the base address.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The type of authentication to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	/// <exception cref="InvalidOperationException">Thrown when BaseAddress is not configured.</exception>
	Task<ApiResponse<TResponse>> GetAsync<TResponse>(string relativePath, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a POST request with a payload.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request payload.</typeparam>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute URI to request.</param>
	/// <param name="payload">The request payload to serialize and send.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The type of authentication to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a PUT request with a payload.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request payload.</typeparam>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute URI to request.</param>
	/// <param name="payload">The request payload to serialize and send.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The type of authentication to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a DELETE request against the provided URI.
	/// </summary>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute URI to request.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The type of authentication to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a PATCH request with a payload.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request payload.</typeparam>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute URI to request.</param>
	/// <param name="payload">The request payload to serialize and send.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The type of authentication to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the reason why a configuration change to <see cref="ApiClient"/> is not allowed.
/// </summary>
public enum ApiClientConfigurationReason
{
	/// <summary>Modification of DefaultRequestHeaders is not allowed after the first request.</summary>
	HeadersModificationNotAllowed,

	/// <summary>BaseAddress modification is not allowed after the first request.</summary>
	BaseAddressModificationNotAllowed,

	/// <summary>Timeout modification is not allowed after the first request.</summary>
	TimeoutModificationNotAllowed,

	/// <summary>Operation attempted on a disposed client.</summary>
	ClientDisposed
}

/// <summary>
/// Exception thrown when <see cref="ApiClient"/> configuration changes are attempted after the first request has been sent.
/// </summary>
public class ApiClientConfigurationException : InvalidOperationException
{
	/// <summary>
	/// Gets the reason why the configuration change was not allowed.
	/// </summary>
	public ApiClientConfigurationReason Reason { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiClientConfigurationException"/> class.
	/// </summary>
	/// <param name="reason">The reason for the configuration restriction.</param>
	/// <param name="message">The error message that explains the reason for the exception.</param>
	public ApiClientConfigurationException(ApiClientConfigurationReason reason, string message)
		: base(message)
	{
		Reason = reason;
	}
}

/// <summary>
/// A resilient, thread-safe HTTP API client that manages its own <see cref="HttpClient"/> instance.
/// Each instance is isolated with independent configuration including timeouts, base addresses, and retry policies.
/// Prevents modification of sensitive configuration after the first request is made to ensure consistency.
/// </summary>
public sealed class ApiClient : IApiClient, IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly ResiliencePipeline<HttpResponseMessage> _resiliencePipeline;
	private readonly JsonSerializerSettings _jsonSettings;
	private readonly ILogger<ApiClient>? _logger;
	private readonly object _stateLock = new();
	private bool _hasSentRequests = false;
	private bool _disposed = false;

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiClient"/> class with optional configuration.
	/// Each instance manages its own <see cref="HttpClient"/> for isolated configuration.
	/// </summary>
	/// <param name="baseAddress">The base address for all HTTP requests. Can be null if absolute URIs will always be used.</param>
	/// <param name="timeout">Request timeout duration. Defaults to 30 seconds if not specified.</param>
	/// <param name="logger">Optional logger for capturing client activity and diagnostics.</param>
	/// <param name="jsonSettings">Optional Newtonsoft.Json serializer settings. Uses default settings if not provided.</param>
	/// <param name="resiliencePipeline">Optional Polly resilience pipeline for retry logic. Uses default light retry pipeline if not provided.</param>
	public ApiClient(
		Uri? baseAddress = null,
		TimeSpan? timeout = null,
		ILogger<ApiClient>? logger = null,
		JsonSerializerSettings? jsonSettings = null,
		ResiliencePipeline<HttpResponseMessage>? resiliencePipeline = null)
	{
		_logger = logger;
		_jsonSettings = jsonSettings ?? DefaultJsonOptions();
		_resiliencePipeline = resiliencePipeline ?? CreateLightRetryPipeline();

		_httpClient = new HttpClient
		{
			Timeout = timeout ?? TimeSpan.FromSeconds(30)
		};

		_httpClient.DefaultRequestHeaders.Accept.Add(
			new MediaTypeWithQualityHeaderValue("application/json"));

		if (baseAddress != null)
			_httpClient.BaseAddress = baseAddress;
	}

	/// <summary>
	/// Adds a default header that will be included in all requests made by this client.
	/// This method can only be called before the first request is sent.
	/// </summary>
	/// <param name="name">The name of the header.</param>
	/// <param name="value">The value of the header.</param>
	/// <exception cref="ApiClientConfigurationException">Thrown when attempting to modify headers after the first request has been sent.</exception>
	/// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
	public void AddDefaultRequestHeader(string name, string value)
	{
		EnsureNotDisposed();
		lock (_stateLock)
		{
			if (_hasSentRequests)
				throw new ApiClientConfigurationException(
					ApiClientConfigurationReason.HeadersModificationNotAllowed,
					"Cannot modify DefaultRequestHeaders after sending requests.");

			if (_httpClient.DefaultRequestHeaders.Contains(name))
				_httpClient.DefaultRequestHeaders.Remove(name);

			_httpClient.DefaultRequestHeaders.Add(name, value);
		}
	}

	/// <summary>
	/// Sets the base address for the HTTP client. This method can only be called before the first request is sent.
	/// </summary>
	/// <param name="baseAddress">The base URI for all requests.</param>
	/// <exception cref="ApiClientConfigurationException">Thrown when attempting to modify the base address after the first request has been sent.</exception>
	/// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
	public void SetBaseAddress(Uri baseAddress)
	{
		EnsureNotDisposed();
		lock (_stateLock)
		{
			if (_hasSentRequests)
				throw new ApiClientConfigurationException(
					ApiClientConfigurationReason.BaseAddressModificationNotAllowed,
					"Cannot change BaseAddress after sending requests.");
			_httpClient.BaseAddress = baseAddress;
		}
	}

	/// <summary>
	/// Sets the timeout for the HTTP client. This method can only be called before the first request is sent.
	/// </summary>
	/// <param name="timeout">The timeout duration for requests.</param>
	/// <exception cref="ApiClientConfigurationException">Thrown when attempting to modify the timeout after the first request has been sent.</exception>
	/// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
	public void SetTimeout(TimeSpan timeout)
	{
		EnsureNotDisposed();
		lock (_stateLock)
		{
			if (_hasSentRequests)
				throw new ApiClientConfigurationException(
					ApiClientConfigurationReason.TimeoutModificationNotAllowed,
					"Cannot change Timeout after sending requests.");
			_httpClient.Timeout = timeout;
		}
	}

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(
		string relativePath,
		string? authToken = null,
		AuthType authType = AuthType.None,
		Dictionary<string, string>? customHeaders = null,
		CancellationToken cancellationToken = default)
	{
		if (_httpClient.BaseAddress is null)
			throw new InvalidOperationException("BaseAddress is not configured. Use the overload with absolute Uri or set BaseAddress.");

		var url = new Uri(_httpClient.BaseAddress, relativePath);
		return await GetAsync<TResponse>(url, authToken, authType, customHeaders, cancellationToken);
	}

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(
		Uri url,
		string? authToken = null,
		AuthType authType = AuthType.None,
		Dictionary<string, string>? customHeaders = null,
		CancellationToken cancellationToken = default)
		=> await SendAsync<object, TResponse>(HttpMethod.Get, url, null, authToken, authType, customHeaders, cancellationToken);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(
		Uri url,
		TRequest payload,
		string? authToken = null,
		AuthType authType = AuthType.None,
		Dictionary<string, string>? customHeaders = null,
		CancellationToken cancellationToken = default)
		=> await SendAsync<TRequest, TResponse>(HttpMethod.Post, url, payload, authToken, authType, customHeaders, cancellationToken);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(
		Uri url,
		TRequest payload,
		string? authToken = null,
		AuthType authType = AuthType.None,
		Dictionary<string, string>? customHeaders = null,
		CancellationToken cancellationToken = default)
		=> await SendAsync<TRequest, TResponse>(HttpMethod.Put, url, payload, authToken, authType, customHeaders, cancellationToken);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(
		Uri url,
		string? authToken = null,
		AuthType authType = AuthType.None,
		Dictionary<string, string>? customHeaders = null,
		CancellationToken cancellationToken = default)
		=> await SendAsync<object, TResponse>(HttpMethod.Delete, url, null, authToken, authType, customHeaders, cancellationToken);

	/// <inheritdoc />
	public async Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(
		Uri url,
		TRequest payload,
		string? authToken = null,
		AuthType authType = AuthType.None,
		Dictionary<string, string>? customHeaders = null,
		CancellationToken cancellationToken = default)
		=> await SendAsync<TRequest, TResponse>(HttpMethod.Patch, url, payload, authToken, authType, customHeaders, cancellationToken);

	/// <summary>
	/// Core method that sends HTTP requests with configured resilience policies, authentication, and custom headers.
	/// Handles response deserialization, error handling, and proper resource disposal.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request payload (can be object for requests without a body).</typeparam>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="method">The HTTP method to use.</param>
	/// <param name="url">The target URI.</param>
	/// <param name="payload">The request payload (null for GET/DELETE).</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The type of authentication to use.</param>
	/// <param name="customHeaders">Optional custom headers for this specific request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result, error information, and HTTP status code.</returns>
	private async Task<ApiResponse<TResponse>> SendAsync<TRequest, TResponse>(
		HttpMethod method,
		Uri url,
		TRequest? payload,
		string? authToken,
		AuthType authType,
		Dictionary<string, string>? customHeaders,
		CancellationToken cancellationToken)
	{
		EnsureNotDisposed();
		var result = new ApiResponse<TResponse>();
		HttpResponseMessage? response = null;

		try
		{
			lock (_stateLock)
			{
				_hasSentRequests = true;
			}

			response = await _resiliencePipeline.ExecuteAsync(async token =>
			{
				using var request = new HttpRequestMessage(method, url);
				SetHeaders(request, authToken, authType, customHeaders);
				SetContent(request, payload, method);

				return await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
			}, cancellationToken);

			result.StatusCode = response.StatusCode;

			if (!response.IsSuccessStatusCode)
			{
				result.Success = false;
				result.ErrorMessage = $"HTTP Error {(int)response.StatusCode}";
				result.ErrorData = await response.Content.ReadAsStringAsync(cancellationToken);
				return result;
			}

			// Handle string responses directly without deserialization
			if (typeof(TResponse) == typeof(string))
			{
				var text = await response.Content.ReadAsStringAsync(cancellationToken);
				result.Data = (TResponse)(object)text;
			}
			else
			{
				// Use streaming deserialization for better memory efficiency
				await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
				using var reader = new StreamReader(stream);
				using var jsonReader = new JsonTextReader(reader);
				var serializer = JsonSerializer.Create(_jsonSettings);
				result.Data = serializer.Deserialize<TResponse>(jsonReader);
			}

			result.Success = true;
		}
		catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
		{
			_logger?.LogDebug(ex, "Request cancelled by caller.");
			result.Success = false;
			result.ErrorMessage = "Request cancelled";
		}
		catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
		{
			// This indicates HttpClient timeout, not user-initiated cancellation
			_logger?.LogWarning(ex, "Request timed out.");
			result.Success = false;
			result.ErrorMessage = "Request timeout";
		}
		catch (Exception ex)
		{
			_logger?.LogError(ex, "Error during HTTP request to {Url}", url);
			result.Success = false;
			result.ErrorMessage = ex.Message;
			result.ErrorData = ex.ToString();
		}
		finally
		{
			response?.Dispose();
		}

		return result;
	}

	/// <summary>
	/// Configures HTTP request headers including authentication and custom headers.
	/// </summary>
	/// <param name="request">The HTTP request message to configure.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The type of authentication to apply.</param>
	/// <param name="customHeaders">Optional dictionary of custom headers to add to the request.</param>
	private static void SetHeaders(
		HttpRequestMessage request,
		string? authToken,
		AuthType authType,
		Dictionary<string, string>? customHeaders = null)
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

		if (customHeaders != null)
		{
			foreach (var header in customHeaders)
			{
				request.Headers.Remove(header.Key);
				request.Headers.Add(header.Key, header.Value);
			}
		}
	}

	/// <summary>
	/// Sets the request content by serializing the payload to JSON for POST, PUT, and PATCH requests.
	/// </summary>
	/// <typeparam name="TRequest">The type of the request payload.</typeparam>
	/// <param name="request">The HTTP request message to configure.</param>
	/// <param name="payload">The payload to serialize.</param>
	/// <param name="method">The HTTP method being used.</param>
	private void SetContent<TRequest>(
		HttpRequestMessage request,
		TRequest? payload,
		HttpMethod method)
	{
		if (payload != null && (method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Patch))
		{
			var json = JsonConvert.SerializeObject(payload, _jsonSettings);
			request.Content = new StringContent(json, Encoding.UTF8, "application/json");
		}
	}

	/// <summary>
	/// Creates a default light retry pipeline with exponential backoff and jitter.
	/// Retries on transient HTTP failures and specific status codes (408, 429, 503, 504).
	/// </summary>
	/// <returns>A configured <see cref="ResiliencePipeline{HttpResponseMessage}"/> for handling transient failures.</returns>
	private static ResiliencePipeline<HttpResponseMessage> CreateLightRetryPipeline()
	{
		var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
		{
			MaxRetryAttempts = 2,
			Delay = TimeSpan.FromMilliseconds(300),
			BackoffType = DelayBackoffType.Exponential,
			UseJitter = true,
			ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
				.Handle<HttpRequestException>()
				.Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested) // Only retry on timeout, not user cancellation
				.HandleResult(r => r.StatusCode is
					HttpStatusCode.RequestTimeout or
					HttpStatusCode.TooManyRequests or
					HttpStatusCode.ServiceUnavailable or
					HttpStatusCode.GatewayTimeout)
		};

		return new ResiliencePipelineBuilder<HttpResponseMessage>()
			.AddRetry(retryOptions)
			.Build();
	}

	/// <summary>
	/// Creates an empty resilience pipeline with no retry logic.
	/// Use this for scenarios where retries are handled elsewhere or not desired.
	/// </summary>
	/// <returns>An empty <see cref="ResiliencePipeline{HttpResponseMessage}"/> that performs no retries.</returns>
	public static ResiliencePipeline<HttpResponseMessage> CreateNoRetryPipeline() =>
		ResiliencePipeline<HttpResponseMessage>.Empty;

	/// <summary>
	/// Creates default JSON serializer settings using Newtonsoft.Json with sensible defaults.
	/// </summary>
	/// <returns>A <see cref="JsonSerializerSettings"/> instance with default configuration.</returns>
	private static JsonSerializerSettings DefaultJsonOptions() => new()
	{
		ContractResolver = new DefaultContractResolver { NamingStrategy = null },
		NullValueHandling = NullValueHandling.Ignore,
		Formatting = Formatting.None
	};

	/// <summary>
	/// Ensures the client has not been disposed before performing operations.
	/// </summary>
	/// <exception cref="ObjectDisposedException">Thrown when the client has been disposed.</exception>
	private void EnsureNotDisposed()
	{
		ObjectDisposedException.ThrowIf(_disposed, nameof(ApiClient));
	}

	/// <summary>
	/// Releases all resources used by the <see cref="ApiClient"/>.
	/// Disposes the internal <see cref="HttpClient"/> instance and marks the client as disposed.
	/// </summary>
	public void Dispose()
	{
		if (!_disposed)
		{
			_httpClient.Dispose();
			_disposed = true;
		}
	}
}