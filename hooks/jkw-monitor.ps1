# JKW Monitor hook — forwards Claude Code events to the local monitor app.
# Called by Claude Code hooks via: powershell -NoProfile -File "D:/Projekty/HonestJKW/hooks/jkw-monitor.ps1"
# Never throws — app failures are silent so Claude Code is never blocked.

$endpoint = "http://127.0.0.1:7849/"

try {
    $body = [Console]::In.ReadToEnd()
    if ($body) {
        $null = Invoke-WebRequest `
            -Uri         $endpoint `
            -Method      Post `
            -ContentType "application/json" `
            -Body        $body `
            -TimeoutSec  1 `
            -UseBasicParsing `
            -ErrorAction SilentlyContinue
    }
} catch {
    # Swallow all errors — app may not be running
}

exit 0
