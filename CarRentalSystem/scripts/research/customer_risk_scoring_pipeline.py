#!/usr/bin/env python3
"""Train/evaluate a small logistic-regression risk model from anonymized CSV.

This script intentionally uses only the Python standard library so it can run on
the staging server without adding a research dependency stack.
"""

from __future__ import annotations

import argparse
import csv
import datetime as dt
import json
import math
from pathlib import Path


FEATURES = [
    "account_age_days",
    "is_email_verified",
    "is_ekyc_verified",
    "completed_booking_count",
    "cancelled_booking_count",
    "late_return_count",
    "payment_failure_count",
    "claim_count",
    "approved_settlement_amount",
    "is_blacklisted",
    "is_locked",
]
LABEL = "label_high_risk"
TIME = "event_time_utc"


def parse_bool(value: str) -> float:
    return 1.0 if str(value).strip().lower() in {"1", "true", "yes", "y"} else 0.0


def parse_float(value: str) -> float:
    if value is None or str(value).strip() == "":
        return 0.0
    return float(value)


def parse_time(value: str) -> dt.datetime:
    text = value.strip().replace("Z", "+00:00")
    parsed = dt.datetime.fromisoformat(text)
    return parsed if parsed.tzinfo else parsed.replace(tzinfo=dt.timezone.utc)


def load_rows(path: Path):
    rows = []
    with path.open("r", encoding="utf-8-sig", newline="") as handle:
        reader = csv.DictReader(handle)
        missing = {TIME, LABEL, *FEATURES} - set(reader.fieldnames or [])
        if missing:
            raise SystemExit(f"CSV is missing required columns: {sorted(missing)}")
        for row in reader:
            features = []
            for name in FEATURES:
                features.append(parse_bool(row[name]) if name.startswith("is_") else parse_float(row[name]))
            rows.append((parse_time(row[TIME]), features, int(parse_bool(row[LABEL]))))
    rows.sort(key=lambda item: item[0])
    return rows


def split_rows(rows, test_ratio: float):
    split_index = max(1, int(len(rows) * (1.0 - test_ratio)))
    return rows[:split_index], rows[split_index:]


def standardize(train_x, test_x):
    means = [sum(col) / len(train_x) for col in zip(*train_x)]
    stdevs = []
    for idx, mean in enumerate(means):
        variance = sum((row[idx] - mean) ** 2 for row in train_x) / max(1, len(train_x) - 1)
        stdevs.append(math.sqrt(variance) or 1.0)

    def transform(rows):
        return [[(value - means[i]) / stdevs[i] for i, value in enumerate(row)] for row in rows]

    return transform(train_x), transform(test_x), means, stdevs


def sigmoid(value: float) -> float:
    if value < -40:
        return 0.0
    if value > 40:
        return 1.0
    return 1.0 / (1.0 + math.exp(-value))


def train_logistic_regression(x, y, epochs: int, learning_rate: float, l2: float):
    weights = [0.0 for _ in range(len(FEATURES) + 1)]
    n = len(x)
    for _ in range(epochs):
        gradients = [0.0 for _ in weights]
        for row, label in zip(x, y):
            prediction = sigmoid(weights[0] + sum(w * v for w, v in zip(weights[1:], row)))
            error = prediction - label
            gradients[0] += error
            for idx, value in enumerate(row, start=1):
                gradients[idx] += error * value
        for idx in range(len(weights)):
            penalty = l2 * weights[idx] if idx > 0 else 0.0
            weights[idx] -= learning_rate * ((gradients[idx] / n) + penalty)
    return weights


def predict(weights, rows):
    return [sigmoid(weights[0] + sum(w * v for w, v in zip(weights[1:], row))) for row in rows]


def confusion(y_true, y_score, threshold: float):
    tp = fp = tn = fn = 0
    for label, score in zip(y_true, y_score):
        predicted = score >= threshold
        if predicted and label:
            tp += 1
        elif predicted:
            fp += 1
        elif label:
            fn += 1
        else:
            tn += 1
    return tp, fp, tn, fn


def auc_roc(y_true, y_score):
    pairs = sorted(zip(y_score, y_true), reverse=True)
    positives = sum(y_true)
    negatives = len(y_true) - positives
    if positives == 0 or negatives == 0:
        return None
    tp = fp = prev_tp = prev_fp = 0
    area = 0.0
    for _, label in pairs:
        if label:
            tp += 1
        else:
            fp += 1
        area += (fp - prev_fp) * (tp + prev_tp) / 2.0
        prev_tp, prev_fp = tp, fp
    return area / (positives * negatives)


def auc_pr(y_true, y_score):
    pairs = sorted(zip(y_score, y_true), reverse=True)
    positives = sum(y_true)
    if positives == 0:
        return None
    tp = fp = 0
    prev_recall = 0.0
    area = 0.0
    for _, label in pairs:
        if label:
            tp += 1
        else:
            fp += 1
        recall = tp / positives
        precision = tp / max(1, tp + fp)
        area += (recall - prev_recall) * precision
        prev_recall = recall
    return area


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--input", required=True, help="Anonymized CSV path")
    parser.add_argument("--output", required=True, help="Metrics JSON output path")
    parser.add_argument("--test-ratio", type=float, default=0.2)
    parser.add_argument("--epochs", type=int, default=1200)
    parser.add_argument("--learning-rate", type=float, default=0.08)
    parser.add_argument("--l2", type=float, default=0.001)
    parser.add_argument("--threshold", type=float, default=0.5)
    args = parser.parse_args()

    rows = load_rows(Path(args.input))
    if len(rows) < 20:
        raise SystemExit("Need at least 20 rows for a minimally useful split.")

    train, test = split_rows(rows, args.test_ratio)
    train_x, train_y = [r[1] for r in train], [r[2] for r in train]
    test_x, test_y = [r[1] for r in test], [r[2] for r in test]
    train_x, test_x, means, stdevs = standardize(train_x, test_x)

    weights = train_logistic_regression(train_x, train_y, args.epochs, args.learning_rate, args.l2)
    scores = predict(weights, test_x)
    tp, fp, tn, fn = confusion(test_y, scores, args.threshold)
    precision = tp / max(1, tp + fp)
    recall = tp / max(1, tp + fn)
    f1 = 2 * precision * recall / max(1e-9, precision + recall)

    output = {
        "dataset": {
            "row_count": len(rows),
            "train_count": len(train),
            "test_count": len(test),
            "positive_rate": sum(r[2] for r in rows) / len(rows),
            "train_start_utc": train[0][0].isoformat(),
            "train_end_utc": train[-1][0].isoformat(),
            "test_start_utc": test[0][0].isoformat(),
            "test_end_utc": test[-1][0].isoformat(),
            "synthetic": False,
        },
        "features": FEATURES,
        "metrics": {
            "roc_auc": auc_roc(test_y, scores),
            "pr_auc": auc_pr(test_y, scores),
            "precision": precision,
            "recall": recall,
            "f1": f1,
            "confusion_matrix": {"tp": tp, "fp": fp, "tn": tn, "fn": fn},
            "threshold": args.threshold,
        },
        "model": {
            "type": "logistic_regression",
            "weights": weights,
            "standardization": {"means": means, "stdevs": stdevs},
        },
    }

    Path(args.output).write_text(json.dumps(output, indent=2), encoding="utf-8")
    print(json.dumps(output["metrics"], indent=2))


if __name__ == "__main__":
    main()
