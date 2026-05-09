namespace ARSoft.RestApiClient;

/// <summary>
/// Specifies the authentication type for API requests.
/// </summary>
public enum AuthType
{
	/// <summary>No authentication.</summary>
	None,

	/// <summary>Bearer token authentication.</summary>
	Bearer,

	/// <summary>Basic authentication.</summary>
	Basic,

	/// <summary>API key authentication using the X-API-Key header.</summary>
	ApiKey
}
