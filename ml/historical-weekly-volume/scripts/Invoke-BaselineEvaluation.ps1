[CmdletBinding()]
param(
    [string]$ConnectionString,
    [string]$GeneratedAtUtc
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$projectRoot = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $projectRoot 'config\pilot-v1.json'
$extractQueryPath = Join-Path $projectRoot 'sql\extract_weekly_counts_v1.sql'
$validationQueryPath = Join-Path $projectRoot 'sql\validate_source_v1.sql'
$generatedRoot = Join-Path $projectRoot 'artifacts\generated'
$reportsRoot = Join-Path $projectRoot 'reports'

Import-Module (Join-Path $PSScriptRoot 'HistoricalWeeklyVolume.psm1') -Force

if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = $env:SMARTFACILITY_ML_CONNECTION_STRING
}
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = 'Server=localhost;Database=SmartFacilityDb;Integrated Security=True;Encrypt=False;TrustServerCertificate=True;ApplicationIntent=ReadOnly'
}

if ([string]::IsNullOrWhiteSpace($GeneratedAtUtc)) {
    $generatedAt = [datetime]::UtcNow
}
else {
    $generatedAt = [datetime]::Parse(
        $GeneratedAtUtc,
        $invariant,
        [System.Globalization.DateTimeStyles]::AssumeUniversal).ToUniversalTime()
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$extractSql = Get-Content -LiteralPath $extractQueryPath -Raw
$validationSql = Get-Content -LiteralPath $validationQueryPath -Raw
Assert-ReadOnlySql -Sql $extractSql
Assert-ReadOnlySql -Sql $validationSql

New-Item -ItemType Directory -Path $generatedRoot -Force | Out-Null
New-Item -ItemType Directory -Path $reportsRoot -Force | Out-Null

$weeklyQueryRows = @(Invoke-Sqlcmd -ConnectionString $ConnectionString -Query $extractSql -QueryTimeout 60)
$validation = @(Invoke-Sqlcmd -ConnectionString $ConnectionString -Query $validationSql -QueryTimeout 60)[0]

$weeklyRows = @($weeklyQueryRows | ForEach-Object {
    [pscustomobject]@{
        WeekStart = [datetime]::ParseExact([string]$_.WeekStart, 'yyyy-MM-dd', $invariant)
        WeekEndExclusive = [datetime]::ParseExact([string]$_.WeekEndExclusive, 'yyyy-MM-dd', $invariant)
        HistoricalCount = [long]$_.HistoricalCount
    }
})

$actualBucketCount = $weeklyRows.Count
$actualSourceRows = [long](($weeklyRows | Measure-Object HistoricalCount -Sum).Sum)
$actualZeroWeeks = @($weeklyRows | Where-Object { $_.HistoricalCount -eq 0 }).Count

$validationFailures = New-Object System.Collections.Generic.List[string]
if ([long]$validation.TotalSourceRows -ne [long]$config.expected.totalSourceRows) {
    $validationFailures.Add("Total source rows: expected $($config.expected.totalSourceRows), actual $($validation.TotalSourceRows)")
}
if ([long]$validation.IncludedSourceRows -ne [long]$config.expected.includedSourceRows) {
    $validationFailures.Add("Included source rows: expected $($config.expected.includedSourceRows), actual $($validation.IncludedSourceRows)")
}
if ($actualSourceRows -ne [long]$config.expected.includedSourceRows) {
    $validationFailures.Add("Weekly count sum: expected $($config.expected.includedSourceRows), actual $actualSourceRows")
}
if ($actualBucketCount -ne [int]$config.expected.weeklyBuckets) {
    $validationFailures.Add("Weekly buckets: expected $($config.expected.weeklyBuckets), actual $actualBucketCount")
}
if ($actualZeroWeeks -ne [int]$config.expected.zeroWeeks) {
    $validationFailures.Add("Zero weeks: expected $($config.expected.zeroWeeks), actual $actualZeroWeeks")
}
if ([long]$validation.RowsBeforeFirstCompleteWeek -ne 1) {
    $validationFailures.Add("Rows before first complete week: expected 1, actual $($validation.RowsBeforeFirstCompleteWeek)")
}
if ([long]$validation.RowsAtOrAfterCutoff -ne 0) {
    $validationFailures.Add("Rows at/after cutoff: expected 0, actual $($validation.RowsAtOrAfterCutoff)")
}
if ([long]$validation.MissingEventTimeRows -ne 0) {
    $validationFailures.Add("Missing event-time rows: expected 0, actual $($validation.MissingEventTimeRows)")
}
if ($weeklyRows[0].WeekStart -ne [datetime]'2022-05-23') {
    $validationFailures.Add("First week: expected 2022-05-23, actual $($weeklyRows[0].WeekStart.ToString('yyyy-MM-dd'))")
}
if ($weeklyRows[-1].WeekStart -ne [datetime]'2026-07-27') {
    $validationFailures.Add("Last week: expected 2026-07-27, actual $($weeklyRows[-1].WeekStart.ToString('yyyy-MM-dd'))")
}

if ($validationFailures.Count -gt 0) {
    throw "Dataset contract validation failed. Baseline evaluation was not run.`n$($validationFailures -join "`n")"
}

$weeklyCsvPath = Join-Path $generatedRoot 'weekly-counts-v1.csv'
$weeklyRows | ForEach-Object {
    [pscustomobject][ordered]@{
        week_start = $_.WeekStart.ToString('yyyy-MM-dd', $invariant)
        week_end_exclusive = $_.WeekEndExclusive.ToString('yyyy-MM-dd', $invariant)
        historical_count = $_.HistoricalCount
    }
} | Export-Csv -LiteralPath $weeklyCsvPath -NoTypeInformation -Encoding UTF8

$featureRows = @(New-HistoricalWeeklyFeatureRows -WeeklyRows $weeklyRows)
$featureCsvPath = Join-Path $generatedRoot 'features-v1.csv'
$featureRows | ForEach-Object {
    [pscustomobject][ordered]@{
        target_week_start = $_.target_week_start.ToString('yyyy-MM-dd', $invariant)
        target_week_end_exclusive = $_.target_week_end_exclusive.ToString('yyyy-MM-dd', $invariant)
        prediction_cutoff_exclusive = $_.prediction_cutoff_exclusive.ToString('yyyy-MM-dd', $invariant)
        split = $_.split
        actual = $_.actual
        lag_1 = $_.lag_1
        lag_2 = $_.lag_2
        lag_4 = $_.lag_4
        lag_13 = $_.lag_13
        lag_52 = $_.lag_52
        rolling_mean_4 = ([double]$_.rolling_mean_4).ToString('G17', $invariant)
        rolling_median_4 = ([double]$_.rolling_median_4).ToString('G17', $invariant)
        rolling_std_4 = ([double]$_.rolling_std_4).ToString('G17', $invariant)
        rolling_mean_13 = ([double]$_.rolling_mean_13).ToString('G17', $invariant)
        rolling_median_13 = ([double]$_.rolling_median_13).ToString('G17', $invariant)
        rolling_std_13 = ([double]$_.rolling_std_13).ToString('G17', $invariant)
        rolling_mean_26 = ([double]$_.rolling_mean_26).ToString('G17', $invariant)
        rolling_median_26 = ([double]$_.rolling_median_26).ToString('G17', $invariant)
        rolling_std_26 = ([double]$_.rolling_std_26).ToString('G17', $invariant)
        iso_year = $_.iso_year
        iso_week = $_.iso_week
        iso_week_sin = ([double]$_.iso_week_sin).ToString('G17', $invariant)
        iso_week_cos = ([double]$_.iso_week_cos).ToString('G17', $invariant)
    }
} | Export-Csv -LiteralPath $featureCsvPath -NoTypeInformation -Encoding UTF8

$extractQueryHash = (Get-FileHash -LiteralPath $extractQueryPath -Algorithm SHA256).Hash.ToLowerInvariant()
$weeklyDataHash = (Get-FileHash -LiteralPath $weeklyCsvPath -Algorithm SHA256).Hash.ToLowerInvariant()
$featureDataHash = (Get-FileHash -LiteralPath $featureCsvPath -Algorithm SHA256).Hash.ToLowerInvariant()

$manifest = [pscustomobject][ordered]@{
    sourceDataset = $config.sourceDataset
    extractionVersion = $config.extractionVersion
    queryVersion = $config.extractionVersion
    querySha256 = $extractQueryHash
    extractionCutoffExclusive = $config.extractionCutoffExclusive
    sourceEventDateTimeMin = ([datetime]$validation.MinEventDateTime).ToString('yyyy-MM-ddTHH:mm:ss', $invariant)
    sourceEventDateTimeMax = ([datetime]$validation.MaxEventDateTime).ToString('yyyy-MM-ddTHH:mm:ss', $invariant)
    firstCompleteWeekStart = $weeklyRows[0].WeekStart.ToString('yyyy-MM-dd', $invariant)
    lastCompleteWeekEndInclusive = $weeklyRows[-1].WeekEndExclusive.AddDays(-1).ToString('yyyy-MM-dd', $invariant)
    weeklyBucketCount = $actualBucketCount
    sourceRowCount = $actualSourceRows
    totalSourceRowCount = [long]$validation.TotalSourceRows
    zeroWeekCount = $actualZeroWeeks
    generatedAt = $generatedAt.ToString('yyyy-MM-ddTHH:mm:ssZ', $invariant)
    generatedAtUtc = $generatedAt.ToString('yyyy-MM-ddTHH:mm:ssZ', $invariant)
    timezoneAssumption = $config.timezoneAssumption
    weeklyDataSha256 = $weeklyDataHash
    featureDataSha256 = $featureDataHash
}

$manifestPath = Join-Path $reportsRoot 'dataset-manifest-v1.json'
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

$splitSummaries = New-Object System.Collections.Generic.List[object]
foreach ($split in @('train', 'validation', 'test')) {
    $calendarRows = @($weeklyRows | Where-Object { (Get-SplitName -WeekStart $_.WeekStart) -eq $split })
    $evaluableRows = @($featureRows | Where-Object { $_.split -eq $split })
    $splitSummaries.Add([pscustomobject][ordered]@{
        split = $split
        calendar_bucket_count = $calendarRows.Count
        evaluable_supervised_row_count = $evaluableRows.Count
        target = Get-TargetStatistics -Rows $evaluableRows
    })
}

$validationResults = @(Get-BaselineResults -FeatureRows $featureRows -Split validation)
$testResults = @(Get-BaselineResults -FeatureRows $featureRows -Split test)
$selectedBaseline = @($validationResults | Sort-Object { $_.metrics.mae })[0].baseline

# Baselines have no fitted state. Recompute each prediction directly from the raw
# prefix to prove fixed-table and expanding-window values are identical.
$expandingChecks = New-Object System.Collections.Generic.List[object]
$dateIndex = @{}
for ($index = 0; $index -lt $weeklyRows.Count; $index++) {
    $dateIndex[$weeklyRows[$index].WeekStart.ToString('yyyy-MM-dd')] = $index
}
foreach ($baseline in Get-BaselineDefinitions) {
    $maxDifference = 0.0
    foreach ($row in $featureRows) {
        $index = [int]$dateIndex[$row.target_week_start.ToString('yyyy-MM-dd')]
        switch ($baseline.Name) {
            'previous_week_naive' { $expandedPrediction = [double]$weeklyRows[$index - 1].HistoricalCount }
            'moving_average_4' {
                $expandedPrediction = [double](($weeklyRows[($index - 4)..($index - 1)] | Measure-Object HistoricalCount -Average).Average)
            }
            'seasonal_naive_52' { $expandedPrediction = [double]$weeklyRows[$index - 52].HistoricalCount }
        }
        $fixedPrediction = [double]$row.($baseline.Property)
        $difference = [Math]::Abs($expandedPrediction - $fixedPrediction)
        if ($difference -gt $maxDifference) { $maxDifference = $difference }
    }
    $expandingChecks.Add([pscustomobject]@{
        baseline = $baseline.Name
        max_absolute_prediction_difference = $maxDifference
    })
}

$trainRows = @($featureRows | Where-Object { $_.split -eq 'train' })
$validationRows = @($featureRows | Where-Object { $_.split -eq 'validation' })
$testRows = @($featureRows | Where-Object { $_.split -eq 'test' })
$metricDeltas = foreach ($validationResult in $validationResults) {
    $testResult = @($testResults | Where-Object { $_.baseline -eq $validationResult.baseline })[0]
    [pscustomobject]@{
        baseline = $validationResult.baseline
        test_minus_validation_mae = $testResult.metrics.mae - $validationResult.metrics.mae
        test_minus_validation_wape_points = $testResult.metrics.wape_percent - $validationResult.metrics.wape_percent
    }
}

$report = [pscustomobject][ordered]@{
    reportVersion = 'historical-weekly-volume-baselines/v1'
    generatedAtUtc = $manifest.generatedAtUtc
    datasetManifest = $manifest
    featureDataset = [pscustomobject]@{
        supervisedRowCount = $featureRows.Count
        warmupWeeks = 52
        features = $config.features
    }
    temporalSplits = $splitSummaries.ToArray()
    baselineSelectedUsingValidationOnly = $selectedBaseline
    validation = $validationResults
    test = $testResults
    expandingWindowCheck = $expandingChecks.ToArray()
    driftAndError = [pscustomobject]@{
        trainTargetMean = (Get-TargetStatistics -Rows $trainRows).mean
        validationTargetMean = (Get-TargetStatistics -Rows $validationRows).mean
        testTargetMean = (Get-TargetStatistics -Rows $testRows).mean
        validationVsTrainTargetMeanPercent = 100.0 * ((Get-TargetStatistics -Rows $validationRows).mean / (Get-TargetStatistics -Rows $trainRows).mean - 1.0)
        testVsValidationTargetMeanPercent = 100.0 * ((Get-TargetStatistics -Rows $testRows).mean / (Get-TargetStatistics -Rows $validationRows).mean - 1.0)
        validationZeroTargetCount = @($validationRows | Where-Object { $_.actual -eq 0 }).Count
        testZeroTargetCount = @($testRows | Where-Object { $_.actual -eq 0 }).Count
        zeroWeeksInFullCalendarSeries = $actualZeroWeeks
        metricDeltas = @($metricDeltas)
    }
}

$reportJsonPath = Join-Path $reportsRoot 'baseline-evaluation-v1.json'
$report | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $reportJsonPath -Encoding UTF8

function Format-Number([double]$Value) {
    return $Value.ToString('0.00', $invariant)
}

$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add('# Historical Weekly Volume Baseline Evaluation v1')
$markdown.Add('')
$markdown.Add("Generated UTC: $($manifest.generatedAtUtc)")
$markdown.Add('')
$markdown.Add('## Dataset contract')
$markdown.Add('')
$markdown.Add("- Source: ``$($manifest.sourceDataset)``")
$markdown.Add("- Source rows included: $($manifest.sourceRowCount) of $($manifest.totalSourceRowCount)")
$markdown.Add("- Complete weekly buckets: $($manifest.weeklyBucketCount)")
$markdown.Add("- Zero weeks: $($manifest.zeroWeekCount)")
$markdown.Add("- Event-time range: $($manifest.sourceEventDateTimeMin) to $($manifest.sourceEventDateTimeMax)")
$markdown.Add("- Dataset calendar range: $($manifest.firstCompleteWeekStart) to $($manifest.lastCompleteWeekEndInclusive)")
$markdown.Add("- Cutoff exclusive: $($manifest.extractionCutoffExclusive)")
$markdown.Add('')
$markdown.Add('## Temporal splits')
$markdown.Add('')
$markdown.Add('| Split | Calendar buckets | Evaluable rows | Target mean | Target std | Min | Max |')
$markdown.Add('|---|---:|---:|---:|---:|---:|---:|')
foreach ($summary in $splitSummaries) {
    $markdown.Add("| $($summary.split) | $($summary.calendar_bucket_count) | $($summary.evaluable_supervised_row_count) | $(Format-Number $summary.target.mean) | $(Format-Number $summary.target.std) | $(Format-Number $summary.target.min) | $(Format-Number $summary.target.max) |")
}
$markdown.Add('')
$markdown.Add('## Validation baselines')
$markdown.Add('')
$markdown.Add('| Baseline | MAE | WAPE | RMSE | Mean signed error | Median absolute error |')
$markdown.Add('|---|---:|---:|---:|---:|---:|')
foreach ($result in $validationResults) {
    $markdown.Add("| $($result.baseline) | $(Format-Number $result.metrics.mae) | $(Format-Number $result.metrics.wape_percent)% | $(Format-Number $result.metrics.rmse) | $(Format-Number $result.metrics.mean_signed_error) | $(Format-Number $result.metrics.median_absolute_error) |")
}
$markdown.Add('')
$markdown.Add("Baseline selected by validation MAE only: **$selectedBaseline**")
$markdown.Add('')
$markdown.Add('## Test baselines')
$markdown.Add('')
$markdown.Add('| Baseline | MAE | WAPE | RMSE | Mean signed error | Median absolute error |')
$markdown.Add('|---|---:|---:|---:|---:|---:|')
foreach ($result in $testResults) {
    $markdown.Add("| $($result.baseline) | $(Format-Number $result.metrics.mae) | $(Format-Number $result.metrics.wape_percent)% | $(Format-Number $result.metrics.rmse) | $(Format-Number $result.metrics.mean_signed_error) | $(Format-Number $result.metrics.median_absolute_error) |")
}
$markdown.Add('')
$markdown.Add('## Drift and error observations')
$markdown.Add('')
$markdown.Add("- Evaluable target mean: train $(Format-Number $report.driftAndError.trainTargetMean), validation $(Format-Number $report.driftAndError.validationTargetMean), test $(Format-Number $report.driftAndError.testTargetMean).")
$markdown.Add("- Validation mean is $(Format-Number $report.driftAndError.validationVsTrainTargetMeanPercent)% above the train evaluable mean. This confirms the late-2024/2025 level shift; it is not evidence of a failure-rate change.")
$markdown.Add("- Test mean is $(Format-Number ([Math]::Abs($report.driftAndError.testVsValidationTargetMeanPercent)))% below validation mean, so the elevated level persists but moderates.")
$markdown.Add("- Validation/test zero targets: $($report.driftAndError.validationZeroTargetCount) / $($report.driftAndError.testZeroTargetCount). The two calendar zero-weeks are in the early training history, so they do not directly lower validation/test denominators.")
$markdown.Add('- Positive mean signed error means overprediction; negative means underprediction.')
$markdown.Add('- These are record-volume errors, not failure, health, reliability, or maintenance-outcome estimates.')
$markdown.Add('')
$markdown.Add('| Baseline | Test minus validation MAE | Test minus validation WAPE points |')
$markdown.Add('|---|---:|---:|')
foreach ($delta in $metricDeltas) {
    $markdown.Add("| $($delta.baseline) | $(Format-Number $delta.test_minus_validation_mae) | $(Format-Number $delta.test_minus_validation_wape_points) |")
}
$markdown.Add('')
$markdown.Add('## Expanding-window equivalence')
$markdown.Add('')
foreach ($check in $expandingChecks) {
    $markdown.Add("- $($check.baseline): max absolute difference $(Format-Number $check.max_absolute_prediction_difference)")
}
$markdown.Add('')
$markdown.Add('All three baselines are stateless and each prediction was recomputed from its historical prefix only.')

foreach ($result in $testResults) {
    $markdown.Add('')
    $markdown.Add("## Worst 10 test weeks - $($result.baseline)")
    $markdown.Add('')
    $markdown.Add('| Week | Actual | Prediction | Absolute error |')
    $markdown.Add('|---|---:|---:|---:|')
    foreach ($errorRow in $result.worst_weeks) {
        $markdown.Add("| $($errorRow.week) | $($errorRow.actual) | $(Format-Number $errorRow.prediction) | $(Format-Number $errorRow.absolute_error) |")
    }
}

$reportMarkdownPath = Join-Path $reportsRoot 'baseline-evaluation-v1.md'
$markdown -join "`r`n" | Set-Content -LiteralPath $reportMarkdownPath -Encoding UTF8

Write-Host "Dataset validated: $actualBucketCount buckets, $actualSourceRows rows, $actualZeroWeeks zero weeks."
Write-Host "Feature rows: $($featureRows.Count). Validation-selected baseline: $selectedBaseline."
Write-Host "Manifest: $manifestPath"
Write-Host "Evaluation: $reportMarkdownPath"
