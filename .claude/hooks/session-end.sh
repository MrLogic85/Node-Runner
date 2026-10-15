#!/usr/bin/env bash
# SessionEnd (#1320): removes the state session-start.sh recorded.
. "${0%/*}/common.sh"

state=$(state_dir) && rm -rf "$state"
exit 0
