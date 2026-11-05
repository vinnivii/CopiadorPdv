using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

public static class DownloadTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    private sealed class Server : IDisposable
    {
        private readonly TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
        private readonly ManualResetEvent stopped = new ManualResetEvent(false);
        private readonly Thread thread;
        private TcpClient current;
        public int Requests;
        public readonly string Url;
        public Exception Error;
        public Server(Action<int, NetworkStream, WaitHandle> reply)
        {
            listener.Start();
            Url = "http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/file";
            thread = new Thread(() =>
            {
                try
                {
                    while (!stopped.WaitOne(0))
                    {
                        using (TcpClient client = listener.AcceptTcpClient())
                        {
                            current = client;
                            using (NetworkStream stream = client.GetStream())
                            {
                                var request = new byte[4096];
                                stream.Read(request, 0, request.Length);
                                reply(Interlocked.Increment(ref Requests), stream, stopped);
                            }
                            current = null;
                        }
                    }
                }
                catch (Exception ex) { if (!stopped.WaitOne(0)) Error = ex; }
            }) { IsBackground = true };
            thread.Start();
        }
        public void Dispose()
        {
            stopped.Set();
            listener.Stop();
            if (current != null) current.Close();
            if (!thread.Join(3000)) throw new Exception("Servidor de teste ficou ativo");
            stopped.Dispose();
            if (Error != null) throw new Exception("Servidor de teste falhou", Error);
        }
    }

    private static void Reply(NetworkStream stream, int status, string body, string extra = "", int length = -1)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(body);
        string header = "HTTP/1.1 " + status + " Test\r\nConnection: close\r\nContent-Length: " +
            (length < 0 ? bytes.Length : length) + "\r\n" + extra + "\r\n";
        byte[] head = Encoding.ASCII.GetBytes(header);
        stream.Write(head, 0, head.Length);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static Exception Download(object form, string url, string path, CancellationToken token)
    {
        try
        {
            form.GetType().GetMethod("DownloadFileWithCancellation", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(form, new object[] { url, path, "SoftcomSupportAutomation/test", token });
            return null;
        }
        catch (TargetInvocationException ex) { return ex.InnerException; }
    }

    public static void Run(object form, string folder)
    {
        string output = Path.Combine(folder, "download.partial");
        Type type = form.GetType();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var stop = type.GetField("stopRequested", flags);
        const int blockSize = 64 * 1024;
        var payload = new byte[10 * 1024 * 1024];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i * 31 + i / blockSize);
        using (var server = new Server((n, stream, halt) =>
        {
            Reply(stream, 200, "", "", payload.Length);
            for (int offset = 0; offset < payload.Length; offset += blockSize)
            {
                stream.Write(payload, offset, blockSize);
                stream.Flush();
                // Aguarda a gravacao do bloco para o buffering nao esconder uma espera fixa por leitura.
                var waiting = Stopwatch.StartNew();
                while (!File.Exists(output) || new FileInfo(output).Length < offset + blockSize)
                {
                    if (halt.WaitOne(1)) return;
                    if (waiting.ElapsedMilliseconds > 5000) throw new TimeoutException("Cliente nao consumiu o bloco de teste");
                }
            }
        }))
        {
            var elapsed = Stopwatch.StartNew();
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            elapsed.Stop();
            int blocks = payload.Length / blockSize;
            Console.WriteLine("MEASURE: 10 MiB, {0} blocos de 64 KiB: {1:F0} ms ({2:F2} ms/bloco)",
                blocks, elapsed.Elapsed.TotalMilliseconds, elapsed.Elapsed.TotalMilliseconds / blocks);
            Check(error == null && server.Requests == 1, "Download de 10 MiB em blocos sem retry");
            byte[] received = File.ReadAllBytes(output);
            bool matches = received.Length == payload.Length;
            for (int i = 0; matches && i < received.Length; i++) matches = received[i] == payload[i];
            Check(matches, "Todos os bytes dos 10 MiB conferem");
            Check(elapsed.Elapsed.TotalMilliseconds < blocks * 50,
                "Transferencia em blocos abaixo de 50 ms/bloco, sem penalidade fixa de 100 ms por leitura");
        }
        using (var server = new Server((n, stream, halt) => Reply(stream, n == 1 ? 503 : 200, n == 1 ? "" : "ok")))
        {
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            Check(error == null && server.Requests == 2 && File.ReadAllText(output) == "ok", "503 transitorio seguido de sucesso; arquivo valido baixado uma vez");
        }
        using (var server = new Server((n, stream, halt) => Reply(stream, 500, "")))
        {
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            Check(error is WebException && server.Requests == 3 && !File.Exists(output), "Limite de tres tentativas e descarte do parcial");
        }
        foreach (int status in new[] { 401, 403, 404, 501 })
        using (var server = new Server((n, stream, halt) => Reply(stream, status, "")))
        {
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            Check(error is WebException && server.Requests == 1 && !File.Exists(output), "Sem retry para HTTP " + status);
        }
        using (var server = new Server((n, stream, halt) => Reply(stream, 429, "", "Retry-After: 60\r\n")))
        {
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            Check(error is WebException && server.Requests == 1, "Retry-After longo nao e antecipado");
        }
        foreach (string retryAfter in new[] { "1", DateTimeOffset.UtcNow.AddSeconds(2).ToString("R"), "invalid" })
        using (var server = new Server((n, stream, halt) => Reply(stream, n == 1 ? 429 : 200, "ok", n == 1 ? "Retry-After: " + retryAfter + "\r\n" : "")))
        {
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            Check(error == null && server.Requests == 2, "429 aceita Retry-After curto/data e trata cabecalho invalido");
        }
        using (var server = new Server((n, stream, halt) => Reply(stream, 200, n == 1 ? "x" : "ok", "", n == 1 ? 20 : -1)))
        {
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            Check(error == null && server.Requests == 2 && File.ReadAllText(output) == "ok", "Conexao truncada repetida sem reutilizar parcial");
        }
        using (var server = new Server((n, stream, halt) => Reply(stream, 200, "x", "", 20)))
        {
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            Check(error is WebException && server.Requests == 3 && !File.Exists(output),
                "Falha persistente durante transferencia limita tres tentativas e remove parcial");
        }
        foreach (bool backoff in new[] { false, true })
        foreach (bool useStop in new[] { false, true })
        using (var server = new Server((n, stream, halt) =>
        {
            if (backoff) Reply(stream, 503, "");
            else { Reply(stream, 200, "x", "", 1000000); halt.WaitOne(5000); }
        }))
        using (var token = new CancellationTokenSource())
        {
            var elapsed = Stopwatch.StartNew();
            long cancelledAt = 0;
            using (var timer = new System.Threading.Timer(_ =>
            {
                Interlocked.Exchange(ref cancelledAt, elapsed.ElapsedMilliseconds);
                if (useStop) stop.SetValue(form, true); else token.Cancel();
            }, null, 250, Timeout.Infinite))
            {
                Exception error = Download(form, server.Url, output, token.Token);
                long latency = elapsed.ElapsedMilliseconds - Interlocked.Read(ref cancelledAt);
                stop.SetValue(form, false);
                string scenario = (backoff ? "intervalo" : "transferencia") + " por " + (useStop ? "stopRequested" : "token");
                Console.WriteLine("MEASURE: cancelamento durante {0}: {1} ms apos sinal", scenario, latency);
                Check(error is OperationCanceledException && server.Requests == 1 && !File.Exists(output) &&
                    cancelledAt > 0 && latency < 1000 && elapsed.ElapsedMilliseconds < 2000, "Cancelamento durante " + scenario);
            }
        }
        using (var server = new Server((n, stream, halt) => Reply(stream, 200, "ok")))
        {
            Exception error = Download(form, server.Url, Path.Combine(folder, "missing", "file"), CancellationToken.None);
            Check(error is DirectoryNotFoundException && server.Requests == 1, "Falha local de disco nao provoca retry de rede");
        }
        using (var server = new Server((n, stream, halt) => Reply(stream, 200, "<html>erro</html>")))
        {
            Exception error = Download(form, server.Url, output, CancellationToken.None);
            bool invalid = false;
            try { type.GetMethod("ValidateHopToDeskInstaller", flags).Invoke(form, new object[] { output }); }
            catch (TargetInvocationException ex) { invalid = ex.InnerException is InvalidDataException; }
            Check(error == null && invalid && server.Requests == 1, "Conteudo invalido e recusado fora do retry");
        }
        using (var server = new Server((n, stream, halt) => halt.WaitOne(5000)))
        {
            var request = WebRequest.CreateHttp(server.Url);
            Task<WebResponse> pending = request.GetResponseAsync();
            Exception error = null;
            try
            {
                type.GetMethod("WaitForDownloadIo", flags).MakeGenericMethod(typeof(WebResponse))
                    .Invoke(form, new object[] { pending, request, CancellationToken.None, 200 });
            }
            catch (TargetInvocationException ex) { error = ex.InnerException; }
            Check(error is WebException && ((WebException)error).Status == WebExceptionStatus.Timeout && pending.IsCompleted,
                "Timeout de inatividade aborta e aguarda a operacao, sem tarefa orfa");
        }
        using (var server = new Server((n, stream, halt) => { Reply(stream, 200, "x", "", 1000000); halt.WaitOne(5000); }))
        {
            var request = WebRequest.CreateHttp(server.Url);
            using (var response = request.GetResponse())
            using (Stream source = response.GetResponseStream())
            {
                Check(source.ReadByte() == 'x', "Transferencia iniciada antes do teste de inatividade");
                Task<int> pending = source.ReadAsync(new byte[4096], 0, 4096);
                Exception error = null;
                var elapsed = Stopwatch.StartNew();
                try
                {
                    type.GetMethod("WaitForDownloadIo", flags).MakeGenericMethod(typeof(int))
                        .Invoke(form, new object[] { pending, request, CancellationToken.None, 200 });
                }
                catch (TargetInvocationException ex) { error = ex.InnerException; }
                Console.WriteLine("MEASURE: timeout de leitura de 200 ms: {0} ms", elapsed.ElapsedMilliseconds);
                Check(error is WebException && ((WebException)error).Status == WebExceptionStatus.Timeout &&
                    pending.IsCompleted && elapsed.ElapsedMilliseconds >= 200 && elapsed.ElapsedMilliseconds < 1500,
                    "Timeout durante transferencia aborta e aguarda leitura, sem tarefa orfa");
            }
        }
        var retry = type.GetMethod("GetDownloadRetryDelay", BindingFlags.NonPublic | BindingFlags.Static);
        foreach (WebExceptionStatus status in new[] { WebExceptionStatus.TrustFailure, WebExceptionStatus.SecureChannelFailure, WebExceptionStatus.RequestCanceled })
            Check((int)retry.Invoke(null, new object[] { new WebException("test", status), 1 }) == -1, "Sem retry para " + status);

        string installer = Path.Combine(folder, "HopToDesk.exe");
        File.WriteAllText(installer, "test fixture, never executed");
        using (var held = new FileStream(installer, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            type.GetMethod("DeleteHopToDeskDownload", flags).Invoke(form, new object[] { installer });
            Check(File.Exists(installer), "Falha de limpeza por arquivo bloqueado nao interrompe rotina");
        }
        type.GetMethod("DeleteHopToDeskDownload", flags).Invoke(form, new object[] { installer });
        Check(!File.Exists(installer), "Instalador conhecido removido apos liberar o arquivo");

        var measure = type.GetMethod("RunTimedPhase", flags, null, new[] { typeof(string), typeof(Action) }, null);
        foreach (string outcome in new[] { "ok", "failure", "cancel" })
        {
            Action action = () => { if (outcome == "failure") throw new InvalidOperationException(); if (outcome == "cancel") throw new OperationCanceledException(); };
            try { measure.Invoke(form, new object[] { "teste " + outcome, action }); }
            catch (TargetInvocationException) { if (outcome == "ok") throw; }
        }
        Application.DoEvents();
        string log = File.ReadAllText((string)type.GetField("logFile", flags).GetValue(form));
        Check(log.Contains("teste ok") && log.Contains("teste failure") && log.Contains("teste cancel") &&
            log.Contains("concluída") && log.Contains("falhou") && log.Contains("cancelada"), "Medicoes registram sucesso, falha e cancelamento");
        var logField = type.GetField("logFile", flags);
        object previousLog = logField.GetValue(form);
        logField.SetValue(form, Path.Combine(folder, "missing", "test.log"));
        try
        {
            measure.Invoke(form, new object[] { "log indisponivel", (Action)(() => { }) });
            Exception original = new InvalidOperationException("operation failed");
            bool preserved = false;
            try { measure.Invoke(form, new object[] { "log indisponivel", (Action)(() => { throw original; }) }); }
            catch (TargetInvocationException ex) { preserved = object.ReferenceEquals(ex.InnerException, original); }
            Check(preserved, "Falha do log de medicao preserva resultado/erro original");
        }
        finally { logField.SetValue(form, previousLog); }
    }
}
