from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import platform
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

os.environ.setdefault("LOKY_MAX_CPU_COUNT", "1")

import joblib
import numpy as np
import pandas as pd
import sklearn
from sklearn.base import RegressorMixin
from sklearn.ensemble import HistGradientBoostingRegressor, RandomForestRegressor
from sklearn.linear_model import ElasticNet
from sklearn.pipeline import Pipeline
from sklearn.preprocessing import StandardScaler


FEATURE_NAMES = [
    "lag_1",
    "lag_2",
    "lag_4",
    "lag_13",
    "lag_52",
    "rolling_mean_4",
    "rolling_median_4",
    "rolling_std_4",
    "rolling_mean_13",
    "rolling_median_13",
    "rolling_std_13",
    "rolling_mean_26",
    "rolling_median_26",
    "rolling_std_26",
    "iso_week",
    "iso_week_sin",
    "iso_week_cos",
]

FAMILY_COMPLEXITY = {
    "elastic_net": 0,
    "hist_gradient_boosting": 1,
    "random_forest": 2,
}


@dataclass(frozen=True)
class CandidateSpec:
    candidate_id: str
    family: str
    parameters: dict[str, Any]


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def metrics(actual: np.ndarray, predicted: np.ndarray) -> dict[str, float | None]:
    actual_values = np.asarray(actual, dtype=float)
    predicted_values = np.asarray(predicted, dtype=float)
    if actual_values.shape != predicted_values.shape or actual_values.size == 0:
        raise ValueError("Actual and prediction arrays must be non-empty and aligned.")
    if not np.isfinite(actual_values).all() or not np.isfinite(predicted_values).all():
        raise ValueError("Metrics require finite values.")
    errors = predicted_values - actual_values
    absolute_errors = np.abs(errors)
    denominator = float(np.abs(actual_values).sum())
    return {
        "row_count": int(actual_values.size),
        "mae": float(absolute_errors.mean()),
        "wape_percent": None if denominator == 0.0 else float(100.0 * absolute_errors.sum() / denominator),
        "rmse": float(np.sqrt(np.mean(np.square(errors)))),
        "mean_signed_error": float(errors.mean()),
        "median_absolute_error": float(np.median(absolute_errors)),
    }


def load_json(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8-sig"))


def candidate_specs(config: dict[str, Any]) -> list[CandidateSpec]:
    specs: list[CandidateSpec] = []
    for item in config["elasticNet"]:
        specs.append(CandidateSpec(item["id"], "elastic_net", dict(item)))
    for item in config["randomForest"]:
        specs.append(CandidateSpec(item["id"], "random_forest", dict(item)))
    for item in config["histGradientBoosting"]:
        specs.append(CandidateSpec(item["id"], "hist_gradient_boosting", dict(item)))
    return specs


def build_model(spec: CandidateSpec, random_state: int) -> RegressorMixin:
    params = spec.parameters
    if spec.family == "elastic_net":
        return Pipeline(
            [
                ("scaler", StandardScaler()),
                (
                    "model",
                    ElasticNet(
                        alpha=float(params["alpha"]),
                        l1_ratio=float(params["l1Ratio"]),
                        max_iter=50_000,
                        tol=1e-7,
                        selection="cyclic",
                        random_state=random_state,
                    ),
                ),
            ]
        )
    if spec.family == "random_forest":
        return RandomForestRegressor(
            n_estimators=int(params["nEstimators"]),
            max_depth=params["maxDepth"],
            min_samples_leaf=int(params["minSamplesLeaf"]),
            random_state=random_state,
            n_jobs=1,
        )
    if spec.family == "hist_gradient_boosting":
        return HistGradientBoostingRegressor(
            max_iter=int(params["maxIter"]),
            learning_rate=float(params["learningRate"]),
            max_leaf_nodes=int(params["maxLeafNodes"]),
            min_samples_leaf=int(params["minSamplesLeaf"]),
            l2_regularization=float(params["l2Regularization"]),
            early_stopping=False,
            random_state=random_state,
        )
    raise ValueError(f"Unsupported family: {spec.family}")


def frame_xy(frame: pd.DataFrame) -> tuple[np.ndarray, np.ndarray]:
    return frame[FEATURE_NAMES].to_numpy(dtype=float), frame["actual"].to_numpy(dtype=float)


def evaluate_fixed(
    spec: CandidateSpec,
    train: pd.DataFrame,
    validation: pd.DataFrame,
    random_state: int,
    baseline_mae: float,
) -> tuple[dict[str, Any], RegressorMixin]:
    train_x, train_y = frame_xy(train)
    validation_x, validation_y = frame_xy(validation)
    model = build_model(spec, random_state)
    model.fit(train_x, train_y)
    train_predictions = np.asarray(model.predict(train_x), dtype=float)
    validation_predictions = np.asarray(model.predict(validation_x), dtype=float)
    train_metrics = metrics(train_y, train_predictions)
    validation_metrics = metrics(validation_y, validation_predictions)
    if not np.isfinite(train_predictions).all() or not np.isfinite(validation_predictions).all():
        raise ValueError(f"Non-finite prediction from {spec.candidate_id}.")
    result = {
        "candidate_id": spec.candidate_id,
        "family": spec.family,
        "configuration": spec.parameters,
        "preprocessing": "train-only StandardScaler" if spec.family == "elastic_net" else "none",
        "train": train_metrics,
        "validation": validation_metrics,
        "train_validation_mae_gap": float(validation_metrics["mae"] - train_metrics["mae"]),
        "validation_to_train_mae_ratio": float(validation_metrics["mae"] / max(train_metrics["mae"], 1e-12)),
        "validation_relative_mae_improvement_vs_baseline_percent": float(
            100.0 * (baseline_mae - validation_metrics["mae"]) / baseline_mae
        ),
        "expanding_validation": None,
    }
    return result, model


def expanding_evaluation(
    spec: CandidateSpec,
    train: pd.DataFrame,
    validation: pd.DataFrame,
    random_state: int,
) -> dict[str, float | None]:
    predictions: list[float] = []
    history = train.copy()
    for row_index in range(len(validation)):
        model = build_model(spec, random_state)
        history_x, history_y = frame_xy(history)
        model.fit(history_x, history_y)
        current = validation.iloc[[row_index]]
        prediction = float(model.predict(current[FEATURE_NAMES].to_numpy(dtype=float))[0])
        if not math.isfinite(prediction):
            raise ValueError(f"Non-finite expanding prediction from {spec.candidate_id}.")
        predictions.append(prediction)
        history = pd.concat([history, current], ignore_index=True)
    return metrics(validation["actual"].to_numpy(dtype=float), np.asarray(predictions))


def choose_expanding_candidates(
    results: list[dict[str, Any]], top_overall: int, include_best_per_family: bool
) -> list[str]:
    ordered = sorted(results, key=lambda item: (item["validation"]["mae"], item["candidate_id"]))
    selected = {item["candidate_id"] for item in ordered[:top_overall]}
    if include_best_per_family:
        for family in FAMILY_COMPLEXITY:
            family_results = [item for item in ordered if item["family"] == family]
            if family_results:
                selected.add(family_results[0]["candidate_id"])
    return sorted(selected)


def select_final_candidate(
    results: list[dict[str, Any]], baseline_mae: float, rules: dict[str, Any]
) -> tuple[dict[str, Any] | None, list[dict[str, Any]]]:
    audits: list[dict[str, Any]] = []
    eligible: list[dict[str, Any]] = []
    for result in results:
        expanding = result["expanding_validation"]
        fixed_mae = float(result["validation"]["mae"])
        reasons: list[str] = []
        if fixed_mae >= baseline_mae:
            reasons.append("validation_mae_not_better_than_baseline")
        if expanding is None:
            reasons.append("expanding_window_not_evaluated")
        else:
            degradation = 100.0 * (float(expanding["mae"]) - fixed_mae) / fixed_mae
            if degradation > float(rules["maximumExpandingVsFixedMaeDegradationPercent"]):
                reasons.append("expanding_window_degradation_exceeds_limit")
            if float(expanding["mae"]) > baseline_mae * float(rules["maximumExpandingMaeVsBaselinePercent"]) / 100.0:
                reasons.append("expanding_window_mae_not_acceptable_vs_baseline")
        if float(result["validation_to_train_mae_ratio"]) > float(rules["maximumValidationToTrainMaeRatio"]):
            reasons.append("train_validation_gap_exceeds_limit")
        audit = {"candidate_id": result["candidate_id"], "eligible": not reasons, "reasons": reasons}
        audits.append(audit)
        if not reasons:
            eligible.append(result)
    if not eligible:
        return None, audits
    best_mae = min(float(item["validation"]["mae"]) for item in eligible)
    tolerance = float(rules["maeTieTolerance"])
    near_best = [item for item in eligible if float(item["validation"]["mae"]) <= best_mae + tolerance]
    near_best.sort(key=lambda item: (FAMILY_COMPLEXITY[item["family"]], item["candidate_id"]))
    return near_best[0], audits


def environment_versions() -> dict[str, str]:
    return {
        "python": platform.python_version(),
        "numpy": np.__version__,
        "pandas": pd.__version__,
        "scikit_learn": sklearn.__version__,
        "joblib": joblib.__version__,
    }


def validate_contract(root: Path) -> tuple[pd.DataFrame, dict[str, Any], dict[str, Any], dict[str, Any]]:
    config = load_json(root / "config" / "python-training-v1.json")
    manifest = load_json(root / "reports" / "dataset-manifest-v1.json")
    baseline = load_json(root / "reports" / "baseline-evaluation-v1.json")
    feature_path = root / "artifacts" / "generated" / "features-v1.csv"
    if sha256_file(feature_path) != manifest["featureDataSha256"]:
        raise ValueError("Feature hash does not match the locked manifest.")
    frame = pd.read_csv(feature_path)
    expected = config["expected"]
    split_counts = frame["split"].value_counts().to_dict()
    if len(frame) != expected["supervisedRows"]:
        raise ValueError("Supervised row count changed.")
    for split_name, expected_key in (("train", "trainRows"), ("validation", "validationRows"), ("test", "testRows")):
        if int(split_counts.get(split_name, 0)) != int(expected[expected_key]):
            raise ValueError(f"{split_name} row count changed.")
    if int(manifest["weeklyBucketCount"]) != int(expected["weeklyBuckets"]):
        raise ValueError("Weekly bucket count changed.")
    required = FEATURE_NAMES + ["actual"]
    if frame[required].isna().any().any():
        missing = frame[required].isna().sum()
        raise ValueError(f"Missing values found: {missing[missing > 0].to_dict()}")
    if not np.isfinite(frame[required].to_numpy(dtype=float)).all():
        raise ValueError("Non-finite feature or target found.")
    validation_baseline = next(item for item in baseline["validation"] if item["baseline"] == "moving_average_4")
    test_baseline = next(item for item in baseline["test"] if item["baseline"] == "moving_average_4")
    for expected_value, actual_value in (
        (config["validationBaseline"]["mae"], validation_baseline["metrics"]["mae"]),
        (config["validationBaseline"]["wapePercent"], validation_baseline["metrics"]["wape_percent"]),
        (config["validationBaseline"]["rmse"], validation_baseline["metrics"]["rmse"]),
        (config["testBaseline"]["mae"], test_baseline["metrics"]["mae"]),
        (config["testBaseline"]["wapePercent"], test_baseline["metrics"]["wape_percent"]),
        (config["testBaseline"]["rmse"], test_baseline["metrics"]["rmse"]),
    ):
        if round(float(expected_value), 2) != round(float(actual_value), 2):
            raise ValueError("Locked baseline contract changed.")
    return frame, config, manifest, baseline


def json_write(path: Path, payload: dict[str, Any]) -> None:
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def markdown_report(report: dict[str, Any]) -> str:
    lines = [
        "# Python Controlled Model Comparison v1",
        "",
        f"Generated UTC: {report['generatedAt']}",
        "",
        "## A. Python environment",
        "",
    ]
    for name, version in report["environment"].items():
        lines.append(f"- {name}: {version}")
    lines.extend(
        [
            "",
            "## B. Candidate models",
            "",
            f"- Total predefined configurations: {len(report['candidateResults'])}",
            "- ElasticNet: train-only StandardScaler.",
            "- RandomForestRegressor and HistGradientBoostingRegressor: no scaling.",
            "- No randomized search or additional model family was used.",
            "",
            "## C. Validation results",
            "",
            "| Candidate | Family | Train MAE | Validation MAE | WAPE | RMSE | Bias | Median AE | Val/train |",
            "|---|---|---:|---:|---:|---:|---:|---:|---:|",
        ]
    )
    for item in sorted(report["candidateResults"], key=lambda value: (value["validation"]["mae"], value["candidate_id"])):
        lines.append(
            f"| {item['candidate_id']} | {item['family']} | {item['train']['mae']:.2f} | "
            f"{item['validation']['mae']:.2f} | {item['validation']['wape_percent']:.2f}% | "
            f"{item['validation']['rmse']:.2f} | {item['validation']['mean_signed_error']:.2f} | "
            f"{item['validation']['median_absolute_error']:.2f} | {item['validation_to_train_mae_ratio']:.2f} |"
        )
    lines.extend(
        [
            "",
            f"Locked validation baseline MAE: {report['baselineContract']['validation']['mae']:.2f}.",
            "",
            "## D. Expanding-window results",
            "",
            "| Candidate | Fixed MAE | Expanding MAE | WAPE | RMSE | Bias |",
            "|---|---:|---:|---:|---:|---:|",
        ]
    )
    expanding = [item for item in report["candidateResults"] if item["expanding_validation"] is not None]
    for item in sorted(expanding, key=lambda value: value["expanding_validation"]["mae"]):
        metric = item["expanding_validation"]
        lines.append(
            f"| {item['candidate_id']} | {item['validation']['mae']:.2f} | {metric['mae']:.2f} | "
            f"{metric['wape_percent']:.2f}% | {metric['rmse']:.2f} | {metric['mean_signed_error']:.2f} |"
        )
    lines.extend(
        [
            "",
            "Each expanding prediction refits only on the prefix available before its validation target.",
            "",
            "## E. Overfitting check",
            "",
        ]
    )
    for item in sorted(report["candidateResults"], key=lambda value: (value["validation"]["mae"], value["candidate_id"]))[:5]:
        lines.append(
            f"- {item['candidate_id']}: train MAE {item['train']['mae']:.2f}, validation MAE "
            f"{item['validation']['mae']:.2f}, ratio {item['validation_to_train_mae_ratio']:.2f}."
        )
    lines.extend(["", "## F. Selected model or none", ""])
    if report["selection"]["selectedCandidateId"] is None:
        lines.append("**FINAL MODEL = NONE**")
    else:
        lines.append(f"**FINAL MODEL = {report['selection']['selectedCandidateId']}**")
    lines.extend(["", "## G. Untouched test result", ""])
    if report["untouchedTest"]["status"] != "EVALUATED":
        lines.append(f"**{report['untouchedTest']['status']}**. No Python candidate was evaluated on test.")
    else:
        metric = report["untouchedTest"]["metrics"]
        lines.append(
            f"MAE {metric['mae']:.2f}; WAPE {metric['wape_percent']:.2f}%; RMSE {metric['rmse']:.2f}; "
            f"bias {metric['mean_signed_error']:.2f}; median AE {metric['median_absolute_error']:.2f}."
        )
    lines.extend(
        [
            "",
            "## H. Baseline comparison",
            "",
            f"Validation baseline: MAE {report['baselineContract']['validation']['mae']:.2f}, "
            f"WAPE {report['baselineContract']['validation']['wape_percent']:.2f}%, "
            f"RMSE {report['baselineContract']['validation']['rmse']:.2f}.",
            "",
            "## I. Created artifacts",
            "",
            "- Versioned Python config, lock file, training script, selection proof, report, and tests.",
            f"- Model artifact saved: {report['modelArtifactSaved']}.",
            "",
            "## J. Automated tests",
            "",
            "Run `python -m unittest discover -s python/tests -v` from this pilot directory.",
            "",
            "## K. Final verdict",
            "",
            f"**ML MODEL ACCEPTED = {report['verdict']['mlModelAccepted']}**",
            f"**OFFLINE ML FEASIBILITY = {report['verdict']['offlineMlFeasibility']}**",
            "**PRODUCTION INFERENCE = NO-GO**",
        ]
    )
    return "\n".join(lines) + "\n"


def run(root: Path, generated_at: str | None = None) -> dict[str, Any]:
    frame, config, manifest, baseline = validate_contract(root)
    generated = generated_at or datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    train = frame.loc[frame["split"] == "train"].copy()
    validation = frame.loc[frame["split"] == "validation"].copy()
    specs = candidate_specs(config)
    spec_by_id = {spec.candidate_id: spec for spec in specs}
    baseline_validation = next(item for item in baseline["validation"] if item["baseline"] == "moving_average_4")
    baseline_test = next(item for item in baseline["test"] if item["baseline"] == "moving_average_4")
    baseline_mae = float(baseline_validation["metrics"]["mae"])
    results: list[dict[str, Any]] = []
    for spec in specs:
        result, _ = evaluate_fixed(spec, train, validation, int(config["randomState"]), baseline_mae)
        results.append(result)
    expanding_ids = choose_expanding_candidates(
        results,
        int(config["selectionRules"]["expandingTopOverall"]),
        bool(config["selectionRules"]["includeBestPerFamily"]),
    )
    for result in results:
        if result["candidate_id"] in expanding_ids:
            result["expanding_validation"] = expanding_evaluation(
                spec_by_id[result["candidate_id"]], train, validation, int(config["randomState"])
            )
    selected, gate_audit = select_final_candidate(results, baseline_mae, config["selectionRules"])

    reports = root / "reports"
    selection_payload = {
        "selectionVersion": "historical-weekly-volume-python-selection/v1",
        "generatedAt": generated,
        "selectionInputs": ["train", "validation"],
        "testMetricsIncluded": False,
        "testRowsMaterializedForEvaluation": False,
        "selectionMetric": config["selectionMetric"],
        "selectedCandidateId": None if selected is None else selected["candidate_id"],
        "expandingCandidates": expanding_ids,
        "gateAudit": gate_audit,
        "featureDataSha256": manifest["featureDataSha256"],
        "configSha256": sha256_file(root / "config" / "python-training-v1.json"),
    }
    selection_path = reports / "python-model-selection-v1.json"
    json_write(selection_path, selection_payload)
    selection_hash = sha256_file(selection_path)

    test_payload: dict[str, Any] = {
        "status": "NOT_EVALUATED_VALIDATION_GATE_FAILED",
        "evaluationCount": 0,
        "metrics": None,
        "comparison": None,
    }
    model_artifact_saved = False
    accepted = False
    if selected is not None:
        # Test targets are accessed only after the validation-only selection lock exists.
        final_train = pd.concat([train, validation], ignore_index=True)
        test = frame.loc[frame["split"] == "test"].copy()
        selected_spec = spec_by_id[selected["candidate_id"]]
        model = build_model(selected_spec, int(config["randomState"]))
        final_x, final_y = frame_xy(final_train)
        test_x, test_y = frame_xy(test)
        model.fit(final_x, final_y)
        test_predictions = np.asarray(model.predict(test_x), dtype=float)
        test_metrics = metrics(test_y, test_predictions)
        test_payload = {
            "status": "EVALUATED",
            "evaluationCount": 1,
            "metrics": test_metrics,
            "comparison": {
                "absoluteMaeImprovement": float(baseline_test["metrics"]["mae"] - test_metrics["mae"]),
                "relativeMaeImprovementPercent": float(
                    100.0 * (baseline_test["metrics"]["mae"] - test_metrics["mae"]) / baseline_test["metrics"]["mae"]
                ),
                "wapeImprovementPoints": float(
                    baseline_test["metrics"]["wape_percent"] - test_metrics["wape_percent"]
                ),
                "rmseImprovement": float(baseline_test["metrics"]["rmse"] - test_metrics["rmse"]),
            },
        }
        accepted = bool(test_metrics["mae"] <= float(baseline_test["metrics"]["mae"]))
        if accepted:
            models = root / "models"
            models.mkdir(exist_ok=True)
            artifact_path = models / "historical-weekly-python-v1.joblib"
            joblib.dump(model, artifact_path)
            metadata = {
                "modelConfigVersion": config["trainingVersion"],
                "selectedCandidate": selected,
                "datasetManifestSha256": sha256_file(root / "reports" / "dataset-manifest-v1.json"),
                "featureDataSha256": manifest["featureDataSha256"],
                "featureVersion": config["featureVersion"],
                "trainCutoffExclusive": "2024-12-30",
                "validationCutoffExclusive": "2025-06-30",
                "testCutoffExclusive": "2026-08-03",
                "environment": environment_versions(),
                "testMetrics": test_metrics,
                "artifactSha256": sha256_file(artifact_path),
            }
            json_write(models / "historical-weekly-python-v1.metadata.json", metadata)
            model_artifact_saved = True
    else:
        stale_artifact = root / "models" / "historical-weekly-python-v1.joblib"
        if stale_artifact.exists():
            raise RuntimeError("Validation gate failed but a stale Python model artifact exists.")

    report = {
        "reportVersion": "historical-weekly-volume-python-comparison/v1",
        "generatedAt": generated,
        "environment": environment_versions(),
        "dependencyLockSha256": sha256_file(root / "requirements-python.lock"),
        "configSha256": selection_payload["configSha256"],
        "datasetContract": {
            "weeklyBuckets": manifest["weeklyBucketCount"],
            "supervisedRows": len(frame),
            "trainRows": len(train),
            "validationRows": len(validation),
            "testRows": int((frame["split"] == "test").sum()),
            "featureDataSha256": manifest["featureDataSha256"],
        },
        "baselineContract": {
            "validation": baseline_validation["metrics"],
            "test": baseline_test["metrics"],
        },
        "candidateResults": results,
        "selection": {
            "selectedCandidateId": None if selected is None else selected["candidate_id"],
            "selectionLockSha256": selection_hash,
            "selectionCompletedBeforeTestEvaluation": True,
            "gateAudit": gate_audit,
        },
        "untouchedTest": test_payload,
        "modelArtifactSaved": model_artifact_saved,
        "verdict": {
            "mlModelAccepted": "YES" if accepted else "NO",
            "offlineMlFeasibility": "GO" if accepted else "NO-GO",
            "productionInference": "NO-GO",
        },
    }
    json_write(reports / "python-model-comparison-v1.json", report)
    (reports / "python-model-comparison-v1.md").write_text(markdown_report(report), encoding="utf-8")
    return report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--generated-at")
    args = parser.parse_args()
    report = run(args.root.resolve(), args.generated_at)
    print(f"Candidates evaluated on validation: {len(report['candidateResults'])}")
    print(f"Final model: {report['selection']['selectedCandidateId'] or 'NONE'}")
    print(f"Test status: {report['untouchedTest']['status']}")
    print(f"ML MODEL ACCEPTED = {report['verdict']['mlModelAccepted']}")
    print(f"OFFLINE ML FEASIBILITY = {report['verdict']['offlineMlFeasibility']}")
    print("PRODUCTION INFERENCE = NO-GO")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
