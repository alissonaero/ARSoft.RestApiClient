namespace ARSoft.RestApiClient;

public class ApiClientConfigurationException : InvalidOperationException
{
	public ApiClientConfigurationReason Reason { get; }

	public ApiClientConfigurationException(ApiClientConfigurationReason reason, string message)
		: base(message)
	{
		Reason = reason;
	}
}
