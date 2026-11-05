using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace SoftcomSupportAutomation
{
    internal sealed partial class SupportForm
    {
        private void DownloadFileWithCancellation(string address, string destination, string userAgent,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                ThrowIfCancellationRequested(cancellationToken);
                bool downloaded = false;
                int retryDelay = -1;
                try
                {
                    if (File.Exists(destination)) File.Delete(destination);
                    var request = WebRequest.CreateHttp(address);
                    request.UserAgent = userAgent;
                    using (var response = (HttpWebResponse)WaitForDownloadIo(request.GetResponseAsync(), request, cancellationToken))
                    using (Stream source = response.GetResponseStream())
                    using (Stream target = File.Create(destination))
                    {
                        var buffer = new byte[81920];
                        long received = 0;
                        while (true)
                        {
                            int count;
                            try { count = WaitForDownloadIo(source.ReadAsync(buffer, 0, buffer.Length), request, cancellationToken); }
                            catch (IOException ex)
                            {
                                throw new WebException("Conexão interrompida durante o download.", ex, WebExceptionStatus.ReceiveFailure, null);
                            }
                            if (count == 0) break;
                            ThrowIfCancellationRequested(cancellationToken);
                            target.Write(buffer, 0, count);
                            received += count;
                        }
                        if (response.ContentLength >= 0 && received != response.ContentLength)
                            throw new WebException("O download foi interrompido antes de receber o arquivo completo.", WebExceptionStatus.ReceiveFailure);
                    }
                    ThrowIfCancellationRequested(cancellationToken);
                    downloaded = true;
                    return;
                }
                catch (WebException ex)
                {
                    retryDelay = GetDownloadRetryDelay(ex, attempt);
                    string reason = ex.Response is HttpWebResponse response
                        ? "HTTP " + (int)response.StatusCode : ex.Status.ToString();
                    ex.Response?.Close();
                    ThrowIfCancellationRequested(cancellationToken);
                    if (attempt == 3 || retryDelay < 0) throw;
                    WriteLog("Falha temporária no download (" + reason + "). Tentativa " + (attempt + 1) +
                        "/3 em " + (retryDelay / 1000.0).ToString("0.#", CultureInfo.CurrentCulture) + " s.");
                }
                finally
                {
                    if (!downloaded)
                    {
                        try { if (File.Exists(destination)) File.Delete(destination); }
                        catch (Exception ex) { WriteLog("Não foi possível remover o arquivo parcial: " + ex.Message); }
                    }
                }
                SleepWithCancellation(retryDelay, cancellationToken);
            }
        }

        private static int GetDownloadRetryDelay(WebException error, int attempt)
        {
            var response = error.Response as HttpWebResponse;
            if (response != null)
            {
                int status = (int)response.StatusCode;
                if (status != 408 && status != 429 && status != 500 && status != 502 && status != 503 && status != 504) return -1;
                string retryAfter = response.Headers[HttpResponseHeader.RetryAfter];
                if (!string.IsNullOrEmpty(retryAfter))
                {
                    double seconds;
                    int delta;
                    DateTimeOffset date;
                    if (int.TryParse(retryAfter, NumberStyles.None, CultureInfo.InvariantCulture, out delta)) seconds = delta;
                    else if (DateTimeOffset.TryParse(retryAfter, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date))
                        seconds = Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds);
                    else return attempt * 1000;
                    // Não antecipa Retry-After nem mantém o técnico esperando por uma janela longa.
                    if (seconds > 30) return -1;
                    return (int)Math.Max(attempt * 1000, seconds * 1000);
                }
                return attempt * 1000;
            }
            switch (error.Status)
            {
                case WebExceptionStatus.Timeout:
                case WebExceptionStatus.ConnectFailure:
                case WebExceptionStatus.ConnectionClosed:
                case WebExceptionStatus.ReceiveFailure:
                case WebExceptionStatus.SendFailure:
                case WebExceptionStatus.NameResolutionFailure:
                    return attempt * 1000;
                default:
                    return -1;
            }
        }

        private T WaitForDownloadIo<T>(Task<T> operation, HttpWebRequest request, CancellationToken cancellationToken,
            int inactivityTimeoutMilliseconds = 60000)
        {
            var elapsed = Stopwatch.StartNew();
            try
            {
                while (!operation.IsCompleted)
                {
                    ThrowIfCancellationRequested(cancellationToken);
                    if (elapsed.ElapsedMilliseconds >= inactivityTimeoutMilliseconds)
                        throw new WebException("Download sem progresso por tempo demais.", WebExceptionStatus.Timeout);
                    Task.WaitAny(new Task[] { operation }, 100, cancellationToken);
                }
                ThrowIfCancellationRequested(cancellationToken);
                return operation.GetAwaiter().GetResult();
            }
            catch
            {
                request.Abort();
                try { operation.GetAwaiter().GetResult(); } catch { }
                ThrowIfCancellationRequested(cancellationToken);
                throw;
            }
        }
    }
}
