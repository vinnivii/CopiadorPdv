using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

public static class RemoteTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CommandLineToArgvW(string command, out int count);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);

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

    public static void Run(object form, string folder, string fixtureExe)
    {
        Type type = form.GetType();
        string secret = Guid.NewGuid().ToString("N") + " Unicode á中 & % ! \\\" final\\";
        char[] password = secret.ToCharArray();
        string quoted = (string)type.GetMethod("QuoteHopToDeskPassword", Static).Invoke(null, new object[] { password });
        int count;
        IntPtr args = CommandLineToArgvW("fixture.exe --password " + quoted, out count);
        try
        {
            Check(args != IntPtr.Zero && count == 3 && Marshal.PtrToStringUni(Marshal.ReadIntPtr(args, 2 * IntPtr.Size)) == secret,
                "Senha com Unicode, espacos, aspas, barras e metacaracteres preservada como argumento unico");
        }
        finally { LocalFree(args); }
        var stop = type.GetField("stopRequested", Instance);
        try
        {
            foreach (string scenario in new[] { "Done", "NoAck", "Echo", "Hang" })
            {
                string path = Path.Combine(folder, "Remote" + scenario + ".exe");
                File.Copy(fixtureExe, path);
                Exception error = null;
                Thread cancel = null;
                if (scenario == "Hang")
                {
                    cancel = new Thread(() =>
                    {
                        var ready = Stopwatch.StartNew();
                        while (!File.Exists(Path.ChangeExtension(path, ".pid")) && ready.ElapsedMilliseconds < 5000) Thread.Sleep(25);
                        stop.SetValue(form, true);
                    });
                    cancel.Start();
                }
                try { Call(form, "RunHopToDeskPasswordCommand", path, password); }
                catch (Exception ex) { error = ex; }
                finally
                {
                    if (cancel != null) cancel.Join();
                    stop.SetValue(form, false);
                }
                Check(scenario == "Done" ? error == null : scenario == "Hang" ? error is OperationCanceledException :
                    error is InvalidOperationException, "Comando de senha: " + scenario);
                Check(error == null || !error.ToString().Contains(secret), "Excecao nao expoe senha: " + scenario);
                Check(!File.ReadAllText((string)type.GetField("logFile", Instance).GetValue(form)).Contains(secret),
                    "Log nao expoe senha: " + scenario);
                Check(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)).Length == 0,
                    "Processo do comando encerrado: " + scenario);
            }
        }
        finally { Array.Clear(password, 0, password.Length); stop.SetValue(form, false); }

        foreach (string scenario in new[] { "Ui", "Forwarded", "WrongTitle", "UiCancel" })
        {
            string path = Path.Combine(folder, "Remote" + scenario + ".exe");
            File.Copy(fixtureExe, path);
            Exception error = null;
            Thread cancel = null;
            try
            {
                if (scenario == "Forwarded")
                {
                    using (var existing = Process.Start(new ProcessStartInfo(path, "--ui") { UseShellExecute = false, CreateNoWindow = true }))
                    {
                        var ready = Stopwatch.StartNew();
                        while (existing.MainWindowTitle != "Softhecom Remoto" && ready.ElapsedMilliseconds < 5000)
                        { Thread.Sleep(25); existing.Refresh(); }
                        Check(existing.MainWindowTitle == "Softhecom Remoto", "Instancia existente disponivel antes do encaminhamento");
                    }
                }
                if (scenario == "UiCancel")
                {
                    cancel = new Thread(() => { Thread.Sleep(150); stop.SetValue(form, true); });
                    cancel.Start();
                }
                try { Call(form, "OpenHopToDeskApplication", path, scenario == "WrongTitle" ? 1500 : 5000); }
                catch (Exception ex) { error = ex; }
                Check(scenario == "UiCancel" ? error is OperationCanceledException : scenario == "WrongTitle" ?
                    error is InvalidOperationException : error == null,
                    "Abertura confirma janela principal do executavel correto: " + scenario);
            }
            finally
            {
                if (cancel != null) cancel.Join();
                stop.SetValue(form, false);
                foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(path)))
                using (process)
                {
                    if (!process.HasExited) process.Kill();
                    Check(process.WaitForExit(3000), "Janela de fixture encerrada: " + scenario);
                }
            }
        }
    }
}
