using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Este programa só pode ser executado no Windows.");
    return 1;
}

try
{
    var windows = ReadWindowsInfo();
    var activation = ReadActivationStatus();
    var report = $"""
        Relatório de informações do Windows
        Data: {DateTime.Now:yyyy-MM-dd HH:mm:ss}
        Produto: {windows.ProductName}
        Edição: {windows.Edition}
        Versão: {windows.DisplayVersion}
        Compilação: {windows.Build}
        Ativação: {activation}

        Este relatório não contém a chave do produto.
        """;

    Console.WriteLine(report);

    var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    if (string.IsNullOrWhiteSpace(desktop))
    {
        throw new IOException("Não foi possível localizar a pasta da Área de Trabalho.");
    }

    var reportPath = Path.Combine(desktop, $"windows-backup-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
    File.WriteAllText(reportPath, report);
    Console.WriteLine($"\nRelatório salvo em: {reportPath}");
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
                           InvalidOperationException or Win32Exception or TimeoutException)
{
    Console.Error.WriteLine($"Erro ao gerar o relatório: {ex.Message}");
    return 1;
}

Console.WriteLine("\nPressione Enter para fechar.");
Console.ReadLine();
return 0;

static WindowsInfo ReadWindowsInfo()
{
    using var key = Registry.LocalMachine.OpenSubKey(
        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion")
        ?? throw new IOException("Não foi possível ler as informações do Windows no Registro.");

    var productName = key.GetValue("ProductName") as string
        ?? throw new IOException("O nome do produto do Windows não foi encontrado.");
    var edition = key.GetValue("EditionID") as string ?? "Não informado";
    var displayVersion = key.GetValue("DisplayVersion") as string
        ?? key.GetValue("ReleaseId") as string
        ?? "Não informado";
    var build = key.GetValue("CurrentBuildNumber") as string ?? "Não informado";

    if (int.TryParse(build, out var buildNumber) && buildNumber >= 22000 &&
        productName.Contains("Windows 10", StringComparison.OrdinalIgnoreCase))
    {
        productName = productName.Replace("Windows 10", "Windows 11", StringComparison.OrdinalIgnoreCase);
    }

    return new WindowsInfo(productName, edition, displayVersion, build);
}

static string ReadActivationStatus()
{
    const string script = "$ErrorActionPreference='Stop'; " +
        "$p=Get-CimInstance -ClassName SoftwareLicensingProduct | " +
        "Where-Object { $_.ApplicationID -eq '{55c92734-d682-4d71-983e-d6ec3f16059f}' " +
        "-and $_.PartialProductKey } | Select-Object -First 1; " +
        "if ($null -eq $p) { exit 2 }; [Console]::WriteLine([int]$p.LicenseStatus)";

    using var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        }
    };
    process.StartInfo.ArgumentList.Add("-NoProfile");
    process.StartInfo.ArgumentList.Add("-NonInteractive");
    process.StartInfo.ArgumentList.Add("-Command");
    process.StartInfo.ArgumentList.Add(script);

    if (!process.Start())
    {
        throw new InvalidOperationException("Não foi possível iniciar o PowerShell para consultar a ativação.");
    }

    var outputTask = process.StandardOutput.ReadToEndAsync();
    var errorTask = process.StandardError.ReadToEndAsync();
    if (!process.WaitForExit(20000))
    {
        process.Kill(entireProcessTree: true);
        throw new TimeoutException("A consulta do estado de ativação excedeu 20 segundos.");
    }

    Task.WaitAll(outputTask, errorTask);
    if (process.ExitCode != 0)
    {
        var details = errorTask.Result.Trim();
        throw new InvalidOperationException(
            string.IsNullOrEmpty(details)
                ? "O Windows não retornou o estado de ativação."
                : $"Não foi possível consultar a ativação: {details}");
    }

    return outputTask.Result.Trim() switch
    {
        "1" => "Ativado",
        "0" => "Não ativado",
        "2" => "Em período de carência (OOB)",
        "3" => "Em período de carência (OOT)",
        "4" => "Período de carência de cópia não genuína",
        "5" => "Notificação de ativação",
        "6" => "Período de carência estendido",
        var status => throw new InvalidOperationException(
            $"O Windows retornou um estado de ativação desconhecido: {status}")
    };
}

sealed record WindowsInfo(string ProductName, string Edition, string DisplayVersion, string Build);
