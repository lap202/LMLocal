using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LMLocal.Infrastructure.Lm
{
    /// <summary>
    /// Default implementation of IHttpClientWrapper that wraps the actual HttpClient.
    /// Lazily instantiated to defer HttpClient initialization.
    /// </summary>
    internal interface IHttpClientWrapper : IDisposable
    {
        /// <summary>
        /// Sends a GET request and returns the response.
        /// </summary>
        Task<HttpResponseMessage> GetAsync(string requestUri, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default);

        /// <summary>
        /// Sends a POST request and returns the response.
        /// </summary>
        Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default);
    }

    internal class HttpClientWrapper : IHttpClientWrapper
    {
        private readonly Lazy<HttpClient> _lazyClient;
        private bool _disposed;

        public HttpClientWrapper()
        {
            _lazyClient = new Lazy<HttpClient>(() => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
        }

        public async Task<HttpResponseMessage> GetAsync(string requestUri, HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return await _lazyClient.Value.GetAsync(requestUri, completionOption, cancellationToken).ConfigureAwait(false);
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

            GC.SuppressFinalize(this);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(HttpClientWrapper));
        }
    }
}
