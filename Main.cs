using System.Diagnostics;
using System.Text;
using System.Text.Json;

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

public sealed class LxcContainer
{
    public int Vmid { get; }
    public string Status { get; }
    public string Name { get; }

    public LxcContainer(int vmid, string status, string name)
    {
        Vmid = vmid;
        Status = status;
        Name = name;
    }
}

public static class Program
{
    private static readonly string AppTitle = "7LSM";
    private static readonly string ConfigFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "7LSM",
        "hosts.json"
    );

    private static List<SshHost> Hosts = [];
    private static readonly Dictionary<string, string?> HostPasswordCache = [];

    public static void Main()
    {
        Console.Title = AppTitle;

        // Ensure config directory exists
        var configDir = Path.GetDirectoryName(ConfigFilePath);
        if (configDir != null && !Directory.Exists(configDir))
        {
            Directory.CreateDirectory(configDir);
        }

        // Load hosts from file or create default configuration
        LoadHosts();

        while (true)
        {
            var mainOptions = Hosts
                .Select(h => h.DisplayName)
                .Concat(["Add Host", "Remove Host", "Settings", "Quit"])
                .ToList();

            int choice = ShowMenu(AppTitle, "SELECT:", mainOptions);

            if (choice == -1)
                continue;

            if (choice < Hosts.Count)
            {
                ShowHostMenu(Hosts[choice]);
            }
            else if (choice == Hosts.Count)
            {
                AddHost();
            }
            else if (choice == Hosts.Count + 1)
            {
                RemoveHost();
            }
            else if (choice == Hosts.Count + 2)
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

    private static void LoadHosts()
    {
        try
        {
            if (File.Exists(ConfigFilePath))
            {
                string json = File.ReadAllText(ConfigFilePath);
                Hosts = JsonSerializer.Deserialize<List<SshHost>>(json) ?? [];
            }
            else
            {
                // Create default hosts for first-time setup
                Hosts =
                [
                    new SshHost
                    {
                        Name = "My Cool Server",
                        Address = "1.3.3.7",
                        User = "meowington",
                        Port = 22,
                        IdentityFile = null
                    }
                ];
                SaveHosts();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error loading hosts: {ex.Message}");
            Console.ResetColor();
            Hosts = [];
        }
    }

    private static void SaveHosts()
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Hosts, options);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error saving hosts: {ex.Message}");
            Console.ResetColor();
        }
    }

    private static void AddHost()
    {
        Console.Clear();
        WriteHeader("Add New SSH Host");

        Console.WriteLine("Enter the following information for the new SSH host:");
        Console.WriteLine();

        Console.Write("Host Name (e.g., 'Production Server'): ");
        string? name = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(name))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Host name cannot be empty. Adding cancelled.");
            Console.ResetColor();
            Pause();
            return;
        }

        Console.Write("Address (IP or hostname): ");
        string? address = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(address))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Address cannot be empty. Adding cancelled.");
            Console.ResetColor();
            Pause();
            return;
        }

        Console.Write("Username: ");
        string? user = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(user))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Username cannot be empty. Adding cancelled.");
            Console.ResetColor();
            Pause();
            return;
        }

        Console.Write("Port (default 22): ");
        int port = 22;
        string? portInput = Console.ReadLine();
        if (!string.IsNullOrWhiteSpace(portInput) && !int.TryParse(portInput, out port))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Invalid port number. Using default port 22.");
            Console.ResetColor();
            port = 22;
        }

        Console.Write("SSH Key path (leave empty for default): ");
        string? identityFile = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(identityFile))
        {
            identityFile = null;
        }

        var newHost = new SshHost
        {
            Name = name.Trim(),
            Address = address.Trim(),
            User = user.Trim(),
            Port = port,
            IdentityFile = identityFile
        };

        Hosts.Add(newHost);
        SaveHosts();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"Host '{newHost.Name}' has been added successfully!");
        Console.ResetColor();
        Pause();
    }

    private static void RemoveHost()
    {
        Console.Clear();
        WriteHeader("Remove SSH Host");

        if (Hosts.Count == 0)
        {
            Console.WriteLine("No hosts available to remove.");
            Pause();
            return;
        }

        var options = Hosts
            .Select(h => h.DisplayName)
            .Concat(["Cancel"])
            .ToList();

        int choice = ShowMenu("Remove Host", "Select a host to remove:", options);

        if (choice == -1 || choice >= Hosts.Count)
            return;

        var hostToRemove = Hosts[choice];

        Console.Clear();
        WriteHeader("Confirm Removal");
        Console.WriteLine($"Are you sure you want to remove '{hostToRemove.Name}'?");
        Console.WriteLine($"  Address: {hostToRemove.Address}");
        Console.WriteLine($"  User: {hostToRemove.User}");
        Console.WriteLine();
        Console.Write("Type 'yes' to confirm: ");

        string? confirmation = Console.ReadLine();
        if (string.Equals(confirmation, "yes", StringComparison.OrdinalIgnoreCase))
        {
            Hosts.Remove(hostToRemove);
            SaveHosts();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Host '{hostToRemove.Name}' has been removed successfully!");
            Console.ResetColor();
        }
        else
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Removal cancelled.");
            Console.ResetColor();
        }

        Pause();
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
                "TEST CONNECTION",
                "PACKAGES",
                "PROXMOX » UPDATE LXC CONTAINERS",
                "← BACK"
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
                    ShowPackageMenu(host);
                    break;

                case 5:
                    ManageLxcContainers(host);
                    break;

                case 6:
                case -1:
                    return;
            }
        }
    }

    private static void ShowPackageMenu(SshHost host)
    {
        while (true)
        {
            var options = new List<string>
            {
                "Update Packages",
                "Install Package",
                "Remove Package",
                "← BACK"
            };

            int choice = ShowMenu($"{host.Name} - Packages", "Choose a package action:", options);

            switch (choice)
            {
                case 0:
                    UpdatePackages(host);
                    break;

                case 1:
                    InstallPackage(host);
                    break;

                case 2:
                    RemovePackage(host);
                    break;

                case 3:
                case -1:
                    return;
            }
        }
    }

    private static void UpdatePackages(SshHost host)
    {
        Console.Clear();
        WriteHeader($"Update Packages - {host.Name}");

        Console.WriteLine($"Updating packages on {host.Name}...");
        Console.WriteLine();

        GetOrCacheHostPassword(host);

        if (!TryExecuteRemoteSudoCommand(host, "apt-get update && apt-get upgrade -y", allocateTty: true, out var result))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Package update failed.");
            Console.WriteLine(result.Error);
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Package update completed.");
            Console.ResetColor();
        }

        Pause();
    }

    private static void InstallPackage(SshHost host)
    {
        Console.Clear();
        WriteHeader($"Install Package - {host.Name}");

        Console.Write("Enter package name to install (e.g., 'curl', 'git'): ");
        string? packageName = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(packageName))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No package name provided. Operation cancelled.");
            Console.ResetColor();
            Pause();
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Installing '{packageName}' on {host.Name}...");
        Console.WriteLine();

        GetOrCacheHostPassword(host);

        if (!TryExecuteRemoteSudoCommand(host, $"apt-get install -y {packageName.Trim()}", allocateTty: true, out var result))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Package '{packageName}' installation failed.");
            Console.WriteLine(result.Error);
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Package '{packageName}' installation completed.");
            Console.ResetColor();
        }

        Pause();
    }

    private static void RemovePackage(SshHost host)
    {
        Console.Clear();
        WriteHeader($"Remove Package - {host.Name}");

        Console.Write("Enter package name to remove (e.g., 'curl', 'git'): ");
        string? packageName = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(packageName))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("No package name provided. Operation cancelled.");
            Console.ResetColor();
            Pause();
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Are you sure you want to remove '{packageName}' from {host.Name}?");
        Console.Write("Type 'yes' to confirm: ");

        string? confirmation = Console.ReadLine();
        if (!string.Equals(confirmation, "yes", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("Operation cancelled.");
            Pause();
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Removing '{packageName}' from {host.Name}...");
        Console.WriteLine();

        GetOrCacheHostPassword(host);

        if (!TryExecuteRemoteSudoCommand(host, $"apt-get remove -y {packageName.Trim()}", allocateTty: true, out var result))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Package '{packageName}' removal failed.");
            Console.WriteLine(result.Error);
            Console.ResetColor();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Package '{packageName}' removal completed.");
            Console.ResetColor();
        }

        Pause();
    }

    private static void ManageLxcContainers(SshHost host)
    {
        Console.Clear();
        WriteHeader($"Manage LXC Containers - {host.Name}");

        Console.WriteLine("Retrieving LXC container list from Proxmox...");
        Console.WriteLine();

        GetOrCacheHostPassword(host);

        if (!TryExecuteRemoteSudoCommand(host, "pct list", allocateTty: false, out var listResult))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed to retrieve LXC containers.");
            Console.WriteLine(listResult.Error);
            Console.WriteLine();
            Console.WriteLine("Hint: Proxmox commands like pct usually require sudo without an interactive password prompt.");
            Console.WriteLine("  - Configure passwordless sudo for the SSH user, or");
            Console.WriteLine("  - Add the pct commands to /etc/sudoers with NOPASSWD for this user.");
            Console.WriteLine("Example sudoers line:");
            Console.WriteLine($"  {host.User} ALL=(ALL) NOPASSWD: /usr/sbin/pct list, /usr/sbin/pct start, /usr/sbin/pct exec");
            Console.ResetColor();
            Pause();
            return;
        }

        var containers = ParsePctListOutput(listResult.Output).ToList();

        if (containers.Count == 0)
        {
            Console.WriteLine("No LXC containers were found on this host.");
            Pause();
            return;
        }

        Console.WriteLine("       Containers    │");
        Console.WriteLine(" ┌─────┬─────────┬───┘");
        foreach (var container in containers)
        {
            Console.WriteLine($" ├ {container.Vmid} │ {container.Status} │ {container.Name}");
        }
        Console.WriteLine(" └─────┴─────────┴────");

        var stoppedContainers = containers
            .Where(c => !string.Equals(c.Status, "running", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (stoppedContainers.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Starting stopped containers...");

            foreach (var container in stoppedContainers)
            {
                Console.WriteLine($"Starting {container.Vmid} ({container.Name})...");
                var startResult = ExecuteRemoteSudoCommand(host, $"pct start {container.Vmid}", allocateTty: false);
                if (startResult.ExitCode != 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Failed to start {container.Vmid}: {startResult.Error}");
                    Console.ResetColor();
                }
            }
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("All containers are already running.");
        }

        Console.WriteLine();
        Console.WriteLine("Updating containers in place...");

        foreach (var container in containers)
        {
            // Update the container, removing old packages and upgrading the rest.
            Console.WriteLine($"Updating {container.Vmid} ({container.Name})...");
            var updateResult = ExecuteRemoteSudoCommand(host, $"pct exec {container.Vmid} -- bash -c \"apt-get update 2>/dev/null | grep 'packages.*upgraded'; apt list --upgradable 2>/dev/null | cat && apt-get autoremove -y;", allocateTty: false);
            Console.WriteLine(updateResult.ToString());
            if (updateResult.ExitCode != 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Update failed for {container.Vmid}. See output below:");
                Console.ResetColor();
                Console.WriteLine(updateResult.Output);
                Console.WriteLine(updateResult.Error);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Update completed for {container.Vmid}.");
                Console.ResetColor();
            }
        }

        Pause();
    }

    private static bool TryExecuteRemoteSudoCommand(SshHost host, string command, bool allocateTty, out (int ExitCode, string Output, string Error) result)
    {
        result = ExecuteRemoteSudoCommand(host, command, allocateTty);

        if (result.ExitCode == 0)
            return true;

        // Prompt for password if not cached and the error indicates a password is required
        bool hasCachedPassword = HostPasswordCache.TryGetValue(host.Address, out var cachedPwd) && !string.IsNullOrEmpty(cachedPwd);
        if (!hasCachedPassword && ContainsPasswordRequired(result))
        {
            Console.Write("Enter sudo password: ");
            string password = ReadPassword();
            HostPasswordCache[host.Address] = password;
            result = ExecuteRemoteSudoCommand(host, command, allocateTty);
            return result.ExitCode == 0;
        }

        return false;
    }

    private static (int ExitCode, string Output, string Error) ExecuteRemoteSudoCommand(SshHost host, string command, bool allocateTty)
    {
        string? sudoPassword = null;
        if (HostPasswordCache.TryGetValue(host.Address, out var pwd) && !string.IsNullOrEmpty(pwd))
        {
            sudoPassword = pwd;
        }

        string sudoCommand = sudoPassword is null ? $"sudo -n {command}" : $"sudo -S -p '' {command}";
        string args = BuildRemoteCommandArgs(host, sudoCommand, allocateTty);
        return RunProcessCaptureOutput("ssh", args, sudoPassword);
    }

    private static void GetOrCacheHostPassword(SshHost host)
    {
        if (!HostPasswordCache.ContainsKey(host.Address))
        {
            HostPasswordCache[host.Address] = null;
        }
    }

    private static bool ContainsPasswordRequired((int ExitCode, string Output, string Error) result)
    {
        return result.Error.Contains("sudo: a password is required", StringComparison.OrdinalIgnoreCase)
            || result.Output.Contains("sudo: a password is required", StringComparison.OrdinalIgnoreCase)
            || result.Error.Contains("sudo: a password is required", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadPassword()
    {
        var builder = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.Enter)
                break;

            if (key.Key == ConsoleKey.Backspace && builder.Length > 0)
            {
                builder.Length--;
                continue;
            }

            if (!char.IsControl(key.KeyChar))
                builder.Append(key.KeyChar);
        }

        Console.WriteLine();
        return builder.ToString();
    }

    private static IReadOnlyList<LxcContainer> ParsePctListOutput(string output)
    {
        var containers = new List<LxcContainer>();
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("VMID", StringComparison.OrdinalIgnoreCase) || line.Trim().Length == 0)
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
                continue;

            if (!int.TryParse(parts[0], out int vmid))
                continue;

            string status = parts[1];
            string name = parts.Length > 3 ? string.Join(' ', parts.Skip(3)) : string.Empty;

            containers.Add(new LxcContainer(vmid, status, name));
        }

        return containers;
    }

    private static (int ExitCode, string Output, string Error) RunProcessCaptureOutput(string fileName, string arguments, string? stdin = null)
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
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = stdin != null
                }
            };

            process.Start();

            if (stdin != null)
            {
                process.StandardInput.WriteLine(stdin);
                process.StandardInput.Close();
            }

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return (process.ExitCode, output.TrimEnd(), error.TrimEnd());
        }
        catch (Exception ex)
        {
            return (1, string.Empty, ex.Message);
        }
    }

    private static string BuildRemoteCommandArgs(SshHost host, string command, bool allocateTty = true)
    {
        var args = new List<string>();

        if (allocateTty)
        {
            args.Add("-t");
        }

        args.Add("-p");
        args.Add(host.Port.ToString());

        if (!string.IsNullOrWhiteSpace(host.IdentityFile))
        {
            args.Add("-i");
            args.Add($"\"{host.IdentityFile}\"");
        }

        args.Add($"{host.User}@{host.Address}");
        args.Add(command);

        return string.Join(" ", args);
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
        Console.WriteLine("This is a simple SSH client for connecting to remote servers.");
        Console.ResetColor();
        Console.WriteLine("You can use this tool to connect to your SSH servers.");
        Console.WriteLine("It allows you to test SSH connections and view server information.");
        Console.WriteLine("Just select a server and hit Enter to connect.");
        Console.WriteLine("");
        Console.WriteLine("ProxMox integration coming soon!");
        Console.WriteLine("");
        Console.WriteLine("~ nano11b");

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