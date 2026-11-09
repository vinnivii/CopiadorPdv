using System.Diagnostics;
using System.IO;
using System.Threading;

public static class InstallerHost
{
    public static int Main(string[] args)
    {
        if (args[0] == "wait")
        {
            File.WriteAllText(args[1], Process.GetCurrentProcess().Id.ToString());
            Thread.Sleep(30000);
            return 0;
        }
        return int.Parse(args[0]);
    }
}
