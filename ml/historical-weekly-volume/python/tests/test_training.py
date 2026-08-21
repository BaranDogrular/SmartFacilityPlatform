from __future__ import annotations

import hashlib
import json
import sys
import unittest
from pathlib import Path

import numpy as np


PILOT_ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(PILOT_ROOT / "python"))

import train_models  # noqa: E402


class PythonTrainingTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.frame, cls.config, cls.manifest, cls.baseline = train_models.validate_contract(PILOT_ROOT)
        cls.train = cls.frame.loc[cls.frame["split"] == "train"].copy()
        cls.validation = cls.frame.loc[cls.frame["split"] == "validation"].copy()
        cls.report = train_models.load_json(PILOT_ROOT / "reports" / "python-model-comparison-v1.json")
        cls.selection = train_models.load_json(PILOT_ROOT / "reports" / "python-model-selection-v1.json")

    def test_locked_dataset_contract(self) -> None:
        self.assertEqual(219, self.manifest["weeklyBucketCount"])
        self.assertEqual(167, len(self.frame))
        self.assertEqual(84, len(self.train))
        self.assertEqual(26, len(self.validation))
        self.assertEqual(57, int((self.frame["split"] == "test").sum()))
        self.assertFalse(self.frame[train_models.FEATURE_NAMES + ["actual"]].isna().any().any())

    def test_candidate_scope_is_bounded_and_exact(self) -> None:
        specs = train_models.candidate_specs(self.config)
        self.assertEqual(16, len(specs))
        families = {spec.family for spec in specs}
        self.assertEqual({"elastic_net", "random_forest", "hist_gradient_boosting"}, families)
        self.assertEqual(20260821, self.config["randomState"])
        for spec in specs:
            if spec.family in {"random_forest", "hist_gradient_boosting"}:
                model = train_models.build_model(spec, self.config["randomState"])
                self.assertEqual(20260821, model.random_state)

    def test_metric_calculation_and_zero_safety(self) -> None:
        result = train_models.metrics(np.array([0.0, 10.0]), np.array([2.0, 8.0]))
        self.assertAlmostEqual(2.0, result["mae"])
        self.assertAlmostEqual(40.0, result["wape_percent"])
        self.assertAlmostEqual(2.0, result["rmse"])
        self.assertAlmostEqual(0.0, result["mean_signed_error"])
        self.assertAlmostEqual(2.0, result["median_absolute_error"])
        zero_result = train_models.metrics(np.array([0.0, 0.0]), np.array([0.0, 2.0]))
        self.assertIsNone(zero_result["wape_percent"])

    def test_random_forest_training_is_deterministic(self) -> None:
        spec = next(spec for spec in train_models.candidate_specs(self.config) if spec.family == "random_forest")
        train_x, train_y = train_models.frame_xy(self.train)
        validation_x, _ = train_models.frame_xy(self.validation)
        first = train_models.build_model(spec, self.config["randomState"])
        second = train_models.build_model(spec, self.config["randomState"])
        first.fit(train_x, train_y)
        second.fit(train_x, train_y)
        np.testing.assert_array_equal(first.predict(validation_x), second.predict(validation_x))

    def test_elastic_net_scaler_fits_train_only(self) -> None:
        spec = next(spec for spec in train_models.candidate_specs(self.config) if spec.family == "elastic_net")
        train_x, train_y = train_models.frame_xy(self.train)
        model = train_models.build_model(spec, self.config["randomState"])
        model.fit(train_x, train_y)
        np.testing.assert_allclose(model.named_steps["scaler"].mean_, train_x.mean(axis=0), rtol=0, atol=1e-12)
        combined_x = np.vstack([train_x, train_models.frame_xy(self.validation)[0]])
        self.assertGreater(float(np.max(np.abs(model.named_steps["scaler"].mean_ - combined_x.mean(axis=0)))), 1e-6)

    def test_selection_uses_validation_gate_not_test_values(self) -> None:
        fake = [
            {
                "candidate_id": "a",
                "family": "elastic_net",
                "validation": {"mae": 100.0},
                "validation_to_train_mae_ratio": 1.1,
                "expanding_validation": {"mae": 102.0},
                "test_mae": 9999.0,
            },
            {
                "candidate_id": "b",
                "family": "random_forest",
                "validation": {"mae": 110.0},
                "validation_to_train_mae_ratio": 1.1,
                "expanding_validation": {"mae": 111.0},
                "test_mae": 1.0,
            },
        ]
        selected, _ = train_models.select_final_candidate(fake, 118.47, self.config["selectionRules"])
        self.assertEqual("a", selected["candidate_id"])

    def test_actual_selection_lock_excludes_test_and_gate_stops_test(self) -> None:
        self.assertIsNone(self.selection["selectedCandidateId"])
        self.assertFalse(self.selection["testMetricsIncluded"])
        self.assertFalse(self.selection["testRowsMaterializedForEvaluation"])
        self.assertEqual(["train", "validation"], self.selection["selectionInputs"])
        self.assertEqual("NOT_EVALUATED_VALIDATION_GATE_FAILED", self.report["untouchedTest"]["status"])
        self.assertEqual(0, self.report["untouchedTest"]["evaluationCount"])
        self.assertIsNone(self.report["untouchedTest"]["metrics"])

    def test_report_and_config_provenance(self) -> None:
        feature_path = PILOT_ROOT / "artifacts" / "generated" / "features-v1.csv"
        self.assertEqual(self.manifest["featureDataSha256"], train_models.sha256_file(feature_path))
        self.assertEqual(self.manifest["featureDataSha256"], self.report["datasetContract"]["featureDataSha256"])
        self.assertEqual(
            train_models.sha256_file(PILOT_ROOT / "config" / "python-training-v1.json"),
            self.report["configSha256"],
        )
        self.assertEqual(
            train_models.sha256_file(PILOT_ROOT / "requirements-python.lock"),
            self.report["dependencyLockSha256"],
        )

    def test_rejected_model_has_no_artifact(self) -> None:
        self.assertEqual("NO", self.report["verdict"]["mlModelAccepted"])
        self.assertFalse(self.report["modelArtifactSaved"])
        self.assertFalse((PILOT_ROOT / "models" / "historical-weekly-python-v1.joblib").exists())


if __name__ == "__main__":
    unittest.main()
