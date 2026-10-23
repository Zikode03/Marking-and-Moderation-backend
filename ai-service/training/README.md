# Proprietary model development

This directory is reserved for reproducible training code. **No trained model currently exists.**

Research plan:
1. Establish approved assessment scope (short factual answers first).
2. Obtain lawful, permissioned datasets with expert rubric marks.
3. Split data by assessment and learner into train/validation/held-out test sets to prevent leakage.
4. Create a transparent non-neural baseline and then implement original PyTorch model candidates.
5. Measure mean absolute mark error, criterion accuracy, consistency, subgroup error and reviewer correction effort.
6. Only enable inference after academic review, documented acceptance thresholds and security assessment.

Never commit student work, personally identifying records, secrets or licensed datasets.
