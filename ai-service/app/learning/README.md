# learning/

Reserved for the continuous-learning loop (CLAUDE.md AI pipeline stage 10):

- Capture predicted vs actual category/department/satisfaction on resolved issues
- Nightly aggregate per-model performance metrics
- Weekly retraining trigger when training set grows past threshold
- Shadow → canary → full rollout via the `model_versions` table

Empty in chunk 3. Wired up in chunk 8 (advanced AI pipeline).

PII stripping happens here, **not** at inference. Inference stores raw text
flagged `contains_pii=TRUE`; the extraction job into the training corpus is
where names, emails, phone numbers are scrubbed.
