[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$generatedRoot = Join-Path $projectRoot 'artifacts\generated'
$reportsRoot = Join-Path $projectRoot 'reports'
$configPath = Join-Path $projectRoot 'config\training-v1.json'
$featurePath = Join-Path $generatedRoot 'features-v1.csv'
$manifestPath = Join-Path $reportsRoot 'dataset-manifest-v1.json'
$selectionPath = Join-Path $reportsRoot 'model-selection-v1.json'
$reportPath = Join-Path $reportsRoot 'model-training-evaluation-v1.json'

Import-Module (Join-Path $projectRoot 'scripts\HistoricalWeeklyVolume.psm1') -Force -DisableNameChecking

$script:Passed = 0
$script:Failed = 0
function Assert-Equal($Expected, $Actual, [string]$Name) {
    if ($Expected -ne $Actual) { throw "$Name - expected '$Expected', actual '$Actual'." }
}
function Assert-Near([double]$Expected, [double]$Actual, [double]$Tolerance, [string]$Name) {
    if ([Math]::Abs($Expected - $Actual) -gt $Tolerance) {
        throw "$Name - expected '$Expected', actual '$Actual'."
    }
}
function Test-Case([string]$Name, [scriptblock]$Body) {
    try { & $Body; $script:Passed++; Write-Host "PASS $Name" }
    catch { $script:Failed++; Write-Host "FAIL $Name - $($_.Exception.Message)" }
}

$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$report = Get-Content -LiteralPath $reportPath -Raw | ConvertFrom-Json
$selection = Get-Content -LiteralPath $selectionPath -Raw | ConvertFrom-Json
$rows = @(Import-Csv -LiteralPath $featurePath)
$train = @($rows | Where-Object split -eq 'train')
$validation = @($rows | Where-Object split -eq 'validation')
$selectedAlpha = [double]$selection.selectedAlpha

Test-Case 'training is deterministic' {
    $first = Fit-RidgeRegression -Rows $train -Alpha $selectedAlpha
    $second = Fit-RidgeRegression -Rows $train -Alpha $selectedAlpha
    Assert-Equal ($first | ConvertTo-Json -Depth 8 -Compress) ($second | ConvertTo-Json -Depth 8 -Compress) 'model serialization'
    $firstPredictions = @(Get-RidgePredictions -Model $first -Rows $validation)
    $secondPredictions = @(Get-RidgePredictions -Model $second -Rows $validation)
    for ($index = 0; $index -lt $firstPredictions.Count; $index++) {
        Assert-Near $firstPredictions[$index] $secondPredictions[$index] 0.0000000001 "prediction $index"
    }
}

Test-Case 'ridge solution matches closed-form one-feature result' {
    $synthetic = @(
        [pscustomobject]@{ x = 0.0; actual = 1.0 },
        [pscustomobject]@{ x = 1.0; actual = 3.0 },
        [pscustomobject]@{ x = 2.0; actual = 5.0 }
    )
    $model = Fit-RidgeRegression -Rows $synthetic -Alpha 1.0 -FeatureNames @('x')
    $predictions = @(Get-RidgePredictions -Model $model -Rows $synthetic)
    Assert-Near 1.5 $predictions[0] 0.0000001 'ridge prediction 0'
    Assert-Near 3.0 $predictions[1] 0.0000001 'ridge prediction 1'
    Assert-Near 4.5 $predictions[2] 0.0000001 'ridge prediction 2'
}

Test-Case 'fixed seed is versioned' {
    Assert-Equal 20260821 ([int]$config.randomSeed) 'random seed'
    Assert-Equal 5 @($config.ridgeAlphaCandidates).Count 'bounded alpha count'
}

Test-Case 'scaler fits train only' {
    $model = Fit-RidgeRegression -Rows $train -Alpha $selectedAlpha
    Assert-Equal 84 $model.scaler_fit_row_count 'scaler row count'
    $manualMean = (@($train | ForEach-Object { ConvertTo-InvariantDouble $_.lag_1 }) | Measure-Object -Average).Average
    $lagIndex = [Array]::IndexOf([string[]]$model.feature_names, 'lag_1')
    Assert-Near $manualMean $model.feature_means[$lagIndex] 0.0000000001 'train lag_1 mean'
    $combinedMean = (@($train + $validation | ForEach-Object { ConvertTo-InvariantDouble $_.lag_1 }) | Measure-Object -Average).Average
    if ([Math]::Abs($combinedMean - $model.feature_means[$lagIndex]) -lt 0.0001) {
        throw 'Scaler mean unexpectedly matches train+validation global mean.'
    }
}

Test-Case 'selection cannot use test metrics' {
    $candidates = @(
        [pscustomobject]@{ candidate_id = 'a'; alpha = 1.0; validation_mae = 100.0; test_mae = 9999.0 },
        [pscustomobject]@{ candidate_id = 'b'; alpha = 10.0; validation_mae = 110.0; test_mae = 1.0 }
    )
    $chosen = Select-RidgeCandidate -CandidateResults $candidates -MaeTieTolerance 1.0
    Assert-Equal 'a' $chosen.candidate_id 'validation-only choice'
}

Test-Case 'selection lock predates and excludes test evaluation' {
    Assert-Equal $false ([bool]$selection.testMetricsIncluded) 'selection test flag'
    Assert-Equal 'train' $selection.selectionInputs[0] 'selection input 0'
    Assert-Equal 'validation' $selection.selectionInputs[1] 'selection input 1'
    Assert-Equal $true ([bool]$report.selection.selectionCompletedBeforeTestEvaluation) 'selection order flag'
    Assert-Equal $selection.selectedCandidateId $report.selection.selectedCandidateId 'locked candidate'
    Assert-Equal 1 ([int]$report.untouchedTest.evaluationCount) 'test evaluation count'
}

Test-Case 'model predictions are finite' {
    $model = Fit-RidgeRegression -Rows $train -Alpha $selectedAlpha
    foreach ($prediction in @(Get-RidgePredictions -Model $model -Rows $validation)) {
        if ([double]::IsNaN($prediction) -or [double]::IsInfinity($prediction)) {
            throw 'Non-finite validation prediction.'
        }
    }
}

Test-Case 'metric calculation is correct' {
    $metricRows = @([pscustomobject]@{ actual = 0 }, [pscustomobject]@{ actual = 10 })
    $metrics = Get-MetricsFromPredictions -Rows $metricRows -Predictions ([double[]]@(2, 8))
    Assert-Near 2.0 $metrics.mae 0.0000001 'MAE'
    Assert-Near 40.0 $metrics.wape_percent 0.0000001 'WAPE'
    Assert-Near 2.0 $metrics.rmse 0.0000001 'RMSE'
    Assert-Near 0.0 $metrics.mean_signed_error 0.0000001 'bias'
    Assert-Near 2.0 $metrics.median_absolute_error 0.0000001 'median AE'
}

Test-Case 'artifact and config provenance are reproducible' {
    Assert-Equal $manifest.featureDataSha256 ((Get-FileHash -LiteralPath $featurePath -Algorithm SHA256).Hash.ToLowerInvariant()) 'feature hash'
    Assert-Equal $manifest.featureDataSha256 $report.reproducibility.featureDataSha256 'report feature hash'
    Assert-Equal $config.modelImplementation $report.reproducibility.modelImplementation 'implementation version'
    Assert-Equal ((Get-FileHash -LiteralPath $configPath -Algorithm SHA256).Hash.ToLowerInvariant()) $selection.trainingConfigSha256 'config hash'
    if (-not [bool]$report.artifactGate.artifactSaved -and
        (Test-Path -LiteralPath (Join-Path $projectRoot 'models\historical-weekly-ridge-v1.json'))) {
        throw 'Rejected model must not have an artifact.'
    }
}

if ($script:Failed -gt 0) {
    throw "$script:Failed model checks failed; $script:Passed passed."
}
Write-Host "All model checks passed: $script:Passed."
