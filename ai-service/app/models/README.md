# models/

Reserved for locally-hosted model weights and tokenizers. Empty in chunk 3 —
the early pipeline stages call hosted LLMs (OpenAI / Anthropic) and have
deterministic rule-based fallbacks.

When local models land (chunk 9: voice with Whisper, chunk 9: distilled BERT),
weights mount here from Azure Blob at container start.
