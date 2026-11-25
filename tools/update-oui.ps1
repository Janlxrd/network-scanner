$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot '..\src\NetworkScanner.Core\Vendors\oui.txt'
$csv = Join-Path ([IO.Path]::GetTempPath()) 'oui.csv'
Invoke-WebRequest -Uri 'https://standards-oui.ieee.org/oui/oui.csv' -OutFile $csv -UserAgent 'oui-update'
$lines = Import-Csv $csv |
    Where-Object { $_.Assignment -match '^[0-9A-Fa-f]{6}$' } |
    ForEach-Object { "{0}`t{1}" -f $_.Assignment.ToUpper(), ($_.'Organization Name' -replace '\s+', ' ').Trim() } |
    Sort-Object -Unique
New-Item -ItemType Directory -Force (Split-Path $out) | Out-Null
[IO.File]::WriteAllLines((Resolve-Path (Split-Path $out)).Path + '\oui.txt', $lines, [Text.UTF8Encoding]::new($false))
Remove-Item $csv
"Wrote $($lines.Count) entries"
