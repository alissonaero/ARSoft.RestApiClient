namespace ARSoft.RestApiClient;

public interface IApiClient
{
	Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	Task<ApiResponse<TResponse>> GetAsync<TResponse>(string relativePath, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);
}
