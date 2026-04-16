#!/usr/bin/env bash
# JKW Monitor hook — forwards Claude Code events to the local monitor app.
# Never exits non-zero; app failures are silent so Claude is never blocked.

ENDPOINT="http://127.0.0.1:7849/"

# Read the full hook payload from stdin
PAYLOAD=$(cat)

# POST to JKW Monitor; ignore any failures (app may not be running)
curl \
    --silent \
    --max-time 1 \
    --request POST \
    --header "Content-Type: application/json" \
    --data "$PAYLOAD" \
    "$ENDPOINT" \
  >/dev/null 2>&1 || true

# Always exit 0 — this hook is a passive observer, never blocks Claude Code
exit 0
