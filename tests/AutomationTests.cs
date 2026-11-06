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

        var dismiss = type.GetMethod("DismissAvaloniaDialog", Instances);
        var stop = type.GetField("stopRequested", Instances);
        foreach (string scenario in new[] { "launch", "normal", "mixed", "title", "edit", "unrelated", "no-ok", "disabled", "hidden", "other-process", "cancel" })
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
}
