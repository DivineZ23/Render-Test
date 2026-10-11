[CmdletBinding()]
param(
    [uri]$BaseUrl = 'https://imperial.divin3.space',
    [ValidateRange(1, 20)][int]$Samples = 5
)

$ErrorActionPreference = 'Stop'
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AutomaticDecompression = [System.Net.DecompressionMethods]::GZip -bor [System.Net.DecompressionMethods]::Deflate
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(90)
$paths = @('/check', '/api/v1/properties/featured', '/api/v1/properties/available?page=1&pageSize=12')
$results = @()
try {
    foreach ($path in $paths) {
        $requestUri = [uri]::new($BaseUrl, $path)
        for ($sample = 1; $sample -le $Samples; $sample++) {
            $timer = [System.Diagnostics.Stopwatch]::StartNew()
            $response = $client.GetAsync($requestUri, [System.Net.Http.HttpCompletionOption]::ResponseHeadersRead).GetAwaiter().GetResult()
            try {
                $firstByte = $timer.Elapsed.TotalMilliseconds
                $body = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                $timer.Stop()
                $serverTiming = if ($response.Headers.Contains('Server-Timing')) { $response.Headers.GetValues('Server-Timing') -join ', ' } else { '' }
                $results += [pscustomobject]@{
                    Path = $path; Sample = $sample; Status = [int]$response.StatusCode
                    HeadersMs = [math]::Round($firstByte, 1); TotalMs = [math]::Round($timer.Elapsed.TotalMilliseconds, 1)
                    DecodedBytes = $body.Length; ServerTiming = $serverTiming
                }
            } finally { $response.Dispose() }
        }
    }
} finally { $client.Dispose(); $handler.Dispose() }

$results
