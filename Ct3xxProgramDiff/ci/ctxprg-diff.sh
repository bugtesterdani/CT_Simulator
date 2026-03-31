#!/usr/bin/env bash
set -euo pipefail

BASE_SHA="${CI_MERGE_REQUEST_DIFF_BASE_SHA:-${CI_COMMIT_BEFORE_SHA:-}}"
HEAD_SHA="${CI_COMMIT_SHA:-HEAD}"
OUT_DIR="${CTXPRG_DIFF_OUT_DIR:-artifacts/ctxprg-diff}"

if [[ -z "${BASE_SHA}" ]]; then
  echo "ERROR: BASE_SHA is not set. Provide CI_MERGE_REQUEST_DIFF_BASE_SHA or CI_COMMIT_BEFORE_SHA."
  exit 2
fi

mkdir -p "${OUT_DIR}"

echo "Using base: ${BASE_SHA}"
echo "Using head: ${HEAD_SHA}"

CHANGED_FILES=$(git diff --name-only "${BASE_SHA}" "${HEAD_SHA}" -- '*.ctxprg' || true)

if [[ -z "${CHANGED_FILES}" ]]; then
  echo "No .ctxprg changes detected."
  exit 0
fi

for file in ${CHANGED_FILES}; do
  safe_name=$(echo "${file}" | tr '/\\' '_')
  md_out="${OUT_DIR}/${safe_name}.md"
  html_out="${OUT_DIR}/${safe_name}.html"

  has_old=0
  has_new=0

  if git cat-file -e "${BASE_SHA}:${file}" 2>/dev/null; then
    has_old=1
  fi

  if [[ -f "${file}" ]]; then
    has_new=1
  fi

  if [[ "${has_old}" -eq 1 && "${has_new}" -eq 1 ]]; then
    tmp_old=$(mktemp)
    git show "${BASE_SHA}:${file}" > "${tmp_old}"
    dotnet run --project Ct3xxProgramDiff -- \
      --old "${tmp_old}" \
      --new "${file}" \
      --out "${md_out}" \
      --html "${html_out}"
    rm -f "${tmp_old}"
    continue
  fi

  if [[ "${has_old}" -eq 0 && "${has_new}" -eq 1 ]]; then
    {
      echo "# CTXPRG Semantic Diff"
      echo
      echo "Old: \`(file did not exist)\`"
      echo "New: \`${file}\`"
      echo
      echo "Added: **1**  Removed: **0**  Changed: **0**"
      echo
      echo "## Added (in new)"
      echo "- Program file \`${file}\`"
    } > "${md_out}"
    {
      echo "<!doctype html>"
      echo "<html><head><meta charset=\"utf-8\"/>"
      echo "<title>CTXPRG Semantic Diff</title></head><body>"
      echo "<h1>CTXPRG Semantic Diff</h1>"
      echo "<div>Old: <code>(file did not exist)</code></div>"
      echo "<div>New: <code>${file}</code></div>"
      echo "<div>Added: 1 &nbsp; Removed: 0 &nbsp; Changed: 0</div>"
      echo "<h2>Added (in new)</h2>"
      echo "<div>Program file <code>${file}</code></div>"
      echo "</body></html>"
    } > "${html_out}"
    continue
  fi

  if [[ "${has_old}" -eq 1 && "${has_new}" -eq 0 ]]; then
    {
      echo "# CTXPRG Semantic Diff"
      echo
      echo "Old: \`${file}\`"
      echo "New: \`(file deleted)\`"
      echo
      echo "Added: **0**  Removed: **1**  Changed: **0**"
      echo
      echo "## Removed (missing in new)"
      echo "- Program file \`${file}\`"
    } > "${md_out}"
    {
      echo "<!doctype html>"
      echo "<html><head><meta charset=\"utf-8\"/>"
      echo "<title>CTXPRG Semantic Diff</title></head><body>"
      echo "<h1>CTXPRG Semantic Diff</h1>"
      echo "<div>Old: <code>${file}</code></div>"
      echo "<div>New: <code>(file deleted)</code></div>"
      echo "<div>Added: 0 &nbsp; Removed: 1 &nbsp; Changed: 0</div>"
      echo "<h2>Removed (missing in new)</h2>"
      echo "<div>Program file <code>${file}</code></div>"
      echo "</body></html>"
    } > "${html_out}"
  fi
done

echo "Reports written to ${OUT_DIR}"
