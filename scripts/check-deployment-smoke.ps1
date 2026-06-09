param(
    [string]$ClientDomain = "mabuntle.com",
    [string]$SellerDomain = "seller.mabuntle.com",
    [string]$AdminDomain = "admin.mabuntle.com",
    [string]$ApiDomain = "api.mabuntle.com",
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

function New-Result {
    param(
        [string]$Check,
        [string]$Target,
        [string]$Status,
        [string]$Detail
    )

    [pscustomobject]@{
        Check = $Check
        Target = $Target
        Status = $Status
        Detail = $Detail
    }
}

function Test-Dns {
    param([string]$HostName)

    try {
        $addresses = [System.Net.Dns]::GetHostAddresses($HostName) |
            ForEach-Object { $_.ToString() }

        if ($addresses.Count -eq 0) {
            return New-Result "DNS" $HostName "Fail" "No addresses returned."
        }

        return New-Result "DNS" $HostName "Pass" ($addresses -join ", ")
    }
    catch {
        return New-Result "DNS" $HostName "Fail" $_.Exception.Message
    }
}

function Test-Http {
    param(
        [string]$Uri,
        [int[]]$ExpectedStatuses,
        [switch]$AllowRedirect
    )

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)
    $client.Timeout = [TimeSpan]::FromSeconds($TimeoutSeconds)

    try {
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Head, $Uri)
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        $statusCode = [int]$response.StatusCode
        $location = $response.Headers.Location
        $detail = "HTTP $statusCode"

        if ($location -ne $null) {
            $detail = "$detail -> $location"
        }

        if ($ExpectedStatuses -contains $statusCode) {
            return New-Result "HTTP" $Uri "Pass" $detail
        }

        if ($AllowRedirect -and $statusCode -ge 300 -and $statusCode -lt 400) {
            return New-Result "HTTP" $Uri "Pass" $detail
        }

        return New-Result "HTTP" $Uri "Fail" $detail
    }
    catch {
        return New-Result "HTTP" $Uri "Fail" $_.Exception.Message
    }
    finally {
        $client.Dispose()
        $handler.Dispose()
    }
}

$results = @()

foreach ($domain in @($ClientDomain, $SellerDomain, $AdminDomain, $ApiDomain)) {
    $results += Test-Dns $domain
}

$results += Test-Http "https://$ApiDomain/health" @(200)
$results += Test-Http "https://$ApiDomain/health/ready" @(200)

$results += Test-Http "https://$ClientDomain/" @(200)
$results += Test-Http "https://$ClientDomain/shop" @(200) -AllowRedirect
$results += Test-Http "https://$ClientDomain/cart" @(200) -AllowRedirect
$results += Test-Http "https://$ClientDomain/assistant" @(200) -AllowRedirect

$results += Test-Http "https://$SellerDomain/" @(200) -AllowRedirect
$results += Test-Http "https://$SellerDomain/products" @(200) -AllowRedirect
$results += Test-Http "https://$SellerDomain/seller/products" @(200, 301, 302, 307, 308) -AllowRedirect

$results += Test-Http "https://$AdminDomain/" @(200) -AllowRedirect
$results += Test-Http "https://$AdminDomain/support" @(200) -AllowRedirect
$results += Test-Http "https://$AdminDomain/admin/support" @(200, 301, 302, 307, 308) -AllowRedirect

$results | Format-Table -AutoSize

if ($results | Where-Object { $_.Status -eq "Fail" }) {
    exit 1
}
