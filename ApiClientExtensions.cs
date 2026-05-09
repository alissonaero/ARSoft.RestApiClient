namespace ARSoft.RestApiClient;

/// <summary>
/// Provides configuration helpers for <see cref="IApiClient"/> instances backed by <see cref="ApiClient"/>.
/// </summary>
public static class ApiClientExtensions
{
	/// <summary>
	/// Adds a default header that will be included in all requests made by the underlying <see cref="ApiClient"/>.
	/// </summary>
	/// <param name="client">The API client instance.</param>
	/// <param name="name">The header name.</param>
	/// <param name="value">The header value.</param>
	/// <exception cref="NotSupportedException">Thrown when <paramref name="client"/> is not an <see cref="ApiClient"/> instance.</exception>
	public static void AddDefaultRequestHeader(this IApiClient client, string name, string value)
	{
		GetApiClient(client).AddDefaultRequestHeader(name, value);
	}

	/// <summary>
	/// Sets the base address for the underlying <see cref="ApiClient"/>.
	/// </summary>
	/// <param name="client">The API client instance.</param>
	/// <param name="baseAddress">The base URI for all requests.</param>
	/// <exception cref="NotSupportedException">Thrown when <paramref name="client"/> is not an <see cref="ApiClient"/> instance.</exception>
	public static void SetBaseAddress(this IApiClient client, Uri baseAddress)
	{
		GetApiClient(client).SetBaseAddress(baseAddress);
	}

	/// <summary>
	/// Sets the timeout for the underlying <see cref="ApiClient"/>.
	/// </summary>
	/// <param name="client">The API client instance.</param>
	/// <param name="timeout">The timeout duration for requests.</param>
	/// <exception cref="NotSupportedException">Thrown when <paramref name="client"/> is not an <see cref="ApiClient"/> instance.</exception>
	public static void SetTimeout(this IApiClient client, TimeSpan timeout)
	{
		GetApiClient(client).SetTimeout(timeout);
	}

	private static ApiClient GetApiClient(IApiClient client)
	{
		ArgumentNullException.ThrowIfNull(client);

		if (client is ApiClient apiClient)
			return apiClient;

		throw new NotSupportedException($"This operation requires an {nameof(ApiClient)} instance.");
	}
}
