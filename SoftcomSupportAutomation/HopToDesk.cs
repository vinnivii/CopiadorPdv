using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SoftcomSupportAutomation
{
    internal sealed partial class SupportForm
    {
        private const string RemoteDisplayName = "Softhecom Remoto";
        private const string RemoteAppName = RemoteDisplayName;
        // Arquivo original assinado, gerado pelo Client Builder oficial do HopToDesk.
        private const string HopToDeskDownloadUrl = "https://github.com/vinnivii/CopiadorPdv/releases/download/softhecom-remoto-v1.46.80/softhecomremoto-install.exe";
        private const string HopToDeskPublisher = "Begonia Holdings LLC";
        private const string HopToDeskFolder = @"C:\Softcom\ResetCaixa\SofthecomRemoto";
        private const string HopToDeskServiceName = RemoteAppName;
        private static readonly string[] RemoteAppNames = { RemoteAppName, "SofthecomRemoto", "HopToDesk" };

        private void HopToDeskButton_Click(object sender, EventArgs e)
        {
            if (automationRunning) return;
            if (!IsAdministrator())
            {
                MessageBox.Show(this, "Execute este programa como administrador.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            char[] password;
            try { password = ReadHopToDeskPassword(); }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                hopToDeskPasswordTextBox.Focus();
                return;
            }
            ClearHopToDeskPasswordControls();
            PrepareAutomationStart();
            try
            {
                var worker = new Thread(() => RunHopToDeskInstallation(password)) { IsBackground = true };
                worker.SetApartmentState(ApartmentState.STA);
                worker.Start();
            }
            catch
            {
                if (password != null) Array.Clear(password, 0, password.Length);
                FinishAutomation();
                MessageBox.Show(this, "Não foi possível iniciar a rotina do Softhecom Remoto. Tente novamente.",
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunHopToDeskInstallation(char[] password)
        {
            string installerPath = null;
            string partialPath = null;
            bool installationStarted = false;
            bool installationConfirmed = false;
            bool existingInstallation = false;
            bool passwordConfigurationStarted = false;
            bool passwordConfigured = false;
            bool unattendedConfirmed = false;
            string passwordFailure = null;
            string stage = "detecção da instalação";
            try
            {
                runFolder = Path.Combine(HopToDeskFolder, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss-fff"));
                Directory.CreateDirectory(runFolder);
                logFile = Path.Combine(runFolder, "processo.log");
                File.WriteAllText(logFile, string.Empty, new UTF8Encoding(true));
                WriteLog("Início: instalação do Softhecom Remoto. Privilégio administrativo confirmado.");
                WriteLog("Central em execução: versão " + ApplicationVersion + ".");
                ThrowIfCancellationRequested();
                string[] candidates = GetHopToDeskExecutableCandidates(out existingInstallation);
                foreach (string candidate in candidates.Where(File.Exists))
                {
                    existingInstallation = true;
                    WriteLog("Instalação de acesso remoto encontrada em: " + candidate);
                }
                if (existingInstallation)
                {
                    WriteLog("Instalação existente detectada. Aguardando escolha do técnico para reinstalar/atualizar.");
                    bool reinstall = (bool)Invoke((Func<bool>)(() => !stopRequested &&
                        MessageBox.Show(this,
                            "O Softhecom Remoto ou uma instalação anterior já possui registro no Windows.\r\n\r\n" +
                            "Deseja instalar/atualizar o Softhecom Remoto?\r\n" +
                            (password != null
                                ? "A senha permanente será substituída pela informada nesta execução."
                                : "A Central não alterará senhas ou configurações de acesso."),
                            Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                            MessageBoxDefaultButton.Button2) == DialogResult.Yes));
                    ThrowIfCancellationRequested();
                    if (!reinstall)
                    {
                        WriteLog("Reinstalação/atualização não autorizada pelo técnico. Instalação existente mantida.");
                        return;
                    }
                }
                else WriteLog("Softhecom Remoto não instalado. Será feita uma instalação nova.");

                stage = "download do instalador";
                installerPath = Path.Combine(HopToDeskFolder, "softhecomremoto-install.exe");
                partialPath = installerPath + ".partial";
                // Nunca reutiliza uma saída antiga, mesmo depois de uma falha ou interrupção.
                if (File.Exists(installerPath)) File.Delete(installerPath);
                WriteLog("Baixando instalador do Softhecom Remoto pelo GitHub Releases.");
                RunTimedPhase("Download do Softhecom Remoto", () =>
                    DownloadFileWithCancellation(HopToDeskDownloadUrl, partialPath, AutomationUserAgent));
                ThrowIfCancellationRequested();
                File.Move(partialPath, installerPath);

                stage = "validação do instalador";
                // Impede alteração ou substituição do arquivo entre a validação e sua execução.
                using (var installerLock = new FileStream(installerPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    ValidateHopToDeskInstaller(installerPath);
                    WriteLog("Instalador validado. Assinatura digital confiável de " + HopToDeskPublisher + ".");
                    ThrowIfCancellationRequested();
                    stage = "instalação silenciosa";
                    WriteLog("Instalando Softhecom Remoto com --silent-install. Aguarde a conclusão.");
                    installationStarted = true;
                    RunTimedPhase("Instalação do Softhecom Remoto", () =>
                        RunCommand(installerPath, "--silent-install", 300000, "Instalação do Softhecom Remoto"));
                }

                stage = "verificação do aplicativo e do serviço";
                WriteLog("Confirmando executável instalado e serviço " + HopToDeskServiceName + " em execução.");
                string installedExecutable = RunTimedPhase("Confirmação do serviço do Softhecom Remoto", () => WaitForHopToDeskInstallation(30000));
                installationConfirmed = true;
                WriteLog("Softhecom Remoto instalado em: " + installedExecutable);
                WriteLog("Serviço " + HopToDeskServiceName + " instalado e em execução.");
                if (password != null)
                {
                    stage = "configuração e verificação da senha permanente";
                    passwordConfigurationStarted = true;
                    try
                    {
                        unattendedConfirmed = ConfigureHopToDeskPassword(installedExecutable, password);
                        passwordConfigured = true;
                    }
                    catch (InvalidOperationException ex)
                    {
                        passwordFailure = ex.Message;
                        WriteLog("ATENÇÃO: " + passwordFailure + " A janela será aberta para conferência do ID.");
                    }
                    finally
                    {
                        Array.Clear(password, 0, password.Length);
                        password = null;
                    }
                }
                else WriteLog("Configuração de senha permanente não selecionada. Senhas e opções de acesso mantidas.");
                ThrowIfCancellationRequested();
                stage = "abertura do aplicativo";
                OpenHopToDeskApplication(installedExecutable, 15000);
                if (passwordFailure != null)
                {
                    stage = "configuração e verificação da senha permanente";
                    throw new InvalidOperationException(passwordFailure);
                }
                ShowFinal("Softhecom Remoto instalado e serviço em execução.\r\n" +
                    (passwordConfigured ? unattendedConfirmed
                        ? "Senha permanente configurada e acesso não supervisionado confirmado.\r\n"
                        : "Senha permanente configurada. Acesso não supervisionado não confirmado.\r\n" +
                          "Ative Acesso não supervisionado e confira as opções de senha no aplicativo.\r\n"
                        : string.Empty) +
                    "Confira o ID e teste uma conexão de outro dispositivo.\r\nLog: " + logFile,
                    passwordConfigured && !unattendedConfirmed ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                string state = GetHopToDeskInstallationState(existingInstallation, installationStarted, installationConfirmed);
                state += GetHopToDeskPasswordState(passwordConfigurationStarted, passwordConfigured, unattendedConfirmed);
                WriteLog("CANCELADO durante " + stage + ". " + state);
                ShowFinal("Instalação do Softhecom Remoto cancelada durante " + stage + ".\r\n" + state +
                    "\r\nLog: " + logFile, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                string state = GetHopToDeskInstallationState(existingInstallation, installationStarted, installationConfirmed);
                state += GetHopToDeskPasswordState(passwordConfigurationStarted, passwordConfigured, unattendedConfirmed);
                WriteLog("ERRO durante " + stage + ": " + ex + "\r\n" + state);
                ShowFinal("Falha na etapa de " + stage + " do Softhecom Remoto.\r\n" + ex.Message +
                    "\r\n" + state + "\r\nLog: " + logFile, MessageBoxIcon.Error);
            }
            finally
            {
                if (password != null) Array.Clear(password, 0, password.Length);
                BeginInvoke((Action)ClearHopToDeskPasswordControls);
                DeleteHopToDeskDownload(partialPath);
                DeleteHopToDeskDownload(installerPath);
                FinishAutomation();
            }
        }

        private void OpenHopToDeskApplication(string executable, int timeoutMilliseconds)
        {
            ThrowIfCancellationRequested();
            using (Process application = Process.Start(new ProcessStartInfo(executable)
            {
                WorkingDirectory = Path.GetDirectoryName(executable),
                UseShellExecute = true
            }))
            {
                if (application == null) throw new InvalidOperationException("Não foi possível abrir o Softhecom Remoto.");
            }
            int sessionId;
            using (var current = Process.GetCurrentProcess()) sessionId = current.SessionId;
            var elapsed = Stopwatch.StartNew();
            while (elapsed.ElapsedMilliseconds < timeoutMilliseconds)
            {
                ThrowIfCancellationRequested();
                // O processo de lançamento pode sair após encaminhar a abertura à instância da bandeja.
                foreach (Process process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
                using (process)
                {
                    try
                    {
                        if (process.SessionId != sessionId ||
                            !string.Equals(process.MainModule.FileName, executable, StringComparison.OrdinalIgnoreCase) ||
                            !string.Equals(process.MainWindowTitle, RemoteDisplayName, StringComparison.OrdinalIgnoreCase)) continue;
                        IntPtr window = process.MainWindowHandle;
                        if (window == IntPtr.Zero) continue;
                        ShowWindow(window, 9);
                        BringWindowToTop(window);
                        SetForegroundWindow(window);
                        WriteLog("Janela do Softhecom Remoto aberta para consulta do ID e teste de conexão.");
                        return;
                    }
                    catch (InvalidOperationException) { }
                    catch (System.ComponentModel.Win32Exception) { }
                }
                SleepWithCancellation(100);
            }
            throw new InvalidOperationException("O Softhecom Remoto está instalado e seu processo foi iniciado, mas a janela principal não apareceu. " +
                "Abra-o pelo menu Iniciar para conferir o ID; o serviço não foi encerrado.");
        }

        private static string GetHopToDeskInstallationState(bool existed, bool started, bool confirmed)
        {
            if (confirmed)
                return "O Softhecom Remoto está instalado e o serviço está em execução. Abra o aplicativo pelo menu Iniciar para consultar o ID.";
            if (started)
                return "A instalação pode ter sido alterada. Verifique o Softhecom Remoto e seu serviço no Windows e execute a rotina novamente para concluir.";
            return (existed ? "A instalação existente não foi alterada." : "Nenhuma instalação foi iniciada.") +
                " Verifique a conexão e a mensagem do erro antes de tentar novamente.";
        }

        private void DeleteHopToDeskDownload(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { WriteLog("Não foi possível remover o download descartado em " + path + ": " + ex.Message); }
        }

        private static string GetHopToDeskServiceExecutable(string appName = HopToDeskServiceName)
        {
            using (var service = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + appName))
                return service == null ? null : GetHopToDeskExecutableFromCommand(service.GetValue("ImagePath") as string);
        }

        private static string GetHopToDeskExecutableFromCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command)) return null;
            string value = Environment.ExpandEnvironmentVariables(command.Trim());
            if (value.StartsWith("\"", StringComparison.Ordinal))
            {
                int endQuote = value.IndexOf('"', 1);
                return endQuote > 1 ? value.Substring(1, endQuote - 1) : null;
            }
            int endExe = value.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return endExe >= 0 ? value.Substring(0, endExe + 4) : null;
        }

        private static string[] GetHopToDeskExecutableCandidates(out bool registered)
        {
            var candidates = new List<string>();
            registered = false;
            foreach (string appName in RemoteAppNames)
            {
                using (var service = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + appName))
                    registered |= service != null;
                candidates.Add(GetHopToDeskServiceExecutable(appName));
            }
            string[] roots =
            {
                Environment.GetEnvironmentVariable("ProgramW6432"),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };
            candidates.AddRange(GetRemoteExecutablePaths(roots));

            foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            foreach (RegistryView view in GetUninstallRegistryViews())
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (uninstall == null) continue;
                    foreach (string name in uninstall.GetSubKeyNames())
                    using (var entry = uninstall.OpenSubKey(name))
                    {
                        string appName = entry == null ? null : GetRemoteAppName(entry.GetValue("DisplayName") as string);
                        if (appName == null) continue;
                        registered = true;
                        string location = entry.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(location))
                            candidates.Add(Path.Combine(Environment.ExpandEnvironmentVariables(location.Trim('"')), appName + ".exe"));
                        candidates.Add(GetHopToDeskExecutableFromCommand(entry.GetValue("DisplayIcon") as string));
                    }
                }
            }
            return candidates.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string GetRemoteAppName(string displayName)
        {
            string name = (displayName ?? string.Empty).Trim();
            return RemoteAppNames.FirstOrDefault(appName => string.Equals(appName, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string[] GetRemoteExecutablePaths(string[] roots)
        {
            return roots.Where(root => !string.IsNullOrWhiteSpace(root))
                .SelectMany(root => RemoteAppNames.Select(appName => Path.Combine(root, appName, appName + ".exe")))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string GetRemoteConfigPath(string roamingFolder, string appName, string suffix)
        {
            return Path.Combine(roamingFolder, appName, "config", appName + suffix + ".toml");
        }

        private string WaitForHopToDeskInstallation(int timeoutMilliseconds)
        {
            var elapsed = Stopwatch.StartNew();
            while (elapsed.ElapsedMilliseconds < timeoutMilliseconds)
            {
                ThrowIfCancellationRequested();
                string executable = GetHopToDeskServiceExecutable();
                if (!string.IsNullOrWhiteSpace(executable) && File.Exists(executable))
                {
                    using (var service = new ServiceController(HopToDeskServiceName))
                    {
                        try { if (service.Status == ServiceControllerStatus.Running) return executable; }
                        catch (InvalidOperationException) { } // Serviço ainda pode estar sendo registrado.
                    }
                }
                SleepWithCancellation(500);
            }
            throw new InvalidOperationException("O instalador terminou, mas não foi possível confirmar o executável e o serviço " +
                HopToDeskServiceName + " em execução. Uma instalação anterior de outro perfil não confirma esta instalação.");
        }

        private void ValidateHopToDeskInstaller(string path)
        {
            ThrowIfCancellationRequested();
            using (var reader = new BinaryReader(File.OpenRead(path)))
            {
                if (reader.BaseStream.Length < 512 || reader.ReadUInt16() != 0x5A4D)
                    throw new InvalidDataException("O download não contém um executável Windows válido.");
                reader.BaseStream.Position = 0x3C;
                int peOffset = reader.ReadInt32();
                if (peOffset < 64 || peOffset > reader.BaseStream.Length - 24)
                    throw new InvalidDataException("O executável baixado está incompleto ou é inválido.");
                reader.BaseStream.Position = peOffset;
                if (reader.ReadUInt32() != 0x00004550)
                    throw new InvalidDataException("O download não contém um executável Windows válido.");
            }
            ThrowIfCancellationRequested();
            VerifyHopToDeskSignature(path);
            ThrowIfCancellationRequested();
        }

        private static void VerifyHopToDeskSignature(string path)
        {
            var file = new HopToDeskTrustFile
            {
                Size = (uint)Marshal.SizeOf(typeof(HopToDeskTrustFile)),
                FilePath = path
            };
            IntPtr filePointer = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(HopToDeskTrustFile)));
            Marshal.StructureToPtr(file, filePointer, false);
            var trust = new HopToDeskTrustData
            {
                Size = (uint)Marshal.SizeOf(typeof(HopToDeskTrustData)),
                UiChoice = 2, // WTD_UI_NONE
                UnionChoice = 1, // WTD_CHOICE_FILE
                FileInfo = filePointer,
                StateAction = 1, // WTD_STATEACTION_VERIFY
                // Usa a confiança local do Windows, sem aguardar rede para consultar revogação.
                ProviderFlags = 0x1000 | 0x10 // WTD_CACHE_ONLY_URL_RETRIEVAL | WTD_REVOCATION_CHECK_NONE
            };
            Guid action = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
            try
            {
                int result = WinVerifyTrust(new IntPtr(-1), ref action, ref trust);
                if (result != 0)
                    throw new InvalidDataException("Assinatura digital ausente, inválida ou não confiável (0x" +
                        result.ToString("X8") + "). O instalador não será executado. Verifique os certificados do Windows e tente novamente.");
            }
            finally
            {
                trust.StateAction = 2; // WTD_STATEACTION_CLOSE
                WinVerifyTrust(new IntPtr(-1), ref action, ref trust);
                Marshal.DestroyStructure(filePointer, typeof(HopToDeskTrustFile));
                Marshal.FreeHGlobal(filePointer);
            }
            using (var certificate = X509Certificate.CreateFromSignedFile(path))
            using (var signer = new X509Certificate2(certificate))
            {
                if (!string.Equals(signer.GetNameInfo(X509NameType.SimpleName, false), HopToDeskPublisher,
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("O instalador não foi assinado pelo fabricante esperado: " + HopToDeskPublisher + ".");
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct HopToDeskTrustFile
        {
            public uint Size;
            [MarshalAs(UnmanagedType.LPWStr)] public string FilePath;
            public IntPtr FileHandle;
            public IntPtr KnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HopToDeskTrustData
        {
            public uint Size;
            public IntPtr PolicyCallbackData;
            public IntPtr SipClientData;
            public uint UiChoice;
            public uint RevocationChecks;
            public uint UnionChoice;
            public IntPtr FileInfo;
            public uint StateAction;
            public IntPtr StateData;
            public IntPtr UrlReference;
            public uint ProviderFlags;
            public uint UiContext;
        }

        [DllImport("wintrust.dll", ExactSpelling = true)]
        private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref HopToDeskTrustData trustData);
    }
}
