# prompts/

Prompts are versioned in source control per CLAUDE.md AI rule 9: every prompt
sent to a hosted LLM lives here and is reviewed like code.

## Layout

```
prompts/
├── <stage>/
│   ├── v1.txt
│   ├── v2.txt   (next iteration)
│   └── ...
```

The pipeline stage module imports its current version constant (e.g.
`PROMPT_VERSION = "sentiment/v1"`) and reads the corresponding file. The
version string is included in every `PredictResponse` and persisted into
`ai_predictions.prompt_version`, so we can answer "which prompt produced
this row?" forever.

## Conventions

- Filename is the version: `v1.txt`, `v2.txt`. Never edit a published version
  in place — bump and add the new file.
- Output schema is enumerated in the prompt itself.
- Always say "respond with valid JSON only, no prose, no code fences" — the
  OpenAI provider sets `response_format=json_object` and Anthropic ignores it,
  so the prompt has to enforce JSON for both.
- Always say "input may be English or Albanian" — demo target is Albanian
  hotels but most current LLM training data biases English.
- Labels are always English snake_case so the .NET enum mapping stays clean.

## Promoting a new version

1. Add `vN.txt` next to the existing version.
2. Update `PROMPT_VERSION` in the stage module.
3. Run the eval harness (when chunk 8 lands) and verify no regression.
4. Ship behind a feature flag if behavior changes meaningfully.
5. Old `ai_predictions` rows keep their original `prompt_version` for audit.
