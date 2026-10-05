// Safety net for the Task Scheduler: custom tasks are exported before Sysprep and put back at the first start
// when something is missing. Tasks keep working through Sysprep by themselves, except those that store a
// password and those bound to a local user by SID (the account is created again with another SID).
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

static class TaskKeeper
{
    static string DataDir { get { return Path.Combine(Environment.GetEnvironmentVariable("ProgramData") ?? "C:\\ProgramData", "ProfileKeeper"); } }

    // Tasks outside \Microsoft\ -> XML files plus index.txt (file|path|name|logon type).
    // Local user/group SIDs inside the XML are replaced with account names so the task fits a re-created account.
    const string ExportScript =
@"$ErrorActionPreference = 'SilentlyContinue'
$dir = Join-Path $env:ProgramData 'ProfileKeeper\Tasks'
if (Test-Path -LiteralPath $dir) { Remove-Item -LiteralPath $dir -Recurse -Force }
New-Item -ItemType Directory -Path $dir -Force | Out-Null
$n = 0; $idx = @()
foreach ($t in @(Get-ScheduledTask | Where-Object { $_.TaskPath -notlike '\Microsoft\*' })) {
  $xml = Export-ScheduledTask -TaskName $t.TaskName -TaskPath $t.TaskPath
  if (-not $xml) { continue }
  $xml = [regex]::Replace($xml, '<(UserId|GroupId)>(S-1-5-21-[\d-]+)</\1>', {
    param($m)
    try {
      $a = (New-Object Security.Principal.SecurityIdentifier $m.Groups[2].Value).Translate([Security.Principal.NTAccount]).Value
      '<' + $m.Groups[1].Value + '>' + ($a -replace '^.*\\', '') + '</' + $m.Groups[1].Value + '>'
    } catch { $m.Value }
  })
  $file = ('{0:D4}.xml' -f $n)
  [IO.File]::WriteAllText((Join-Path $dir $file), $xml, [Text.Encoding]::Unicode)
  $idx += ($file + '|' + $t.TaskPath + '|' + $t.TaskName + '|' + $t.Principal.LogonType)
  $n++
}
[IO.File]::WriteAllLines((Join-Path $dir 'index.txt'), [string[]]$idx, [Text.Encoding]::UTF8)
$n
";

    // Runs from SetupComplete.cmd (as SYSTEM, after the accounts exist). Existing tasks are left alone.
    const string RestoreScript =
@"$dir = Join-Path $env:ProgramData 'ProfileKeeper\Tasks'
$idx = Join-Path $dir 'index.txt'
$log = Join-Path $env:ProgramData 'ProfileKeeper\tasks_restore.log'
if (-not (Test-Path -LiteralPath $idx)) { exit }
foreach ($line in Get-Content -LiteralPath $idx -Encoding UTF8) {
  $p = $line -split '\|'
  if ($p.Count -lt 4) { continue }
  $full = $p[1] + $p[2]
  if (Get-ScheduledTask -TaskName $p[2] -TaskPath $p[1] -ErrorAction SilentlyContinue) { Add-Content -LiteralPath $log -Value ('KEEP  ' + $full); continue }
  try {
    Register-ScheduledTask -Xml (Get-Content -LiteralPath (Join-Path $dir $p[0]) -Raw) -TaskName $p[2] -TaskPath $p[1] -Force -ErrorAction Stop | Out-Null
    Add-Content -LiteralPath $log -Value ('OK    ' + $full)
  } catch {
    Add-Content -LiteralPath $log -Value ('FAIL  ' + $full + '  (' + $p[3] + '): ' + $_.Exception.Message)
  }
}
";

    // Returns the number of tasks exported.
    public static int Backup(IProgress2 prog)
    {
        Directory.CreateDirectory(DataDir);
        string o;
        Ps.Run(ExportScript, out o);
        int n = 0;
        foreach (string line in o.Split('\n'))
        {
            int v;
            if (int.TryParse(line.Trim(), out v)) n = v;
        }
        File.WriteAllText(Path.Combine(DataDir, "restore_tasks.ps1"), RestoreScript, new UTF8Encoding(true));
        AddToSetupComplete();
        return n;
    }

    static void AddToSetupComplete()
    {
        string dir = Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? "C:\\Windows", "Setup\\Scripts");
        Directory.CreateDirectory(dir);
        string f = Path.Combine(dir, "SetupComplete.cmd");
        string marker = "REM ::INSTALLER:: restore scheduled tasks";
        string existing = File.Exists(f) ? File.ReadAllText(f) : "";
        if (existing.Contains(marker)) return;
        string add = "\r\n" + marker + "\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File \"%ProgramData%\\ProfileKeeper\\restore_tasks.ps1\"\r\n";
        File.AppendAllText(f, add, Encoding.ASCII);
    }

    // For the pre-checks: custom task names, and which of them store a password (those need to be re-entered).
    public static void Scan(out List<string> all, out List<string> withPassword)
    {
        all = new List<string>(); withPassword = new List<string>();
        string o;
        Ps.Run("Get-ScheduledTask | Where-Object { $_.TaskPath -notlike '\\Microsoft\\*' } | ForEach-Object { $_.TaskPath + $_.TaskName + '|' + $_.Principal.LogonType }", out o);
        foreach (string line in o.Split('\n'))
        {
            string l = line.Trim();
            int i = l.LastIndexOf('|');
            if (i <= 0 || !l.StartsWith("\\")) continue;
            all.Add(l.Substring(0, i));
            if (l.Substring(i + 1).Equals("Password", StringComparison.OrdinalIgnoreCase)) withPassword.Add(l.Substring(0, i));
        }
    }
}
