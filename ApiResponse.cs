using System.Net;

namespace ARSoft.RestApiClient;

/// <summary>
/// Represents a structured response from an API call.
/// </summary>
/// <typeparam name="T">The type of data expected in the response body.</typeparam>
public class ApiResponse<T>
{
	/// <summary>Indicates whether the API call succeeded.</summary>
	public bool Success { get; set; }

	/// <summary>The deserialized response data when the request succeeds.</summary>
	public T? Data { get; set; }

	/// <summary>A message describing the error when the request fails.</summary>
	public string? ErrorMessage { get; set; }

	/// <summary>Raw response content or exception details when the request fails.</summary>
	public string? ErrorData { get; set; }

	/// <summary>The HTTP status code returned by the API.</summary>
	public HttpStatusCode StatusCode { get; set; }
}
