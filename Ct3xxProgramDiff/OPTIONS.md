# Ct3xxProgramDiff Optionen

## CLI-Parameter

- `--old` (Pflicht): Pfad zur Referenz-`.ctxprg`
- `--new` (Pflicht): Pfad zur neuen `.ctxprg`
- `--out`: Pfad fuer den Markdown-Report (Standard: `ctxprg-diff.md`)
- `--html`: Pfad fuer den HTML-Report (Standard: `ctxprg-diff.html`)

## GitLab CI

Das Script `ci/ctxprg-diff.sh` nutzt automatisch:

- `CI_MERGE_REQUEST_DIFF_BASE_SHA` (Merge Request Basis)
- oder `CI_COMMIT_BEFORE_SHA` (Fallback)

Zusaetzlich unterstuetzt:

- `CTXPRG_DIFF_OUT_DIR` (Standard: `artifacts/ctxprg-diff`)
