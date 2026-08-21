[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$modulePath = Join-Path $projectRoot 'scripts\HistoricalWeeklyVolume.psm1'
$generatedRoot = Join-Path $projectRoot 'artifacts\generated'
$reportsRoot = Join-Path $projectRoot 'reports'
$invariant = [System.Globalization.CultureInfo]::InvariantCulture

Import-Module $modulePath -Force

$script:Passed = 0
$script:Failed = 0

function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) {
        throw "$Name - expected '$Expected', actual '$Actual'."
    }
}

function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Name) {
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance) {
        throw "$Name - expected '$Expected', actual '$Actual', tolerance '$Tolerance'."
    }
}

function Test-Case([string]$Name, [scriptblock]$Body) {
    try {
        & $Body
        $script:Passed++
        Write-Host "PASS $Name"
    }
    catch {
        $script:Failed++
        Write-Host "FAIL $Name - $($_.Exception.Message)"
    }
}

function New-SyntheticSeries([int]$Count = 60) {
    $rows = New-Object System.Collections.Generic.List[object]
    $start = [datetime]'2022-05-23'
    for ($index = 0; $index -lt $Count; $index++) {
        $rows.Add([pscustomobject]@{
            WeekStart = $start.AddDays(7 * $index)
            WeekEndExclusive = $start.AddDays(7 * ($index + 1))
            HistoricalCount = [long](100 + $index)
        })
    }
    return $rows.ToArray()
}

Test-Case 'read-only SQL guard rejects DML' {
    $threw = $false
    try { Assert-ReadOnlySql -Sql 'SELECT 1; DELETE FROM x;' } catch { $threw = $true }
    Assert-Equal $true $threw 'DML rejection'
}

Test-Case 'lag values use only weeks before target' {
    $weekly = @(New-SyntheticSeries)
    $features = @(New-HistoricalWeeklyFeatureRows -WeeklyRows $weekly)
    $first = $features[0]
    Assert-Equal 152 $first.actual 'actual'
    Assert-Equal 151 $first.lag_1 'lag_1'
    Assert-Equal 150 $first.lag_2 'lag_2'
    Assert-Equal 148 $first.lag_4 'lag_4'
    Assert-Equal 139 $first.lag_13 'lag_13'
    Assert-Equal 100 $first.lag_52 'lag_52'
}

Test-Case 'rolling features shift before rolling' {
    $weekly = @(New-SyntheticSeries)
    $features = @(New-HistoricalWeeklyFeatureRows -WeeklyRows $weekly)
    $first = $features[0]
    Assert-Near 149.5 $first.rolling_mean_4 0.0000001 'rolling_mean_4'
    Assert-Near 149.5 $first.rolling_median_4 0.0000001 'rolling_median_4'
    Assert-Near ([Math]::Sqrt(5.0 / 3.0)) $first.rolling_std_4 0.0000001 'rolling_std_4'
}

Test-Case 'target mutation cannot change same-row features' {
    $firstSeries = @(New-SyntheticSeries)
    $secondSeries = @(New-SyntheticSeries)
    $secondSeries[52].HistoricalCount = 999999
    $a = @(New-HistoricalWeeklyFeatureRows -WeeklyRows $firstSeries)[0]
    $b = @(New-HistoricalWeeklyFeatureRows -WeeklyRows $secondSeries)[0]
    foreach ($property in @('lag_1','lag_2','lag_4','lag_13','lag_52','rolling_mean_4','rolling_median_4','rolling_std_4','rolling_mean_13','rolling_mean_26','iso_week_sin','iso_week_cos')) {
        Assert-Near ([double]$a.$property) ([double]$b.$property) 0.0000000001 "no leakage $property"
    }
    Assert-Equal 152 $a.actual 'first actual'
    Assert-Equal 999999 $b.actual 'mutated actual'
}

Test-Case 'baseline formulas match locked definitions' {
    $weekly = @(New-SyntheticSeries)
    $row = @(New-HistoricalWeeklyFeatureRows -WeeklyRows $weekly)[0]
    Assert-Equal 151 $row.lag_1 'previous-week naive'
    Assert-Near 149.5 $row.rolling_mean_4 0.0000001 'moving-average-4'
    Assert-Equal 100 $row.lag_52 'seasonal-naive-52'
}

Test-Case 'zero-target WAPE is safe and undefined' {
    $rows = @(
        [pscustomobject]@{ actual = 0; prediction = 0 },
        [pscustomobject]@{ actual = 0; prediction = 4 }
    )
    $metrics = Get-ForecastMetrics -Rows $rows -PredictionProperty prediction
    Assert-Equal $null $metrics.wape_percent 'zero denominator WAPE'
    Assert-Near 2.0 $metrics.mae 0.0000001 'zero target MAE'
}

Test-Case 'feature generation is deterministic' {
    $weekly = @(New-SyntheticSeries)
    $first = @(New-HistoricalWeeklyFeatureRows -WeeklyRows $weekly) | ConvertTo-Json -Depth 5 -Compress
    $second = @(New-HistoricalWeeklyFeatureRows -WeeklyRows $weekly) | ConvertTo-Json -Depth 5 -Compress
    Assert-Equal $first $second 'deterministic features'
}

Test-Case 'actual extraction preserves calendar spine and counts' {
    $weeklyPath = Join-Path $generatedRoot 'weekly-counts-v1.csv'
    $manifestPath = Join-Path $reportsRoot 'dataset-manifest-v1.json'
    if (-not (Test-Path -LiteralPath $weeklyPath) -or -not (Test-Path -LiteralPath $manifestPath)) {
        throw 'Generated extraction artifacts are missing. Run Invoke-BaselineEvaluation.ps1 first.'
    }
    $weekly = @(Import-Csv -LiteralPath $weeklyPath)
    $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
    Assert-Equal 219 $weekly.Count 'bucket count'
    Assert-Equal 167142 ([long](($weekly | Measure-Object historical_count -Sum).Sum)) 'source count'
    Assert-Equal 2 @($weekly | Where-Object { [long]$_.historical_count -eq 0 }).Count 'zero-fill count'
    Assert-Equal '2022-05-23' $weekly[0].week_start 'partial first week excluded'
    Assert-Equal '2026-07-27' $weekly[-1].week_start 'final week start'
    Assert-Equal '2026-08-03T00:00:00' $manifest.extractionCutoffExclusive 'cutoff'
    Assert-Equal $manifest.weeklyDataSha256 ((Get-FileHash -LiteralPath $weeklyPath -Algorithm SHA256).Hash.ToLowerInvariant()) 'deterministic weekly hash'
    Assert-Equal $manifest.featureDataSha256 ((Get-FileHash -LiteralPath (Join-Path $generatedRoot 'features-v1.csv') -Algorithm SHA256).Hash.ToLowerInvariant()) 'deterministic feature hash'
    for ($index = 1; $index -lt $weekly.Count; $index++) {
        $previous = [datetime]::ParseExact($weekly[$index - 1].week_start, 'yyyy-MM-dd', $invariant)
        $current = [datetime]::ParseExact($weekly[$index].week_start, 'yyyy-MM-dd', $invariant)
        Assert-Equal $previous.AddDays(7) $current "contiguous week $index"
    }
}

Test-Case 'actual temporal split boundaries and warm-up are locked' {
    $featurePath = Join-Path $generatedRoot 'features-v1.csv'
    $features = @(Import-Csv -LiteralPath $featurePath)
    Assert-Equal 167 $features.Count 'supervised rows'
    Assert-Equal 84 @($features | Where-Object split -eq 'train').Count 'train evaluable rows'
    Assert-Equal 26 @($features | Where-Object split -eq 'validation').Count 'validation evaluable rows'
    Assert-Equal 57 @($features | Where-Object split -eq 'test').Count 'test evaluable rows'
    Assert-Equal '2024-12-30' @($features | Where-Object split -eq 'validation')[0].target_week_start 'validation start'
    Assert-Equal '2025-06-30' @($features | Where-Object split -eq 'test')[0].target_week_start 'test start'
    Assert-Equal '2026-07-27' @($features | Where-Object split -eq 'test')[-1].target_week_start 'test end'
}

if ($script:Failed -gt 0) {
    throw "$script:Failed automated checks failed; $script:Passed passed."
}

Write-Host "All automated checks passed: $script:Passed."
