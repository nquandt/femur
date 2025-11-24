using System.Net;
using BenchmarkDotNet.Attributes;

namespace Benchmark;

public class Bench : IDisposable
{
    private readonly HttpClient _httpClient = new HttpClient();
    private bool _disposed;

    [Benchmark]
    public HttpStatusCode MapEndpoint_Http_Call() => this._httpClient.GetAsync($"http://localhost:5000/instance").GetAwaiter().GetResult().StatusCode;

    [Benchmark]
    public HttpStatusCode MapGet_Http_Call() => this._httpClient.GetAsync($"http://localhost:5000/standard").GetAwaiter().GetResult().StatusCode;

    [GlobalCleanup]
    public void CloseHost()
    {

        this._httpClient.Dispose();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!this._disposed)
        {
            if (disposing)
            {
                this._httpClient?.Dispose();
            }

            this._disposed = true;
        }
    }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }
}