using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ARSoft.RestApiClient;

public enum AuthType
{
	None,
	Bearer,
	Basic,
	ApiKey
}

public class ApiResponse<T>
{
	public bool Success { get; set; }
	public T? Data { get; set; }
	public string? ErrorMessage { get; set; }
	public string? ErrorData { get; set; }
	public HttpStatusCode StatusCode { get; set; }
}

public interface IApiClient
{
	Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
	Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default);
}

public class ApiClient : IApiClient, IDisposable
{
	private readonly HttpClient _httpClient;
	private readonly ResiliencePipeline<HttpResponseMessage> _retryPipeline;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ILogger<ApiClient>? _logger;
	private readonly bool _disposeHttpClient;

	/// <summary>
	/// Initializes a new instance of the <see cref="ApiClient"/> class with the specified HTTP client, logger, JSON
	/// serializer options, and retry pipeline.
	/// </summary>
	/// <param name="httpClient">The <see cref="HttpClient"/> instance used to send HTTP requests. This parameter cannot be <see langword="null"/>.</param>
	/// <param name="logger">An optional logger for logging API client operations. If <see langword="null"/>, logging is disabled.</param>
	/// <param name="jsonOptions">Optional JSON serializer options for customizing JSON serialization and deserialization. If <see langword="null"/>,
	/// default options are used.</param>
	/// <param name="retryPipeline">An optional resilience pipeline for handling retries of HTTP requests. If <see langword="null"/>, a default retry
	/// pipeline is used.</param>
	/// <param name="disposeHttpClient">A value indicating whether the <see cref="HttpClient"/> should be disposed when the <see cref="ApiClient"/> is
	/// disposed. <see langword="true"/> to dispose the <see cref="HttpClient"/>; otherwise, <see langword="false"/>.</param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="httpClient"/> is <see langword="null"/>.</exception>
	public ApiClient(HttpClient httpClient, ILogger<ApiClient>? logger = null, JsonSerializerOptions? jsonOptions = null, ResiliencePipeline<HttpResponseMessage>? retryPipeline = null, bool disposeHttpClient = false)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_logger = logger;
		_jsonOptions = jsonOptions ?? DefaultJsonOptions();
		_retryPipeline = retryPipeline ?? CreateDefaultRetryPipeline();
		_disposeHttpClient = disposeHttpClient;
	}

	public async Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default) =>
		await SendAsync<object, TResponse>(HttpMethod.Get, url, null, authToken, authType, cancellationToken);

	public async Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default) =>
		await SendAsync<TRequest, TResponse>(HttpMethod.Post, url, payload, authToken, authType, cancellationToken);

	public async Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default) =>
		await SendAsync<TRequest, TResponse>(HttpMethod.Put, url, payload, authToken, authType, cancellationToken);

	public async Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default) =>
		await SendAsync<object, TResponse>(HttpMethod.Delete, url, null, authToken, authType, cancellationToken);

	public async Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, CancellationToken cancellationToken = default) =>
		await SendAsync<TRequest, TResponse>(HttpMethod.Patch, url, payload, authToken, authType, cancellationToken);

	private async Task<ApiResponse<TResponse>> SendAsync<TRequest, TResponse>(HttpMethod method, Uri url, TRequest? payload, string? authToken, AuthType authType, CancellationToken cancellationToken)
	{
		var result = new ApiResponse<TResponse>();
		HttpResponseMessage? httpResponse = null;

		try
		{
			httpResponse = await _retryPipeline.ExecuteAsync(async (ctx, token) =>
			{
				using var request = new HttpRequestMessage(method, url);
				SetHeaders(request, authToken, authType);
				SetContent(request, payload, method);

				return await _httpClient.SendAsync(request, token).ConfigureAwait(false);
			}, cancellationToken);

			var responseJson = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
			result.StatusCode = httpResponse.StatusCode;

			if (!httpResponse.IsSuccessStatusCode)
			{
				result.Success = false;
				result.ErrorMessage = $"HTTP Error {(int)httpResponse.StatusCode}";
				result.ErrorData = responseJson;
				return result;
			}

			result.Data = typeof(TResponse) == typeof(string)
				? (TResponse)(object)responseJson
				: JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions);

			result.Success = true;
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

	private void SetHeaders(HttpRequestMessage request, string? authToken, AuthType authType)
	{
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
				.HandleResult(r => !r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.TooManyRequests),
			MaxRetryAttempts = 3,
			Delay = TimeSpan.FromSeconds(1),
			BackoffType = DelayBackoffType.Exponential,
			UseJitter = true
		};

		return new ResiliencePipelineBuilder<HttpResponseMessage>()
			.AddRetry(retryOptions)
			.Build();
	}

	private static JsonSerializerOptions DefaultJsonOptions() => new(JsonSerializerDefaults.Web)
	{
		PropertyNamingPolicy = null,
		PropertyNameCaseInsensitive = true,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	public void Dispose()
	{
		if (_disposeHttpClient)
			_httpClient?.Dispose();
	}
}
