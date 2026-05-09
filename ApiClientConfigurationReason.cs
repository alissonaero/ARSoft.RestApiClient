namespace ARSoft.RestApiClient;

/// <summary>
/// Represents the reason why an <see cref="ApiClient"/> configuration change is not allowed.
/// </summary>
public enum ApiClientConfigurationReason
{
	/// <summary>Default request headers cannot be modified after the first request.</summary>
	HeadersModificationNotAllowed,

	/// <summary>The base address cannot be modified after the first request.</summary>
	BaseAddressModificationNotAllowed,

	/// <summary>The timeout cannot be modified after the first request.</summary>
	TimeoutModificationNotAllowed,

	/// <summary>The operation was attempted on a disposed client.</summary>
	ClientDisposed
}
