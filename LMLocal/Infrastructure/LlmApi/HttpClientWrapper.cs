using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LMLocal.Infrastructure.Api
{
    /// <summary>
    /// Abstraction over HttpClient to enable easier testing and mock implementations.
    /// </summary>
    internal interface IHttpClientWrapper : IDisposable
    {
        /// <summary>
        /// Sends a POST request and returns the response.
        /// </summary>
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default);
    }

    internal sealed class HttpClientWrapper : IHttpClientWrapper
    {
        private readonly Lazy<HttpClient> _lazyClient;
        private bool _disposed;

        public HttpClientWrapper()
        {
            _lazyClient = new Lazy<HttpClient>(() => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        }

        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _lazyClient.Value.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_lazyClient.IsValueCreated)
            {
                _lazyClient.Value?.Dispose();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpClientWrapper));
        }
    }
}
