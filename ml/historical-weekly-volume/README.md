# Historical weekly record-volume pilot

This directory contains the versioned, offline retrospective baseline for:

> Predict the total `analytics.HistoricalWorkOrders` record count in the next
> complete Monday-to-Monday calendar week using only earlier weekly totals.

It does not train a complex model, serve production inference, use current
`WorkOrders`, or estimate failures, asset health, RUL, MTBF, or MTTR.

## Runtime

The repository host has Windows PowerShell 5.1 and the SQL Server
`Invoke-Sqlcmd` command. No Python runtime is installed, so this minimal pilot
uses those existing dependencies rather than adding a runtime or ML framework.

## Run

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\ml\historical-weekly-volume\scripts\Invoke-BaselineEvaluation.ps1
powershell -ExecutionPolicy Bypass -File .\ml\historical-weekly-volume\tests\Run-Tests.ps1
```

The default connection is local integrated security with
`ApplicationIntent=ReadOnly`. Override it without storing credentials in the
repository:

```powershell
$env:SMARTFACILITY_ML_CONNECTION_STRING = '<read-only SQL Server connection string>'
powershell -ExecutionPolicy Bypass -File .\ml\historical-weekly-volume\scripts\Invoke-BaselineEvaluation.ps1
```

The SQL scripts are guarded against DML/DDL tokens and contain only read-only
queries. The pipeline stops before feature or metric generation when the locked
dataset contract does not match:

- 219 complete weeks
- 167,142 included source rows
- two zero-filled weeks
- first week starting 2022-05-23
- last week ending 2026-08-02

Generated aggregate/feature CSVs are reproducible and ignored by Git under
`artifacts/generated/`. The versioned manifest and evaluation reports are kept
under `reports/`.

## Leakage boundary

Each feature row is keyed by its target week. `lag_1` is the immediately prior
completed week. Every rolling feature is calculated after a one-week shift, so
the target week's count never participates. No global scaling, fitting, random
split, Discipline feature, `CreatedAt`, import metadata, current WorkOrder, or
SCADA data is used.

The baseline selected for comparison is chosen with validation MAE only. Test
metrics are reported after that selection and are not used to choose a baseline.
