#pragma warning disable 0414
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using File = ProbeFile;
using Directory = ProbeDirectory;

public static class ProbeFile
{
    public static bool Exists(string path) { return path.EndsWith("Softshop.exe", StringComparison.OrdinalIgnoreCase); }
    public static void WriteAllText(string path, string content, Encoding encoding) { }
}
public static class ProbeDirectory
{
    public static bool Exists(string path) { return path.Contains("Softshop Caixa"); }
    public static void CreateDirectory(string path) { }
}
public class WorkflowProbe
{
    private enum PdvAutomationMode { FullReset, CleanReinstall, ConfigurationPrints }
    private PdvAutomationMode selectedPdvAutomationMode;
    private bool updateDllsRequested;
    private string runFolder, logFile;
    private string selectedPdvVersionName = "test-version";
    private volatile bool workflowRunning, stopRequested;
    private int activeSoftshopProcessId, cacheBackupDialogClosed, rpcUnavailableDialogClosed;
    private readonly ManualResetEventSlim packageStarted = new ManualResetEventSlim();
    private readonly ManualResetEventSlim packageMayFinish = new ManualResetEventSlim();
    private readonly List<string> steps = new List<string>();
    private bool failPackage, cancelCapture, cancelDlls, ready;

    // O metodo de producao e inserido integralmente pelo script, sem reescrever suas ramificacoes.
    __RUN_WORKFLOW__

    private void Record(string step) { lock(steps) steps.Add(step); }
    private void ThrowIfCancellationRequested() { if(stopRequested) throw new OperationCanceledException(); }
    private void WriteLog(string message) { }
    private static bool IsAdministrator() { return true; }
    private static string ComputeFileSha256WithCancellation(string path) { return "test"; }
    private string PreparePdvPackage(CancellationToken token)
    {
        Record("prepare"); packageStarted.Set();
        if (failPackage) throw new InvalidDataException("Injected package failure");
        packageMayFinish.Wait(token);
        ready = true;
        Record("ready");
        return "prepared.msi";
    }
    private void StopSoftshop(bool forced = false)
    {
        if(selectedPdvAutomationMode != PdvAutomationMode.ConfigurationPrints && !packageStarted.Wait(3000))
            throw new Exception("Package preparation was not started early");
        Record("stop");
    }
    private string[] GetSoftshopExecutableCandidates()
    {
        return new[] { @"C:\Program Files\Softcom Tecnologia\Softshop Caixa\Softshop.exe", @"C:\Program Files (x86)\Softcom Tecnologia\Softshop Caixa\Softshop.exe" };
    }
    private void CaptureConfigurationTabs(string executable)
    {
        Record("capture"); packageMayFinish.Set();
        if(cancelCapture) stopRequested = true;
    }
    private string WaitForPreparedPdvPackage(Task<string> task, string waiting, string failure)
    {
        Record("wait"); ThrowIfCancellationRequested();
        return task.GetAwaiter().GetResult();
    }
    private void UninstallAllSoftshopEntries() { Record("uninstall"); }
    private void BackupFolders(IEnumerable<string> folders, bool localAppData)
    {
        if(folders.Count() != 2) throw new Exception("Both installation directories must be preserved");
        Record(localAppData ? "preserve+localappdata" : "preserve-install-only");
        if(!updateDllsRequested) packageMayFinish.Set();
    }
    private void UpdateSoftcomDlls()
    {
        Record("dlls");
        if(selectedPdvAutomationMode == PdvAutomationMode.CleanReinstall && ready)
            throw new Exception("DLL update should run while the deliberately delayed package is preparing");
        packageMayFinish.Set();
        if(cancelDlls) stopRequested = true;
    }
    private void InstallPdvMsi(string path) { if(!ready) throw new Exception("Package not ready"); Record("install"); }
    private void OpenNewInstallationAtConfiguration() { Record("open-config"); }
    private void OpenPrintFolder() { Record("open-prints"); }
    private void ShowFinal(string text, MessageBoxIcon icon) { Record(icon == MessageBoxIcon.Error ? "error" : icon == MessageBoxIcon.Warning ? "cancelled" : "success"); }
    private void FinishAutomation() { Record("finished"); }
    private void WatchAndDismissKnownSoftshopDialogs() { }
    private static void Require(bool condition, string message)
    {
        if(!condition) throw new Exception("FAIL: " + message);
        Console.WriteLine("PASS: " + message);
    }
    private static WorkflowProbe Run(PdvAutomationMode mode, bool dlls, bool fail = false, bool cancel = false, bool cancelDlls = false)
    {
        var probe = new WorkflowProbe { selectedPdvAutomationMode = mode, updateDllsRequested = dlls,
            failPackage = fail, cancelCapture = cancel, cancelDlls = cancelDlls };
        probe.RunWorkflow();
        Console.WriteLine("TRACE: " + mode + ", dlls=" + dlls + ": " + string.Join(" -> ", probe.steps));
        return probe;
    }
    public static void RunAll()
    {
        foreach(bool dlls in new[] { false, true })
        {
            var clean = Run(PdvAutomationMode.CleanReinstall, dlls);
            Require(clean.steps.Contains("success") && clean.steps.Contains("dlls") == dlls, "Reinstalacao Limpa respeita checkbox " + dlls);
            Require(clean.steps.Contains("preserve-install-only") && !clean.steps.Contains("capture") && !clean.steps.Contains("preserve+localappdata") && !clean.steps.Contains("open-config") && !clean.steps.Contains("open-prints"), "Reinstalacao Limpa preserva ambas as instalacoes, sem captura/LocalAppData/abertura");
            Require(clean.steps.IndexOf("prepare") < clean.steps.IndexOf("uninstall") && clean.steps.IndexOf("uninstall") < clean.steps.IndexOf("preserve-install-only") && clean.steps.IndexOf("preserve-install-only") < clean.steps.IndexOf("install"), "Download antecipado e ordem de desinstalacao/preservacao/instalacao");
            if(dlls) Require(clean.steps.IndexOf("preserve-install-only") < clean.steps.IndexOf("dlls") && clean.steps.IndexOf("dlls") < clean.steps.IndexOf("ready"), "DLLs apos preservar pastas, com preparacao do PDV ainda em andamento");
        }
        var full = Run(PdvAutomationMode.FullReset, true);
        Require(full.steps.Contains("capture") && full.steps.Contains("dlls") && full.steps.Contains("preserve+localappdata") && full.steps.Contains("open-config") && full.steps.Contains("open-prints") && full.steps.Contains("success"), "Reset Completo mantem captura, DLLs, preservacao e abertura");
        Require(full.steps.IndexOf("ready") < full.steps.IndexOf("uninstall"), "Reset Completo so desinstala apos pacote pronto");
        var prints = Run(PdvAutomationMode.ConfigurationPrints, true);
        Require(prints.steps.Contains("capture") && prints.steps.Contains("open-prints") && !prints.steps.Contains("prepare") && !prints.steps.Contains("uninstall") && !prints.steps.Contains("dlls") && !prints.steps.Contains("install"), "Prints de Configuracao nao prepara, instala ou atualiza DLLs mesmo com flag antiga");
        var failure = Run(PdvAutomationMode.FullReset, true, true);
        Require(failure.steps.Contains("error") && !failure.steps.Contains("uninstall") && !failure.steps.Contains("install"), "Falha do pacote no Reset Completo preserva instalacao atual");
        var cancellation = Run(PdvAutomationMode.FullReset, true, false, true);
        Require(cancellation.steps.Contains("cancelled") && !cancellation.steps.Contains("uninstall") && !cancellation.steps.Contains("install") && cancellation.steps.Last() == "finished", "Cancelamento na captura impede desinstalacao/instalacao e encerra workflow");
        var dllCancellation = Run(PdvAutomationMode.CleanReinstall, true, false, false, true);
        Require(dllCancellation.steps.Contains("cancelled") && !dllCancellation.steps.Contains("install") && dllCancellation.steps.Last() == "finished", "Cancelamento durante DLLs na Reinstalacao Limpa impede instalar PDV");
    }
}
