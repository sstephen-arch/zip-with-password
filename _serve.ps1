$port = 7842
$root = "C:\Users\shem_\Claude\Projects\Zip+Password"

$listener = New-Object System.Net.HttpListener
$listener.Prefixes.Add("http://localhost:$port/")
$listener.Start()
Write-Host "Server running at http://localhost:$port — keep this window open"

while ($listener.IsListening) {
    $ctx = $listener.GetContext()
    $reqPath = $ctx.Request.Url.LocalPath.TrimStart('/')
    $filePath = Join-Path $root $reqPath

    $ctx.Response.Headers.Add("Access-Control-Allow-Origin", "*")
    $ctx.Response.Headers.Add("Cache-Control", "no-cache")

    if (Test-Path $filePath -PathType Leaf) {
        $bytes = [System.IO.File]::ReadAllBytes($filePath)
        $ctx.Response.ContentLength64 = $bytes.Length
        $ctx.Response.ContentType = "text/plain; charset=utf-8"
        $ctx.Response.OutputStream.Write($bytes, 0, $bytes.Length)
    } else {
        $ctx.Response.StatusCode = 404
    }
    $ctx.Response.OutputStream.Close()
}
