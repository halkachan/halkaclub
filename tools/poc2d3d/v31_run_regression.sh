#!/usr/bin/env bash
# はるかわーるど ver3.1: runs the regression suites one after another in hidden Unity batch mode
# (no window), one log per suite, then a summary. Files the 2D game's checks rewrite (FirstDay.unity
# and other generated scenes) are put back to their Git state afterwards, as in ver3.0 Phase 7.
#   bash tools/poc2d3d/v31_run_regression.sh [LOG_DIR]
set -u
REPO="$(cd "$(dirname "$0")/../.." && pwd)"
LOGS="${1:-$REPO/../HARUKA_WORLD_V31_LOGS/regression}"
UNITY="/c/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Unity.exe"
PROJECT="$(cygpath -w "$REPO/unity-project" 2>/dev/null || echo "$REPO/unity-project")"
mkdir -p "$LOGS"
SUMMARY="$LOGS/summary.txt"
: > "$SUMMARY"
run() {
  local name="$1" method="$2" quit="$3"
  local log="$LOGS/$name.log"
  local start=$(date +%s)
  if [ "$quit" = "quit" ]; then
    "$UNITY" -batchmode -quit -projectPath "$PROJECT" -executeMethod "$method" -logFile "$(cygpath -w "$log" 2>/dev/null || echo "$log")"
  else
    "$UNITY" -batchmode -projectPath "$PROJECT" -executeMethod "$method" -logFile "$(cygpath -w "$log" 2>/dev/null || echo "$log")"
  fi
  local code=$?
  local line
  line=$(grep -aE "checks: [0-9]+ passed, [0-9]+ failed|[0-9]+ passed, [0-9]+ failed" "$log" | tail -1)
  echo "$name | exit $code | $(( $(date +%s) - start )) s | ${line:-no summary line}" | tee -a "$SUMMARY"
}
run 10-PocChecks Halka.Poc2D3D.EditorTools.PocChecks.RunBatch batch
run 11-Phase2EnvironmentChecks Halka.Poc2D3D.EditorTools.Phase2EnvironmentChecks.RunBatch batch
run 12-Phase21PresentationChecks Halka.Poc2D3D.EditorTools.Phase21PresentationChecks.RunBatch batch
run 13-Phase3BuildingFurnitureChecks Halka.Poc2D3D.EditorTools.Phase3BuildingFurnitureChecks.RunBatch batch
run 14-Phase4IntegrationChecks Halka.Poc2D3D.EditorTools.Phase4IntegrationChecks.RunBatch batch
run 15-Phase5ReleaseCandidateChecks Halka.Poc2D3D.EditorTools.Phase5ReleaseCandidateChecks.RunBatch batch
run 16-Phase6SaveChecks Halka.Poc2D3D.EditorTools.Phase6SaveChecks.RunBatch batch
run 17-Phase7ReleaseChecks Halka.Poc2D3D.EditorTools.Phase7ReleaseChecks.RunBatch batch
run 18-Version30Checks Halka.Game.Editor.Version30Checks.Run quit
run 19-MapAuthoringChecks Halka.Game.Editor.MapAuthoringChecks.Run quit
cd "$REPO"
echo "--- generated files touched by the suites (restored to Git):" | tee -a "$SUMMARY"
git status --short -- unity-project/Assets/Scenes unity-project/ProjectSettings | tee -a "$SUMMARY"
git checkout -- unity-project/Assets/Scenes 2>/dev/null
# ProjectSettings.asset: put back only when the change is line endings alone (as in ver3.0 Phase 7)
git diff --quiet --ignore-cr-at-eol -- unity-project/ProjectSettings/ProjectSettings.asset && git checkout -- unity-project/ProjectSettings/ProjectSettings.asset 2>/dev/null
git status --short -- unity-project/ProjectSettings | tee -a "$SUMMARY"
echo "done" | tee -a "$SUMMARY"
