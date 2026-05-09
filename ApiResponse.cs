using System.Net;

namespace ARSoft.RestApiClient;

public class ApiResponse<T>
{
	public bool Success { get; set; }

	public T? Data { get; set; }

	public string? ErrorMessage { get; set; }

	public string? ErrorData { get; set; }

	public HttpStatusCode StatusCode { get; set; }
}
