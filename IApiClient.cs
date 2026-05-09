namespace ARSoft.RestApiClient;

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
	/// <param name="authType">The authentication type to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> GetAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a GET request using a relative path with the configured base address.
	/// </summary>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="relativePath">The relative path to append to the base address.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The authentication type to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	/// <exception cref="InvalidOperationException">Thrown when no base address is configured.</exception>
	Task<ApiResponse<TResponse>> GetAsync<TResponse>(string relativePath, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a POST request with a JSON payload.
	/// </summary>
	/// <typeparam name="TRequest">The request payload type.</typeparam>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute or relative URI to request.</param>
	/// <param name="payload">The request payload to serialize and send.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The authentication type to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> PostAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a PUT request with a JSON payload.
	/// </summary>
	/// <typeparam name="TRequest">The request payload type.</typeparam>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute or relative URI to request.</param>
	/// <param name="payload">The request payload to serialize and send.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The authentication type to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> PutAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a DELETE request against the provided URI.
	/// </summary>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute or relative URI to request.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The authentication type to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> DeleteAsync<TResponse>(Uri url, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);

	/// <summary>
	/// Performs a PATCH request with a JSON payload.
	/// </summary>
	/// <typeparam name="TRequest">The request payload type.</typeparam>
	/// <typeparam name="TResponse">The expected response type.</typeparam>
	/// <param name="url">The absolute or relative URI to request.</param>
	/// <param name="payload">The request payload to serialize and send.</param>
	/// <param name="authToken">Optional authentication token.</param>
	/// <param name="authType">The authentication type to use.</param>
	/// <param name="customHeaders">Optional custom headers for this request.</param>
	/// <param name="cancellationToken">Cancellation token for the operation.</param>
	/// <returns>An <see cref="ApiResponse{TResponse}"/> containing the result.</returns>
	Task<ApiResponse<TResponse>> PatchAsync<TRequest, TResponse>(Uri url, TRequest payload, string? authToken = null, AuthType authType = AuthType.None, Dictionary<string, string>? customHeaders = null, CancellationToken cancellationToken = default);
}
