# Historical weekly record-volume pilot

This directory contains the versioned, offline retrospective baseline for:

> Predict the total `analytics.HistoricalWorkOrders` record count in the next
> complete Monday-to-Monday calendar week using only earlier weekly totals.

It does not train a complex model, serve production inference, use current
`WorkOrders`, or estimate failures, asset health, RUL, MTBF, or MTTR.

## Runtime

The extraction/baseline stages require Windows PowerShell and the SQL Server
`Invoke-Sqlcmd` command. The final controlled comparison requires Python 3.12
and uses the project-local environment documented below.

## Run

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\ml\historical-weekly-volume\scripts\Invoke-BaselineEvaluation.ps1
powershell -ExecutionPolicy Bypass -File .\ml\historical-weekly-volume\tests\Run-Tests.ps1
powershell -ExecutionPolicy Bypass -File .\ml\historical-weekly-volume\scripts\Invoke-ModelTraining.ps1
powershell -ExecutionPolicy Bypass -File .\ml\historical-weekly-volume\tests\Run-ModelTests.ps1
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

## Offline model training

The training stage evaluates five predefined Ridge alpha values. Its scaler is
fit only on the training prefix for fixed validation and refit only on each
available historical prefix for expanding-window validation. A validation-only
selection lock is written before the test rows are materialized. The selected
candidate is then refit once on train plus validation and evaluated once on the
untouched test period.

In the initial PowerShell-only stage, Random Forest and Gradient Boosting were
unavailable because Python packages were not yet installed. That stage did not
substitute a handwritten tree implementation. The separately versioned final
Python comparison below uses only the dependencies explicitly authorized for
that controlled round. A model file is saved only if all artifact gates pass.

## Final controlled Python comparison

The final bounded comparison uses a project-local, ignored `.venv` and versions
its complete dependency set in `requirements-python.lock`. If Python is not on
`PATH`, set `SMARTFACILITY_PYTHON` to the local Python 3.12 executable without
committing that machine-specific path:

```powershell
$env:SMARTFACILITY_PYTHON = '<path to Python 3.12 executable>'
& $env:SMARTFACILITY_PYTHON -m venv .\ml\historical-weekly-volume\.venv
.\ml\historical-weekly-volume\.venv\Scripts\python.exe -m pip install -r .\ml\historical-weekly-volume\requirements-python.lock
.\ml\historical-weekly-volume\.venv\Scripts\python.exe .\ml\historical-weekly-volume\python\train_models.py
.\ml\historical-weekly-volume\.venv\Scripts\python.exe -m unittest discover -s .\ml\historical-weekly-volume\python\tests -v
```

The Python selection stage evaluates only train and validation. It writes a
selection proof before any candidate test evaluation. If no candidate passes
the locked validation, expanding-window, and overfitting gates, the test remains
unread for evaluation and no model artifact is produced.
