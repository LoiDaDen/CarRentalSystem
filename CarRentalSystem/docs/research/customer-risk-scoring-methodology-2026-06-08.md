# Customer Risk Scoring Research Methodology

Date: 2026-06-08

## Purpose

Build a scientific research track for tenant risk scoring. The score is advisory for staff review only. It must not automatically reject, lock, blacklist, or price-discriminate against a customer.

## Dataset Schema

Use anonymized rows only. Do not export raw CCCD, driving license, phone, email, name, address, selfie, document image URLs, IP address, or exact GPS coordinates.

Required columns:

| Column | Type | Notes |
| --- | --- | --- |
| event_time_utc | datetime | Used for time-based split |
| account_age_days | number | Numeric feature |
| is_email_verified | boolean | 0/1 |
| is_ekyc_verified | boolean | 0/1 |
| completed_booking_count | number | Numeric feature |
| cancelled_booking_count | number | Numeric feature |
| late_return_count | number | Numeric feature |
| payment_failure_count | number | Numeric feature |
| claim_count | number | Damage/lost item/violation signals |
| approved_settlement_amount | number | VND amount, capped or bucketed before export if needed |
| is_blacklisted | boolean | 0/1 |
| is_locked | boolean | 0/1 |
| label_high_risk | boolean | Ground truth for research only |

## Hypothesis

Historical rental behavior, payment failures, late returns, and settlement claims can predict customers that need manual risk review better than a pure eKYC-only rule.

## Models

1. Rule-based baseline: implemented in `ICustomerRiskScoringService` as `rule-based-v1`.
2. Logistic regression: trained with time-based split using `scripts/research/customer_risk_scoring_pipeline.py`.

## Metrics

Report ROC-AUC, PR-AUC, precision, recall, F1, confusion matrix, sample count, positive rate, train/test split dates, and feature list.

## Required Evidence Before Claiming Scientific Result

- Real anonymized dataset exported from staging or production pilot.
- Reproducible pipeline command and output metrics.
- Comparison between rule-based baseline and logistic regression.
- Limitations, bias review, and decision policy confirming human review only.

Synthetic data can be used for technical demo only and must be labeled as synthetic.
