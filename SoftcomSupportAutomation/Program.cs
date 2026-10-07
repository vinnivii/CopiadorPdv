using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;
using System.Windows.Forms;
using Microsoft.Win32;
using SharpCompress.Archives;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Readers;

namespace SoftcomSupportAutomation
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SupportForm());
        }
    }

    internal sealed class SupportForm : Form
    {
        private const string SoftshopProductName = "Softshop Caixa";
        // A senha foi solicitada pelo suporte para automação. Proteja a distribuição do executável.
        private const string SupportPassword = "suporte@softcom";
        private const string DownloadUrl = "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/140/";
        private const string DllDownloadUrl = "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/47/";
        private const string SoftshopInstallSubdirectory = @"Softcom Tecnologia\Softshop Caixa";
        private const string SoftshopExecutableName = "Softshop.exe";
        private const string BackupScriptResource = "SoftcomSupportAutomation.Scripts.Atualizar-SoftcomBackup-v6.ps1";
        private const string RdpScriptResource = "SoftcomSupportAutomation.Scripts.AJUSTE_UPDATE_RDP.bat";
        private const string SoftconnectScriptResource = "SoftcomSupportAutomation.Scripts.Finalizar e Reiniciar o Softconect.bat";
        private const string PrintQueueScriptResource = "SoftcomSupportAutomation.Scripts.LimparFilaDeImpressao.bat";
        private static readonly DownloadOption[] DownloadOptions =
        {
            new DownloadOption("Backup Utility", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/382/"),
            new DownloadOption("Softshop Caixa (PDV)", DownloadUrl),
            new DownloadOption("Setup DLLs", DllDownloadUrl),
            new DownloadOption("Emissor", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/17/"),
            new DownloadOption("Nuvem Fiscal", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/220/"),
            new DownloadOption("Setup Softshop", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/264/"),
            new DownloadOption("SQL Server 2014", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/249/"),
            new DownloadOption("QR Code DLL", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/110/"),
            new DownloadOption("SPED.NET", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/262/"),
            new DownloadOption("Office 2003", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/350/"),
            new DownloadOption("WinRAR", "http://177.43.232.2:25123/helptools2/public/core/arquivo/download/id/28/")
        };
        private readonly Button startButton = new Button();
        private readonly Button stopButton = new Button();
        private readonly Button backupButton = new Button();
        private readonly Button rdpButton = new Button();
        private readonly Button softconnectButton = new Button();
        private readonly Button printQueueButton = new Button();
        private readonly Button softshopNavigationButton = new Button();
        private readonly Button backupNavigationButton = new Button();
        private readonly Button utilitiesNavigationButton = new Button();
        private readonly Button downloadsNavigationButton = new Button();
        private readonly CheckBox updateDllsCheckBox = new CheckBox();
        private readonly ComboBox pdvAutomationComboBox = new ComboBox();
        private readonly ComboBox pdvVersionComboBox = new ComboBox();
        private readonly ComboBox backupAutomationComboBox = new ComboBox();
        private readonly TextBox logBox = new TextBox();
        private readonly object logSync = new object();
        private string runFolder;
        private string logFile;
        private volatile bool workflowRunning;
        private volatile bool automationRunning;
        private volatile bool stopRequested;
        private volatile bool closeWhenStopped;
        private int cacheBackupDialogClosed;
        private int rpcUnavailableDialogClosed;
        private volatile int activeSoftshopProcessId;
        private int screenshotNumber;
        private bool updateDllsRequested;
        private PdvAutomationMode selectedPdvAutomationMode = PdvAutomationMode.FullReset;
        private string selectedPdvDownloadUrl = DownloadUrl;
        private string selectedPdvVersionName = "Mais recente disponível";
        private string selectedPdvExpectedSha256;
        private string selectedPdvArchiveExtension = ".rar";
        private static readonly Color DarkBackground = Color.FromArgb(10, 10, 10);
        private static readonly Color SidebarBackground = Color.FromArgb(6, 6, 6);
        private static readonly Color DarkPanel = Color.FromArgb(18, 18, 18);
        private static readonly Color DarkCard = Color.FromArgb(25, 25, 25);
        private static readonly Color ControlBackground = Color.FromArgb(31, 31, 31);
        private static readonly Color HoverBackground = Color.FromArgb(42, 42, 42);
        private static readonly Color BorderColor = Color.FromArgb(52, 52, 52);
        private static readonly Color PrimaryAccent = Color.FromArgb(244, 244, 245);
        private static readonly Color TextPrimary = Color.FromArgb(250, 250, 250);
        private static readonly Color TextSecondary = Color.FromArgb(166, 166, 166);
        private static readonly Color TextMuted = Color.FromArgb(108, 108, 108);

        public SupportForm()
        {
            Text = "Central de Automações Softcom - V2";
            ClientSize = new Size(1040, 700);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = DarkBackground;
            ForeColor = TextPrimary;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            DoubleBuffered = true;
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            var headerContext = new Label
            {
                AutoSize = false,
                Location = new Point(250, 14),
                Size = new Size(560, 18),
                ForeColor = TextMuted,
                Font = new Font(Font.FontFamily, 8, FontStyle.Bold),
                Text = "CENTRAL DE SUPORTE  /  AUTOMAÇÕES"
            };
            var info = new Label
            {
                AutoSize = false,
                Location = new Point(250, 33),
                Size = new Size(620, 34),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 18, FontStyle.Bold),
                Text = "Workspace de automações"
            };
            var subtitle = new Label
            {
                AutoSize = false,
                Location = new Point(250, 69),
                Size = new Size(766, 24),
                ForeColor = TextSecondary,
                Text = "Selecione um produto na barra lateral e escolha a automação desejada."
            };
            var administratorBadge = new Label
            {
                AutoSize = false,
                Location = new Point(882, 27),
                Size = new Size(134, 30),
                BackColor = DarkCard,
                ForeColor = TextSecondary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font(Font.FontFamily, 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "ADMINISTRADOR"
            };

            var sidebar = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(220, 700),
                BackColor = SidebarBackground
            };
            var sidebarBrand = new Label
            {
                AutoSize = false,
                Location = new Point(20, 22),
                Size = new Size(180, 46),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
                Text = "SOFTCOM\r\nSUPORTE"
            };
            var sidebarDivider = new Panel
            {
                Location = new Point(20, 82),
                Size = new Size(180, 1),
                BackColor = BorderColor
            };
            var productsLabel = new Label
            {
                AutoSize = false,
                Location = new Point(20, 101),
                Size = new Size(180, 20),
                ForeColor = TextMuted,
                Font = new Font(Font.FontFamily, 8, FontStyle.Bold),
                Text = "PRODUTOS"
            };
            ConfigureNavigationButton(softshopNavigationButton, "Softshop Caixa", 130);
            ConfigureNavigationButton(backupNavigationButton, "Softcom Backup", 178);
            ConfigureNavigationButton(utilitiesNavigationButton, "Utilitários", 226);
            ConfigureNavigationButton(downloadsNavigationButton, "Central Downloads", 274);
            var sidebarVersion = new Label
            {
                AutoSize = false,
                Location = new Point(20, 640),
                Size = new Size(180, 20),
                ForeColor = TextMuted,
                Text = "CENTRAL V2  •  2.12.0"
            };
            var sidebarCredit = new Label
            {
                AutoSize = false,
                Location = new Point(20, 664),
                Size = new Size(190, 20),
                ForeColor = TextMuted,
                Text = "Marcus Silva - Teresina"
            };
            sidebar.Controls.Add(sidebarBrand);
            sidebar.Controls.Add(sidebarDivider);
            sidebar.Controls.Add(productsLabel);
            sidebar.Controls.Add(softshopNavigationButton);
            sidebar.Controls.Add(backupNavigationButton);
            sidebar.Controls.Add(utilitiesNavigationButton);
            sidebar.Controls.Add(downloadsNavigationButton);
            sidebar.Controls.Add(sidebarVersion);
            sidebar.Controls.Add(sidebarCredit);

            var productHost = new Panel
            {
                Location = new Point(250, 104),
                Size = new Size(766, 334),
                BackColor = DarkBackground
            };

            var pdvGroup = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(766, 334),
                BackColor = DarkPanel
            };
            ApplyPanelBorder(pdvGroup);
            var pdvTitle = new Label
            {
                AutoSize = false,
                Location = new Point(22, 15),
                Size = new Size(420, 24),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
                Text = "Softshop Caixa"
            };
            var pdvDescription = new Label
            {
                AutoSize = false,
                Location = new Point(22, 43),
                Size = new Size(710, 24),
                ForeColor = TextSecondary,
                Text = "Automações de manutenção, preservação de configurações e instalação do caixa."
            };
            var pdvAutomationLabel = new Label
            {
                AutoSize = false,
                Location = new Point(22, 74),
                Size = new Size(300, 20),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
                Text = "Escolha a automação"
            };
            ConfigureComboBox(pdvAutomationComboBox, new Point(22, 97), new Size(420, 30));
            pdvAutomationComboBox.Items.Add(new PdvAutomationOption(
                PdvAutomationMode.FullReset, "Reset Completo / Instalação do PDV"));
            pdvAutomationComboBox.Items.Add(new PdvAutomationOption(
                PdvAutomationMode.CleanReinstall, "Reinstalação Limpa"));
            pdvAutomationComboBox.Items.Add(new PdvAutomationOption(
                PdvAutomationMode.ConfigurationPrints, "Prints de Configuração"));
            pdvAutomationComboBox.SelectedIndex = 0;

            var pdvConfigurationPanel = new Panel
            {
                Location = new Point(22, 141),
                Size = new Size(720, 118),
                BackColor = DarkCard
            };
            ApplyPanelBorder(pdvConfigurationPanel);
            var configurationTitle = new Label
            {
                AutoSize = false,
                Location = new Point(16, 12),
                Size = new Size(300, 22),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
                Text = "Configurações da automação"
            };
            updateDllsCheckBox.Text = "Atualizar SetupSoftcomDLLs antes de instalar";
            updateDllsCheckBox.Location = new Point(16, 43);
            updateDllsCheckBox.Size = new Size(315, 28);
            updateDllsCheckBox.ForeColor = TextPrimary;
            updateDllsCheckBox.BackColor = DarkCard;
            updateDllsCheckBox.FlatStyle = FlatStyle.Flat;
            updateDllsCheckBox.FlatAppearance.BorderColor = BorderColor;
            updateDllsCheckBox.FlatAppearance.CheckedBackColor = TextPrimary;
            updateDllsCheckBox.Checked = false;
            var pdvVersionLabel = new Label
            {
                AutoSize = false,
                Location = new Point(360, 12),
                Size = new Size(330, 20),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
                Text = "Versão do caixa que será instalada"
            };
            ConfigureComboBox(pdvVersionComboBox, new Point(360, 39), new Size(334, 30));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "Mais recente disponível (servidor atual)", DownloadUrl));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.40.3.0 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs2/PDV_SoftshopCaixa.8.40.3.0.zip",
                "96ae04de6b32615c89975e9ec7aa590f4d98ec6b94aafd7f6335510036db06c3"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.40 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.40.zip",
                "d4d682feab59cfdeb02d3c83940eb659e4d06aed49b258ef2beffaac1a763f25"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.39.4 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.39.4.zip",
                "e12e5fd090884bf49f3028a491c0d2f0660986f99d5e18dd248abeb63085983a"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.38.2 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.38.2.zip",
                "aaaa319cf54d8a8b75c679c1a5db04df3cb1180b0340d5ebab4bd061eec75216"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.38.0.0 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.38.0.0.zip",
                "d9c51ef0336d576710f6962fb917b9748895b280b3865b52a1bd0761e56033a8"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.37.3 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.37.3.zip",
                "b525938f6f609df5139015d8edc7accd5a9e3c8014be227710d272652ebbf34e"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.37.2 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.37.2.zip",
                "31f9f6649ea58fcbebb360d19da2c77dd9112da7a65ca19f18e32f30d1515f34"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.36.0 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.36.0.zip",
                "9b5f349fedbaa5f1b28a9ca5515424beacbb098814065762038cb2dfa39a3b7b"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.35.1.0 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.35.1.0.zip",
                "fc09198e579f358ceab5336431270c41b8e48a3225b0a53ed26df9d7daa086b6"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.34.14.0 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.34.14.0.zip",
                "147ab3c0b1c5944609034684f79059c2808d20eafd91082de3432362c4b1898f"));
            pdvVersionComboBox.Items.Add(new PdvVersionOption(
                "8.33.8 (GitHub)",
                "https://github.com/vinnivii/PDVs/releases/download/pdvs/PDV_SoftshopCaixa.8.33.8.zip",
                "a9927e4cc7b5b821febf782701a7a10e3b5c9e25b39883c6d6ef4b5288d58d82"));
            pdvVersionComboBox.SelectedIndex = 0;
            var versionHint = new Label
            {
                AutoSize = false,
                Location = new Point(360, 76),
                Size = new Size(340, 38),
                ForeColor = TextSecondary,
                Text = "Versões do GitHub são verificadas por SHA-256 antes da instalação."
            };
            pdvConfigurationPanel.Controls.Add(configurationTitle);
            pdvConfigurationPanel.Controls.Add(updateDllsCheckBox);
            pdvConfigurationPanel.Controls.Add(pdvVersionLabel);
            pdvConfigurationPanel.Controls.Add(pdvVersionComboBox);
            pdvConfigurationPanel.Controls.Add(versionHint);

            startButton.Text = "Executar Reset / Instalação";
            startButton.Location = new Point(22, 278);
            startButton.Size = new Size(260, 40);
            StyleActionButton(startButton, PrimaryAccent);
            startButton.Click += StartButton_Click;
            pdvAutomationComboBox.SelectedIndexChanged += (sender, args) =>
            {
                var option = pdvAutomationComboBox.SelectedItem as PdvAutomationOption;
                bool cleanReinstall = option != null && option.Mode == PdvAutomationMode.CleanReinstall;
                bool configurationPrints = option != null && option.Mode == PdvAutomationMode.ConfigurationPrints;
                startButton.Text = configurationPrints
                    ? "Gerar Prints de Configuração"
                    : cleanReinstall
                    ? "Executar Reinstalação Limpa"
                    : "Executar Reset / Instalação";
                pdvDescription.Text = configurationPrints
                    ? "Captura as configurações do caixa instalado e abre a pasta dos prints ao concluir."
                    : cleanReinstall
                    ? "Desinstala, preserva a pasta do caixa e instala a versão selecionada, sem alterar o LocalAppData."
                    : "Automações de manutenção, preservação de configurações e instalação do caixa.";
                UpdatePdvDllOption(!automationRunning);
            };
            backupButton.Text = "Atualizar SoftcomBackup";
            var backupGroup = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(766, 334),
                BackColor = DarkPanel
            };
            ApplyPanelBorder(backupGroup);
            var backupTitle = new Label
            {
                AutoSize = false,
                Location = new Point(22, 15),
                Size = new Size(420, 24),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
                Text = "Softcom Backup"
            };
            var backupDescription = new Label
            {
                AutoSize = false,
                Location = new Point(22, 43),
                Size = new Size(710, 44),
                ForeColor = TextSecondary,
                Text = "Executa o script oficial de atualização em uma janela PowerShell elevada."
            };
            var backupAutomationLabel = new Label
            {
                AutoSize = false,
                Location = new Point(22, 84),
                Size = new Size(300, 20),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 9, FontStyle.Bold),
                Text = "Escolha a automação"
            };
            ConfigureComboBox(backupAutomationComboBox, new Point(22, 107), new Size(420, 30));
            backupAutomationComboBox.Items.Add("Atualizar Softcom Backup");
            backupAutomationComboBox.SelectedIndex = 0;
            var backupInfoPanel = new Panel
            {
                Location = new Point(22, 151),
                Size = new Size(720, 90),
                BackColor = DarkCard
            };
            ApplyPanelBorder(backupInfoPanel);
            var backupInfo = new Label
            {
                AutoSize = false,
                Location = new Point(16, 16),
                Size = new Size(680, 60),
                ForeColor = TextSecondary,
                Text = "O script original é extraído do executável sem alterações e executado como administrador. Acompanhe o processo pela janela do PowerShell."
            };
            backupInfoPanel.Controls.Add(backupInfo);
            backupButton.Location = new Point(22, 278);
            backupButton.Size = new Size(260, 40);
            StyleActionButton(backupButton, PrimaryAccent);
            backupButton.Click += BackupButton_Click;

            var utilitiesGroup = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(766, 334),
                BackColor = DarkPanel
            };
            ApplyPanelBorder(utilitiesGroup);
            var utilitiesTitle = new Label
            {
                AutoSize = false,
                Location = new Point(22, 15),
                Size = new Size(420, 24),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
                Text = "Utilitários"
            };
            var utilitiesDescription = new Label
            {
                AutoSize = false,
                Location = new Point(22, 43),
                Size = new Size(710, 32),
                ForeColor = TextSecondary,
                Text = "Automações rápidas e scripts administrativos do Windows e do ambiente de suporte."
            };
            Panel rdpCard = CreateUtilityCard(
                "Ajuste Update RDP",
                "Ajusta a confirmação de dados do cliente RDP no Windows 11.",
                rdpButton,
                "Executar ajuste",
                Color.FromArgb(55, 55, 55));
            rdpCard.Location = new Point(22, 82);
            Panel softconnectCard = CreateUtilityCard(
                "Softconnect",
                "Finaliza o processo atual e inicia o Softconnect novamente.",
                softconnectButton,
                "Reiniciar Softconnect",
                Color.FromArgb(55, 55, 55));
            softconnectCard.Location = new Point(271, 82);
            Panel printQueueCard = CreateUtilityCard(
                "Fila de impressão",
                "Reinicia o Spooler e remove documentos travados na fila.",
                printQueueButton,
                "Limpar fila",
                Color.FromArgb(55, 55, 55));
            printQueueCard.Location = new Point(520, 82);
            rdpButton.Click += (sender, args) => StartEmbeddedBatchScript(
                RdpScriptResource, "AJUSTE_UPDATE_RDP.bat", "Ajuste Update RDP", "Ajuste_Update_RDP");
            softconnectButton.Click += (sender, args) => StartEmbeddedBatchScript(
                SoftconnectScriptResource, "Finalizar e Reiniciar o Softconect.bat", "Reinicialização do Softconnect", "Softconnect");
            printQueueButton.Click += (sender, args) => StartEmbeddedBatchScript(
                PrintQueueScriptResource, "LimparFilaDeImpressao.bat", "Limpeza da fila de impressão", "Fila_Impressao");
            utilitiesGroup.Controls.Add(utilitiesTitle);
            utilitiesGroup.Controls.Add(utilitiesDescription);
            utilitiesGroup.Controls.Add(rdpCard);
            utilitiesGroup.Controls.Add(softconnectCard);
            utilitiesGroup.Controls.Add(printQueueCard);

            var downloadsGroup = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(766, 334),
                BackColor = DarkPanel
            };
            ApplyPanelBorder(downloadsGroup);
            var downloadsTitle = new Label
            {
                AutoSize = false,
                Location = new Point(22, 15),
                Size = new Size(420, 24),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 11, FontStyle.Bold),
                Text = "Central Downloads"
            };
            var downloadsDescription = new Label
            {
                AutoSize = false,
                Location = new Point(22, 43),
                Size = new Size(720, 24),
                ForeColor = TextSecondary,
                Text = "Acesso rápido aos principais instaladores e ferramentas utilizadas pelo suporte."
            };
            var downloadsHint = new Label
            {
                AutoSize = false,
                Location = new Point(22, 67),
                Size = new Size(720, 18),
                ForeColor = TextMuted,
                Text = "Os links são abertos no navegador padrão."
            };
            var downloadsList = new Panel
            {
                Location = new Point(22, 88),
                Size = new Size(720, 224),
                AutoScroll = true,
                BackColor = DarkPanel
            };
            int downloadsPerColumn = (DownloadOptions.Length + 1) / 2;
            for (int index = 0; index < DownloadOptions.Length; index++)
            {
                Panel card = CreateDownloadCard(DownloadOptions[index]);
                card.Location = new Point((index / downloadsPerColumn) * 370, (index % downloadsPerColumn) * 38);
                downloadsList.Controls.Add(card);
            }
            downloadsGroup.Controls.Add(downloadsTitle);
            downloadsGroup.Controls.Add(downloadsDescription);
            downloadsGroup.Controls.Add(downloadsHint);
            downloadsGroup.Controls.Add(downloadsList);

            var logTitle = new Label
            {
                AutoSize = true,
                Location = new Point(250, 459),
                ForeColor = TextPrimary,
                Font = new Font(Font.FontFamily, 10, FontStyle.Bold),
                Text = "Atividade"
            };
            var logStatus = new Label
            {
                AutoSize = false,
                Location = new Point(706, 459),
                Size = new Size(140, 18),
                ForeColor = TextMuted,
                Font = new Font(Font.FontFamily, 8, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleRight,
                Text = "LOG EM TEMPO REAL"
            };
            stopButton.Text = "Parar execução";
            stopButton.Location = new Point(866, 450);
            stopButton.Size = new Size(150, 30);
            StyleActionButton(stopButton, Color.FromArgb(55, 55, 55));
            stopButton.Enabled = false;
            stopButton.Click += StopButton_Click;
            logBox.Location = new Point(250, 484);
            logBox.Size = new Size(766, 170);
            logBox.Multiline = true;
            logBox.ScrollBars = ScrollBars.Vertical;
            logBox.ReadOnly = true;
            logBox.Font = new Font(FontFamily.GenericMonospace, 9);
            logBox.BackColor = Color.FromArgb(13, 13, 13);
            logBox.ForeColor = Color.FromArgb(218, 218, 218);
            logBox.BorderStyle = BorderStyle.FixedSingle;
            var logHint = new Label
            {
                AutoSize = true,
                Location = new Point(250, 663),
                ForeColor = TextMuted,
                Text = "O andamento e os erros de cada automação serão exibidos nesta área."
            };
            pdvGroup.Controls.Add(pdvTitle);
            pdvGroup.Controls.Add(pdvDescription);
            pdvGroup.Controls.Add(pdvAutomationLabel);
            pdvGroup.Controls.Add(pdvAutomationComboBox);
            pdvGroup.Controls.Add(pdvConfigurationPanel);
            pdvGroup.Controls.Add(startButton);
            backupGroup.Controls.Add(backupTitle);
            backupGroup.Controls.Add(backupDescription);
            backupGroup.Controls.Add(backupAutomationLabel);
            backupGroup.Controls.Add(backupAutomationComboBox);
            backupGroup.Controls.Add(backupInfoPanel);
            backupGroup.Controls.Add(backupButton);
            productHost.Controls.Add(pdvGroup);
            productHost.Controls.Add(backupGroup);
            productHost.Controls.Add(utilitiesGroup);
            productHost.Controls.Add(downloadsGroup);
            softshopNavigationButton.Click += (sender, args) => ShowProductPage(
                pdvGroup, softshopNavigationButton, pdvGroup, backupGroup, utilitiesGroup, downloadsGroup);
            backupNavigationButton.Click += (sender, args) => ShowProductPage(
                backupGroup, backupNavigationButton, pdvGroup, backupGroup, utilitiesGroup, downloadsGroup);
            utilitiesNavigationButton.Click += (sender, args) => ShowProductPage(
                utilitiesGroup, utilitiesNavigationButton, pdvGroup, backupGroup, utilitiesGroup, downloadsGroup);
            downloadsNavigationButton.Click += (sender, args) => ShowProductPage(
                downloadsGroup, downloadsNavigationButton, pdvGroup, backupGroup, utilitiesGroup, downloadsGroup);
            Controls.Add(sidebar);
            Controls.Add(headerContext);
            Controls.Add(info);
            Controls.Add(subtitle);
            Controls.Add(administratorBadge);
            Controls.Add(productHost);
            Controls.Add(logTitle);
            Controls.Add(logStatus);
            Controls.Add(stopButton);
            Controls.Add(logBox);
            Controls.Add(logHint);
            ShowProductPage(pdvGroup, softshopNavigationButton, pdvGroup, backupGroup, utilitiesGroup, downloadsGroup);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int enabled = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(Handle, 19, ref enabled, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && automationRunning)
            {
                e.Cancel = true;
                closeWhenStopped = true;
                RequestStop("Fechamento solicitado. A automação será interrompida antes de fechar a central.");
            }
            base.OnFormClosing(e);
        }

        private static void ConfigureNavigationButton(Button button, string text, int top)
        {
            button.Text = text;
            button.Location = new Point(10, top);
            button.Size = new Size(200, 44);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = HoverBackground;
            button.FlatAppearance.MouseDownBackColor = ControlBackground;
            button.BackColor = SidebarBackground;
            button.ForeColor = TextSecondary;
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(14, 0, 0, 0);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
        }

        private static void ConfigureComboBox(ComboBox comboBox, Point location, Size size)
        {
            comboBox.Location = location;
            comboBox.Size = size;
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.FlatStyle = FlatStyle.Flat;
            comboBox.BackColor = ControlBackground;
            comboBox.ForeColor = TextPrimary;
            comboBox.DrawMode = DrawMode.OwnerDrawFixed;
            comboBox.ItemHeight = 24;
            comboBox.DrawItem += DrawComboBoxItem;
            comboBox.IntegralHeight = false;
            comboBox.DropDownHeight = 216;
        }

        private static void DrawComboBoxItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var comboBox = (ComboBox)sender;
            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            Color background = selected ? TextPrimary : ControlBackground;
            Color foreground = selected ? DarkBackground : TextPrimary;
            using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
            var textBounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, Math.Max(0, e.Bounds.Width - 12), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, comboBox.GetItemText(comboBox.Items[e.Index]), comboBox.Font,
                textBounds, foreground, TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private void ShowProductPage(Control selectedPage, Button selectedButton, params Control[] pages)
        {
            foreach (Control page in pages) page.Visible = page == selectedPage;
            selectedPage.BringToFront();

            foreach (Button button in new[]
                     { softshopNavigationButton, backupNavigationButton, utilitiesNavigationButton, downloadsNavigationButton })
            {
                bool selected = button == selectedButton;
                button.BackColor = selected ? ControlBackground : SidebarBackground;
                button.ForeColor = selected ? TextPrimary : TextSecondary;
                button.FlatAppearance.BorderSize = selected ? 1 : 0;
            }
        }

        private Panel CreateDownloadCard(DownloadOption option)
        {
            var card = new Panel
            {
                Size = new Size(350, 34),
                BackColor = DarkCard
            };
            ApplyPanelBorder(card);
            var nameLabel = new Label
            {
                AutoSize = false,
                Location = new Point(10, 0),
                Size = new Size(244, 34),
                ForeColor = TextPrimary,
                TextAlign = ContentAlignment.MiddleLeft,
                Text = option.Name
            };
            var downloadButton = new Button
            {
                Location = new Point(264, 4),
                Size = new Size(76, 26),
                Text = "Baixar",
                AccessibleName = "Baixar " + option.Name
            };
            StyleActionButton(downloadButton, Color.FromArgb(55, 55, 55));
            downloadButton.Click += (sender, args) => OpenDownloadLink(option.Url);
            card.Controls.Add(nameLabel);
            card.Controls.Add(downloadButton);
            return card;
        }

        private void OpenDownloadLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Não foi possível abrir o link de download.\r\n\r\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Panel CreateUtilityCard(string title, string description, Button button, string buttonText, Color buttonColor)
        {
            var card = new Panel
            {
                Size = new Size(224, 198),
                BackColor = DarkCard
            };
            ApplyPanelBorder(card);
            var titleLabel = new Label
            {
                AutoSize = false,
                Location = new Point(14, 12),
                Size = new Size(196, 22),
                ForeColor = TextPrimary,
                Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 9, FontStyle.Bold),
                Text = title
            };
            var descriptionLabel = new Label
            {
                AutoSize = false,
                Location = new Point(14, 38),
                Size = new Size(196, 72),
                ForeColor = TextSecondary,
                Text = description
            };
            button.Text = buttonText;
            button.Location = new Point(14, 148);
            button.Size = new Size(196, 34);
            StyleActionButton(button, buttonColor);
            card.Controls.Add(titleLabel);
            card.Controls.Add(descriptionLabel);
            card.Controls.Add(button);
            return card;
        }

        private static void StyleActionButton(Button button, Color backgroundColor)
        {
            bool lightBackground = backgroundColor.GetBrightness() >= 0.65F;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = lightBackground ? 0 : 1;
            button.FlatAppearance.BorderColor = BorderColor;
            button.FlatAppearance.MouseOverBackColor = lightBackground
                ? Color.White
                : HoverBackground;
            button.FlatAppearance.MouseDownBackColor = lightBackground
                ? Color.FromArgb(210, 210, 210)
                : Color.FromArgb(65, 65, 65);
            button.BackColor = backgroundColor;
            button.ForeColor = lightBackground ? Color.Black : TextPrimary;
            button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
        }

        private static void ApplyPanelBorder(Panel panel)
        {
            panel.Paint += (sender, args) =>
            {
                if (panel.ClientSize.Width <= 0 || panel.ClientSize.Height <= 0) return;
                using (var pen = new Pen(BorderColor))
                    args.Graphics.DrawRectangle(pen, 0, 0, panel.ClientSize.Width - 1, panel.ClientSize.Height - 1);
            };
            panel.Resize += (sender, args) => panel.Invalidate();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("Execute este programa como administrador.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var selectedAutomation = pdvAutomationComboBox.SelectedItem as PdvAutomationOption;
            if (selectedAutomation == null)
            {
                MessageBox.Show("Selecione a automação do caixa que será executada.", Text,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (selectedAutomation.Mode != PdvAutomationMode.ConfigurationPrints)
            {
                var selectedVersion = pdvVersionComboBox.SelectedItem as PdvVersionOption;
                if (selectedVersion == null)
                {
                    MessageBox.Show("Selecione a versão do caixa que será instalada.", Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                selectedPdvDownloadUrl = selectedVersion.DownloadUrl;
                selectedPdvVersionName = selectedVersion.DisplayName;
                selectedPdvExpectedSha256 = selectedVersion.Sha256;
                selectedPdvArchiveExtension = selectedVersion.ArchiveExtension;
            }

            selectedPdvAutomationMode = selectedAutomation.Mode;
            updateDllsRequested = selectedPdvAutomationMode == PdvAutomationMode.FullReset && updateDllsCheckBox.Checked;
            PrepareAutomationStart();
            var worker = new Thread(RunWorkflow) { IsBackground = true };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        private void BackupButton_Click(object sender, EventArgs e)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("Execute este programa como administrador.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            PrepareAutomationStart();
            var worker = new Thread(RunSoftcomBackupScript) { IsBackground = true };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        private void StartEmbeddedBatchScript(string resourceName, string fileName, string displayName, string logFolderName)
        {
            if (!IsAdministrator())
            {
                MessageBox.Show("Execute este programa como administrador.", Text, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            PrepareAutomationStart();
            var worker = new Thread(() => RunEmbeddedBatchScript(resourceName, fileName, displayName, logFolderName))
            {
                IsBackground = true
            };
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
        }

        private void PrepareAutomationStart()
        {
            runFolder = null;
            logFile = null;
            stopRequested = false;
            closeWhenStopped = false;
            automationRunning = true;
            SetActionsEnabled(false);
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            RequestStop("Parada solicitada pelo usuário. A etapa atual será encerrada com segurança.");
        }

        private void RequestStop(string logMessage)
        {
            if (stopRequested) return;
            stopRequested = true;
            stopButton.Enabled = false;
            WriteLog(logMessage);
        }

        private void FinishAutomation()
        {
            automationRunning = false;
            SetActionsEnabled(true);
            if (!closeWhenStopped || IsDisposed || !IsHandleCreated) return;
            BeginInvoke((Action)Close);
        }

        private void RunEmbeddedBatchScript(string resourceName, string fileName, string displayName, string logFolderName)
        {
            try
            {
                runFolder = Path.Combine(@"C:\Softcom\ResetCaixa\Utilitarios", logFolderName, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(runFolder);
                logFile = Path.Combine(runFolder, "processo.log");
                File.WriteAllText(logFile, string.Empty, new UTF8Encoding(true));
                WriteLog("Início: " + displayName + ".");
                ThrowIfCancellationRequested();

                string scriptFolder = @"C:\Softcom\ResetCaixa\Scripts";
                Directory.CreateDirectory(scriptFolder);
                string scriptPath = Path.Combine(scriptFolder, fileName);
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
                {
                    if (source == null)
                        throw new InvalidOperationException("O script '" + fileName + "' não foi encontrado dentro do executável.");
                    using (Stream destination = File.Create(scriptPath)) source.CopyTo(destination);
                }
                WriteLog("Script extraído sem alterações em: " + scriptPath);

                string commandPrompt = Path.Combine(Environment.SystemDirectory, "cmd.exe");
                var startInfo = new ProcessStartInfo
                {
                    FileName = commandPrompt,
                    Arguments = "/D /C \"\"" + scriptPath + "\"\"",
                    WorkingDirectory = scriptFolder,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Normal
                };
                WriteLog("Rotina iniciada em uma janela administrativa. Aguarde sua conclusão.");
                ThrowIfCancellationRequested();
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new InvalidOperationException("Não foi possível iniciar o Prompt de Comando.");
                    WaitForProcessExitWithCancellation(process, displayName);
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException("O script terminou com o código " + process.ExitCode + ".");
                }

                WriteLog(displayName + " concluído com sucesso.");
                ShowFinal(displayName + " concluído.\r\nLog: " + logFile, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                if (string.Equals(resourceName, PrintQueueScriptResource, StringComparison.Ordinal))
                    TryRestorePrintSpoolerAfterCancellation();
                WriteLog("CANCELADO: " + displayName + " foi interrompido pelo usuário.");
                ShowFinal(displayName + " cancelado.\r\nLog: " + logFile, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                WriteLog("ERRO: " + ex);
                ShowFinal(displayName + " foi interrompido.\r\nConsulte: " + logFile, MessageBoxIcon.Error);
            }
            finally
            {
                FinishAutomation();
            }
        }

        private void TryRestorePrintSpoolerAfterCancellation()
        {
            try
            {
                string serviceControl = Path.Combine(Environment.SystemDirectory, "sc.exe");
                using (var process = Process.Start(new ProcessStartInfo(serviceControl, "start Spooler")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    if (process != null) process.WaitForExit(10000);
                }
                WriteLog("Verificação de segurança: solicitado o reinício do serviço Spooler após o cancelamento.");
            }
            catch (Exception ex)
            {
                WriteLog("Aviso: não foi possível solicitar o reinício do Spooler após o cancelamento: " + ex.Message);
            }
        }

        private void RunSoftcomBackupScript()
        {
            try
            {
                runFolder = Path.Combine(@"C:\Softcom\ResetCaixa\SoftcomBackup", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(runFolder);
                logFile = Path.Combine(runFolder, "processo.log");
                File.WriteAllText(logFile, string.Empty, new UTF8Encoding(true));
                WriteLog("Início da atualização do SoftcomBackup.");
                ThrowIfCancellationRequested();

                string scriptFolder = @"C:\Softcom\ResetCaixa\Scripts";
                Directory.CreateDirectory(scriptFolder);
                string scriptPath = Path.Combine(scriptFolder, "Atualizar-SoftcomBackup-v6.ps1");
                using (Stream source = Assembly.GetExecutingAssembly().GetManifestResourceStream(BackupScriptResource))
                {
                    if (source == null) throw new InvalidOperationException("O script SoftcomBackup não foi encontrado dentro do executável.");
                    using (Stream destination = File.Create(scriptPath)) source.CopyTo(destination);
                }
                WriteLog("Script extraído em: " + scriptPath);

                string powershell = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");
                if (!File.Exists(powershell)) throw new FileNotFoundException("Windows PowerShell 5.1 não foi encontrado.", powershell);
                var startInfo = new ProcessStartInfo
                {
                    FileName = powershell,
                    Arguments = "-NoLogo -NoProfile -ExecutionPolicy RemoteSigned -File \"" + scriptPath + "\"",
                    WorkingDirectory = scriptFolder,
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Normal
                };
                WriteLog("PowerShell iniciado. Acompanhe o andamento na janela aberta.");
                ThrowIfCancellationRequested();
                using (Process process = Process.Start(startInfo))
                {
                    if (process == null) throw new InvalidOperationException("Não foi possível iniciar o PowerShell.");
                    WaitForProcessExitWithCancellation(process, "atualização do SoftcomBackup");
                    if (process.ExitCode != 0)
                        throw new InvalidOperationException("O script SoftcomBackup terminou com o código " + process.ExitCode + ".");
                }
                WriteLog("Atualização do SoftcomBackup concluída com sucesso.");
                ShowFinal("SoftcomBackup atualizado com sucesso.\r\nLog: " + logFile, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                WriteLog("CANCELADO: atualização do SoftcomBackup interrompida pelo usuário.");
                ShowFinal("Atualização do SoftcomBackup cancelada.\r\nLog: " + logFile, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                WriteLog("ERRO: " + ex);
                ShowFinal("A atualização do SoftcomBackup foi interrompida.\r\nConsulte: " + logFile, MessageBoxIcon.Error);
            }
            finally
            {
                FinishAutomation();
            }
        }

        private void SetActionsEnabled(bool enabled)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() =>
                {
                    startButton.Enabled = enabled;
                    backupButton.Enabled = enabled;
                    rdpButton.Enabled = enabled;
                    softconnectButton.Enabled = enabled;
                    printQueueButton.Enabled = enabled;
                    softshopNavigationButton.Enabled = enabled;
                    backupNavigationButton.Enabled = enabled;
                    utilitiesNavigationButton.Enabled = enabled;
                    downloadsNavigationButton.Enabled = enabled;
                    UpdatePdvDllOption(enabled);
                    pdvAutomationComboBox.Enabled = enabled;
                    backupAutomationComboBox.Enabled = enabled;
                    stopButton.Enabled = automationRunning && !stopRequested;
                }));
                return;
            }
            startButton.Enabled = enabled;
            backupButton.Enabled = enabled;
            rdpButton.Enabled = enabled;
            softconnectButton.Enabled = enabled;
            printQueueButton.Enabled = enabled;
            softshopNavigationButton.Enabled = enabled;
            backupNavigationButton.Enabled = enabled;
            utilitiesNavigationButton.Enabled = enabled;
            downloadsNavigationButton.Enabled = enabled;
            UpdatePdvDllOption(enabled);
            pdvAutomationComboBox.Enabled = enabled;
            backupAutomationComboBox.Enabled = enabled;
            stopButton.Enabled = automationRunning && !stopRequested;
        }

        private void UpdatePdvDllOption(bool actionsEnabled)
        {
            var option = pdvAutomationComboBox.SelectedItem as PdvAutomationOption;
            updateDllsCheckBox.Enabled = actionsEnabled && option != null && option.Mode == PdvAutomationMode.FullReset;
            pdvVersionComboBox.Enabled = actionsEnabled && option != null && option.Mode != PdvAutomationMode.ConfigurationPrints;
        }

        private void RunWorkflow()
        {
            Task<string> pdvPackageTask = null;
            bool fullReset = selectedPdvAutomationMode == PdvAutomationMode.FullReset;
            bool cleanReinstall = selectedPdvAutomationMode == PdvAutomationMode.CleanReinstall;
            bool configurationPrints = selectedPdvAutomationMode == PdvAutomationMode.ConfigurationPrints;
            Thread dialogWatcher = null;
            int captureInProgress = 0;
            int packageReadyDuringCaptureLogged = 0;
            var packageCancellation = new CancellationTokenSource();
            try
            {
                runFolder = Path.Combine(fullReset || configurationPrints ? @"C:\Softcom\ResetCaixa\Prints" : @"C:\Softcom\ResetCaixa\ReinstalacaoLimpa",
                    DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                Directory.CreateDirectory(runFolder);
                logFile = Path.Combine(runFolder, "processo.log");
                File.WriteAllText(logFile, string.Empty, new UTF8Encoding(true));
                if (fullReset || cleanReinstall)
                {
                    WriteLog("Iniciando download antecipado do PDV: " + selectedPdvVersionName + ".");
                    pdvPackageTask = Task.Run(() =>
                    {
                        string preparedMsi = PreparePdvPackage(packageCancellation.Token);
                        if (Volatile.Read(ref captureInProgress) != 0 &&
                            Interlocked.Exchange(ref packageReadyDuringCaptureLogged, 1) == 0)
                            WriteLog("Pacote do PDV preparado e aguardando conclusão da captura.");
                        return preparedMsi;
                    });
                    WriteLog("Download do PDV executando em paralelo.");
                }
                WriteLog("Início do processo. Pasta do log: " + runFolder);
                WriteLog(configurationPrints
                    ? "Automação selecionada: Prints de Configuração."
                    : fullReset
                    ? "Automação selecionada: Reset Completo / Instalação do PDV."
                    : "Automação selecionada: Reinstalação Limpa.");
                Assembly runningAssembly = Assembly.GetExecutingAssembly();
                WriteLog("Central em execução: versão " + runningAssembly.GetName().Version +
                    ", caminho " + runningAssembly.Location + ".");
                try
                {
                    if (File.Exists(runningAssembly.Location))
                        WriteLog("SHA-256 da central: " + ComputeFileSha256WithCancellation(runningAssembly.Location) + ".");
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    WriteLog("Aviso: não foi possível calcular o SHA-256 da central: " + ex.Message);
                }
                if (fullReset || cleanReinstall)
                    WriteLog("Versão selecionada para instalação: " + selectedPdvVersionName + ".");
                WriteLog("Privilégio administrativo: " + (IsAdministrator() ? "confirmado" : "não detectado") + ".");
                WriteLog("Arquitetura detectada: Windows " +
                    (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits") +
                    ", processo " + (Environment.Is64BitProcess ? "64 bits" : "32 bits") + ".");
                activeSoftshopProcessId = 0;
                workflowRunning = true;
                cacheBackupDialogClosed = 0;
                rpcUnavailableDialogClosed = 0;
                dialogWatcher = new Thread(WatchAndDismissKnownSoftshopDialogs) { IsBackground = true };
                dialogWatcher.SetApartmentState(ApartmentState.STA);
                dialogWatcher.Start();

                ThrowIfCancellationRequested();
                StopSoftshop();
                ThrowIfCancellationRequested();
                string[] softshopCandidates = GetSoftshopExecutableCandidates();
                foreach (string candidate in softshopCandidates)
                    WriteLog("Verificando caminho do Softshop: " + candidate);
                string[] existingPdvExecutables = softshopCandidates.Where(File.Exists).ToArray();
                string existingPdvExecutable = existingPdvExecutables.FirstOrDefault();
                string[] existingPdvDirectories = softshopCandidates.Select(Path.GetDirectoryName)
                    .Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (configurationPrints)
                {
                    if (existingPdvExecutable == null)
                        throw new FileNotFoundException("Softshop Caixa não foi encontrado. Não é possível gerar os prints de configuração.");
                    WriteLog("Softshop Caixa encontrado em: " + existingPdvExecutable);
                    WriteLog("Abrindo Softshop para captura de configurações.");
                    WriteLog("Capturando configurações.");
                    ThrowIfCancellationRequested();
                    CaptureConfigurationTabs(existingPdvExecutable);
                    ThrowIfCancellationRequested();
                    WriteLog("Encerrando Softshop após a captura.");
                    StopSoftshop();
                    ThrowIfCancellationRequested();
                    WriteLog("Abrindo pasta dos prints.");
                    OpenPrintFolder();
                    ThrowIfCancellationRequested();
                    WriteLog("Prints de Configuração concluídos com sucesso.");
                    ShowFinal("Prints de Configuração concluídos.\r\nPrints e log: " + runFolder, MessageBoxIcon.Information);
                    return;
                }
                string msiPath;
                if (fullReset)
                {
                    if (!string.IsNullOrWhiteSpace(existingPdvExecutable))
                    {
                        WriteLog("Softshop Caixa encontrado em: " + existingPdvExecutable);
                        if (existingPdvExecutables.Length > 1)
                            WriteLog("Aviso: foram encontradas instalações nas duas pastas; ambas serão preservadas após a desinstalação: " +
                                string.Join("; ", existingPdvExecutables) + ".");
                        WriteLog("Modo Reset Completo: pastas do LocalAppData serão preservadas por renomeação.");
                        WriteLog("Capturando configurações enquanto o pacote é baixado.");
                        ThrowIfCancellationRequested();
                        Volatile.Write(ref captureInProgress, 1);
                        try
                        {
                            if (pdvPackageTask.Status == TaskStatus.RanToCompletion &&
                                Interlocked.Exchange(ref packageReadyDuringCaptureLogged, 1) == 0)
                                WriteLog("Pacote do PDV preparado e aguardando conclusão da captura.");
                            CaptureConfigurationTabs(existingPdvExecutable);
                        }
                        finally { Volatile.Write(ref captureInProgress, 0); }
                        ThrowIfCancellationRequested();
                        StopSoftshop();
                    }
                    else
                    {
                        WriteLog("Softshop.exe não encontrado em nenhum caminho compatível com a arquitetura do Windows.");
                        WriteLog("Modo de instalação nova ativado: captura, desinstalação e renomeação de pastas serão ignoradas.");
                    }
                    msiPath = WaitForPreparedPdvPackage(pdvPackageTask,
                        existingPdvExecutable != null
                            ? "Captura concluída. Aguardando preparação do pacote do PDV."
                            : "Aguardando preparação do pacote do PDV.",
                        existingPdvExecutable != null
                            ? "Não foi possível preparar o novo pacote do PDV. A instalação atual foi mantida."
                            : "Não foi possível preparar o pacote do PDV para a instalação nova.");
                    if (existingPdvExecutable != null)
                    {
                        ThrowIfCancellationRequested();
                        UninstallAllSoftshopEntries();
                        ThrowIfCancellationRequested();
                        StopSoftshop();
                        ThrowIfCancellationRequested();
                        BackupFolders(existingPdvExecutables.Select(Path.GetDirectoryName), true);
                    }
                    ThrowIfCancellationRequested();
                    if (updateDllsRequested) UpdateSoftcomDlls();
                    else WriteLog("Atualização de SetupSoftcomDLLs não selecionada.");
                }
                else
                {
                    WriteLog("Modo Reinstalação Limpa: desinstalação, preservação da pasta do caixa e instalação; sem capturas ou atualização de DLLs.");
                    WriteLog("As pastas do LocalAppData não serão alteradas.");
                    foreach (string directory in existingPdvDirectories)
                        WriteLog("Pasta do Softshop Caixa encontrada para preservação: " + directory);
                    ThrowIfCancellationRequested();
                    UninstallAllSoftshopEntries();
                    ThrowIfCancellationRequested();
                    StopSoftshop();
                    ThrowIfCancellationRequested();
                    BackupFolders(existingPdvDirectories, false);
                    msiPath = WaitForPreparedPdvPackage(pdvPackageTask,
                        "Aguardando preparação do pacote do PDV após a desinstalação.",
                        "Não foi possível preparar o novo pacote do PDV após a desinstalação. Verifique o log e tente novamente.");
                }
                ThrowIfCancellationRequested();
                InstallPdvMsi(msiPath);
                if (fullReset)
                {
                    ThrowIfCancellationRequested();
                    OpenNewInstallationAtConfiguration();
                    ThrowIfCancellationRequested();
                    OpenPrintFolder();
                }
                ThrowIfCancellationRequested();
                WriteLog("Concluído com sucesso.");
                ShowFinal(fullReset ? "Processo concluído.\r\nPrints e log: " + runFolder
                    : "Reinstalação Limpa concluída.\r\nLog: " + runFolder, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException)
            {
                try { StopSoftshop(true); } catch { }
                WriteLog("CANCELADO: processo interrompido pelo usuário.");
                ShowFinal("Processo cancelado.\r\nLog: " + runFolder, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                if (configurationPrints)
                {
                    try { StopSoftshop(true); } catch { }
                }
                WriteLog("ERRO: " + ex);
                ShowFinal(ex.Message + "\r\n\r\nO processo foi interrompido. Consulte o log em:\r\n" + runFolder, MessageBoxIcon.Error);
            }
            finally
            {
                activeSoftshopProcessId = 0;
                workflowRunning = false;
                packageCancellation.Cancel();
                if (pdvPackageTask != null)
                {
                    // A próxima execução só pode começar após o download/extração anterior encerrar.
                    try { pdvPackageTask.GetAwaiter().GetResult(); } catch { }
                }
                if (dialogWatcher != null && dialogWatcher.IsAlive)
                    dialogWatcher.Join();
                packageCancellation.Dispose();
                FinishAutomation();
            }
        }

        private void CaptureConfigurationTabs(string installedExecutable)
        {
            ThrowIfCancellationRequested();
            if (!File.Exists(installedExecutable))
                throw new FileNotFoundException("Softshop.exe não encontrado no caminho identificado.", installedExecutable);

            WriteLog("Abrindo Softshop Caixa.");
            activeSoftshopProcessId = 0;
            ThrowIfCancellationRequested();
            Process.Start(new ProcessStartInfo(installedExecutable)
            {
                WorkingDirectory = Path.GetDirectoryName(installedExecutable),
                UseShellExecute = true
            });
            AutomationElement appWindow = WaitForSoftshopWindow(45);
            if (appWindow == null) throw new InvalidOperationException("A janela de login do Softshop não apareceu em 45 segundos.");
            activeSoftshopProcessId = appWindow.Current.ProcessId;

            WriteLog("Preenchendo senha de suporte.");
            ActivateWindow(new IntPtr(appWindow.Current.NativeWindowHandle));
            AutomationElement passwordBox = WaitForPasswordField(appWindow, 8);
            FillPassword(appWindow, passwordBox);
            WriteLog("Senha preenchida; abrindo painel de configurações diretamente com F11.");
            ActivateWindow(new IntPtr(appWindow.Current.NativeWindowHandle));
            SendKey(Keys.F11);
            AutomationElement configWindow = WaitForConfigurationWindow(appWindow.Current.ProcessId, 20);
            if (configWindow == null) throw new InvalidOperationException("O painel de Configurações não apareceu após F11.");

            screenshotNumber = 0;
            CaptureConfiguredRoute(configWindow);
            WriteLog("Captura das abas finalizada.");
        }

        private AutomationElement WaitForSoftshopWindow(int timeoutSeconds)
        {
            return WaitForWindow(timeoutSeconds, element =>
            {
                int processId = element.Current.ProcessId;
                try
                {
                    if (!Process.GetProcessById(processId).ProcessName.Equals("softshop", StringComparison.OrdinalIgnoreCase)) return false;
                    return IsLoginWindow(element);
                }
                catch { return false; }
            });
        }

        private static bool IsLoginWindow(AutomationElement window)
        {
            string title = window.Current.Name ?? string.Empty;
            if (title.IndexOf("login", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            try
            {
                var controls = window.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>();
                return controls.Any(c =>
                {
                    ControlType type = c.Current.ControlType;
                    if (type == ControlType.Edit || type == ControlType.ComboBox) return true;
                    string name = c.Current.Name ?? string.Empty;
                    return type == ControlType.Button &&
                        (name.IndexOf("logar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         name.IndexOf("cancelar", StringComparison.OrdinalIgnoreCase) >= 0);
                });
            }
            catch { return false; }
        }

        private AutomationElement WaitForConfigurationWindow(int softshopProcessId, int timeoutSeconds)
        {
            return WaitForWindow(timeoutSeconds, element =>
            {
                if (element.Current.ProcessId != softshopProcessId) return false;
                string name = element.Current.Name ?? string.Empty;
                if (name.IndexOf("Configura", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                try
                {
                    return element.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)).Count > 0;
                }
                catch { return false; }
            });
        }

        private AutomationElement WaitForWindow(int timeoutSeconds, Func<AutomationElement, bool> predicate)
        {
            for (int second = 0; second < timeoutSeconds * 2; second++)
            {
                ThrowIfCancellationRequested();
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement window in windows)
                    if (predicate(window)) return window;
                SleepWithCancellation(500);
            }
            return null;
        }

        private AutomationElement WaitForPasswordField(AutomationElement root, int timeoutSeconds)
        {
            for (int attempt = 0; attempt < timeoutSeconds * 2; attempt++)
            {
                ThrowIfCancellationRequested();
                var inputCondition = new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox));
                AutomationElement[] inputs;
                try
                {
                    inputs = root.FindAll(TreeScope.Descendants, inputCondition).Cast<AutomationElement>()
                        .Where(IsUsableLoginInput)
                        .OrderBy(e => e.Current.BoundingRectangle.Top)
                        .ThenBy(e => e.Current.BoundingRectangle.Left)
                        .ToArray();
                }
                catch
                {
                    inputs = new AutomationElement[0];
                }

                // Preferimos sinais semânticos do próprio controle. TextBox com PasswordChar
                // normalmente expõe IsPassword; algumas versões também usam "senha" no ID.
                AutomationElement password = inputs.FirstOrDefault(IsPasswordInput);
                if (password != null) return password;

                password = inputs.FirstOrDefault(HasPasswordSemanticName);
                if (password != null) return password;

                // Se os controles não possuem nome técnico, relaciona o campo com o rótulo
                // "Senha" pela mesma linha visual. Isso também funciona quando existe "Turno".
                password = FindInputAlignedWithPasswordLabel(root, inputs);
                if (password != null) return password;

                // Último recurso: agrupa ComboBox e Edit interno pelo centro vertical. A lógica
                // anterior dividia ambos quando os seus topos caíam em faixas de 12 px diferentes.
                AutomationElement[] logicalInputs = RemoveComboBoxInnerEdits(inputs);
                var inputRows = GroupInputsByVisualRow(logicalInputs);
                if (inputRows.Count >= 2)
                    return inputRows[1].FirstOrDefault(e => e.Current.ControlType == ControlType.Edit) ?? inputRows[1][0];
                // Em algumas versões o ComboBox do usuário não é exposto, restando apenas Senha.
                if (logicalInputs.Length == 1 && logicalInputs[0].Current.ControlType == ControlType.Edit) return logicalInputs[0];
                SleepWithCancellation(500);
            }
            return null;
        }

        private static bool IsUsableLoginInput(AutomationElement element)
        {
            try
            {
                var bounds = element.Current.BoundingRectangle;
                return element.Current.IsEnabled && !element.Current.IsOffscreen &&
                       !bounds.IsEmpty && bounds.Width >= 40 && bounds.Height >= 10;
            }
            catch { return false; }
        }

        private static bool IsPasswordInput(AutomationElement element)
        {
            try
            {
                return element.Current.ControlType == ControlType.Edit && element.Current.IsPassword;
            }
            catch { return false; }
        }

        private static bool HasPasswordSemanticName(AutomationElement element)
        {
            try
            {
                if (element.Current.ControlType != ControlType.Edit) return false;
                string metadata = NormalizeName((element.Current.Name ?? string.Empty) + " " +
                                                (element.Current.AutomationId ?? string.Empty) + " " +
                                                (element.Current.ClassName ?? string.Empty) + " " +
                                                (element.Current.HelpText ?? string.Empty));
                if (metadata.Contains("senha") || metadata.Contains("password")) return true;
                AutomationElement label = element.Current.LabeledBy;
                if (label == null) return false;
                string labelName = NormalizeName(label.Current.Name);
                return labelName == "senha" || labelName == "password";
            }
            catch { return false; }
        }

        private static AutomationElement FindInputAlignedWithPasswordLabel(AutomationElement root, AutomationElement[] inputs)
        {
            AutomationElement[] labels;
            try
            {
                labels = root.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                    .Cast<AutomationElement>()
                    .Where(e =>
                    {
                        try
                        {
                            string name = NormalizeName(e.Current.Name);
                            return !e.Current.IsOffscreen && !e.Current.BoundingRectangle.IsEmpty &&
                                   (name == "senha" || name == "password");
                        }
                        catch { return false; }
                    })
                    .ToArray();
            }
            catch
            {
                return null;
            }

            foreach (AutomationElement label in labels)
            {
                try
                {
                    var labelBounds = label.Current.BoundingRectangle;
                    double labelCenter = labelBounds.Top + labelBounds.Height / 2.0;
                    AutomationElement match = inputs
                        .Where(input =>
                        {
                            var bounds = input.Current.BoundingRectangle;
                            double inputCenter = bounds.Top + bounds.Height / 2.0;
                            bool sameRow = Math.Abs(inputCenter - labelCenter) <= Math.Max(12, bounds.Height / 2.0);
                            return sameRow && bounds.Left >= labelBounds.Right - 8;
                        })
                        .OrderBy(input => input.Current.ControlType == ControlType.Edit ? 0 : 1)
                        .ThenBy(input => Math.Abs(
                            input.Current.BoundingRectangle.Top + input.Current.BoundingRectangle.Height / 2.0 - labelCenter))
                        .ThenBy(input => input.Current.BoundingRectangle.Left)
                        .FirstOrDefault();
                    if (match != null) return match;
                }
                catch { }
            }
            return null;
        }

        private static AutomationElement[] RemoveComboBoxInnerEdits(AutomationElement[] inputs)
        {
            AutomationElement[] comboBoxes = inputs.Where(input =>
            {
                try { return input.Current.ControlType == ControlType.ComboBox; }
                catch { return false; }
            }).ToArray();
            if (comboBoxes.Length == 0) return inputs;

            return inputs.Where(input =>
            {
                try
                {
                    if (input.Current.ControlType != ControlType.Edit) return true;
                    var editBounds = input.Current.BoundingRectangle;
                    double centerX = editBounds.Left + editBounds.Width / 2.0;
                    double centerY = editBounds.Top + editBounds.Height / 2.0;
                    return !comboBoxes.Any(combo =>
                    {
                        var comboBounds = combo.Current.BoundingRectangle;
                        return centerX >= comboBounds.Left - 2 && centerX <= comboBounds.Right + 2 &&
                               centerY >= comboBounds.Top - 2 && centerY <= comboBounds.Bottom + 2;
                    });
                }
                catch { return false; }
            }).ToArray();
        }

        private static List<List<AutomationElement>> GroupInputsByVisualRow(AutomationElement[] inputs)
        {
            var rows = new List<List<AutomationElement>>();
            foreach (AutomationElement input in inputs)
            {
                double center;
                try
                {
                    var bounds = input.Current.BoundingRectangle;
                    center = bounds.Top + bounds.Height / 2.0;
                }
                catch { continue; }

                List<AutomationElement> row = rows.FirstOrDefault(existing =>
                {
                    var bounds = existing[0].Current.BoundingRectangle;
                    double existingCenter = bounds.Top + bounds.Height / 2.0;
                    return Math.Abs(existingCenter - center) <= 14;
                });
                if (row == null)
                {
                    row = new List<AutomationElement>();
                    rows.Add(row);
                }
                row.Add(input);
            }
            return rows.OrderBy(row => row.Min(input => input.Current.BoundingRectangle.Top)).ToList();
        }

        private static void SetText(AutomationElement element, string value)
        {
            object pattern;
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
            {
                ((ValuePattern)pattern).SetValue(value);
                return;
            }
            element.SetFocus();
            SendUnicodeText(value);
        }

        private void FillPassword(AutomationElement loginWindow, AutomationElement passwordBox)
        {
            if (passwordBox != null)
            {
                try
                {
                    var field = passwordBox.Current;
                    WriteLog("Campo Senha identificado: tipo=" + field.ControlType.ProgrammaticName +
                             ", id=" + (string.IsNullOrWhiteSpace(field.AutomationId) ? "(sem id)" : field.AutomationId) +
                             ", protegido=" + field.IsPassword + ".");
                }
                catch { WriteLog("Campo Senha identificado pela automação."); }
                SetText(passwordBox, SupportPassword);
                return;
            }

            // Algumas versões antigas não expõem os campos pela UI Automation. A tela de login
            // mantém o campo Senha como o segundo campo, na mesma posição proporcional da janela.
            var bounds = loginWindow.Current.BoundingRectangle;
            if (bounds.Width < 300 || bounds.Height < 200)
                throw new InvalidOperationException("A janela de login não possui dimensões válidas para preencher a senha.");
            System.Windows.Rect passwordLabelBounds;
            bool labelFound = TryGetPasswordLabelBounds(loginWindow, out passwordLabelBounds);
            WriteLog(labelFound
                ? "Campo Senha não exposto; preenchendo pela linha do rótulo Senha."
                : "Campo Senha não exposto; usando a posição proporcional da tela de login.");
            SetForegroundWindow(new IntPtr(loginWindow.Current.NativeWindowHandle));
            int x = (int)(bounds.Left + bounds.Width * 0.62);
            int y = labelFound
                ? (int)(passwordLabelBounds.Top + passwordLabelBounds.Height / 2.0)
                : (int)(bounds.Top + bounds.Height * 0.51);
            SetCursorPos(x, y);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            SendUnicodeText(SupportPassword);
        }

        private static bool TryGetPasswordLabelBounds(AutomationElement root, out System.Windows.Rect bounds)
        {
            bounds = System.Windows.Rect.Empty;
            try
            {
                AutomationElement label = root.FindAll(TreeScope.Descendants,
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text))
                    .Cast<AutomationElement>()
                    .FirstOrDefault(element =>
                    {
                        try
                        {
                            string name = NormalizeName(element.Current.Name);
                            return !element.Current.IsOffscreen &&
                                   (name == "senha" || name == "password");
                        }
                        catch { return false; }
                    });
                if (label == null || label.Current.BoundingRectangle.IsEmpty) return false;
                bounds = label.Current.BoundingRectangle;
                return true;
            }
            catch { return false; }
        }

        private static void ClickTab(AutomationElement tab)
        {
            var r = tab.Current.BoundingRectangle;
            int x = (int)(r.Left + r.Width / 2);
            int y = (int)(r.Top + r.Height / 2);
            SetCursorPos(x, y);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        }

        private void CaptureConfiguredRoute(AutomationElement configWindow)
        {
            ThrowIfCancellationRequested();
            AutomationElement main;

            main = OpenOptionalMainTab(configWindow, "Configurações Iniciais", "Configurações Iniciais", "Configuração Inicial");
            if (main != null)
            {
                var sql = OpenOptionalNestedTab(configWindow, "Configurações Iniciais / SQL Server", "SQL Server", "SQLServer");
                if (sql != null)
                {
                    EnsureCheckBoxIsChecked(configWindow, "Mostrar Senha", "Exibir Senha");
                    CaptureSelectedTab(configWindow, sql, "Configurações Iniciais - SQL Server");
                }
                else
                {
                    var web = OpenOptionalNestedTab(configWindow, "Configurações Iniciais / Web Service", "Web Service", "WebService");
                    CaptureSelectedTab(configWindow, web ?? main, "Configurações Iniciais - Web Service");
                }
            }

            CaptureSimpleMainTab(configWindow, "Tela de Vendas", "Tela de Vendas", "Vendas");
            CaptureSimpleMainTab(configWindow, "Funções", "Funções", "Funcoes");
            CaptureSimpleMainTab(configWindow, "TEF", "TEF");
            CaptureSimpleMainTab(configWindow, "Balança", "Balança", "Balanca");

            main = OpenOptionalMainTab(configWindow, "NFC-e/NFe", "NFC-e/NFe", "NFCe/NFe", "NFC-e NF-e");
            if (main != null)
            {
                int before = screenshotNumber;
                CaptureOptionalNestedTab(configWindow, "NFC-e/NFe - NFC-e", "NFC-e", "NFCe");
                CaptureOptionalNestedTab(configWindow, "NFC-e/NFe - NF-e", "NF-e", "NFe");
                if (screenshotNumber == before) CaptureSelectedTab(configWindow, main, "NFC-e-NFe");
            }

            CaptureSimpleMainTab(configWindow, "Impressões", "Impressões", "Impressoes");

            main = OpenOptionalMainTab(configWindow, "E-Mail", "E-Mail", "Email", "E Mail");
            if (main != null)
            {
                EnsureCheckBoxIsChecked(configWindow, "Exibir Senha", "Mostrar Senha");
                CaptureSelectedTab(configWindow, main, "E-Mail");
            }

            main = OpenOptionalMainTab(configWindow, "Restaurante/Delivery", "Restaurante/Delivery", "Restaurante Delivery", "Restaurante");
            if (main != null)
            {
                int before = screenshotNumber;
                CaptureOptionalNestedTab(configWindow, "Restaurante-Delivery - Configurações Gerais", "Configurações Gerais", "Configuracoes Gerais");
                CaptureOptionalNestedTab(configWindow, "Restaurante-Delivery - Parâmetros de Impressão", "Parâmetros de Impressão", "Parametros de Impressao");
                CaptureOptionalNestedTab(configWindow, "Restaurante-Delivery - Configurações Iniciais", "Configurações Iniciais", "Configuracoes Iniciais");
                if (screenshotNumber == before) CaptureSelectedTab(configWindow, main, "Restaurante-Delivery");
            }

            // A seta apenas desloca a faixa; não seleciona abas. Ela deve ser acionada somente aqui,
            // depois de Restaurante/Delivery, para revelar o segundo grupo de abas.
            RevealLaterMainTabs(configWindow, 7);
            CaptureSimpleMainTab(configWindow, "Funções Caixa", "Funções Caixa", "Funcoes Caixa");
            CaptureSimpleMainTab(configWindow, "Outros", "Outros");

            main = OpenOptionalMainTab(configWindow, "Integrações APIs Softcom", "Integrações APIs Softcom", "Integracoes APIs Softcom", "APIs Softcom");
            if (main != null)
            {
                int before = screenshotNumber;
                CaptureApiTab(configWindow, "Pix", "Pix");
                CaptureApiTab(configWindow, "Quero Bônus", "Quero Bônus", "Quero Bonus");
                CaptureOptionalNestedTab(configWindow, "Integrações APIs Softcom - SoftDelivery", "SoftDelivery", "Soft Delivery");
                if (screenshotNumber == before) CaptureSelectedTab(configWindow, main, "Integrações APIs Softcom");
            }

            main = OpenOptionalMainTab(configWindow, "Integrações APIs Delivery", "Integrações APIs Delivery", "Integracoes APIs Delivery", "APIs Delivery");
            if (main != null)
            {
                int before = screenshotNumber;
                CaptureOptionalNestedTab(configWindow, "Integrações APIs Delivery - IFood", "IFood", "iFood");
                CaptureOptionalNestedTab(configWindow, "Integrações APIs Delivery - Meu Carrinho", "Meu Carrinho", "MeuCarrinho");
                if (screenshotNumber == before) CaptureSelectedTab(configWindow, main, "Integrações APIs Delivery");
            }
        }

        private void CaptureSimpleMainTab(AutomationElement configWindow, string displayName, params string[] aliases)
        {
            var tab = OpenOptionalMainTab(configWindow, displayName, aliases);
            if (tab != null) CaptureSelectedTab(configWindow, tab, displayName);
        }

        private void CaptureOptionalNestedTab(AutomationElement configWindow, string displayName, params string[] aliases)
        {
            var tab = OpenOptionalNestedTab(configWindow, displayName, aliases);
            if (tab != null) CaptureSelectedTab(configWindow, tab, displayName);
        }

        private void CaptureApiTab(AutomationElement configWindow, string displayName, params string[] aliases)
        {
            var tab = OpenOptionalNestedTab(configWindow, "Integrações APIs Softcom / " + displayName, aliases);
            if (tab == null) return;
            SaveApiCredentials(configWindow, displayName);
            CaptureSelectedTab(configWindow, tab, "Integrações APIs Softcom - " + displayName);
        }

        private AutomationElement OpenOptionalMainTab(AutomationElement configWindow, string displayName, params string[] aliases)
        {
            AutomationElement tab = FindVisibleTab(configWindow, aliases, true);
            if (tab == null)
            {
                WriteLog("Aba ausente (ignorada): " + displayName);
                return null;
            }
            if (!ActivateTab(tab))
            {
                WriteLog("Aba não pôde ser selecionada (ignorada): " + displayName);
                return null;
            }
            return tab;
        }

        private AutomationElement OpenOptionalNestedTab(AutomationElement configWindow, string displayName, params string[] aliases)
        {
            AutomationElement tab = FindVisibleTab(configWindow, aliases, false);
            if (tab == null)
            {
                WriteLog("Subaba ausente (ignorada): " + displayName);
                return null;
            }
            if (!ActivateTab(tab))
            {
                WriteLog("Subaba não pôde ser selecionada (ignorada): " + displayName);
                return null;
            }
            return tab;
        }

        private static bool ActivateTab(AutomationElement tab)
        {
            ClickTab(tab);
            object pattern;
            if (!tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out pattern))
            {
                Thread.Sleep(250);
                return true;
            }
            var selection = (SelectionItemPattern)pattern;
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < 700)
            {
                try { if (selection.Current.IsSelected) return true; }
                catch { return false; }
                Thread.Sleep(50);
            }
            try { selection.Select(); }
            catch { return false; }
            watch.Restart();
            while (watch.ElapsedMilliseconds < 700)
            {
                try { if (selection.Current.IsSelected) return true; }
                catch { return false; }
                Thread.Sleep(50);
            }
            return false;
        }

        private static AutomationElement FindVisibleTab(AutomationElement configWindow, string[] aliases, bool mainOnly)
        {
            var tabs = GetVisibleTabs(configWindow);
            if (tabs.Length == 0) return null;
            double mainTop = tabs.Min(t => t.Current.BoundingRectangle.Top);
            var candidates = tabs.Where(t => mainOnly
                ? Math.Abs(t.Current.BoundingRectangle.Top - mainTop) <= 8
                : t.Current.BoundingRectangle.Top > mainTop + 8).ToArray();
            var normalizedAliases = aliases.Select(NormalizeName).Where(a => a.Length > 0).ToArray();
            var exact = candidates.FirstOrDefault(t => normalizedAliases.Contains(NormalizeName(t.Current.Name)));
            if (exact != null) return exact;
            return candidates.FirstOrDefault(t =>
            {
                string candidate = NormalizeName(t.Current.Name);
                return normalizedAliases.Any(alias => alias.Length >= 4 &&
                    (candidate.Contains(alias) || alias.Contains(candidate)));
            });
        }

        private static AutomationElement[] GetVisibleTabs(AutomationElement root)
        {
            return root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem))
                .Cast<AutomationElement>()
                .Where(t => !string.IsNullOrWhiteSpace(t.Current.Name) &&
                            !t.Current.IsOffscreen && !t.Current.BoundingRectangle.IsEmpty)
                .ToArray();
        }

        private void RevealLaterMainTabs(AutomationElement configWindow, int clicks)
        {
            WriteLog("Exibindo abas adicionais com " + clicks + " cliques na seta direita.");
            for (int i = 0; i < clicks; i++)
            {
                ClickRightTabScroller(configWindow);
                Thread.Sleep(100);
            }
        }

        private static void ClickRightTabScroller(AutomationElement configWindow)
        {
            var tabs = GetVisibleTabs(configWindow);
            var window = configWindow.Current.BoundingRectangle;
            double tabTop = tabs.Length == 0 ? window.Top + 22 : tabs.Min(t => t.Current.BoundingRectangle.Top);
            var buttons = configWindow.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button))
                .Cast<AutomationElement>()
                .Where(b => !b.Current.IsOffscreen && !b.Current.BoundingRectangle.IsEmpty &&
                            b.Current.BoundingRectangle.Left > window.Right - 80 &&
                            Math.Abs(b.Current.BoundingRectangle.Top - tabTop) < 18)
                .OrderBy(b => b.Current.BoundingRectangle.Left).ToArray();
            var arrow = buttons.FirstOrDefault(b =>
                (b.Current.Name ?? string.Empty).IndexOf("▶", StringComparison.Ordinal) >= 0);
            if (arrow != null)
            {
                ClickAt(arrow.Current.BoundingRectangle);
            }
            else if (buttons.Length > 0)
            {
                ClickAt(buttons[buttons.Length - 1].Current.BoundingRectangle);
            }
            else
            {
                int x = (int)(window.Right - 8);
                int y = (int)(tabTop + 8);
                ClickAt(x, y);
            }
            Thread.Sleep(70);
        }

        private static void ClickAt(System.Windows.Rect bounds)
        {
            ClickAt((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
        }

        private static void ClickAt(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
        }

        private static string NormalizeName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            string decomposed = value.Normalize(NormalizationForm.FormD);
            var result = new StringBuilder();
            foreach (char c in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(c)) result.Append(char.ToLowerInvariant(c));
            }
            return result.ToString();
        }

        private static bool EnsureCheckBoxIsChecked(AutomationElement configWindow, params string[] aliases)
        {
            var checkBox = configWindow.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.CheckBox))
                .Cast<AutomationElement>().FirstOrDefault(c =>
                    aliases.Select(NormalizeName).Any(a => NormalizeName(c.Current.Name).Contains(a)));
            if (checkBox == null) return false;

            object pattern;
            if (checkBox.TryGetCurrentPattern(TogglePattern.Pattern, out pattern))
            {
                if (((TogglePattern)pattern).Current.ToggleState == ToggleState.Off)
                    ((TogglePattern)pattern).Toggle();
                return true;
            }
            var r = checkBox.Current.BoundingRectangle;
            ClickAt(r);
            return true;
        }

        private void SaveApiCredentials(AutomationElement configWindow, string integrationName)
        {
            var edits = configWindow.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit))
                .Cast<AutomationElement>()
                .Where(e => !e.Current.IsOffscreen && !e.Current.BoundingRectangle.IsEmpty && e.Current.IsEnabled)
                .OrderBy(e => e.Current.BoundingRectangle.Top).ThenBy(e => e.Current.BoundingRectangle.Left)
                .ToArray();
            if (edits.Length < 2)
            {
                WriteLog("Campos Cliente ID/Secret ausentes em " + integrationName + ".");
                return;
            }
            string clientId = ReadElementValue(edits[0]);
            string clientSecret = ReadElementValue(edits[1]);
            string credentialsFile = Path.Combine(runFolder, "Credenciais_APIs_Softcom.txt");
            var content = new StringBuilder()
                .AppendLine("[" + integrationName + "]")
                .AppendLine("Cliente ID: " + clientId)
                .AppendLine("Cliente Secret: " + clientSecret)
                .AppendLine()
                .ToString();
            File.AppendAllText(credentialsFile, content, new UTF8Encoding(true));
            WriteLog("Credenciais de " + integrationName + " copiadas para " + Path.GetFileName(credentialsFile) + ".");
        }

        private static string ReadElementValue(AutomationElement element)
        {
            object pattern;
            if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern))
                return ((ValuePattern)pattern).Current.Value ?? string.Empty;
            return string.Empty;
        }

        private void CaptureSelectedTab(AutomationElement configWindow, AutomationElement tab, string fileName)
        {
            ThrowIfCancellationRequested();
            SleepWithCancellation(250);
            screenshotNumber++;
            SaveTabScreenshot(configWindow, tab.Current.BoundingRectangle, screenshotNumber, fileName);
        }

        private void SaveTabScreenshot(AutomationElement configWindow, System.Windows.Rect tabRect, int number, string tabName)
        {
            var windowRect = configWindow.Current.BoundingRectangle;
            var bounds = Rectangle.FromLTRB((int)windowRect.Left, (int)windowRect.Top, (int)windowRect.Right, (int)windowRect.Bottom);
            if (bounds.Width < 100 || bounds.Height < 100) throw new InvalidOperationException("A janela de configurações não está visível para captura.");
            ActivateWindow(new IntPtr(configWindow.Current.NativeWindowHandle));
            using (var image = new Bitmap(bounds.Width, bounds.Height))
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size, CopyPixelOperation.SourceCopy);
                DrawArrow(graphics, new Point((int)(tabRect.Left - bounds.Left + tabRect.Width / 2), Math.Max(6, (int)(tabRect.Top - bounds.Top - 38))),
                    new Point((int)(tabRect.Left - bounds.Left + tabRect.Width / 2), (int)(tabRect.Top - bounds.Top + 3)));
                string safeName = string.Concat(tabName.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
                string file = Path.Combine(runFolder, number.ToString("00") + " - " + safeName + ".png");
                image.Save(file, ImageFormat.Png);
                WriteLog("Print salvo: " + Path.GetFileName(file));
            }
        }

        private static void DrawArrow(Graphics g, Point start, Point end)
        {
            using (var pen = new Pen(Color.Red, 4) { EndCap = LineCap.ArrowAnchor })
                g.DrawLine(pen, start, end);
        }

        private static string[] GetSoftshopExecutableCandidates()
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (Environment.Is64BitOperatingSystem)
            {
                string nativeProgramFiles = Environment.GetEnvironmentVariable("ProgramW6432");
                if (!string.IsNullOrWhiteSpace(nativeProgramFiles)) programFiles = nativeProgramFiles;
            }
            if (string.IsNullOrWhiteSpace(programFiles))
                programFiles = Environment.GetEnvironmentVariable("ProgramFiles");

            string programFilesX86 = null;
            if (Environment.Is64BitOperatingSystem)
            {
                programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (string.IsNullOrWhiteSpace(programFilesX86))
                    programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            }
            if (string.IsNullOrWhiteSpace(programFilesX86))
            {
                string driveRoot = Path.GetPathRoot(programFiles);
                if (string.IsNullOrWhiteSpace(driveRoot))
                    driveRoot = Path.GetPathRoot(Environment.SystemDirectory);
                if (!string.IsNullOrWhiteSpace(driveRoot))
                    programFilesX86 = Path.Combine(driveRoot, "Program Files (x86)");
            }

            return BuildSoftshopExecutableCandidates(
                Environment.Is64BitOperatingSystem, programFiles, programFilesX86);
        }

        private static string[] BuildSoftshopExecutableCandidates(
            bool is64BitOperatingSystem, string programFiles, string programFilesX86)
        {
            var roots = new List<string>();
            if (is64BitOperatingSystem)
            {
                AddUniqueDirectory(roots, programFilesX86);
                AddUniqueDirectory(roots, programFiles);
            }
            else
            {
                AddUniqueDirectory(roots, programFiles);
                AddUniqueDirectory(roots, programFilesX86);
            }
            if (roots.Count == 0)
                throw new InvalidOperationException("O Windows não informou o diretório Program Files.");

            return roots.Select(root => Path.Combine(root, SoftshopInstallSubdirectory, SoftshopExecutableName)).ToArray();
        }

        private static void AddUniqueDirectory(ICollection<string> directories, string directory)
        {
            if (string.IsNullOrWhiteSpace(directory)) return;
            string normalized = directory.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!directories.Any(existing => string.Equals(existing, normalized, StringComparison.OrdinalIgnoreCase)))
                directories.Add(normalized);
        }

        private static string FindInstalledSoftshopExecutable()
        {
            return GetSoftshopExecutableCandidates().FirstOrDefault(File.Exists);
        }

        private void StopSoftshop(bool ignoreCancellation = false)
        {
            activeSoftshopProcessId = 0;
            WriteLog("Encerrando processos Softshop.");
            foreach (var process in Process.GetProcesses().Where(p => p.ProcessName.Equals("softshop", StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    if (stopRequested)
                    {
                        TryTerminateProcessTree(process);
                        if (!ignoreCancellation) throw new OperationCanceledException("Encerramento do Softshop cancelado.");
                    }
                    else
                    {
                        if (!process.CloseMainWindow()) TryTerminateProcessTree(process);
                        var stopwatch = Stopwatch.StartNew();
                        while (!process.HasExited && stopwatch.ElapsedMilliseconds < 8000)
                        {
                            if (stopRequested)
                            {
                                TryTerminateProcessTree(process);
                                if (!ignoreCancellation) throw new OperationCanceledException("Encerramento do Softshop cancelado.");
                                break;
                            }
                            process.WaitForExit(200);
                        }
                        if (!process.HasExited) TryTerminateProcessTree(process);
                    }
                    WriteLog("Processo encerrado: PID " + process.Id);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex) { WriteLog("Aviso ao encerrar processo: " + ex.Message); }
                finally { process.Dispose(); }
            }
        }

        private void BackupFolders(IEnumerable<string> softshopInstallDirectories, bool backupLocalAppData)
        {
            foreach (string directory in softshopInstallDirectories
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                ThrowIfCancellationRequested();
                MoveToNumberedBackup(directory);
            }
            if (!backupLocalAppData) return;
            ThrowIfCancellationRequested();
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            MoveToNumberedBackup(Path.Combine(local, "Softcom Tecnologia"));
            ThrowIfCancellationRequested();
            MoveToNumberedBackup(Path.Combine(local, "Softcom_Tecnologia"));
        }

        private void MoveToNumberedBackup(string source)
        {
            if (!Directory.Exists(source))
            {
                WriteLog("Pasta não encontrada (ignorada): " + source);
                return;
            }
            string parent = Path.GetDirectoryName(source);
            string name = Path.GetFileName(source);
            Exception lastError = null;
            const int maximumAttempts = 30;
            for (int attempt = 1; attempt <= maximumAttempts; attempt++)
            {
                ThrowIfCancellationRequested();
                if (!Directory.Exists(source))
                {
                    WriteLog("Pasta removida durante a espera; não há conteúdo para preservar: " + source);
                    return;
                }

                string destination = GetAvailableBackupPath(parent, name);
                try
                {
                    Directory.Move(source, destination);
                    WriteLog("Pasta preservada: " + destination);
                    return;
                }
                catch (UnauthorizedAccessException ex)
                {
                    lastError = ex;
                }
                catch (IOException ex)
                {
                    lastError = ex;
                }

                if (attempt == 1)
                    WriteLog("A pasta ainda está bloqueada após a desinstalação. Tentando novamente por até 30 segundos: " +
                        source + ". Motivo: " + DescribeFileSystemError(lastError));
                else if (attempt % 5 == 0)
                    WriteLog("Pasta ainda bloqueada; tentativa " + attempt + " de " + maximumAttempts + ".");
                if (attempt < maximumAttempts) SleepWithCancellation(1000);
            }

            throw new IOException("Não foi possível renomear a pasta após " + maximumAttempts +
                " tentativas: " + source + ". Mesmo com privilégios administrativos, um processo, antivírus " +
                "ou uma ACL restritiva pode impedir a renomeação. Detalhe final: " +
                DescribeFileSystemError(lastError), lastError);
        }

        private static string DescribeFileSystemError(Exception error)
        {
            if (error == null) return "não informado";
            int win32Code = error.HResult & 0xFFFF;
            return error.Message + " (HRESULT 0x" + error.HResult.ToString("X8") +
                ", código Win32 " + win32Code + ")";
        }

        private static string GetAvailableBackupPath(string parent, string name)
        {
            string destination = Path.Combine(parent, name + "_");
            int number = 1;
            while (Directory.Exists(destination) || File.Exists(destination))
                destination = Path.Combine(parent, name + "_" + number++);
            return destination;
        }

        private void UninstallAllSoftshopEntries()
        {
            var entries = GetUninstallRegistryViews()
                .SelectMany(ReadUninstallEntries)
                .Where(e => e.DisplayName.IndexOf(SoftshopProductName, StringComparison.OrdinalIgnoreCase) >= 0)
                .GroupBy(e => e.UninstallString ?? e.QuietUninstallString ??
                    (e.DisplayName + "|" + e.DisplayVersion), StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First()).ToArray();
            if (entries.Length == 0)
            {
                WriteLog("Nenhuma entrada de desinstalação do Softshop Caixa foi encontrada.");
                return;
            }
            foreach (var entry in entries)
            {
                ThrowIfCancellationRequested();
                WriteLog("Desinstalando: " + entry.DisplayName);
                string command = entry.QuietUninstallString;
                if (string.IsNullOrWhiteSpace(command))
                {
                    string id = ExtractProductCode(entry.UninstallString);
                    if (string.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Não foi possível obter o ProductCode de " + entry.DisplayName);
                    command = "msiexec.exe /x " + id + " /qn /norestart";
                }
                RunCommand("cmd.exe", "/c " + command, 600000, "desinstalação");
            }
        }

        private void UpdateSoftcomDlls()
        {
            ThrowIfCancellationRequested();
            WriteLog("Atualização opcional de SetupSoftcomDLLs iniciada.");
            var installedEntries = GetSetupDllEntries();
            if (installedEntries.Length == 0)
            {
                WriteLog("SetupSoftcomDLLs não estava instalado; será feita uma instalação nova.");
            }
            else
            {
                foreach (var entry in installedEntries)
                {
                    ThrowIfCancellationRequested();
                    WriteLog("Desinstalando " + entry.DisplayName +
                        (string.IsNullOrWhiteSpace(entry.DisplayVersion) ? "." : " versão " + entry.DisplayVersion + "."));
                    string command = entry.QuietUninstallString;
                    if (string.IsNullOrWhiteSpace(command))
                    {
                        string productCode = ExtractProductCode(entry.UninstallString);
                        if (string.IsNullOrWhiteSpace(productCode))
                            throw new InvalidOperationException("Não foi possível obter o ProductCode de " + entry.DisplayName + ".");
                        command = "msiexec.exe /x " + productCode + " /qn /norestart";
                    }
                    RunCommand("cmd.exe", "/c " + command, 600000, "desinstalação de SetupSoftcomDLLs");
                }
            }

            string dllFolder = @"C:\Softcom\ResetCaixa\DLLs";
            Directory.CreateDirectory(dllFolder);
            string rarPath = Path.Combine(dllFolder, "SetupSoftcomDLLs.rar");
            string msiPath = Path.Combine(dllFolder, "SetupSoftcomDLLs.msi");
            WriteLog("Baixando SetupSoftcomDLLs mais recente.");
            DownloadFileWithCancellation(DllDownloadUrl, rarPath, "SoftcomSupportAutomation/2.12.0");
            if (!File.Exists(rarPath) || new FileInfo(rarPath).Length == 0)
                throw new InvalidOperationException("O download de SetupSoftcomDLLs retornou um arquivo vazio.");
            WriteLog("Download de SetupSoftcomDLLs concluído: " + new FileInfo(rarPath).Length + " bytes.");
            ExtractMsiFromArchive(rarPath, msiPath);
            if (!File.Exists(msiPath))
                throw new FileNotFoundException("SetupSoftcomDLLs.msi não foi encontrado dentro do RAR.");

            int exitCode = InstallSetupDllMsi(msiPath);
            bool registered = WaitForSetupDllRegistration(15);
            if (!registered)
                throw new InvalidOperationException("A instalação de SetupSoftcomDLLs terminou, mas o produto não apareceu em Programas e Recursos. Código: " + exitCode + ".");
            if (exitCode != 0 && exitCode != 1641 && exitCode != 3010)
                WriteLog("Aviso: o instalador de SetupSoftcomDLLs retornou " + exitCode + ", mas o produto ficou instalado; continuando conforme configurado.");
            else
                WriteLog("SetupSoftcomDLLs instalado e confirmado em Programas e Recursos.");
        }

        private static UninstallEntry[] GetSetupDllEntries()
        {
            return GetUninstallRegistryViews()
                .SelectMany(ReadUninstallEntries)
                .Where(e => NormalizeName(e.DisplayName).Contains("setupsoftcomdlls"))
                .GroupBy(e => e.UninstallString ?? e.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()).ToArray();
        }

        private static RegistryView[] GetUninstallRegistryViews()
        {
            return BuildUninstallRegistryViews(Environment.Is64BitOperatingSystem);
        }

        private static RegistryView[] BuildUninstallRegistryViews(bool is64BitOperatingSystem)
        {
            return is64BitOperatingSystem
                ? new[] { RegistryView.Registry64, RegistryView.Registry32 }
                : new[] { RegistryView.Registry32 };
        }

        private bool WaitForSetupDllRegistration(int timeoutSeconds)
        {
            for (int attempt = 0; attempt < timeoutSeconds * 2; attempt++)
            {
                ThrowIfCancellationRequested();
                if (GetSetupDllEntries().Length > 0) return true;
                SleepWithCancellation(500);
            }
            return false;
        }

        private int InstallSetupDllMsi(string msiPath)
        {
            WriteLog("Instalando SetupSoftcomDLLs.");
            ThrowIfCancellationRequested();
            using (var process = Process.Start(new ProcessStartInfo("msiexec.exe",
                "/i \"" + msiPath + "\" /qb! /norestart REBOOT=ReallySuppress")
            { UseShellExecute = false, CreateNoWindow = false }))
            {
                if (process == null) throw new InvalidOperationException("Não foi possível iniciar o instalador de SetupSoftcomDLLs.");
                var stopwatch = Stopwatch.StartNew();
                bool ignoreLogged = false;
                while (!process.HasExited && stopwatch.ElapsedMilliseconds < 600000)
                {
                    if (stopRequested)
                    {
                        TryTerminateProcessTree(process);
                        throw new OperationCanceledException("Instalação de SetupSoftcomDLLs cancelada.");
                    }
                    if (DismissDllInstallWarningDialog() && !ignoreLogged)
                    {
                        ignoreLogged = true;
                        WriteLog("Aviso de instalação de DLL ignorado automaticamente; instalação continuará.");
                    }
                    Thread.Sleep(250);
                }
                if (!process.HasExited)
                {
                    try { process.Kill(); } catch { }
                    throw new TimeoutException("Tempo limite excedido durante a instalação de SetupSoftcomDLLs.");
                }
                return process.ExitCode;
            }
        }

        private static bool DismissDllInstallWarningDialog()
        {
            try
            {
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement window in windows)
                {
                    var children = window.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                        .Cast<AutomationElement>().ToArray();
                    var ignoreButton = children.FirstOrDefault(e => e.Current.ControlType == ControlType.Button &&
                        (NormalizeName(e.Current.Name) == "ignorar" || NormalizeName(e.Current.Name) == "ignore"));
                    if (ignoreButton == null) continue;
                    SetForegroundWindow(new IntPtr(window.Current.NativeWindowHandle));
                    object invoke;
                    if (ignoreButton.TryGetCurrentPattern(InvokePattern.Pattern, out invoke))
                        ((InvokePattern)invoke).Invoke();
                    else
                        ClickAt(ignoreButton.Current.BoundingRectangle);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static UninstallEntry[] ReadUninstallEntries(RegistryView view)
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var key = baseKey.OpenSubKey(keyPath))
                {
                    if (key == null) return new UninstallEntry[0];
                    return key.GetSubKeyNames().Select(name =>
                    {
                        using (var sub = key.OpenSubKey(name))
                        {
                            if (sub == null) return null;
                            var title = sub.GetValue("DisplayName") as string;
                            return string.IsNullOrWhiteSpace(title) ? null : new UninstallEntry
                            {
                                DisplayName = title,
                                DisplayVersion = sub.GetValue("DisplayVersion") as string,
                                UninstallString = sub.GetValue("UninstallString") as string,
                                QuietUninstallString = sub.GetValue("QuietUninstallString") as string
                            };
                        }
                    }).Where(x => x != null).ToArray();
                }
            }
            catch { return new UninstallEntry[0]; }
        }

        private static string ExtractProductCode(string uninstallString)
        {
            if (string.IsNullOrWhiteSpace(uninstallString)) return null;
            int start = uninstallString.IndexOf('{');
            int end = uninstallString.IndexOf('}', start + 1);
            return start >= 0 && end > start ? uninstallString.Substring(start, end - start + 1) : null;
        }

        private string PreparePdvPackage(CancellationToken cancellationToken)
        {
            ThrowIfCancellationRequested(cancellationToken);
            string resetFolder = @"C:\Softcom\ResetCaixa";
            string downloadFolder = Path.Combine(resetFolder, "Download");
            Directory.CreateDirectory(downloadFolder);
            string archivePath = Path.Combine(downloadFolder,
                "PDV_SoftshopCaixa_Selecionado" + selectedPdvArchiveExtension);
            string msiPath = Path.Combine(downloadFolder, "SetupSoftshopFrenteLoja.msi");
            // Remover apenas as três saídas conhecidas, sem tocar em backups ou outros diretórios.
            foreach (string name in new[] { "PDV_SoftshopCaixa_Selecionado.zip", "PDV_SoftshopCaixa_Selecionado.rar", "SetupSoftshopFrenteLoja.msi" })
            {
                ThrowIfCancellationRequested(cancellationToken);
                string staleFile = Path.Combine(downloadFolder, name);
                if (File.Exists(staleFile)) File.Delete(staleFile);
            }
            try
            {
                System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12;
                DownloadFileWithCancellation(selectedPdvDownloadUrl, archivePath, "SoftcomSupportAutomation/2.12.0", cancellationToken);
                ThrowIfCancellationRequested(cancellationToken);
                if (!File.Exists(archivePath) || new FileInfo(archivePath).Length == 0)
                    throw new InvalidOperationException("O download do pacote retornou um arquivo vazio.");
                WriteLog("Download do PDV concluído: " + new FileInfo(archivePath).Length + " bytes.");
                if (!string.IsNullOrWhiteSpace(selectedPdvExpectedSha256))
                {
                    WriteLog("Validando integridade SHA-256 do pacote.");
                    string actualSha256 = ComputeFileSha256WithCancellation(archivePath, cancellationToken);
                    if (!string.Equals(actualSha256, selectedPdvExpectedSha256, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("O SHA-256 do pacote baixado não corresponde ao publicado no GitHub. A instalação foi cancelada.");
                    WriteLog("SHA-256 validado com sucesso.");
                }
                else
                {
                    WriteLog("Pacote do servidor atual não possui SHA-256 cadastrado; continuando com a validação de conteúdo.");
                }
                WriteLog("Extraindo SetupSoftshopFrenteLoja.msi do pacote " + selectedPdvArchiveExtension.ToUpperInvariant() + ".");
                ExtractMsiFromArchive(archivePath, msiPath, cancellationToken);
                ThrowIfCancellationRequested(cancellationToken);
                if (!File.Exists(msiPath) || new FileInfo(msiPath).Length == 0)
                    throw new InvalidOperationException("O arquivo SetupSoftshopFrenteLoja.msi não foi extraído ou está vazio.");
                WriteLog("Pacote do PDV preparado para instalação.");
                return msiPath;
            }
            catch
            {
                // Nunca deixar uma extração incompleta ou um pacote inválido para a próxima tentativa.
                try { if (File.Exists(msiPath)) File.Delete(msiPath); } catch { }
                try { if (File.Exists(archivePath)) File.Delete(archivePath); } catch { }
                throw;
            }
        }

        private string WaitForPreparedPdvPackage(Task<string> packageTask, string waitingMessage, string failureMessage)
        {
            try
            {
                ThrowIfCancellationRequested();
                if (!packageTask.IsCompleted) WriteLog(waitingMessage);
                else if (packageTask.Status == TaskStatus.RanToCompletion)
                    WriteLog("Pacote já estava pronto; continuando imediatamente.");
                // RunWorkflow está em uma thread de background; a interface continua disponível.
                string msiPath = packageTask.GetAwaiter().GetResult();
                ThrowIfCancellationRequested();
                if (!File.Exists(msiPath) || new FileInfo(msiPath).Length == 0)
                    throw new InvalidOperationException("O MSI preparado não está disponível para instalação.");
                return msiPath;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                WriteLog(failureMessage);
                throw new InvalidOperationException(failureMessage, ex);
            }
        }

        private void InstallPdvMsi(string msiPath)
        {
            ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(msiPath) || !File.Exists(msiPath) || new FileInfo(msiPath).Length == 0)
                throw new InvalidOperationException("O MSI preparado não está disponível para instalação.");
            WriteLog("Instalando Softshop Caixa.");
            RunCommand("msiexec.exe", "/i \"" + msiPath + "\" /qn /norestart", 600000, "instalação");
        }

        private void OpenNewInstallationAtConfiguration()
        {
            ThrowIfCancellationRequested();
            WriteLog("Abrindo a nova instalação no painel de Configurações.");
            string installedExecutable = null;
            for (int attempt = 0; attempt < 60; attempt++)
            {
                installedExecutable = FindInstalledSoftshopExecutable();
                if (!string.IsNullOrWhiteSpace(installedExecutable)) break;
                SleepWithCancellation(1000);
            }
            if (string.IsNullOrWhiteSpace(installedExecutable))
                throw new FileNotFoundException("A nova instalação não criou Softshop.exe. Caminhos verificados: " +
                    string.Join("; ", GetSoftshopExecutableCandidates()) + ".");

            WriteLog("Nova instalação localizada em: " + installedExecutable);
            ThrowIfCancellationRequested();
            activeSoftshopProcessId = 0;
            Process.Start(new ProcessStartInfo(installedExecutable)
            {
                WorkingDirectory = Path.GetDirectoryName(installedExecutable),
                UseShellExecute = true
            });
            AutomationElement appWindow = WaitForSoftshopWindow(120);
            if (appWindow == null) throw new InvalidOperationException("A tela de senha da nova instalação não apareceu.");
            activeSoftshopProcessId = appWindow.Current.ProcessId;
            ActivateWindow(new IntPtr(appWindow.Current.NativeWindowHandle));
            AutomationElement passwordBox = WaitForPasswordField(appWindow, 30);
            FillPassword(appWindow, passwordBox);
            ActivateWindow(new IntPtr(appWindow.Current.NativeWindowHandle));
            SendKey(Keys.F11);
            if (WaitForConfigurationWindow(appWindow.Current.ProcessId, 60) == null)
                throw new InvalidOperationException("O painel de Configurações não abriu na nova instalação.");
            WriteLog("Nova instalação aberta no painel de Configurações.");
        }

        private void OpenPrintFolder()
        {
            ThrowIfCancellationRequested();
            Process.Start(new ProcessStartInfo(runFolder) { UseShellExecute = true });
            WriteLog("Pasta dos prints aberta.");
        }

        private static string ComputeFileSha256(string filePath)
        {
            using (var stream = File.OpenRead(filePath))
            using (var sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        private string ComputeFileSha256WithCancellation(string filePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var stream = File.OpenRead(filePath))
            using (var sha256 = SHA256.Create())
            {
                var buffer = new byte[81920];
                int bytesRead;
                while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ThrowIfCancellationRequested(cancellationToken);
                    sha256.TransformBlock(buffer, 0, bytesRead, buffer, 0);
                }
                ThrowIfCancellationRequested(cancellationToken);
                sha256.TransformFinalBlock(new byte[0], 0, 0);
                return BitConverter.ToString(sha256.Hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private void DownloadFileWithCancellation(string address, string destination, string userAgent,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfCancellationRequested(cancellationToken);
            if (File.Exists(destination)) File.Delete(destination);
            using (var completed = new ManualResetEvent(false))
            using (var client = new System.Net.WebClient())
            {
                Exception downloadError = null;
                bool downloadCancelled = false;
                client.Headers[System.Net.HttpRequestHeader.UserAgent] = userAgent;
                client.DownloadFileCompleted += (sender, args) =>
                {
                    downloadError = args.Error;
                    downloadCancelled = args.Cancelled;
                    try { completed.Set(); } catch (ObjectDisposedException) { }
                };
                client.DownloadFileAsync(new Uri(address), destination);
                while (!completed.WaitOne(250))
                {
                    if (!stopRequested && !cancellationToken.IsCancellationRequested) continue;
                    client.CancelAsync();
                    completed.WaitOne(5000);
                    try { if (File.Exists(destination)) File.Delete(destination); } catch { }
                    throw new OperationCanceledException("Download cancelado pelo usuário.");
                }
                if (stopRequested || cancellationToken.IsCancellationRequested || downloadCancelled)
                {
                    try { if (File.Exists(destination)) File.Delete(destination); } catch { }
                    throw new OperationCanceledException("Download cancelado pelo usuário.");
                }
                if (downloadError != null)
                {
                    try { if (File.Exists(destination)) File.Delete(destination); } catch { }
                    throw downloadError;
                }
            }
        }

        private void ThrowIfCancellationRequested(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stopRequested) throw new OperationCanceledException("Execução cancelada pelo usuário.");
            cancellationToken.ThrowIfCancellationRequested();
        }

        private void SleepWithCancellation(int milliseconds)
        {
            int remaining = milliseconds;
            while (remaining > 0)
            {
                ThrowIfCancellationRequested();
                int interval = Math.Min(100, remaining);
                Thread.Sleep(interval);
                remaining -= interval;
            }
            ThrowIfCancellationRequested();
        }

        private void WaitForProcessExitWithCancellation(Process process, string action)
        {
            while (true)
            {
                if (stopRequested)
                {
                    TryTerminateProcessTree(process);
                    throw new OperationCanceledException(action + " cancelada.");
                }
                if (process.WaitForExit(250)) return;
            }
        }

        private static void TryTerminateProcessTree(Process process)
        {
            try
            {
                if (process == null || process.HasExited) return;
                string taskkill = Path.Combine(Environment.SystemDirectory, "taskkill.exe");
                using (var killer = Process.Start(new ProcessStartInfo(taskkill,
                    "/PID " + process.Id + " /T /F")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    if (killer != null) killer.WaitForExit(5000);
                }
            }
            catch { }
            try { if (process != null && !process.HasExited) process.Kill(); } catch { }
        }

        private void ExtractMsiFromArchive(string archivePath, string destinationMsi,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            ThrowIfCancellationRequested(cancellationToken);
            string expectedName = Path.GetFileName(destinationMsi);
            try
            {
                using (var archive = ArchiveFactory.OpenArchive(archivePath, new ReaderOptions()))
                {
                    var entry = archive.Entries.FirstOrDefault(e => !e.IsDirectory &&
                        string.Equals(Path.GetFileName(e.Key), expectedName, StringComparison.OrdinalIgnoreCase));
                    if (entry == null) throw new InvalidOperationException("O pacote compactado baixado não contém " + expectedName + ".");
                    using (Stream source = entry.OpenEntryStream())
                    using (Stream destination = File.Create(destinationMsi))
                    {
                        var buffer = new byte[81920];
                        int bytesRead;
                        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ThrowIfCancellationRequested(cancellationToken);
                            destination.Write(buffer, 0, bytesRead);
                        }
                    }
                }
                ThrowIfCancellationRequested(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(destinationMsi)) File.Delete(destinationMsi); } catch { }
                throw;
            }
        }

        private void RunCommand(string fileName, string arguments, int timeoutMilliseconds, string action)
        {
            ThrowIfCancellationRequested();
            using (var process = Process.Start(new ProcessStartInfo(fileName, arguments) { UseShellExecute = false, CreateNoWindow = true }))
            {
                if (process == null) throw new InvalidOperationException("Não foi possível iniciar " + action + ".");
                var stopwatch = Stopwatch.StartNew();
                while (!process.HasExited && stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
                {
                    if (stopRequested)
                    {
                        TryTerminateProcessTree(process);
                        throw new OperationCanceledException(action + " cancelada.");
                    }
                    Thread.Sleep(250);
                }
                if (!process.HasExited)
                {
                    TryTerminateProcessTree(process);
                    throw new TimeoutException("Tempo limite excedido durante " + action + ".");
                }
                if (process.ExitCode != 0 && process.ExitCode != 1641 && process.ExitCode != 3010)
                    throw new InvalidOperationException(action + " retornou o código " + process.ExitCode + ".");
                WriteLog(action + " finalizada (código " + process.ExitCode + ").");
            }
        }

        private void WatchAndDismissKnownSoftshopDialogs()
        {
            while (workflowRunning && !stopRequested)
            {
                if (DismissCacheBackupDialog() && Interlocked.Exchange(ref cacheBackupDialogClosed, 1) == 0)
                    WriteLog("Aviso de backup de cache fechado automaticamente.");
                int processId = activeSoftshopProcessId;
                if (processId > 0 && DismissRpcUnavailableDialog(processId) &&
                    Interlocked.Exchange(ref rpcUnavailableDialogClosed, 1) == 0)
                    WriteLog("Aviso 'O servidor RPC não está disponível' fechado automaticamente.");
                Thread.Sleep(250);
            }
        }

        private static bool DismissRpcUnavailableDialog(int softshopProcessId)
        {
            try
            {
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement window in windows)
                {
                    if (window.Current.ProcessId != softshopProcessId ||
                        NormalizeName(window.Current.Name) != "aviso") continue;
                    var children = window.FindAll(TreeScope.Descendants, Condition.TrueCondition)
                        .Cast<AutomationElement>().ToArray();
                    if (!children.Any(element => IsRpcUnavailableMessage(element.Current.Name))) continue;

                    AutomationElement okButton = children.FirstOrDefault(element =>
                        element.Current.ControlType == ControlType.Button && NormalizeName(element.Current.Name) == "ok");
                    if (okButton == null) return false;
                    return InvokeDialogButton(window, okButton);
                }
            }
            catch { }
            return false;
        }

        private static bool IsRpcUnavailableMessage(string text)
        {
            return NormalizeName(text) == "oservidorrpcnaoestadisponivel";
        }

        private static bool InvokeDialogButton(AutomationElement window, AutomationElement button)
        {
            try
            {
                object pattern;
                if (button.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                {
                    ((InvokePattern)pattern).Invoke();
                    return true;
                }
            }
            catch { }
            SetForegroundWindow(new IntPtr(window.Current.NativeWindowHandle));
            System.Windows.Point point;
            if (!button.TryGetClickablePoint(out point))
            {
                var bounds = button.Current.BoundingRectangle;
                point = new System.Windows.Point(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
            }
            SetCursorPos((int)point.X, (int)point.Y);
            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
            return true;
        }

        private static bool DismissCacheBackupDialog()
        {
            try
            {
                var windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
                foreach (AutomationElement window in windows)
                {
                    if (!string.Equals(window.Current.Name, "Aviso", StringComparison.OrdinalIgnoreCase)) continue;
                    var children = window.FindAll(TreeScope.Descendants, Condition.TrueCondition).Cast<AutomationElement>().ToArray();
                    bool isExpectedNotice = children.Any(t =>
                    {
                        string text = t.Current.Name ?? string.Empty;
                        return text.IndexOf("backup dos arquivos de cache", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               text.IndexOf("timestamps de sincronização SoftcomShop foram zerados", StringComparison.OrdinalIgnoreCase) >= 0;
                    });
                    if (!isExpectedNotice) continue;

                    var okButton = children.Where(c => c.Current.ControlType == ControlType.Button)
                        .Cast<AutomationElement>().FirstOrDefault(b =>
                            string.Equals(b.Current.Name, "OK", StringComparison.OrdinalIgnoreCase));
                    if (okButton == null) return false;
                    SetForegroundWindow(new IntPtr(window.Current.NativeWindowHandle));
                    object invokePattern;
                    if (okButton.TryGetCurrentPattern(InvokePattern.Pattern, out invokePattern))
                    {
                        ((InvokePattern)invokePattern).Invoke();
                        return true;
                    }
                    /* Controle clássico sem InvokePattern: clicar fisicamente no botão OK. */
                    var bounds = okButton.Current.BoundingRectangle;
                    SetCursorPos((int)(bounds.Left + bounds.Width / 2), (int)(bounds.Top + bounds.Height / 2));
                    mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                    mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private void WriteLog(string text)
        {
            string line = DateTime.Now.ToString("HH:mm:ss") + "  " + text;
            if (!string.IsNullOrWhiteSpace(logFile))
            {
                lock (logSync) File.AppendAllText(logFile, line + Environment.NewLine);
            }
            BeginInvoke((Action)(() => { logBox.AppendText(line + Environment.NewLine); }));
        }

        private void ShowFinal(string text, MessageBoxIcon icon)
        {
            BeginInvoke((Action)(() => MessageBox.Show(this, text, Text, MessageBoxButtons.OK, icon)));
        }

        private static bool IsAdministrator()
        {
            var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        private sealed class DownloadOption
        {
            public DownloadOption(string name, string url)
            {
                Name = name;
                Url = url;
            }

            public string Name { get; private set; }
            public string Url { get; private set; }
        }

        private enum PdvAutomationMode
        {
            FullReset,
            CleanReinstall,
            ConfigurationPrints
        }

        private sealed class PdvAutomationOption
        {
            public PdvAutomationOption(PdvAutomationMode mode, string displayName)
            {
                Mode = mode;
                DisplayName = displayName;
            }

            public PdvAutomationMode Mode { get; private set; }
            public string DisplayName { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class PdvVersionOption
        {
            public PdvVersionOption(string displayName, string downloadUrl, string sha256 = null)
            {
                DisplayName = displayName;
                DownloadUrl = downloadUrl;
                Sha256 = sha256;
                string extension;
                try { extension = Path.GetExtension(new Uri(downloadUrl).AbsolutePath); }
                catch { extension = ".rar"; }
                ArchiveExtension = string.Equals(extension, ".zip", StringComparison.OrdinalIgnoreCase)
                    ? ".zip"
                    : ".rar";
            }

            public string DisplayName { get; private set; }
            public string DownloadUrl { get; private set; }
            public string Sha256 { get; private set; }
            public string ArchiveExtension { get; private set; }

            public override string ToString()
            {
                return DisplayName;
            }
        }

        private sealed class UninstallEntry
        {
            public string DisplayName;
            public string DisplayVersion;
            public string UninstallString;
            public string QuietUninstallString;
        }

        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref int value, int valueSize);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
        [DllImport("user32.dll")] private static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);
        [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint numberOfInputs, INPUT[] inputs, int size);
        private static void SendKey(Keys key)
        {
            var inputs = new[]
            {
                INPUT.CreateVirtualKey((ushort)key, false),
                INPUT.CreateVirtualKey((ushort)key, true)
            };
            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) != inputs.Length)
                throw new InvalidOperationException("Não foi possível enviar F11 ao Softshop. Erro do Windows: " + Marshal.GetLastWin32Error());
        }

        private void ActivateWindow(IntPtr windowHandle)
        {
            ThrowIfCancellationRequested();
            ShowWindow(windowHandle, 9); // SW_RESTORE
            BringWindowToTop(windowHandle);
            SetForegroundWindow(windowHandle);
            for (int attempt = 0; attempt < 20; attempt++)
            {
                if (GetForegroundWindow() == windowHandle) return;
                BringWindowToTop(windowHandle);
                SetForegroundWindow(windowHandle);
                SleepWithCancellation(50);
            }
            throw new InvalidOperationException("Não foi possível ativar a janela do Softshop para enviar F11.");
        }
        private static void SendUnicodeText(string value)
        {
            var inputs = value.SelectMany(c => new[]
            {
                INPUT.CreateUnicode(c, false),
                INPUT.CreateUnicode(c, true)
            }).ToArray();
            if (SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT))) != inputs.Length)
                throw new InvalidOperationException("Não foi possível enviar a senha ao campo de login. Erro do Windows: " + Marshal.GetLastWin32Error());
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT
        {
            public uint Type;
            public InputUnion Data;

            public static INPUT CreateUnicode(char character, bool keyUp)
            {
                return new INPUT
                {
                    Type = 1,
                    Data = new InputUnion
                    {
                        Keyboard = new KEYBDINPUT
                        {
                            VirtualKey = 0,
                            ScanCode = character,
                            Flags = 0x0004u | (keyUp ? 0x0002u : 0u),
                            Time = 0,
                            ExtraInfo = UIntPtr.Zero
                        }
                    }
                };
            }

            public static INPUT CreateVirtualKey(ushort key, bool keyUp)
            {
                return new INPUT
                {
                    Type = 1,
                    Data = new InputUnion
                    {
                        Keyboard = new KEYBDINPUT
                        {
                            VirtualKey = key,
                            ScanCode = 0,
                            Flags = keyUp ? 0x0002u : 0u,
                            Time = 0,
                            ExtraInfo = UIntPtr.Zero
                        }
                    }
                };
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT Mouse;
            [FieldOffset(0)] public KEYBDINPUT Keyboard;
            [FieldOffset(0)] public HARDWAREINPUT Hardware;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT
        {
            public int X;
            public int Y;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint Flags;
            public uint Time;
            public UIntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HARDWAREINPUT
        {
            public uint Message;
            public ushort ParameterLow;
            public ushort ParameterHigh;
        }
    }
}
