using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Shouldly;

namespace ARSoft.RestApiClient.Tests;

public class ApiClientTests
{
	[Fact]
	public async Task GetAsync_WithRelativePath_DeserializesJson()
	{
		var handler = new StubHttpMessageHandler(_ => JsonResponse(new TestUser(10, "Ana")));
		using var client = new ApiClient(handler, new Uri("https://api.test/"));

		var response = await client.GetAsync<TestUser>("users/10");

		response.Success.ShouldBeTrue();
		response.StatusCode.ShouldBe(HttpStatusCode.OK);
		response.Data.ShouldNotBeNull();
		response.Data.Id.ShouldBe(10);
		response.Data.Name.ShouldBe("Ana");
		handler.Requests.Single().RequestUri.ShouldBe(new Uri("https://api.test/users/10"));
	}

	[Fact]
	public async Task GetAsync_WithStringResponse_ReturnsRawText()
	{
		var handler = new StubHttpMessageHandler(_ => TextResponse("plain text"));
		using var client = new ApiClient(handler);

		var response = await client.GetAsync<string>(new Uri("https://api.test/status"));

		response.Success.ShouldBeTrue();
		response.Data.ShouldBe("plain text");
	}

	[Fact]
	public async Task GetAsync_WithHttpError_ReturnsErrorData()
	{
		var handler = new StubHttpMessageHandler(_ => TextResponse("bad request", HttpStatusCode.BadRequest));
		using var client = new ApiClient(handler);

		var response = await client.GetAsync<TestUser>(new Uri("https://api.test/users/10"));

		response.Success.ShouldBeFalse();
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		response.ErrorMessage.ShouldBe("HTTP Error 400");
		response.ErrorData.ShouldBe("bad request");
	}

	[Fact]
	public async Task PostAsync_WithAuthAndCustomHeaders_SendsExpectedHeadersAndBody()
	{
		var customHeaders = new Dictionary<string, string>
		{
			["X-Trace-Id"] = "trace-123",
			["Content-Type"] = "application/problem+json"
		};
		var handler = new StubHttpMessageHandler(_ => JsonResponse(new TestUser(5, "Bia")));
		using var client = new ApiClient(handler);

		var response = await client.PostAsync<TestUser, TestUser>(
			new Uri("https://api.test/users"),
			new TestUser(5, "Bia"),
			authToken: "token-123",
			authType: AuthType.Bearer,
			customHeaders: customHeaders);

		var request = handler.Requests.Single();
		response.Success.ShouldBeTrue();
		request.Method.ShouldBe(HttpMethod.Post);
		request.AuthorizationScheme.ShouldBe("Bearer");
		request.AuthorizationParameter.ShouldBe("token-123");
		request.Headers["X-Trace-Id"].Single().ShouldBe("trace-123");
		request.ContentHeaders["Content-Type"].Single().ShouldBe("application/problem+json");
		request.Body.ShouldBe("{\"Id\":5,\"Name\":\"Bia\"}");
	}

	[Fact]
	public async Task GetAsync_WithApiKey_SendsApiKeyHeader()
	{
		var handler = new StubHttpMessageHandler(_ => JsonResponse(new TestUser(7, "Leo")));
		using var client = new ApiClient(handler);

		await client.GetAsync<TestUser>(
			new Uri("https://api.test/users/7"),
			authToken: "key-123",
			authType: AuthType.ApiKey);

		handler.Requests.Single().Headers["X-API-Key"].Single().ShouldBe("key-123");
	}

	[Fact]
	public async Task ConfigurationMethods_AfterFirstRequest_ThrowConfigurationException()
	{
		var handler = new StubHttpMessageHandler(_ => JsonResponse(new TestUser(1, "Tom")));
		IApiClient client = new ApiClient(handler, new Uri("https://api.test/"));

		client.AddDefaultRequestHeader("X-App", "tests");
		await client.GetAsync<TestUser>("users/1");

		var exception = Should.Throw<ApiClientConfigurationException>(() =>
			client.SetTimeout(TimeSpan.FromSeconds(10)));

		exception.Reason.ShouldBe(ApiClientConfigurationReason.TimeoutModificationNotAllowed);
	}

	[Fact]
	public async Task Dispose_PreventsFurtherRequests()
	{
		var handler = new StubHttpMessageHandler(_ => JsonResponse(new TestUser(1, "Tom")));
		var client = new ApiClient(handler);

		client.Dispose();

		await Should.ThrowAsync<ObjectDisposedException>(() =>
			client.GetAsync<TestUser>(new Uri("https://api.test/users/1")));
	}

	[Fact]
	public async Task DefaultRetryPipeline_DisposesTransientResponses()
	{
		var transientContent = new TrackableStringContent("try later");
		var handler = new StubHttpMessageHandler(
			_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
			{
				Content = transientContent
			},
			_ => JsonResponse(new TestUser(2, "Eva")));
		using var client = new ApiClient(handler);

		var response = await client.GetAsync<TestUser>(new Uri("https://api.test/users/2"));

		response.Success.ShouldBeTrue();
		handler.Requests.Count.ShouldBe(2);
		transientContent.Disposed.ShouldBeTrue();
	}

	private static HttpResponseMessage JsonResponse<T>(T payload, HttpStatusCode statusCode = HttpStatusCode.OK)
	{
		return new HttpResponseMessage(statusCode)
		{
			Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json")
		};
	}

	private static HttpResponseMessage TextResponse(string text, HttpStatusCode statusCode = HttpStatusCode.OK)
	{
		return new HttpResponseMessage(statusCode)
		{
			Content = new StringContent(text, Encoding.UTF8, "text/plain")
		};
	}

	private sealed record TestUser(int Id, string Name);

	private sealed record RequestRecord(
		HttpMethod Method,
		Uri? RequestUri,
		string? AuthorizationScheme,
		string? AuthorizationParameter,
		Dictionary<string, string[]> Headers,
		Dictionary<string, string[]> ContentHeaders,
		string? Body);

	private sealed class StubHttpMessageHandler : HttpMessageHandler
	{
		private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses;

		public StubHttpMessageHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
		{
			_responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(responses);
		}

		public List<RequestRecord> Requests { get; } = [];

		protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			var body = request.Content is null
				? null
				: await request.Content.ReadAsStringAsync(cancellationToken);

			Requests.Add(new RequestRecord(
				request.Method,
				request.RequestUri,
				request.Headers.Authorization?.Scheme,
				request.Headers.Authorization?.Parameter,
				ToDictionary(request.Headers),
				request.Content is null ? [] : ToDictionary(request.Content.Headers),
				body));

			return _responses.Dequeue()(request);
		}

		private static Dictionary<string, string[]> ToDictionary(HttpHeaders headers)
		{
			return headers.ToDictionary(header => header.Key, header => header.Value.ToArray());
		}
	}

	private sealed class TrackableStringContent : StringContent
	{
		public TrackableStringContent(string content)
			: base(content)
		{
		}

		public bool Disposed { get; private set; }

		protected override void Dispose(bool disposing)
		{
			Disposed = true;
			base.Dispose(disposing);
		}
	}
}
