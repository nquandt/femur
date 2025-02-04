using System.Diagnostics;
using System.Net;
using BenchmarkDotNet.Attributes;

public class Bench
{
	private readonly HttpClient httpClient = new HttpClient();
	

	[Benchmark]
	public HttpStatusCode MapEndpoint_Http_Call() => httpClient.GetAsync($"http://localhost:5000/instance").GetAwaiter().GetResult().StatusCode;

    [Benchmark]
	public HttpStatusCode MapGet_Http_Call() => httpClient.GetAsync($"http://localhost:5000/standard").GetAwaiter().GetResult().StatusCode;

	[GlobalCleanup]
	public void CloseHost()
	{
		
		httpClient.Dispose();
	}
}