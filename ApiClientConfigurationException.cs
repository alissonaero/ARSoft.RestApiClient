namespace ARSoft.RestApiClient;

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
