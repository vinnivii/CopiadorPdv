using System;
using System.IO;
using System.Windows.Forms;

public static class DialogHost
{
    [STAThread]
    public static void Main(string[] args)
    {
        string scenario = args.Length > 0 ? args[0] : "normal";
        string marker = args.Length > 1 ? args[1] : Path.ChangeExtension(Application.ExecutablePath, ".clicked");
        if (scenario.StartsWith("modal-", StringComparison.Ordinal))
        {
            using (var login = new Form { Text = "Login de teste", Width = 440, Height = 180, KeyPreview = true })
            using (var timeout = new Timer { Interval = 30000 })
            using (var showDialog = new Timer { Interval = 100 })
            {
                login.Controls.Add(new TextBox { Name = "txtSenha", UseSystemPasswordChar = true, Left = 20, Top = 20, Width = 200 });
                login.KeyDown += (sender, e) =>
                {
                    if (e.KeyCode == Keys.F11) File.AppendAllText(marker, "F11\n");
                };
                showDialog.Tick += (sender, e) =>
                {
                    if (!File.Exists(marker + ".show")) return;
                    showDialog.Stop();
                    using (var dialog = new Form { Text = scenario == "modal-avalonia" ? "Erro Avalonia" :
                        scenario == "modal-untitled" ? "" : scenario == "modal-rpc" ? "Aviso" : scenario == "modal-unknown" || scenario == "modal-cancel" ?
                        "Aviso de teste" : "Configurações", Width = 480, Height = 240 })
                    {
                        if (scenario == "modal-config" || scenario == "modal-untitled" || scenario == "modal-avalonia")
                        {
                            var tabs = new TabControl { Dock = DockStyle.Fill };
                            tabs.TabPages.Add("Configurações Iniciais");
                            dialog.Controls.Add(tabs);
                        }
                        if (scenario == "modal-rpc") dialog.Controls.Add(new Label { Text = "O servidor RPC não está disponível.", AutoSize = true });
                        var ok = new Button { Text = "OK", Dock = DockStyle.Bottom };
                        ok.Click += (s, a) => { File.WriteAllText(marker + ".ok", "clicked"); dialog.Close(); };
                        dialog.Controls.Add(ok);
                        using (var loading = new Timer { Interval = 7000 })
                        {
                            if (scenario == "modal-slow")
                            {
                                dialog.Text = "Carregando";
                                loading.Tick += (s, a) =>
                                {
                                    loading.Stop();
                                    dialog.Text = "Configurações";
                                    var tabs = new TabControl { Dock = DockStyle.Fill };
                                    tabs.TabPages.Add("Configurações Iniciais");
                                    dialog.Controls.Add(tabs);
                                };
                                loading.Start();
                            }
                            dialog.ShowDialog(login);
                        }
                    }
                };
                timeout.Tick += (sender, e) => Application.Exit();
                login.Shown += (sender, e) => { timeout.Start(); showDialog.Start(); };
                Application.Run(login);
            }
            return;
        }
        using (var window = new Form { Text = scenario == "title" ? "Erro Avalonia" : scenario == "login" ? "Login de teste" : "Aviso de teste", Width = 440, Height = 160 })
        using (var timeout = new Timer { Interval = 15000 })
        {
            var text = new Label { Left = 12, Top = 12, Width = 400, Height = 40,
                Text = scenario == "mixed" ? "Falha em aVaLoNiA.Rendering" : "Nao foi possivel iniciar Avalonia." };
            if (scenario == "unrelated" || scenario == "title" || scenario == "login") text.Text = "Outro aviso de inicializacao.";
            if (scenario == "hidden") text.Visible = false;
            var button = new Button { Left = 150, Top = 65, Text = scenario == "no-ok" ? "Fechar" : "OK",
                Enabled = scenario != "disabled" };
            button.Click += (sender, e) => { File.WriteAllText(marker, "clicked"); window.Close(); };
            window.Controls.Add(text);
            window.Controls.Add(button);
            if (scenario == "edit") window.Controls.Add(new TextBox { Left = 12, Top = 95, Width = 220, ReadOnly = true, Text = "Detalhes da excecao" });
            timeout.Tick += (sender, e) => window.Close();
            window.Shown += (sender, e) => timeout.Start();
            Application.Run(window);
        }
    }
}
