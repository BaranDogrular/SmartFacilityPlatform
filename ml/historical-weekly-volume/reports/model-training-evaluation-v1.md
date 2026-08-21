# Offline ML Model Training and Baseline Comparison v1

Generated UTC: 2026-08-21T12:17:48Z

## A. Candidate models

- Ridge Regression: evaluated with five predefined alpha values.
- Random Forest Regressor: unavailable; Python/scikit-learn is not installed.
- Gradient Boosting Regressor: unavailable; Python/scikit-learn is not installed.
- No dependency, AutoML, XGBoost, LightGBM, or neural-network framework was added.

## B. Train / validation results

| Alpha | Train MAE | Validation MAE | WAPE | RMSE | Bias | Median AE | Val/train ratio |
|---:|---:|---:|---:|---:|---:|---:|---:|
| 0.10 | 70.46 | 176.77 | 15.07% | 208.08 | 5.66 | 175.78 | 2.51 |
| 1.00 | 73.35 | 168.03 | 14.33% | 194.32 | -92.27 | 159.80 | 2.29 |
| 10.00 | 75.89 | 194.78 | 16.61% | 225.62 | -157.16 | 191.52 | 2.57 |
| 100.00 | 80.90 | 190.04 | 16.20% | 217.41 | -141.07 | 182.72 | 2.35 |
| 1000.00 | 132.13 | 322.69 | 27.51% | 358.74 | -305.28 | 332.07 | 2.44 |

Validation baseline MAE: 118.47. No Ridge candidate beats it.

## C. Expanding-window results

| Alpha | Fixed validation MAE | Expanding MAE | Expanding WAPE | Expanding RMSE | Expanding bias |
|---:|---:|---:|---:|---:|---:|
| 0.10 | 176.77 | 155.21 | 13.23% | 186.93 | -26.95 |
| 1.00 | 168.03 | 153.28 | 13.07% | 182.58 | -28.40 |
| 10.00 | 194.78 | 143.65 | 12.25% | 177.16 | -29.07 |
| 100.00 | 190.04 | 137.85 | 11.75% | 180.78 | -22.38 |
| 1000.00 | 322.69 | 258.07 | 22.00% | 296.04 | -213.87 |

Each expanding prediction refits its scaler and Ridge model using only the prefix available before that target week.

## D. Overfitting analysis

Selected diagnostic candidate train MAE 73.35 versus validation MAE 168.03, ratio 2.29.
The gap is substantial and is consistent with weak generalization under the observed level shift and small training set; it is not assigned a business cause.

## E. Selected model

Validation-only selection: **ridge-alpha-1**. It is selected as the best Ridge diagnostic candidate, not as an accepted production model.
Validation relative MAE improvement versus baseline: -41.84%.

## F. Untouched test results

| Evaluation | MAE | WAPE | RMSE | Bias | Median AE |
|---|---:|---:|---:|---:|---:|
| Selected Ridge | 211.51 | 20.41% | 259.28 | -176.37 | 187.51 |
| 4-week baseline | 120.96 | 11.67% | 163.48 | -3.49 | 96.75 |

## G. Baseline vs model

- Absolute MAE improvement: -90.55
- Relative MAE improvement: -74.85%
- WAPE improvement: -8.74 percentage points
- RMSE improvement: -95.81
Negative improvement means the model is worse than the baseline.

## H. Error analysis

- Model wins: 21 weeks; baseline wins: 36 weeks; ties: 0.
- Underpredictions: 48; overpredictions: 9.
- Target means are train 560.49, validation 1172.85, and test 1036.33. Test bias -176.37 and 48 underpredictions show that the selected Ridge does not fully track the elevated test level.

| Worst week | Actual | Predicted | Absolute error | Baseline error |
|---|---:|---:|---:|---:|
| 2026-03-02 | 1426.00 | 739.26 | 686.74 | 373.25 |
| 2026-04-13 | 1434.00 | 874.15 | 559.85 | 319.75 |
| 2026-01-05 | 1224.00 | 693.27 | 530.73 | 249.00 |
| 2026-05-04 | 1374.00 | 858.82 | 515.18 | 142.00 |
| 2026-02-02 | 1213.00 | 741.30 | 471.70 | 153.75 |
| 2026-07-06 | 1310.00 | 933.03 | 376.97 | 122.25 |
| 2025-12-22 | 1044.00 | 693.51 | 350.49 | 28.75 |
| 2026-06-01 | 1228.00 | 882.08 | 345.92 | 157.75 |
| 2026-02-16 | 1102.00 | 758.96 | 343.04 | 69.25 |
| 2025-12-01 | 1036.00 | 694.93 | 341.07 | 117.50 |

| Largest change week | Previous-to-actual change | Actual | Predicted | Absolute error |
|---|---:|---:|---:|---:|
| 2026-06-01 | 642.00 | 1228.00 | 882.08 | 345.92 |
| 2026-05-25 | -459.00 | 586.00 | 847.04 | 261.04 |
| 2026-03-02 | 451.00 | 1426.00 | 739.26 | 686.74 |
| 2026-01-05 | 405.00 | 1224.00 | 693.27 | 530.73 |
| 2026-03-16 | -376.00 | 893.00 | 945.11 | 52.11 |
| 2026-07-13 | -372.00 | 938.00 | 1053.15 | 115.15 |
| 2026-05-04 | 333.00 | 1374.00 | 858.82 | 515.18 |
| 2026-02-09 | -292.00 | 921.00 | 847.85 | 73.15 |
| 2026-03-23 | 259.00 | 1152.00 | 870.91 | 281.09 |
| 2026-07-20 | 248.00 | 1186.00 | 936.36 | 249.64 |

No business cause is inferred for spike/drop weeks.

## I. Interpretability

| Feature | Standardized coefficient |
|---|---:|
| lag_1 | 125.10 |
| rolling_std_26 | 107.28 |
| rolling_mean_4 | 77.52 |
| rolling_median_13 | -67.59 |
| rolling_median_26 | 59.59 |
| rolling_median_4 | -49.67 |
| rolling_mean_13 | 41.94 |
| rolling_mean_26 | -37.54 |
| iso_week_sin | -36.53 |
| iso_week_cos | 33.40 |
| lag_2 | 32.77 |
| lag_13 | 32.43 |
| rolling_std_4 | 30.87 |
| lag_52 | -25.75 |
| rolling_std_13 | 24.03 |
| iso_week | -20.22 |
| lag_4 | 8.52 |

Coefficient magnitude is not causality.

## J. Automated tests

Run `tests/Run-Tests.ps1` followed by `tests/Run-ModelTests.ps1`.

## K. Created / changed artifacts

- Versioned training config, deterministic Ridge implementation, selection lock, JSON/Markdown evaluation, and model tests.
- Accepted model artifact saved: False

## L. Database write status

Training consumes the previously generated aggregate feature CSV and performs no database access or write.

## M. Offline ML feasibility

**NO-GO**

## N. Production inference status

**NO-GO** - data refresh and as-of availability contracts remain absent.

## O. Known limitations

- Only 84 evaluable train targets are available.
- A strong late-2024/2025 level shift remains.
- Random Forest and Gradient Boosting were not available without adding a runtime dependency.
- The target is historical record volume, not failure, health, or maintenance outcome.
