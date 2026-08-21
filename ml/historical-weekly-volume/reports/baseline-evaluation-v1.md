# Historical Weekly Volume Baseline Evaluation v1

Generated UTC: 2026-08-21T10:36:39Z

## Dataset contract

- Source: `analytics.HistoricalWorkOrders`
- Source rows included: 167142 of 167143
- Complete weekly buckets: 219
- Zero weeks: 2
- Event-time range: 2022-05-18T00:00:00 to 2026-08-02T00:00:00
- Dataset calendar range: 2022-05-23 to 2026-08-02
- Cutoff exclusive: 2026-08-03T00:00:00

## Temporal splits

| Split | Calendar buckets | Evaluable rows | Target mean | Target std | Min | Max |
|---|---:|---:|---:|---:|---:|---:|
| train | 136 | 84 | 560.49 | 289.17 | 124.00 | 1432.00 |
| validation | 26 | 26 | 1172.85 | 216.49 | 706.00 | 1502.00 |
| test | 57 | 57 | 1036.33 | 179.67 | 586.00 | 1434.00 |

## Validation baselines

| Baseline | MAE | WAPE | RMSE | Mean signed error | Median absolute error |
|---|---:|---:|---:|---:|---:|
| previous_week_naive | 154.69 | 13.19% | 199.17 | 7.00 | 115.50 |
| moving_average_4 | 118.47 | 10.10% | 176.14 | 34.16 | 72.88 |
| seasonal_naive_52 | 794.69 | 67.76% | 820.84 | -794.69 | 761.50 |

Baseline selected by validation MAE only: **moving_average_4**

## Test baselines

| Baseline | MAE | WAPE | RMSE | Mean signed error | Median absolute error |
|---|---:|---:|---:|---:|---:|
| previous_week_naive | 164.75 | 15.90% | 209.95 | 2.26 | 141.00 |
| moving_average_4 | 120.96 | 11.67% | 163.48 | -3.49 | 96.75 |
| seasonal_naive_52 | 289.19 | 27.91% | 341.10 | -46.88 | 289.00 |

## Drift and error observations

- Evaluable target mean: train 560.49, validation 1172.85, test 1036.33.
- Validation mean is 109.25% above the train evaluable mean. This confirms the late-2024/2025 level shift; it is not evidence of a failure-rate change.
- Test mean is 11.64% below validation mean, so the elevated level persists but moderates.
- Validation/test zero targets: 0 / 0. The two calendar zero-weeks are in the early training history, so they do not directly lower validation/test denominators.
- Positive mean signed error means overprediction; negative means underprediction.
- These are record-volume errors, not failure, health, reliability, or maintenance-outcome estimates.

| Baseline | Test minus validation MAE | Test minus validation WAPE points |
|---|---:|---:|
| previous_week_naive | 10.06 | 2.71 |
| moving_average_4 | 2.49 | 1.57 |
| seasonal_naive_52 | -505.50 | -39.85 |

## Expanding-window equivalence

- previous_week_naive: max absolute difference 0.00
- moving_average_4: max absolute difference 0.00
- seasonal_naive_52: max absolute difference 0.00

All three baselines are stateless and each prediction was recomputed from its historical prefix only.

## Worst 10 test weeks - previous_week_naive

| Week | Actual | Prediction | Absolute error |
|---|---:|---:|---:|
| 2026-06-01 | 1228 | 586.00 | 642.00 |
| 2026-05-25 | 586 | 1045.00 | 459.00 |
| 2026-03-02 | 1426 | 975.00 | 451.00 |
| 2026-01-05 | 1224 | 819.00 | 405.00 |
| 2026-03-16 | 893 | 1269.00 | 376.00 |
| 2026-07-13 | 938 | 1310.00 | 372.00 |
| 2026-05-04 | 1374 | 1041.00 | 333.00 |
| 2026-02-09 | 921 | 1213.00 | 292.00 |
| 2026-03-23 | 1152 | 893.00 | 259.00 |
| 2026-07-20 | 1186 | 938.00 | 248.00 |

## Worst 10 test weeks - moving_average_4

| Week | Actual | Prediction | Absolute error |
|---|---:|---:|---:|
| 2026-05-25 | 586 | 1184.00 | 598.00 |
| 2026-03-02 | 1426 | 1052.75 | 373.25 |
| 2026-04-13 | 1434 | 1114.25 | 319.75 |
| 2026-03-16 | 893 | 1193.00 | 300.00 |
| 2026-06-08 | 1332 | 1033.75 | 298.25 |
| 2025-08-25 | 642 | 912.75 | 270.75 |
| 2026-01-05 | 1224 | 975.00 | 249.00 |
| 2026-07-13 | 938 | 1182.25 | 244.25 |
| 2026-04-27 | 1041 | 1272.75 | 231.75 |
| 2026-06-15 | 1272 | 1047.75 | 224.25 |

## Worst 10 test weeks - seasonal_naive_52

| Week | Actual | Prediction | Absolute error |
|---|---:|---:|---:|
| 2025-07-07 | 1093 | 370.00 | 723.00 |
| 2025-07-28 | 995 | 335.00 | 660.00 |
| 2025-07-14 | 913 | 283.00 | 630.00 |
| 2025-06-30 | 967 | 375.00 | 592.00 |
| 2025-07-21 | 948 | 393.00 | 555.00 |
| 2026-03-16 | 893 | 1418.00 | 525.00 |
| 2026-03-30 | 1204 | 706.00 | 498.00 |
| 2026-05-25 | 586 | 1076.00 | 490.00 |
| 2025-08-11 | 964 | 487.00 | 477.00 |
| 2026-06-08 | 1332 | 863.00 | 469.00 |
