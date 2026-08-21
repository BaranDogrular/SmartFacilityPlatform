# Python Controlled Model Comparison v1

Generated UTC: 2026-08-21T12:42:48Z

## A. Python environment

- python: 3.12.6
- numpy: 2.5.2
- pandas: 3.0.5
- scikit_learn: 1.9.0
- joblib: 1.5.3

## B. Candidate models

- Total predefined configurations: 16
- ElasticNet: train-only StandardScaler.
- RandomForestRegressor and HistGradientBoostingRegressor: no scaling.
- No randomized search or additional model family was used.

## C. Validation results

| Candidate | Family | Train MAE | Validation MAE | WAPE | RMSE | Bias | Median AE | Val/train |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| enet-a0.01-l10.5 | elastic_net | 71.89 | 162.81 | 13.88% | 190.94 | -52.65 | 163.29 | 2.26 |
| enet-a0.01-l10.1 | elastic_net | 72.87 | 165.70 | 14.13% | 191.87 | -79.66 | 154.74 | 2.27 |
| hgb-i150-r0.03-l7-m10-reg1 | hist_gradient_boosting | 32.05 | 176.33 | 15.03% | 213.61 | -10.30 | 168.25 | 5.50 |
| hgb-i100-r0.1-l7-m10-reg5 | hist_gradient_boosting | 19.70 | 177.84 | 15.16% | 211.88 | 39.17 | 155.09 | 9.03 |
| hgb-i100-r0.05-l15-m10-reg1 | hist_gradient_boosting | 29.68 | 177.94 | 15.17% | 213.37 | 1.94 | 164.52 | 6.00 |
| hgb-i100-r0.05-l7-m10-reg1 | hist_gradient_boosting | 29.70 | 177.94 | 15.17% | 213.37 | 1.94 | 164.52 | 5.99 |
| rf-n200-dnull-l5 | random_forest | 53.09 | 180.20 | 15.36% | 216.11 | 3.84 | 178.45 | 3.39 |
| rf-n200-d5-l5 | random_forest | 53.49 | 180.34 | 15.38% | 216.21 | 3.86 | 178.99 | 3.37 |
| enet-a0.001-l10.1 | elastic_net | 70.40 | 181.64 | 15.49% | 212.25 | 13.57 | 168.91 | 2.58 |
| rf-n100-d3-l5 | random_forest | 59.90 | 184.09 | 15.70% | 220.32 | -6.05 | 185.66 | 3.07 |
| rf-n200-d5-l3 | random_forest | 41.15 | 185.92 | 15.85% | 228.84 | 84.93 | 146.33 | 4.52 |
| enet-a0.1-l10.5 | elastic_net | 75.29 | 187.49 | 15.99% | 217.10 | -144.32 | 191.64 | 2.49 |
| rf-n200-d3-l3 | random_forest | 50.34 | 189.09 | 16.12% | 232.65 | 90.16 | 149.90 | 3.76 |
| enet-a0.001-l10.5 | elastic_net | 70.44 | 190.39 | 16.23% | 219.70 | 26.20 | 157.73 | 2.70 |
| enet-a0.1-l10.1 | elastic_net | 75.71 | 192.53 | 16.42% | 223.37 | -154.00 | 191.19 | 2.54 |
| hgb-i100-r0.05-l7-m20-reg1 | hist_gradient_boosting | 62.62 | 244.16 | 20.82% | 293.24 | -201.03 | 202.87 | 3.90 |

Locked validation baseline MAE: 118.47.

## D. Expanding-window results

| Candidate | Fixed MAE | Expanding MAE | WAPE | RMSE | Bias |
|---|---:|---:|---:|---:|---:|
| rf-n200-dnull-l5 | 180.20 | 133.71 | 11.40% | 186.65 | 8.04 |
| hgb-i150-r0.03-l7-m10-reg1 | 176.33 | 146.86 | 12.52% | 194.53 | 10.10 |
| enet-a0.01-l10.1 | 165.70 | 153.69 | 13.10% | 182.84 | -26.99 |
| enet-a0.01-l10.5 | 162.81 | 155.41 | 13.25% | 184.14 | -25.49 |

Each expanding prediction refits only on the prefix available before its validation target.

## E. Overfitting check

- enet-a0.01-l10.5: train MAE 71.89, validation MAE 162.81, ratio 2.26.
- enet-a0.01-l10.1: train MAE 72.87, validation MAE 165.70, ratio 2.27.
- hgb-i150-r0.03-l7-m10-reg1: train MAE 32.05, validation MAE 176.33, ratio 5.50.
- hgb-i100-r0.1-l7-m10-reg5: train MAE 19.70, validation MAE 177.84, ratio 9.03.
- hgb-i100-r0.05-l15-m10-reg1: train MAE 29.68, validation MAE 177.94, ratio 6.00.

## F. Selected model or none

**FINAL MODEL = NONE**

## G. Untouched test result

**NOT_EVALUATED_VALIDATION_GATE_FAILED**. No Python candidate was evaluated on test.

## H. Baseline comparison

Validation baseline: MAE 118.47, WAPE 10.10%, RMSE 176.14.

## I. Created artifacts

- Versioned Python config, lock file, training script, selection proof, report, and tests.
- Model artifact saved: False.

## J. Automated tests

Run `python -m unittest discover -s python/tests -v` from this pilot directory.

## K. Final verdict

**ML MODEL ACCEPTED = NO**
**OFFLINE ML FEASIBILITY = NO-GO**
**PRODUCTION INFERENCE = NO-GO**
