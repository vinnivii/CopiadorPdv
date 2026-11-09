using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Automation;
using System.Windows.Forms;

public static class AutomationTests
{
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags Instances = BindingFlags.Instance | BindingFlags.NonPublic;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }

    public static void Run(object form, string folder, string fixtureExe)
    {
        Type type = form.GetType();
        Func<string, string> appName = name => (string)type.GetMethod("GetRemoteAppName", Statics).Invoke(null, new object[] { name });
        Check(appName("SofthecomRemoto") == "SofthecomRemoto" && appName("softhecom remoto") == "SofthecomRemoto" &&
            appName("HopToDesk") == "HopToDesk" && appName("Outro produto") == null,
            "Perfis personalizado e legado reconhecidos sem aceitar outro produto");
        Check((string)type.GetField("HopToDeskServiceName", Statics).GetRawConstantValue() == "SofthecomRemoto",
            "Confirmacao posterior a instalacao exige o servico personalizado");
        var roots = new[] { @"C:\Program Files", @"C:\Program Files (x86)", null, "", @"C:\Program Files" };
        var paths = (string[])type.GetMethod("GetRemoteExecutablePaths", Statics).Invoke(null, new object[] { roots });
        Check(paths.Length == 4 && paths.Contains(@"C:\Program Files\SofthecomRemoto\SofthecomRemoto.exe") &&
            paths.Contains(@"C:\Program Files (x86)\SofthecomRemoto\SofthecomRemoto.exe") &&
            paths.Contains(@"C:\Program Files\HopToDesk\HopToDesk.exe") && paths.Contains(@"C:\Program Files (x86)\HopToDesk\HopToDesk.exe"),
            "Ambos os perfis em Program Files e Program Files (x86), sem duplicar caminhos");
        var configPath = type.GetMethod("GetRemoteConfigPath", Statics);
        var hasPassword = type.GetMethod("HasHopToDeskPermanentPassword", Statics);
        foreach (string profile in new[] { "HopToDesk", "SofthecomRemoto" })
        {
            string path = (string)configPath.Invoke(null, new object[] { folder, profile, "" });
            string options = (string)configPath.Invoke(null, new object[] { folder, profile, "2" });
            Check(path == Path.Combine(folder, profile, "config", profile + ".toml") &&
                options == Path.Combine(folder, profile, "config", profile + "2.toml"), "Arquivos nativos do perfil " + profile);
            Check(!(bool)hasPassword.Invoke(null, new object[] { path }), "Configuracao de outro perfil nao confirma " + profile);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, "permanent_password = 'encrypted-test-fixture-only'\n");
            Check((bool)hasPassword.Invoke(null, new object[] { path }), "Presenca da configuracao propria de " + profile);
        }
        Check(((Button)type.GetField("hopToDeskButton", Instances).GetValue(form)).Text == "Instalar Softhecom Remoto",
            "Botao identificado como Softhecom Remoto");
        var passwordBox = (TextBox)type.GetField("hopToDeskPasswordTextBox", Instances).GetValue(form);
        Check(passwordBox.Text == "" && passwordBox.UseSystemPasswordChar &&
            ((CheckBox)type.GetField("configureHopToDeskPasswordCheckBox", Instances).GetValue(form)).Checked,
            "Senha inicialmente vazia, oculta e habilitada por padrao");

        VerifyModalConfiguration(form, folder, fixtureExe);

        var dismiss = type.GetMethod("DismissAvaloniaDialog", Instances);
        var stop = type.GetField("stopRequested", Instances);
        foreach (string scenario in new[] { "launch", "login", "normal", "mixed", "title", "edit", "unrelated", "no-ok", "disabled", "hidden", "other-process", "cancel" })
        {
            string marker = scenario == "launch" ? Path.ChangeExtension(fixtureExe, ".clicked") : Path.Combine(folder, "dialog-" + scenario + ".txt");
            Process fixture;
            if (scenario == "launch")
            {
                type.GetMethod("StartSoftshopForConfiguration", Instances).Invoke(form, new object[] { fixtureExe });
                int processId = (int)type.GetField("activeSoftshopProcessId", Instances).GetValue(form);
                Check(processId > 0, "PID registrado antes da busca pelo login");
                fixture = Process.GetProcessById(processId);
            }
            else fixture = Process.Start(new ProcessStartInfo(fixtureExe, scenario + " \"" + marker + "\"")
                { UseShellExecute = false, CreateNoWindow = true });
            using (var process = fixture)
            {
                try
                {
                    var ready = Stopwatch.StartNew();
                    AutomationElement window = null;
                    while (ready.ElapsedMilliseconds < 5000)
                    {
                        window = AutomationElement.RootElement.FindFirst(TreeScope.Children,
                            new PropertyCondition(AutomationElement.ProcessIdProperty, process.Id));
                        if (window != null && window.FindFirst(TreeScope.Descendants,
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)) != null) break;
                        Thread.Sleep(25);
                    }
                    Check(window != null, "Janela de teste disponivel: " + scenario);
                    if (scenario == "login")
                    {
                        var activePid = type.GetField("activeSoftshopProcessId", Instances);
                        activePid.SetValue(form, process.Id);
                        var login = (AutomationElement)type.GetMethod("WaitForSoftshopWindow", Instances).Invoke(form, new object[] { 1 });
                        Check(login != null && login.Current.ProcessId == process.Id, "Login associado ao PID iniciado");
                        activePid.SetValue(form, Process.GetCurrentProcess().Id);
                        Check(type.GetMethod("WaitForSoftshopWindow", Instances).Invoke(form, new object[] { 1 }) == null,
                            "Janela de outro PID nao e aceita como login");
                    }
                    if (scenario == "edit")
                    {
                        type.GetField("activeSoftshopProcessId", Instances).SetValue(form, process.Id);
                        Check((bool)type.GetMethod("IsLoginWindow", Statics).Invoke(null, new object[] { window }),
                            "Fixture reproduz dialogo de erro com campo que parecia login");
                        Check(type.GetMethod("WaitForSoftshopWindow", Instances).Invoke(form, new object[] { 1 }) == null,
                            "Espera do login rejeita o dialogo Avalonia mesmo com campo de texto");
                    }
                    stop.SetValue(form, scenario == "cancel");
                    int target = scenario == "other-process" ? Process.GetCurrentProcess().Id : process.Id;
                    bool expected = scenario == "launch" || scenario == "normal" || scenario == "mixed" || scenario == "title" || scenario == "edit";
                    bool clicked = (bool)dismiss.Invoke(form, new object[] { target });
                    if (expected)
                    {
                        var completed = Stopwatch.StartNew();
                        while (!File.Exists(marker) && completed.ElapsedMilliseconds < 3000) Thread.Sleep(25);
                    }
                    Check(clicked == expected && File.Exists(marker) == expected, "Aviso Avalonia: " + scenario);
                }
                finally
                {
                    stop.SetValue(form, false);
                    type.GetField("activeSoftshopProcessId", Instances).SetValue(form, 0);
                    if (!process.HasExited) process.Kill();
                    Check(process.WaitForExit(3000), "Processo de fixture encerrado: " + scenario);
                }
            }
        }
    }

    private static void VerifyModalConfiguration(object form, string folder, string fixtureExe)
    {
        Type type = form.GetType();
        var open = type.GetMethod("OpenConfigurationFromLogin", Instances);
        var stop = type.GetField("stopRequested", Instances);
        foreach (string scenario in new[] { "modal-config", "modal-untitled", "modal-slow", "modal-unknown", "modal-avalonia", "modal-rpc", "modal-cancel" })
        {
            string marker = Path.Combine(folder, scenario + ".txt");
            using (var process = Process.Start(new ProcessStartInfo(fixtureExe, scenario + " \"" + marker + "\"") { UseShellExecute = false }))
            {
                try
                {
                    type.GetField("activeSoftshopProcessId", Instances).SetValue(form, process.Id);
                    var login = (AutomationElement)type.GetMethod("WaitForSoftshopWindow", Instances).Invoke(form, new object[] { 5 });
                    Check(login != null, "Login da fixture modal: " + scenario);
                    File.WriteAllText(marker + ".show", "show");
                    var modalReady = Stopwatch.StartNew();
                    while (login.Current.IsEnabled && modalReady.ElapsedMilliseconds < 5000) Thread.Sleep(25);
                    Check(!login.Current.IsEnabled, "Login bloqueado pela janela modal: " + scenario);
                    bool expected = scenario == "modal-config" || scenario == "modal-untitled" || scenario == "modal-slow";
                    AutomationElement config = null;
                    Exception failure = null;
                    var elapsed = Stopwatch.StartNew();
                    Thread cancellation = null;
                    if (scenario == "modal-cancel")
                    {
                        cancellation = new Thread(() => { Thread.Sleep(150); stop.SetValue(form, true); });
                        cancellation.Start();
                    }
                    try { config = (AutomationElement)open.Invoke(form, new object[] { login, 1, expected ? 10 : 1 }); }
                    catch (TargetInvocationException ex) { failure = ex.InnerException; }
                    finally { if (cancellation != null) cancellation.Join(); }
                    Check(expected ? config != null && config.Current.ProcessId == process.Id :
                        config == null && (scenario == "modal-cancel" ? failure is OperationCanceledException && elapsed.ElapsedMilliseconds < 1500 :
                            failure is InvalidOperationException), "Configuracao modal: " + scenario +
                        (failure == null ? "" : " (" + failure.Message + ")"));
                    Check(!File.Exists(marker), "F11 nao enviado sobre login bloqueado: " + scenario);
                    if (expected)
                    {
                        var wrongPid = type.GetMethod("WaitForConfigurationWindow", Instances).Invoke(form,
                            new object[] { Process.GetCurrentProcess().Id, 1 });
                        Check(wrongPid == null, "Configuracoes de outro PID rejeitadas: " + scenario);
                    }
                    else if (scenario == "modal-avalonia" || scenario == "modal-rpc")
                    {
                        bool dismissed = scenario == "modal-avalonia" ?
                            (bool)type.GetMethod("DismissAvaloniaDialog", Instances).Invoke(form, new object[] { process.Id }) :
                            (bool)type.GetMethod("DismissRpcUnavailableDialog", Statics).Invoke(null, new object[] { process.Id });
                        var clicked = Stopwatch.StartNew();
                        while (!File.Exists(marker + ".ok") && clicked.ElapsedMilliseconds < 3000) Thread.Sleep(25);
                        Check(dismissed && File.Exists(marker + ".ok"), "Monitor encontra aviso modal conhecido: " + scenario);
                    }
                    else Check(!File.Exists(marker + ".ok"), "Aviso desconhecido/cancelado nao fechado: " + scenario);
                }
                finally
                {
                    stop.SetValue(form, false);
                    type.GetField("activeSoftshopProcessId", Instances).SetValue(form, 0);
                    if (!process.HasExited) process.Kill();
                    Check(process.WaitForExit(3000), "Processo modal encerrado: " + scenario);
                }
            }
        }
    }
}
