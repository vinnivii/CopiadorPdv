using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace SoftcomSupportAutomation
{
    internal sealed partial class SupportForm
    {
        private readonly CheckBox configureHopToDeskPasswordCheckBox = new CheckBox();
        private readonly TextBox hopToDeskPasswordTextBox = new TextBox();
        private readonly Button showHopToDeskPasswordButton = new Button();

        private Panel CreateHopToDeskCard()
        {
            var card = new Panel { Size = new Size(352, 232), BackColor = DarkCard };
            ApplyPanelBorder(card);
            card.Controls.Add(new Label
            {
                Location = new Point(12, 8), Size = new Size(328, 20), Text = "HopToDesk",
                ForeColor = TextPrimary, Font = new Font(Font.FontFamily, 9, FontStyle.Bold)
            });
            card.Controls.Add(new Label
            {
                Location = new Point(12, 30), Size = new Size(328, 34), ForeColor = TextSecondary,
                Text = "Instala o HopToDesk silenciosamente para acesso remoto aos clientes."
            });
            configureHopToDeskPasswordCheckBox.Text = "Configurar senha permanente";
            configureHopToDeskPasswordCheckBox.Location = new Point(12, 68);
            configureHopToDeskPasswordCheckBox.Size = new Size(328, 24);
            configureHopToDeskPasswordCheckBox.Checked = true;
            configureHopToDeskPasswordCheckBox.CheckedChanged += (sender, args) =>
            {
                if (!configureHopToDeskPasswordCheckBox.Checked) ClearHopToDeskPasswordControls();
                SetHopToDeskPasswordControlsEnabled(!automationRunning);
            };
            card.Controls.Add(configureHopToDeskPasswordCheckBox);
            card.Controls.Add(new Label
            {
                Location = new Point(12, 98), Size = new Size(328, 20), ForeColor = TextSecondary,
                Text = "Senha permanente de acesso"
            });
            hopToDeskPasswordTextBox.Location = new Point(12, 122);
            hopToDeskPasswordTextBox.Size = new Size(240, 26);
            hopToDeskPasswordTextBox.UseSystemPasswordChar = true;
            hopToDeskPasswordTextBox.MaxLength = 128;
            hopToDeskPasswordTextBox.BackColor = ControlBackground;
            hopToDeskPasswordTextBox.ForeColor = TextPrimary;
            hopToDeskPasswordTextBox.BorderStyle = BorderStyle.FixedSingle;
            hopToDeskPasswordTextBox.AccessibleName = "Senha permanente de acesso";
            showHopToDeskPasswordButton.Text = "Mostrar";
            showHopToDeskPasswordButton.Location = new Point(260, 120);
            showHopToDeskPasswordButton.Size = new Size(80, 28);
            StyleActionButton(showHopToDeskPasswordButton, ControlBackground);
            showHopToDeskPasswordButton.Click += (sender, args) =>
            {
                hopToDeskPasswordTextBox.UseSystemPasswordChar = !hopToDeskPasswordTextBox.UseSystemPasswordChar;
                showHopToDeskPasswordButton.Text = hopToDeskPasswordTextBox.UseSystemPasswordChar ? "Mostrar" : "Ocultar";
            };
            card.Controls.Add(hopToDeskPasswordTextBox);
            card.Controls.Add(showHopToDeskPasswordButton);
            card.Controls.Add(new Label
            {
                Location = new Point(12, 156), Size = new Size(328, 34), ForeColor = TextMuted,
                Text = "O Windows pode expor a senha na linha de comando durante a configuração."
            });
            hopToDeskButton.Text = "Instalar HopToDesk";
            hopToDeskButton.Location = new Point(12, 196);
            hopToDeskButton.Size = new Size(328, 28);
            StyleActionButton(hopToDeskButton, Color.FromArgb(55, 55, 55));
            card.Controls.Add(hopToDeskButton);
            return card;
        }

        private void SetHopToDeskPasswordControlsEnabled(bool enabled)
        {
            configureHopToDeskPasswordCheckBox.Enabled = enabled;
            hopToDeskPasswordTextBox.Enabled = enabled && configureHopToDeskPasswordCheckBox.Checked;
            showHopToDeskPasswordButton.Enabled = hopToDeskPasswordTextBox.Enabled;
            if (!enabled)
            {
                hopToDeskPasswordTextBox.UseSystemPasswordChar = true;
                showHopToDeskPasswordButton.Text = "Mostrar";
            }
        }

        private void ClearHopToDeskPasswordControls()
        {
            hopToDeskPasswordTextBox.Clear();
            hopToDeskPasswordTextBox.UseSystemPasswordChar = true;
            showHopToDeskPasswordButton.Text = "Mostrar";
        }

        private char[] ReadHopToDeskPassword()
        {
            if (!configureHopToDeskPasswordCheckBox.Checked) return null;
            string value = hopToDeskPasswordTextBox.Text;
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException("Preencha a senha permanente ou desmarque Configurar senha permanente antes de iniciar.");
            // Limite de persistência do próprio HopToDesk (ENCRYPT_MAX_LEN, em bytes UTF-8).
            if (Encoding.UTF8.GetByteCount(value) > 128 || value.IndexOfAny(new[] { '\0', '\r', '\n' }) >= 0)
                throw new InvalidOperationException("A senha excede o limite do HopToDesk ou contém uma quebra de linha. Use até 128 bytes em UTF-8.");
            return value.ToCharArray();
        }

        private static string GetHopToDeskPasswordState(bool started, bool confirmed, bool unattended)
        {
            if (confirmed) return unattended
                ? " Senha permanente configurada e acesso não supervisionado confirmado; teste a conexão de outro dispositivo."
                : " A senha permanente foi configurada, mas o acesso não supervisionado não foi confirmado. Confira as opções no aplicativo.";
            return started ? " A senha pode ter sido alterada, mas a configuração não foi confirmada. Confira as opções no HopToDesk antes de usar." : string.Empty;
        }

        private bool ConfigureHopToDeskPassword(string executable, char[] password)
        {
            try
            {
                WriteLog("Configurando senha permanente informada nesta execução. O valor não será registrado pela Central.");
                RunHopToDeskPasswordCommand(executable, password);
                ThrowIfCancellationRequested();
                // Leitura somente: o próprio HopToDesk persiste sua senha nativa criptografada.
                string serviceConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    @"ServiceProfiles\LocalService\AppData\Roaming", HopToDeskServiceName, "config");
                string userConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    HopToDeskServiceName, "config");
                var elapsed = Stopwatch.StartNew();
                while (true)
                {
                    ThrowIfCancellationRequested();
                    bool present = false;
                    try
                    {
                        present = HasHopToDeskPermanentPassword(Path.Combine(serviceConfig, "HopToDesk.toml")) &&
                            HasHopToDeskPermanentPassword(Path.Combine(userConfig, "HopToDesk.toml"));
                    }
                    catch (IOException) { } // O aplicativo pode estar concluindo a gravação nativa.
                    if (present) break;
                    if (elapsed.ElapsedMilliseconds >= 5000) throw new InvalidOperationException();
                    SleepWithCancellation(100);
                }
                WaitForHopToDeskInstallation(5000);
                WriteLog("Comando de senha confirmado pelo HopToDesk. Senha permanente presente na configuração nativa do usuário e do serviço.");
                bool unattended = false;
                try { unattended = HopToDeskUnattendedEnabled(ReadHopToDeskAccessOptions(Path.Combine(serviceConfig, "HopToDesk2.toml"))); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (InvalidDataException) { }
                WriteLog(unattended
                    ? "Acesso não supervisionado confirmado nas opções nativas. Teste uma conexão de outro dispositivo."
                    : "ATENÇÃO: acesso não supervisionado não confirmado. Ative Acesso não supervisionado e confira as opções de senha no aplicativo antes de testar.");
                return unattended;
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Nunca propaga stdout, stderr ou exceções que possam incluir o segredo.
                throw new InvalidOperationException("Não foi possível confirmar a configuração da senha permanente. " +
                    "O HopToDesk permanece instalado. Abra o aplicativo, confira a senha e o acesso não supervisionado e teste uma conexão.");
            }
        }

        private static bool HasHopToDeskPermanentPassword(string configPath)
        {
            if (!File.Exists(configPath)) return false;
            foreach (string line in File.ReadLines(configPath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal)) break;
                // Apenas presença; não descriptografa, copia, registra ou regrava o valor.
                if (Regex.IsMatch(trimmed, @"^permanent_password\s*=\s*(?:'[^']+'|""[^""]+"")\s*(?:#.*)?$")) return true;
            }
            return false;
        }

        private static Dictionary<string, string> ReadHopToDeskAccessOptions(string configPath)
        {
            if (!File.Exists(configPath)) throw new FileNotFoundException();
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            var keys = new HashSet<string> { "unattended-access", "verification-method", "approve-mode", "stop-service", "sso-login", "account-access-only" };
            bool inOptions = false;
            foreach (string line in File.ReadLines(configPath))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("[", StringComparison.Ordinal)) { inOptions = trimmed == "[options]"; continue; }
                if (!inOptions) continue;
                int equals = trimmed.IndexOf('=');
                if (equals <= 0) continue;
                string key = trimmed.Substring(0, equals).Trim().Trim('\'', '"');
                if (!keys.Contains(key)) continue;
                // As opções verificadas são strings simples, serializadas pelo próprio aplicativo.
                Match value = Regex.Match(trimmed.Substring(equals + 1).Trim(), @"^(?:'(?<value>[^']*)'|""(?<value>[^""\\]*)"")\s*(?:#.*)?$");
                if (!value.Success || options.ContainsKey(key)) throw new InvalidDataException();
                options.Add(key, value.Groups["value"].Value);
            }
            return options;
        }

        private static string HopToDeskOption(Dictionary<string, string> options, string key)
        {
            string value;
            return options.TryGetValue(key, out value) ? value : string.Empty;
        }

        private static bool HopToDeskUnattendedEnabled(Dictionary<string, string> options)
        {
            return HopToDeskOption(options, "unattended-access") == "true" &&
                HopToDeskOption(options, "verification-method") != "use-temporary-password" &&
                HopToDeskOption(options, "approve-mode") != "click" &&
                HopToDeskOption(options, "stop-service") != "Y" &&
                HopToDeskOption(options, "sso-login") == string.Empty &&
                HopToDeskOption(options, "account-access-only") != "Y";
        }

        private void RunHopToDeskPasswordCommand(string executable, char[] password)
        {
            ThrowIfCancellationRequested();
            var startInfo = new ProcessStartInfo(executable, "--password " + QuoteHopToDeskPassword(password))
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                WorkingDirectory = Path.GetDirectoryName(executable)
            };
            try
            {
                using (Process process = Process.Start(startInfo))
                using (var outputEnded = new ManualResetEvent(false))
                {
                    startInfo.Arguments = string.Empty;
                    if (process == null) throw new InvalidOperationException();
                    int acknowledged = 0;
                    // Somente o marcador oficial de sucesso é retido; as demais saídas são descartadas.
                    process.OutputDataReceived += (sender, args) =>
                    {
                        if (args.Data == null) { try { outputEnded.Set(); } catch (ObjectDisposedException) { } }
                        else if (args.Data.Trim() == "Done!") Interlocked.Exchange(ref acknowledged, 1);
                    };
                    process.ErrorDataReceived += (sender, args) => { };
                    var elapsed = Stopwatch.StartNew();
                    try
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        while (!process.WaitForExit(100))
                        {
                            ThrowIfCancellationRequested();
                            if (elapsed.ElapsedMilliseconds > 30000) throw new TimeoutException();
                        }
                        while (!outputEnded.WaitOne(100))
                        {
                            ThrowIfCancellationRequested();
                            if (elapsed.ElapsedMilliseconds > 30000) throw new TimeoutException();
                        }
                        ThrowIfCancellationRequested();
                        if (process.ExitCode != 0 || Volatile.Read(ref acknowledged) == 0) throw new InvalidOperationException();
                    }
                    finally
                    {
                        if (!process.HasExited) TryTerminateProcessTree(process);
                        try { process.CancelOutputRead(); } catch (InvalidOperationException) { }
                        try { process.CancelErrorRead(); } catch (InvalidOperationException) { }
                    }
                }
            }
            finally { startInfo.Arguments = string.Empty; }
        }

        private static string QuoteHopToDeskPassword(char[] password)
        {
            // Regras CommandLineToArgvW: um argumento literal, sem cmd.exe/PowerShell.
            var quoted = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in password)
            {
                if (character == '\\') { slashes++; continue; }
                quoted.Append('\\', character == '"' ? slashes * 2 + 1 : slashes);
                quoted.Append(character);
                slashes = 0;
            }
            quoted.Append('\\', slashes * 2);
            return quoted.Append('"').ToString();
        }
    }
}
