using System.Diagnostics;

namespace SevenLabsSshMenu;

public class SshHost
{
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string User { get; set; } = "";
    public int Port { get; set; } = 22;
    public string? IdentityFile { get; set; }

    public string DisplayName => $"{Name} - {User}@{Address}";
}

public static class Program
{
    private static readonly string AppTitle = "7LSM";

    private static readonly List<SshHost> Hosts =
    [
        new SshHost
        {
            Name = "AWS EAST | OHIO",
            Address = "3.129.217.82",
            User = "nano",
            Port = 22,
            IdentityFile = null
        },
        new SshHost
        {
            Name = "PROXMOX",
            Address = "10.0.10.4",
            User = "nano",
            Port = 22,
            IdentityFile = null
        },
        new SshHost
        {
            Name = "NANO-DEV",
            Address = "10.0.10.55",
            User = "nano",
            Port = 22,
            IdentityFile = null
        }
    ];

    public static void Main()
    {
        Console.Title = AppTitle;

        while (true)
        {
            var mainOptions = Hosts
                .Select(h => h.DisplayName)
                .Concat(["Settings", "Quit"])
                .ToList();

            int choice = ShowMenu(AppTitle, "Select a server:", mainOptions);

            if (choice == -1)
                continue;

            if (choice < Hosts.Count)
            {
                ShowHostMenu(Hosts[choice]);
            }
            else if (choice == Hosts.Count)
            {
                ShowHelp();
            }
            else
            {
                ClearAndExit();
                return;
            }
        }
    }

    private static void ShowHostMenu(SshHost host)
    {
        while (true)
        {
            var options = new List<string>
            {
                "CONNECT",
                "PING",
                "CHECK",
                "PROBE",
                "Back"
            };

            int choice = ShowMenu(host.Name, $"Choose an action for {host.Name}:", options);

            switch (choice)
            {
                case 0:
                    ConnectSsh(host);
                    break;

                case 1:
                    PingHost(host);
                    break;

                case 2:
                    TestSshPort(host);
                    break;

                case 3:
                    ShowHostInfo(host);
                    break;

                case 4:
                case -1:
                    return;
            }
        }
    }

    private static int ShowMenu(string title, string subtitle, List<string> options)
    {
        int selectedIndex = 0;
        ConsoleKey key;

        do
        {
            Console.Clear();

            WriteHeader(title);
            Console.WriteLine(subtitle);
            Console.WriteLine();

            for (int i = 0; i < options.Count; i++)
            {
                if (i == selectedIndex)
                {
                    Console.ForegroundColor = ConsoleColor.Black;
                    Console.BackgroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($" > {options[i]} ");
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine($"   {options[i]}");
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Use ↑/↓ to move, Enter to select, Esc to go back.");
            Console.ResetColor();

            key = Console.ReadKey(true).Key;

            switch (key)
            {
                case ConsoleKey.UpArrow:
                    selectedIndex--;
                    if (selectedIndex < 0)
                        selectedIndex = options.Count - 1;
                    break;

                case ConsoleKey.DownArrow:
                    selectedIndex++;
                    if (selectedIndex >= options.Count)
                        selectedIndex = 0;
                    break;

                case ConsoleKey.Escape:
                    return -1;
            }

        } while (key != ConsoleKey.Enter);

        return selectedIndex;
    }

    private static void ConnectSsh(SshHost host)
    {
        Console.Clear();

        WriteHeader($"Connect to {host.Name}");

        Console.WriteLine($"Host: {host.Address}");
        Console.WriteLine($"User: {host.User}");
        Console.WriteLine($"Port: {host.Port}");
        Console.WriteLine();

        Console.Write("Continue? [y/N]: ");
        string? answer = Console.ReadLine();

        if (!string.Equals(answer, "y", StringComparison.OrdinalIgnoreCase))
            return;

        Console.Clear();

        Console.WriteLine($"Connecting to {host.Name}...");
        Console.WriteLine($"Target: {host.User}@{host.Address}:{host.Port}");
        Console.WriteLine();

        var args = BuildSshArguments(host);

        RunProcess("ssh", args);

        Console.WriteLine();
        Console.WriteLine($"Disconnected from {host.Name}.");
        Pause();
    }

    private static string BuildSshArguments(SshHost host)
    {
        var args = new List<string>
        {
            "-p",
            host.Port.ToString()
        };

        if (!string.IsNullOrWhiteSpace(host.IdentityFile))
        {
            args.Add("-i");
            args.Add($"\"{host.IdentityFile}\"");
        }

        args.Add($"{host.User}@{host.Address}");

        return string.Join(" ", args);
    }

    private static void PingHost(SshHost host)
    {
        Console.Clear();

        WriteHeader($"Ping {host.Name}");

        Console.WriteLine($"Testing network reachability for {host.Address}...");
        Console.WriteLine();

        RunProcess("ping", $"-c 3 {host.Address}");

        Console.WriteLine();
        Console.WriteLine("Note: If ping fails, SSH may still work if ICMP is blocked.");
        Pause();
    }

    private static void TestSshPort(SshHost host)
    {
        Console.Clear();

        WriteHeader($"Test SSH Port - {host.Name}");

        Console.WriteLine($"Testing {host.Address}:{host.Port}...");
        Console.WriteLine();

        // Uses bash TCP test, so this is intended for Linux/macOS.
        string command = $"timeout 5 bash -c \"cat < /dev/null > /dev/tcp/{host.Address}/{host.Port}\"";

        int exitCode = RunProcess("bash", $"-c \"{command}\"", showOutput: false);

        if (exitCode == 0)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"SSH port {host.Port} appears open on {host.Name}.");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Could not connect to port {host.Port} on {host.Name}.");
        }

        Console.ResetColor();
        Pause();
    }

    private static void ShowHostInfo(SshHost host)
    {
        Console.Clear();

        WriteHeader($"{host.Name} Info");

        Console.WriteLine($"Name:       {host.Name}");
        Console.WriteLine($"Address:    {host.Address}");
        Console.WriteLine($"User:       {host.User}");
        Console.WriteLine($"Port:       {host.Port}");
        Console.WriteLine($"SSH Key:    {(string.IsNullOrWhiteSpace(host.IdentityFile) ? "Default SSH key" : host.IdentityFile)}");

        Pause();
    }

    private static void ShowHelp()
    {
        Console.Clear();

        WriteHeader("Help");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("(__)          (__)          (__)          (__)      (__)");
        Console.WriteLine("(@@)_____     (OO)_____     (UU)_____     (XX)      (/\\)");
        Console.WriteLine("(oo)    /|\\   (oo)    /|\\   (oo)    /|\\   (oo)\\     (oo)____");
        Console.WriteLine("  | |--/ | *    | |--/ | *    | |--/ | *   /   \\    /  \\    )\\");
        Console.WriteLine("  w w w  w      w w w  w      w w w  w    w  £__)   \\  /  e_\\ *");
        Console.WriteLine("                                                     ww");

        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine("");
        Console.ResetColor();
        Console.WriteLine("Good future actions to add:");
        Console.WriteLine(" - Open SFTP");
        Console.WriteLine(" - Run uptime");
        Console.WriteLine(" - Tail logs");
        Console.WriteLine(" - Reboot server");
        Console.WriteLine(" - Open Proxmox web UI");

        Pause();
    }

    private static int RunProcess(string fileName, string arguments, bool showOutput = true)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = !showOutput,
                    RedirectStandardError = !showOutput
                }
            };

            process.Start();
            process.WaitForExit();

            return process.ExitCode;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed to run command: {fileName} {arguments}");
            Console.WriteLine(ex.Message);
            Console.ResetColor();

            return 1;
        }
    }

    private static void WriteHeader(string title)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("============================================================");
        Console.WriteLine($" {title}");
        Console.WriteLine("============================================================");
        Console.ResetColor();
        Console.WriteLine();
    }

    private static void Pause()
    {
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.Write("Press Enter to return to the menu...");
        Console.ResetColor();
        Console.ReadLine();
    }

    private static void ClearAndExit()
    {
        Console.Clear();
        Console.WriteLine("Goodbye.");
    }
}