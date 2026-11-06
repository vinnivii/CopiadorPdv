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
        using (var window = new Form { Text = scenario == "title" ? "Erro Avalonia" : "Aviso de teste", Width = 440, Height = 160 })
        using (var timeout = new Timer { Interval = 15000 })
        {
            var text = new Label { Left = 12, Top = 12, Width = 400, Height = 40,
                Text = scenario == "mixed" ? "Falha em aVaLoNiA.Rendering" : "Nao foi possivel iniciar Avalonia." };
            if (scenario == "unrelated" || scenario == "title") text.Text = "Outro aviso de inicializacao.";
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
