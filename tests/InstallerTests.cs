using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

public static class InstallerTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;

    private static object Call(object form, string method, params object[] args)
    {
        try { return form.GetType().GetMethod(method, Instance).Invoke(form, args); }
        catch (TargetInvocationException ex) { throw ex.InnerException; }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new Exception("FAIL: Expected " + typeof(T).Name);
    }

    public static void Run(object form, string folder, string fixtureExe)
    {
        var stop = form.GetType().GetField("stopRequested", Instance);
        Func<Func<int, int>, int, int> retry = (attempt, timeout) =>
            (int)Call(form, "RetryWhenInstallerBusy", attempt, timeout, "teste de instalador");
        Func<int, int, bool, int> command = (code, timeout, allowBusy) =>
            (int)Call(form, "RunCommand", fixtureExe, code.ToString(), timeout, "teste de instalador", allowBusy);

        foreach (int code in new[] { 0, 1641, 3010 })
        {
            int calls = 0;
            Check(retry(remaining => { calls++; return command(code, remaining, true); }, 5000) == code && calls == 1,
                "Sucesso/reinicio " + code + " preservado sem retry");
        }
        int attempts = 0;
        Check(retry(remaining => command(++attempts == 1 ? 1618 : 0, remaining, true), 5000) == 0 && attempts == 2,
            "1618 transitorio aguarda e repete o mesmo comando ate sucesso");
        Expect<InvalidOperationException>(() => command(1618, 5000, false));
        Check(true, "Comando de outra automacao continua rejeitando 1618 sem retry implicito");
        attempts = 0;
        Expect<InvalidOperationException>(() => retry(remaining => { attempts++; return command(1603, remaining, true); }, 5000));
        Check(attempts == 1, "Falha definitiva 1603 nao e repetida");
        var elapsed = Stopwatch.StartNew();
        Expect<TimeoutException>(() => retry(remaining => { Thread.Sleep(100); return 1618; }, 250));
        Check(elapsed.ElapsedMilliseconds < 1500, "1618 persistente respeita prazo total incluindo tentativa e espera");

        var cancel = new Thread(() => { Thread.Sleep(150); stop.SetValue(form, true); });
        attempts = 0;
        elapsed.Restart();
        cancel.Start();
        try { Expect<OperationCanceledException>(() => retry(remaining => { attempts++; return 1618; }, 5000)); }
        finally { cancel.Join(); stop.SetValue(form, false); }
        Check(attempts == 1 && elapsed.ElapsedMilliseconds < 1500, "Cancelamento interrompe espera pelo MSI sem iniciar nova tentativa");
        stop.SetValue(form, true);
        try { Expect<OperationCanceledException>(() => retry(remaining => { throw new Exception("Nao deve iniciar"); }, 5000)); }
        finally { stop.SetValue(form, false); }

        string foreignMarker = Path.Combine(folder, "installer-unrelated.pid");
        using (var unrelated = Process.Start(new ProcessStartInfo(fixtureExe, "wait \"" + foreignMarker + "\"")
            { UseShellExecute = false, CreateNoWindow = true }))
        {
            try
            {
                string marker = Path.Combine(folder, "installer-cancel.pid");
                var cancelProcess = new Thread(() =>
                {
                    var ready = Stopwatch.StartNew();
                    while (!File.Exists(marker) && ready.ElapsedMilliseconds < 5000) Thread.Sleep(25);
                    stop.SetValue(form, true);
                });
                cancelProcess.Start();
                try { Expect<OperationCanceledException>(() => Call(form, "RunCommand", fixtureExe, "wait \"" + marker + "\"", 10000, "teste cancelavel", false)); }
                finally { cancelProcess.Join(); stop.SetValue(form, false); }
                CheckExited(marker);
                string timeoutMarker = Path.Combine(folder, "installer-timeout.pid");
                Expect<TimeoutException>(() => Call(form, "RunCommand", fixtureExe, "wait \"" + timeoutMarker + "\"", 2000, "teste timeout", false));
                CheckExited(timeoutMarker);
                Check(!unrelated.HasExited, "Cancelamento e timeout encerram somente processo iniciado pela rotina");
            }
            finally
            {
                if (!unrelated.HasExited) unrelated.Kill();
                unrelated.WaitForExit(3000);
            }
        }
    }

    private static void CheckExited(string marker)
    {
        Check(File.Exists(marker), "Fixture de instalacao iniciou antes de cancelar/expirar");
        int pid = int.Parse(File.ReadAllText(marker));
        try
        {
            using (var process = Process.GetProcessById(pid))
                Check(process.WaitForExit(3000), "Processo iniciado pela rotina foi encerrado");
        }
        catch (ArgumentException) { Check(true, "Processo iniciado pela rotina foi encerrado"); }
    }
}
