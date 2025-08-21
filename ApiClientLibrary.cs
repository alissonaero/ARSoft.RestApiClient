using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace ARSoft.ApiClient;

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
	private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
	private readonly JsonSerializerOptions _jsonOptions;
	private readonly ILogger<ApiClient>? _logger;
	private readonly bool _disposeHttpClient;

	public ApiClient(HttpClient httpClient, ILogger<ApiClient>? logger = null, JsonSerializerOptions? jsonOptions = null, IAsyncPolicy<HttpResponseMessage>? retryPolicy = null, bool disposeHttpClient = false)
	{
		_httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
		_logger = logger;
		_jsonOptions = jsonOptions ?? DefaultJsonOptions();
		_retryPolicy = retryPolicy ?? CreateDefaultRetryPolicy();
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
			httpResponse = await _retryPolicy.ExecuteAsync(async () =>
			{
				using var request = new HttpRequestMessage(method, url);
				SetHeaders(request, authToken, authType);
				SetContent(request, payload, method);

				return await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
			});

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

	private static IAsyncPolicy<HttpResponseMessage> CreateDefaultRetryPolicy() =>
			 HttpPolicyExtensions
			.HandleTransientHttpError()
			.OrResult(msg => (int)msg.StatusCode == 429)
			.WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

	private static JsonSerializerOptions DefaultJsonOptions() => new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

	public void Dispose()
	{
		if (_disposeHttpClient)
			_httpClient?.Dispose();
	}
}
