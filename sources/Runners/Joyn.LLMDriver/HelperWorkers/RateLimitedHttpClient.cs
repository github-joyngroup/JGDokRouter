using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Joyn.LLMDriver.HelperWorkers
{
    public class RateLimitedHttpClient : HttpClient
    {
        private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private int _requestCount = 0;
        private DateTime _resetTime = DateTime.UtcNow.AddMinutes(1);
        private readonly int _maxRequestsPerMinute;

        public RateLimitedHttpClient(int maxRequestsPerMinute, HttpMessageHandler handler) : base(handler)
        {
            _maxRequestsPerMinute = maxRequestsPerMinute;
        }

        public RateLimitedHttpClient(int maxRequestsPerMinute) : base()
        {
            _maxRequestsPerMinute = maxRequestsPerMinute;
        }

        private async Task EnsureRateLimitAsync()
        {
            await _semaphore.WaitAsync();

            try
            {
                if (_requestCount >= _maxRequestsPerMinute)
                {
                    var delay = _resetTime - DateTime.UtcNow;
                    if (delay > TimeSpan.Zero)
                    {
                        await Task.Delay(delay);
                    }

                    _requestCount = 0;
                    _resetTime = DateTime.UtcNow.AddMinutes(1);
                }

                _requestCount++;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public new async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await EnsureRateLimitAsync();
            return await base.SendAsync(request, cancellationToken);
        }

        public new async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            await EnsureRateLimitAsync();
            return await base.SendAsync(request);
        }

        public new async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken)
        {
            await EnsureRateLimitAsync();
            return await base.GetAsync(requestUri, cancellationToken);
        }

        public new async Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            await EnsureRateLimitAsync();
            return await base.GetAsync(requestUri);
        }

        public new async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            await EnsureRateLimitAsync();
            return await base.PostAsync(requestUri, content, cancellationToken);
        }

        public new async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
        {
            await EnsureRateLimitAsync();
            return await base.PostAsync(requestUri, content);
        }

        // Add similar overrides for other HttpClient methods as needed
    }

}
