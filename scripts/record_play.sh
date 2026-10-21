#!/usr/bin/env bash
# 플레이 영상 한 편 찍기(Editor/PlayRecording) → Logs/play_videos/<커밋>.mp4
# 사용: scripts/record_play.sh [이름] [초]   (기본 이름: 짧은 커밋 해시, 커밋 안 된 변경이 있으면 -dirty. 초: 기본 60, 0이면 승패가 날 때까지)
# Unity 에디터를 닫고 실행한다. 에디터 경로는 UNITY_EXE로 바꿀 수 있다.
set -euo pipefail
cd "$(dirname "$0")/.."

version=$(sed -n 's/^m_EditorVersion: //p' ProjectSettings/ProjectVersion.txt)
unity=${UNITY_EXE:-"/n/Unity/Unity Hub/Editor/$version/Editor/Unity.exe"}
name=${1:-$(git rev-parse --short HEAD)$(git diff --quiet HEAD -- Assets || echo -dirty)}

"$unity" -batchmode -projectPath . -logFile Logs/play_recording.log \
  -executeMethod TacticsECS.EditorTools.PlayRecording.Run -recordName "$name" -recordSeconds "${2:-60}" || true
grep -h "\[PlayRecording\]" Logs/play_recording.log | tail -1
grep -q "\[PlayRecording\] DONE" Logs/play_recording.log
