using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

public static class RemoteHost
{
    [STAThread]
    public static int Main(string[] args)
    {
        string name = Path.GetFileNameWithoutExtension(Application.ExecutablePath);
        if (args.Length > 0 && args[0] == "--password")
        {
            if (args.Length != 2) return 2;
            if (name.Contains("Echo"))
            {
                Console.WriteLine(args[1]);
                Console.Error.WriteLine(args[1]);
                return 1;
            }
            if (name.Contains("Hang"))
            {
                File.WriteAllText(Path.ChangeExtension(Application.ExecutablePath, ".pid"), System.Diagnostics.Process.GetCurrentProcess().Id.ToString());
                Thread.Sleep(30000);
            }
            if (!name.Contains("NoAck")) Console.WriteLine("Done!");
            return 0;
        }
        if (name.Contains("Forwarded") && args.Length == 0) return 0;
        using (var window = new Form { Text = name.Contains("WrongTitle") || name.Contains("Cancel") ? "Outra janela" : "Softhecom Remoto", Width = 440, Height = 180 })
        using (var timeout = new System.Windows.Forms.Timer { Interval = 20000 })
        {
            timeout.Tick += (sender, e) => window.Close();
            window.Shown += (sender, e) => timeout.Start();
            Application.Run(window);
        }
        return 0;
    }
}
