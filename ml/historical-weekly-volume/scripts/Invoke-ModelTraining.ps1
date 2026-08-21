[CmdletBinding()]
param([string]$GeneratedAtUtc)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$projectRoot = Split-Path -Parent $PSScriptRoot
$generatedRoot = Join-Path $projectRoot 'artifacts\generated'
$reportsRoot = Join-Path $projectRoot 'reports'
$modelsRoot = Join-Path $projectRoot 'models'
$featurePath = Join-Path $generatedRoot 'features-v1.csv'
$datasetManifestPath = Join-Path $reportsRoot 'dataset-manifest-v1.json'
$baselineReportPath = Join-Path $reportsRoot 'baseline-evaluation-v1.json'
$trainingConfigPath = Join-Path $projectRoot 'config\training-v1.json'

Import-Module (Join-Path $PSScriptRoot 'HistoricalWeeklyVolume.psm1') -Force -DisableNameChecking

if (-not (Test-Path -LiteralPath $featurePath)) {
    throw 'Feature dataset is missing. Run Invoke-BaselineEvaluation.ps1 first.'
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

$config = Get-Content -LiteralPath $trainingConfigPath -Raw | ConvertFrom-Json
$manifest = Get-Content -LiteralPath $datasetManifestPath -Raw | ConvertFrom-Json
$baselineReport = Get-Content -LiteralPath $baselineReportPath -Raw | ConvertFrom-Json
$featureHash = (Get-FileHash -LiteralPath $featurePath -Algorithm SHA256).Hash.ToLowerInvariant()
if ($featureHash -ne $manifest.featureDataSha256) {
    throw 'Feature dataset hash does not match the locked dataset manifest.'
}

$validationBaseline = @($baselineReport.validation | Where-Object baseline -eq 'moving_average_4')[0]
$testBaseline = @($baselineReport.test | Where-Object baseline -eq 'moving_average_4')[0]
function Assert-RoundedContract([double]$Expected, [double]$Actual, [string]$Name) {
    if ([Math]::Round($Expected, 2) -ne [Math]::Round($Actual, 2)) {
        throw "$Name contract changed: expected $Expected, actual $Actual."
    }
}
Assert-RoundedContract 118.47 $validationBaseline.metrics.mae 'Validation MAE'
Assert-RoundedContract 10.10 $validationBaseline.metrics.wape_percent 'Validation WAPE'
Assert-RoundedContract 176.14 $validationBaseline.metrics.rmse 'Validation RMSE'
Assert-RoundedContract 120.96 $testBaseline.metrics.mae 'Test MAE'
Assert-RoundedContract 11.67 $testBaseline.metrics.wape_percent 'Test WAPE'
Assert-RoundedContract 163.48 $testBaseline.metrics.rmse 'Test RMSE'

# Selection phase: test rows are deliberately not materialized here.
$selectionRows = @(Import-Csv -LiteralPath $featurePath | Where-Object { $_.split -ne 'test' })
$trainRows = @($selectionRows | Where-Object split -eq 'train')
$validationRows = @($selectionRows | Where-Object split -eq 'validation')
if ($trainRows.Count -ne 84 -or $validationRows.Count -ne 26) {
    throw "Locked train/validation counts changed: $($trainRows.Count)/$($validationRows.Count)."
}

$featureNames = @(Get-ModelFeatureNames)
$candidateResults = New-Object System.Collections.Generic.List[object]
foreach ($alphaValue in $config.ridgeAlphaCandidates) {
    $alpha = [double]$alphaValue
    $model = Fit-RidgeRegression -Rows $trainRows -Alpha $alpha -FeatureNames $featureNames
    $trainPredictions = @(Get-RidgePredictions -Model $model -Rows $trainRows)
    $validationPredictions = @(Get-RidgePredictions -Model $model -Rows $validationRows)
    $trainMetrics = Get-MetricsFromPredictions -Rows $trainRows -Predictions $trainPredictions
    $validationMetrics = Get-MetricsFromPredictions -Rows $validationRows -Predictions $validationPredictions
    $expanding = Get-ExpandingRidgeEvaluation `
        -InitialTrainRows $trainRows `
        -ValidationRows $validationRows `
        -Alpha $alpha `
        -FeatureNames $featureNames

    $candidateResults.Add([pscustomobject][ordered]@{
        candidate_id = "ridge-alpha-$($alpha.ToString('G', $invariant))"
        model = 'ridge_regression'
        alpha = $alpha
        scaler_fit_split = 'train-only'
        scaler_fit_row_count = $model.scaler_fit_row_count
        train_mae = $trainMetrics.mae
        validation_mae = $validationMetrics.mae
        validation_wape_percent = $validationMetrics.wape_percent
        validation_rmse = $validationMetrics.rmse
        validation_bias = $validationMetrics.mean_signed_error
        validation_median_absolute_error = $validationMetrics.median_absolute_error
        train_validation_mae_gap = $validationMetrics.mae - $trainMetrics.mae
        validation_to_train_mae_ratio = $validationMetrics.mae / $trainMetrics.mae
        expanding_validation_mae = $expanding.metrics.mae
        expanding_validation_wape_percent = $expanding.metrics.wape_percent
        expanding_validation_rmse = $expanding.metrics.rmse
        expanding_validation_bias = $expanding.metrics.mean_signed_error
        validation_relative_mae_improvement_vs_baseline_percent =
            100.0 * ($validationBaseline.metrics.mae - $validationMetrics.mae) / $validationBaseline.metrics.mae
    })
}

$selected = Select-RidgeCandidate `
    -CandidateResults $candidateResults.ToArray() `
    -MaeTieTolerance ([double]$config.selectionMaeTieTolerance)
$selectionPassedBaseline = [double]$selected.validation_relative_mae_improvement_vs_baseline_percent -ge
    [double]$config.artifactGate.minimumValidationRelativeMaeImprovementPercent

New-Item -ItemType Directory -Path $reportsRoot -Force | Out-Null
$selectionLock = [pscustomobject][ordered]@{
    selectionVersion = 'historical-weekly-volume-selection/v1'
    generatedAt = $generatedAt.ToString('yyyy-MM-ddTHH:mm:ssZ', $invariant)
    selectionInputs = @('train', 'validation')
    testMetricsIncluded = $false
    selectionMetric = $config.selectionMetric
    maeTieTolerance = $config.selectionMaeTieTolerance
    selectedCandidateId = $selected.candidate_id
    selectedAlpha = $selected.alpha
    selectedValidationMae = $selected.validation_mae
    validationBaselineMae = $validationBaseline.metrics.mae
    validationGatePassed = $selectionPassedBaseline
    featureDataSha256 = $manifest.featureDataSha256
    trainingConfigSha256 = (Get-FileHash -LiteralPath $trainingConfigPath -Algorithm SHA256).Hash.ToLowerInvariant()
}
$selectionLockPath = Join-Path $reportsRoot 'model-selection-v1.json'
$selectionLock | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $selectionLockPath -Encoding UTF8
$selectionLockHash = (Get-FileHash -LiteralPath $selectionLockPath -Algorithm SHA256).Hash.ToLowerInvariant()

# Final phase starts only after the validation-only selection lock exists.
$finalTrainingRows = @($selectionRows)
$finalModel = Fit-RidgeRegression `
    -Rows $finalTrainingRows `
    -Alpha ([double]$selected.alpha) `
    -FeatureNames $featureNames
$testRows = @(Import-Csv -LiteralPath $featurePath | Where-Object split -eq 'test')
if ($testRows.Count -ne 57) {
    throw "Locked test count changed: $($testRows.Count)."
}
$testPredictions = @(Get-RidgePredictions -Model $finalModel -Rows $testRows)
$testMetrics = Get-MetricsFromPredictions -Rows $testRows -Predictions $testPredictions

$testErrors = New-Object System.Collections.Generic.List[object]
$modelWins = 0
$baselineWins = 0
$ties = 0
$underPredictions = 0
$overPredictions = 0
for ($index = 0; $index -lt $testRows.Count; $index++) {
    $actual = ConvertTo-InvariantDouble $testRows[$index].actual
    $prediction = $testPredictions[$index]
    $baselinePrediction = ConvertTo-InvariantDouble $testRows[$index].rolling_mean_4
    $modelError = [Math]::Abs($prediction - $actual)
    $baselineError = [Math]::Abs($baselinePrediction - $actual)
    if ($modelError -lt $baselineError) { $modelWins++ }
    elseif ($baselineError -lt $modelError) { $baselineWins++ }
    else { $ties++ }
    if ($prediction -lt $actual) { $underPredictions++ }
    elseif ($prediction -gt $actual) { $overPredictions++ }

    $testErrors.Add([pscustomobject][ordered]@{
        week = [string]$testRows[$index].target_week_start
        actual = $actual
        prediction = $prediction
        absolute_error = $modelError
        baseline_prediction = $baselinePrediction
        baseline_absolute_error = $baselineError
        week_over_week_change = $actual - (ConvertTo-InvariantDouble $testRows[$index].lag_1)
    })
}
$worstTestWeeks = @($testErrors | Sort-Object @{ Expression = 'absolute_error'; Descending = $true }, week | Select-Object -First 10)
$largestChanges = @($testErrors | Sort-Object @{ Expression = { [Math]::Abs($_.week_over_week_change) }; Descending = $true }, week | Select-Object -First 10)

$validationImprovement = 100.0 * ($validationBaseline.metrics.mae - $selected.validation_mae) / $validationBaseline.metrics.mae
$expandingDegradation = 100.0 * ($selected.expanding_validation_mae - $selected.validation_mae) / $selected.validation_mae
$testAbsoluteMaeImprovement = $testBaseline.metrics.mae - $testMetrics.mae
$testRelativeMaeImprovement = 100.0 * $testAbsoluteMaeImprovement / $testBaseline.metrics.mae
$testWapeImprovement = $testBaseline.metrics.wape_percent - $testMetrics.wape_percent
$testRmseImprovement = $testBaseline.metrics.rmse - $testMetrics.rmse

$expandingGatePassed = $expandingDegradation -le
    [double]$config.artifactGate.maximumExpandingVsFixedMaeDegradationPercent
$testGatePassed = (-not [bool]$config.artifactGate.requireTestMaeNoWorseThanBaseline) -or
    ($testMetrics.mae -le $testBaseline.metrics.mae)
$artifactGatePassed = $selectionPassedBaseline -and $expandingGatePassed -and $testGatePassed

$coefficients = New-Object System.Collections.Generic.List[object]
for ($index = 0; $index -lt $finalModel.feature_names.Count; $index++) {
    $coefficients.Add([pscustomobject][ordered]@{
        feature = $finalModel.feature_names[$index]
        standardized_coefficient = [double]$finalModel.standardized_coefficients[$index]
        absolute_standardized_coefficient = [Math]::Abs([double]$finalModel.standardized_coefficients[$index])
    })
}
$coefficientRanking = @($coefficients | Sort-Object @{ Expression = 'absolute_standardized_coefficient'; Descending = $true }, feature)
$trainTargetMean = (Get-TargetStatistics -Rows $trainRows).mean
$validationTargetMean = (Get-TargetStatistics -Rows $validationRows).mean
$testTargetMean = (Get-TargetStatistics -Rows $testRows).mean

$modelArtifactPath = Join-Path $modelsRoot 'historical-weekly-ridge-v1.json'
$artifactSaved = $false
if ($artifactGatePassed) {
    New-Item -ItemType Directory -Path $modelsRoot -Force | Out-Null
    $artifact = [pscustomobject][ordered]@{
        modelConfigVersion = $config.trainingVersion
        modelImplementation = $config.modelImplementation
        datasetManifestSha256 = (Get-FileHash -LiteralPath $datasetManifestPath -Algorithm SHA256).Hash.ToLowerInvariant()
        weeklyDataSha256 = $manifest.weeklyDataSha256
        featureDataSha256 = $manifest.featureDataSha256
        featureVersion = $config.featureVersion
        trainCutoffExclusive = '2024-12-30'
        validationCutoffExclusive = '2025-06-30'
        testCutoffExclusive = '2026-08-03'
        runtime = "$($PSVersionTable.PSEdition) PowerShell $($PSVersionTable.PSVersion)"
        randomSeed = $config.randomSeed
        metrics = [pscustomobject]@{ validation = $selected; test = $testMetrics }
        model = $finalModel
    }
    $artifact | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $modelArtifactPath -Encoding UTF8
    $artifactSaved = $true
}
elseif (Test-Path -LiteralPath $modelArtifactPath) {
    throw 'Artifact gate failed but a stale model artifact exists; refusing to present it as accepted.'
}

$offlineFeasibility = if ($artifactGatePassed) { 'GO' } else { 'NO-GO' }
$report = [pscustomobject][ordered]@{
    reportVersion = 'historical-weekly-volume-model-training/v1'
    generatedAt = $generatedAt.ToString('yyyy-MM-ddTHH:mm:ssZ', $invariant)
    baselineContract = [pscustomobject]@{
        validation = $validationBaseline.metrics
        test = $testBaseline.metrics
    }
    availableCandidates = @('ridge_regression')
    unavailableCandidates = $config.unavailableCandidates
    featureNames = $featureNames
    randomSeed = $config.randomSeed
    candidateResults = $candidateResults.ToArray()
    selection = [pscustomobject]@{
        selectedCandidateId = $selected.candidate_id
        selectedAlpha = $selected.alpha
        selectionInputs = @('train', 'validation')
        selectionLockSha256 = $selectionLockHash
        selectionCompletedBeforeTestEvaluation = $true
        validationRelativeMaeImprovementPercent = $validationImprovement
        validationGatePassed = $selectionPassedBaseline
    }
    finalRefit = [pscustomobject]@{
        fitSplits = @('train', 'validation')
        fitRowCount = $finalTrainingRows.Count
        scalerFitRowCount = $finalModel.scaler_fit_row_count
    }
    untouchedTest = [pscustomobject]@{
        evaluationCount = 1
        rowCount = $testRows.Count
        metrics = $testMetrics
        baselineMetrics = $testBaseline.metrics
        absoluteMaeImprovement = $testAbsoluteMaeImprovement
        relativeMaeImprovementPercent = $testRelativeMaeImprovement
        wapeImprovementPoints = $testWapeImprovement
        rmseImprovement = $testRmseImprovement
    }
    errorAnalysis = [pscustomobject]@{
        targetMeans = [pscustomobject]@{
            train = $trainTargetMean
            validation = $validationTargetMean
            test = $testTargetMean
        }
        modelWinsWeeks = $modelWins
        baselineWinsWeeks = $baselineWins
        ties = $ties
        underpredictionWeeks = $underPredictions
        overpredictionWeeks = $overPredictions
        worstTestWeeks = $worstTestWeeks
        largestWeekOverWeekChanges = $largestChanges
    }
    interpretability = [pscustomobject]@{
        statement = 'Standardized coefficient magnitude is association/influence in this fitted model, not causality.'
        coefficients = $coefficientRanking
    }
    artifactGate = [pscustomobject]@{
        validationGatePassed = $selectionPassedBaseline
        expandingWindowGatePassed = $expandingGatePassed
        testNoWorseThanBaselineGatePassed = $testGatePassed
        artifactSaved = $artifactSaved
    }
    offlineMlFeasibility = $offlineFeasibility
    productionInference = 'NO-GO'
    productionReason = 'A data refresh and as-of availability contract is still absent.'
    reproducibility = [pscustomobject]@{
        trainingConfigSha256 = $selectionLock.trainingConfigSha256
        featureDataSha256 = $manifest.featureDataSha256
        datasetQuerySha256 = $manifest.querySha256
        modelImplementation = $config.modelImplementation
        runtime = "$($PSVersionTable.PSEdition) PowerShell $($PSVersionTable.PSVersion)"
    }
}

$reportJsonPath = Join-Path $reportsRoot 'model-training-evaluation-v1.json'
$report | ConvertTo-Json -Depth 14 | Set-Content -LiteralPath $reportJsonPath -Encoding UTF8

function Format-Number([double]$Value) { $Value.ToString('0.00', $invariant) }
$markdown = New-Object System.Collections.Generic.List[string]
$markdown.Add('# Offline ML Model Training and Baseline Comparison v1')
$markdown.Add('')
$markdown.Add("Generated UTC: $($report.generatedAt)")
$markdown.Add('')
$markdown.Add('## A. Candidate models')
$markdown.Add('')
$markdown.Add('- Ridge Regression: evaluated with five predefined alpha values.')
$markdown.Add('- Random Forest Regressor: unavailable; Python/scikit-learn is not installed.')
$markdown.Add('- Gradient Boosting Regressor: unavailable; Python/scikit-learn is not installed.')
$markdown.Add('- No dependency, AutoML, XGBoost, LightGBM, or neural-network framework was added.')
$markdown.Add('')
$markdown.Add('## B. Train / validation results')
$markdown.Add('')
$markdown.Add('| Alpha | Train MAE | Validation MAE | WAPE | RMSE | Bias | Median AE | Val/train ratio |')
$markdown.Add('|---:|---:|---:|---:|---:|---:|---:|---:|')
foreach ($candidate in $candidateResults) {
    $markdown.Add("| $(Format-Number $candidate.alpha) | $(Format-Number $candidate.train_mae) | $(Format-Number $candidate.validation_mae) | $(Format-Number $candidate.validation_wape_percent)% | $(Format-Number $candidate.validation_rmse) | $(Format-Number $candidate.validation_bias) | $(Format-Number $candidate.validation_median_absolute_error) | $(Format-Number $candidate.validation_to_train_mae_ratio) |")
}
$markdown.Add('')
$markdown.Add("Validation baseline MAE: $(Format-Number $validationBaseline.metrics.mae). No Ridge candidate beats it.")
$markdown.Add('')
$markdown.Add('## C. Expanding-window results')
$markdown.Add('')
$markdown.Add('| Alpha | Fixed validation MAE | Expanding MAE | Expanding WAPE | Expanding RMSE | Expanding bias |')
$markdown.Add('|---:|---:|---:|---:|---:|---:|')
foreach ($candidate in $candidateResults) {
    $markdown.Add("| $(Format-Number $candidate.alpha) | $(Format-Number $candidate.validation_mae) | $(Format-Number $candidate.expanding_validation_mae) | $(Format-Number $candidate.expanding_validation_wape_percent)% | $(Format-Number $candidate.expanding_validation_rmse) | $(Format-Number $candidate.expanding_validation_bias) |")
}
$markdown.Add('')
$markdown.Add('Each expanding prediction refits its scaler and Ridge model using only the prefix available before that target week.')
$markdown.Add('')
$markdown.Add('## D. Overfitting analysis')
$markdown.Add('')
$markdown.Add("Selected diagnostic candidate train MAE $(Format-Number $selected.train_mae) versus validation MAE $(Format-Number $selected.validation_mae), ratio $(Format-Number $selected.validation_to_train_mae_ratio).")
$markdown.Add('The gap is substantial and is consistent with weak generalization under the observed level shift and small training set; it is not assigned a business cause.')
$markdown.Add('')
$markdown.Add('## E. Selected model')
$markdown.Add('')
$markdown.Add("Validation-only selection: **$($selected.candidate_id)**. It is selected as the best Ridge diagnostic candidate, not as an accepted production model.")
$markdown.Add("Validation relative MAE improvement versus baseline: $(Format-Number $validationImprovement)%.")
$markdown.Add('')
$markdown.Add('## F. Untouched test results')
$markdown.Add('')
$markdown.Add('| Evaluation | MAE | WAPE | RMSE | Bias | Median AE |')
$markdown.Add('|---|---:|---:|---:|---:|---:|')
$markdown.Add("| Selected Ridge | $(Format-Number $testMetrics.mae) | $(Format-Number $testMetrics.wape_percent)% | $(Format-Number $testMetrics.rmse) | $(Format-Number $testMetrics.mean_signed_error) | $(Format-Number $testMetrics.median_absolute_error) |")
$markdown.Add("| 4-week baseline | $(Format-Number $testBaseline.metrics.mae) | $(Format-Number $testBaseline.metrics.wape_percent)% | $(Format-Number $testBaseline.metrics.rmse) | $(Format-Number $testBaseline.metrics.mean_signed_error) | $(Format-Number $testBaseline.metrics.median_absolute_error) |")
$markdown.Add('')
$markdown.Add('## G. Baseline vs model')
$markdown.Add('')
$markdown.Add("- Absolute MAE improvement: $(Format-Number $testAbsoluteMaeImprovement)")
$markdown.Add("- Relative MAE improvement: $(Format-Number $testRelativeMaeImprovement)%")
$markdown.Add("- WAPE improvement: $(Format-Number $testWapeImprovement) percentage points")
$markdown.Add("- RMSE improvement: $(Format-Number $testRmseImprovement)")
$markdown.Add('Negative improvement means the model is worse than the baseline.')
$markdown.Add('')
$markdown.Add('## H. Error analysis')
$markdown.Add('')
$markdown.Add("- Model wins: $modelWins weeks; baseline wins: $baselineWins weeks; ties: $ties.")
$markdown.Add("- Underpredictions: $underPredictions; overpredictions: $overPredictions.")
$markdown.Add("- Target means are train $(Format-Number $trainTargetMean), validation $(Format-Number $validationTargetMean), and test $(Format-Number $testTargetMean). Test bias $(Format-Number $testMetrics.mean_signed_error) and 48 underpredictions show that the selected Ridge does not fully track the elevated test level.")
$markdown.Add('')
$markdown.Add('| Worst week | Actual | Predicted | Absolute error | Baseline error |')
$markdown.Add('|---|---:|---:|---:|---:|')
foreach ($errorRow in $worstTestWeeks) {
    $markdown.Add("| $($errorRow.week) | $(Format-Number $errorRow.actual) | $(Format-Number $errorRow.prediction) | $(Format-Number $errorRow.absolute_error) | $(Format-Number $errorRow.baseline_absolute_error) |")
}
$markdown.Add('')
$markdown.Add('| Largest change week | Previous-to-actual change | Actual | Predicted | Absolute error |')
$markdown.Add('|---|---:|---:|---:|---:|')
foreach ($changeRow in $largestChanges) {
    $markdown.Add("| $($changeRow.week) | $(Format-Number $changeRow.week_over_week_change) | $(Format-Number $changeRow.actual) | $(Format-Number $changeRow.prediction) | $(Format-Number $changeRow.absolute_error) |")
}
$markdown.Add('')
$markdown.Add('No business cause is inferred for spike/drop weeks.')
$markdown.Add('')
$markdown.Add('## I. Interpretability')
$markdown.Add('')
$markdown.Add('| Feature | Standardized coefficient |')
$markdown.Add('|---|---:|')
foreach ($coefficient in $coefficientRanking) {
    $markdown.Add("| $($coefficient.feature) | $(Format-Number $coefficient.standardized_coefficient) |")
}
$markdown.Add('')
$markdown.Add('Coefficient magnitude is not causality.')
$markdown.Add('')
$markdown.Add('## J. Automated tests')
$markdown.Add('')
$markdown.Add('Run `tests/Run-Tests.ps1` followed by `tests/Run-ModelTests.ps1`.')
$markdown.Add('')
$markdown.Add('## K. Created / changed artifacts')
$markdown.Add('')
$markdown.Add('- Versioned training config, deterministic Ridge implementation, selection lock, JSON/Markdown evaluation, and model tests.')
$markdown.Add("- Accepted model artifact saved: $artifactSaved")
$markdown.Add('')
$markdown.Add('## L. Database write status')
$markdown.Add('')
$markdown.Add('Training consumes the previously generated aggregate feature CSV and performs no database access or write.')
$markdown.Add('')
$markdown.Add('## M. Offline ML feasibility')
$markdown.Add('')
$markdown.Add("**$offlineFeasibility**")
$markdown.Add('')
$markdown.Add('## N. Production inference status')
$markdown.Add('')
$markdown.Add('**NO-GO** - data refresh and as-of availability contracts remain absent.')
$markdown.Add('')
$markdown.Add('## O. Known limitations')
$markdown.Add('')
$markdown.Add('- Only 84 evaluable train targets are available.')
$markdown.Add('- A strong late-2024/2025 level shift remains.')
$markdown.Add('- Random Forest and Gradient Boosting were not available without adding a runtime dependency.')
$markdown.Add('- The target is historical record volume, not failure, health, or maintenance outcome.')

$reportMarkdownPath = Join-Path $reportsRoot 'model-training-evaluation-v1.md'
$markdown -join "`r`n" | Set-Content -LiteralPath $reportMarkdownPath -Encoding UTF8

Write-Host "Validation-only selected candidate: $($selected.candidate_id)."
Write-Host "Validation MAE: $([Math]::Round($selected.validation_mae, 2)); baseline: $([Math]::Round($validationBaseline.metrics.mae, 2))."
Write-Host "Untouched test MAE: $([Math]::Round($testMetrics.mae, 2)); baseline: $([Math]::Round($testBaseline.metrics.mae, 2))."
Write-Host "Artifact saved: $artifactSaved. Offline ML feasibility: $offlineFeasibility. Production: NO-GO."
