Set-StrictMode -Version Latest

$script:InvariantCulture = [System.Globalization.CultureInfo]::InvariantCulture

function Assert-ReadOnlySql {
    param([Parameter(Mandatory = $true)][string]$Sql)

    $forbidden = '(?im)\b(INSERT|UPDATE|DELETE|MERGE|TRUNCATE|DROP|ALTER|CREATE|GRANT|REVOKE|EXEC(?:UTE)?)\b'
    if ($Sql -match $forbidden) {
        throw "SQL contains a forbidden non-read-only token: $($Matches[1])"
    }
}

function Get-SampleStandardDeviation {
    param([Parameter(Mandatory = $true)][double[]]$Values)

    if ($Values.Count -le 1) {
        return 0.0
    }

    $mean = ($Values | Measure-Object -Average).Average
    $sumSquares = 0.0
    foreach ($value in $Values) {
        $sumSquares += [Math]::Pow($value - $mean, 2)
    }

    return [Math]::Sqrt($sumSquares / ($Values.Count - 1))
}

function Get-Median {
    param([Parameter(Mandatory = $true)][double[]]$Values)

    if ($Values.Count -eq 0) {
        throw 'Median requires at least one value.'
    }

    $sorted = @($Values | Sort-Object)
    $middle = [int][Math]::Floor($sorted.Count / 2)
    if (($sorted.Count % 2) -eq 1) {
        return [double]$sorted[$middle]
    }

    return ([double]$sorted[$middle - 1] + [double]$sorted[$middle]) / 2.0
}

function Get-IsoCalendarParts {
    param([Parameter(Mandatory = $true)][datetime]$Date)

    $calendar = $script:InvariantCulture.Calendar
    $week = $calendar.GetWeekOfYear(
        $Date,
        [System.Globalization.CalendarWeekRule]::FirstFourDayWeek,
        [DayOfWeek]::Monday)
    $year = $Date.Year

    if ($Date.Month -eq 1 -and $week -ge 52) {
        $year--
    }
    elseif ($Date.Month -eq 12 -and $week -eq 1) {
        $year++
    }

    [pscustomobject]@{ Year = $year; Week = $week }
}

function Get-SplitName {
    param([Parameter(Mandatory = $true)][datetime]$WeekStart)

    if ($WeekStart -ge [datetime]'2022-05-23' -and $WeekStart -le [datetime]'2024-12-23') {
        return 'train'
    }
    if ($WeekStart -ge [datetime]'2024-12-30' -and $WeekStart -le [datetime]'2025-06-23') {
        return 'validation'
    }
    if ($WeekStart -ge [datetime]'2025-06-30' -and $WeekStart -le [datetime]'2026-07-27') {
        return 'test'
    }

    throw "Week $($WeekStart.ToString('yyyy-MM-dd')) is outside the locked temporal splits."
}

function Get-WindowValues {
    param(
        [Parameter(Mandatory = $true)][long[]]$Counts,
        [Parameter(Mandatory = $true)][int]$TargetIndex,
        [Parameter(Mandatory = $true)][int]$Window
    )

    # Shift first: for target index i, use [i-window, i), never count[i].
    $values = New-Object double[] $Window
    for ($offset = 0; $offset -lt $Window; $offset++) {
        $values[$offset] = [double]$Counts[$TargetIndex - $Window + $offset]
    }
    return $values
}

function New-HistoricalWeeklyFeatureRows {
    param([Parameter(Mandatory = $true)][object[]]$WeeklyRows)

    if ($WeeklyRows.Count -lt 53) {
        throw 'At least 53 weekly buckets are required for a target plus 52-week history.'
    }

    $dates = New-Object datetime[] $WeeklyRows.Count
    $counts = New-Object long[] $WeeklyRows.Count
    for ($index = 0; $index -lt $WeeklyRows.Count; $index++) {
        $dates[$index] = [datetime]$WeeklyRows[$index].WeekStart
        $counts[$index] = [long]$WeeklyRows[$index].HistoricalCount
        if ($index -gt 0 -and $dates[$index] -ne $dates[$index - 1].AddDays(7)) {
            throw "Weekly series is not contiguous at $($dates[$index].ToString('yyyy-MM-dd'))."
        }
    }

    $features = New-Object System.Collections.Generic.List[object]
    for ($targetIndex = 52; $targetIndex -lt $WeeklyRows.Count; $targetIndex++) {
        $rolling4 = Get-WindowValues -Counts $counts -TargetIndex $targetIndex -Window 4
        $rolling13 = Get-WindowValues -Counts $counts -TargetIndex $targetIndex -Window 13
        $rolling26 = Get-WindowValues -Counts $counts -TargetIndex $targetIndex -Window 26
        $iso = Get-IsoCalendarParts -Date $dates[$targetIndex]
        $angle = 2.0 * [Math]::PI * ($iso.Week - 1) / 52.0

        $features.Add([pscustomobject][ordered]@{
            target_week_start = $dates[$targetIndex]
            target_week_end_exclusive = $dates[$targetIndex].AddDays(7)
            prediction_cutoff_exclusive = $dates[$targetIndex]
            split = Get-SplitName -WeekStart $dates[$targetIndex]
            actual = $counts[$targetIndex]
            lag_1 = $counts[$targetIndex - 1]
            lag_2 = $counts[$targetIndex - 2]
            lag_4 = $counts[$targetIndex - 4]
            lag_13 = $counts[$targetIndex - 13]
            lag_52 = $counts[$targetIndex - 52]
            rolling_mean_4 = ($rolling4 | Measure-Object -Average).Average
            rolling_median_4 = Get-Median -Values $rolling4
            rolling_std_4 = Get-SampleStandardDeviation -Values $rolling4
            rolling_mean_13 = ($rolling13 | Measure-Object -Average).Average
            rolling_median_13 = Get-Median -Values $rolling13
            rolling_std_13 = Get-SampleStandardDeviation -Values $rolling13
            rolling_mean_26 = ($rolling26 | Measure-Object -Average).Average
            rolling_median_26 = Get-Median -Values $rolling26
            rolling_std_26 = Get-SampleStandardDeviation -Values $rolling26
            iso_year = $iso.Year
            iso_week = $iso.Week
            iso_week_sin = [Math]::Sin($angle)
            iso_week_cos = [Math]::Cos($angle)
        })
    }

    return $features.ToArray()
}

function Get-ForecastMetrics {
    param(
        [Parameter(Mandatory = $true)][object[]]$Rows,
        [Parameter(Mandatory = $true)][string]$PredictionProperty
    )

    if ($Rows.Count -eq 0) {
        throw 'Metrics require at least one row.'
    }

    $absoluteErrors = New-Object double[] $Rows.Count
    $squaredErrorSum = 0.0
    $signedErrorSum = 0.0
    $actualAbsoluteSum = 0.0

    for ($index = 0; $index -lt $Rows.Count; $index++) {
        $actual = [double]$Rows[$index].actual
        $prediction = [double]$Rows[$index].$PredictionProperty
        $signedError = $prediction - $actual
        $absoluteErrors[$index] = [Math]::Abs($signedError)
        $squaredErrorSum += $signedError * $signedError
        $signedErrorSum += $signedError
        $actualAbsoluteSum += [Math]::Abs($actual)
    }

    $wape = $null
    if ($actualAbsoluteSum -gt 0.0) {
        $wape = 100.0 * ($absoluteErrors | Measure-Object -Sum).Sum / $actualAbsoluteSum
    }

    [pscustomobject][ordered]@{
        row_count = $Rows.Count
        mae = ($absoluteErrors | Measure-Object -Average).Average
        wape_percent = $wape
        rmse = [Math]::Sqrt($squaredErrorSum / $Rows.Count)
        mean_signed_error = $signedErrorSum / $Rows.Count
        median_absolute_error = Get-Median -Values $absoluteErrors
    }
}

function Get-BaselineDefinitions {
    @(
        [pscustomobject]@{ Name = 'previous_week_naive'; Property = 'lag_1' },
        [pscustomobject]@{ Name = 'moving_average_4'; Property = 'rolling_mean_4' },
        [pscustomobject]@{ Name = 'seasonal_naive_52'; Property = 'lag_52' }
    )
}

function Get-BaselineResults {
    param(
        [Parameter(Mandatory = $true)][object[]]$FeatureRows,
        [Parameter(Mandatory = $true)][ValidateSet('validation', 'test')][string]$Split
    )

    $splitRows = @($FeatureRows | Where-Object { $_.split -eq $Split })
    $results = New-Object System.Collections.Generic.List[object]
    foreach ($baseline in Get-BaselineDefinitions) {
        $metrics = Get-ForecastMetrics -Rows $splitRows -PredictionProperty $baseline.Property
        $errors = foreach ($row in $splitRows) {
            $prediction = [double]$row.($baseline.Property)
            [pscustomobject]@{
                week = $row.target_week_start.ToString('yyyy-MM-dd')
                actual = [long]$row.actual
                prediction = $prediction
                absolute_error = [Math]::Abs($prediction - [double]$row.actual)
            }
        }
        $worst = @($errors | Sort-Object @{ Expression = 'absolute_error'; Descending = $true }, week | Select-Object -First 10)
        $results.Add([pscustomobject][ordered]@{
            baseline = $baseline.Name
            prediction_property = $baseline.Property
            metrics = $metrics
            worst_weeks = $worst
        })
    }

    return $results.ToArray()
}

function Get-TargetStatistics {
    param([Parameter(Mandatory = $true)][object[]]$Rows)

    $values = [double[]]@($Rows | ForEach-Object { [double]$_.actual })
    [pscustomobject][ordered]@{
        mean = ($values | Measure-Object -Average).Average
        std = Get-SampleStandardDeviation -Values $values
        min = ($values | Measure-Object -Minimum).Minimum
        max = ($values | Measure-Object -Maximum).Maximum
    }
}

Export-ModuleMember -Function @(
    'Assert-ReadOnlySql',
    'Get-SampleStandardDeviation',
    'Get-Median',
    'Get-SplitName',
    'New-HistoricalWeeklyFeatureRows',
    'Get-ForecastMetrics',
    'Get-BaselineDefinitions',
    'Get-BaselineResults',
    'Get-TargetStatistics'
)
