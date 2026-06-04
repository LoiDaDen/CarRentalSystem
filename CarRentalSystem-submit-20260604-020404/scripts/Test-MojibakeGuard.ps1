param(
    [string]$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
)

$ErrorActionPreference = "Stop"

$extensions = @(".cs", ".cshtml", ".js", ".css", ".resx")
$excludedSegments = @(
    [IO.Path]::DirectorySeparatorChar + "bin" + [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::DirectorySeparatorChar + "obj" + [IO.Path]::DirectorySeparatorChar,
    [IO.Path]::DirectorySeparatorChar + "artifacts" + [IO.Path]::DirectorySeparatorChar
)

# Common mojibake fragments produced when UTF-8 Vietnamese text is decoded as Windows-1252.
$mojibakePattern = '(?:\u00E1[\u00BA-\u00BF]|\u00C3[\u00A0-\u00BF]|\u00C4[\u0080-\u00BF]|\u00C6[\u0080-\u00BF]|\u00C2[\u0080-\u00BF])'

$hits = New-Object System.Collections.Generic.List[string]

Get-ChildItem -Path $Root -Recurse -File |
    Where-Object {
        $filePath = $_.FullName
        $extensions -contains $_.Extension.ToLowerInvariant() -and
        -not ($excludedSegments | Where-Object { $filePath.Contains($_) })
    } |
    ForEach-Object {
        $file = $_
        $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
        $matches = [regex]::Matches($content, $mojibakePattern)
        if ($matches.Count -eq 0) {
            return
        }

        $lines = $content -split "`r?`n"
        for ($i = 0; $i -lt $lines.Length; $i++) {
            if ($lines[$i] -match $mojibakePattern) {
                $rootWithSeparator = $Root.TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
                if ($file.FullName.StartsWith($rootWithSeparator, [StringComparison]::OrdinalIgnoreCase)) {
                    $relative = $file.FullName.Substring($rootWithSeparator.Length)
                }
                else {
                    $relative = $file.FullName
                }
                $message = "{0}:{1}: possible mojibake: {2}" -f $relative, ($i + 1), $lines[$i].Trim()
                $hits.Add($message)
            }
        }
    }

if ($hits.Count -gt 0) {
    $hits | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    exit 1
}

Write-Host "Mojibake guard passed."
