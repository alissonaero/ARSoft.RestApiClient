namespace ARSoft.RestApiClient;

public static class ApiClientExtensions
{
	public static void AddDefaultRequestHeader(this IApiClient client, string name, string value)
	{
		GetApiClient(client).AddDefaultRequestHeader(name, value);
	}

	public static void SetBaseAddress(this IApiClient client, Uri baseAddress)
	{
		GetApiClient(client).SetBaseAddress(baseAddress);
	}

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
