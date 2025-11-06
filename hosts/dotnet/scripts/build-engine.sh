#!/bin/sh

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENGINE_DIR="${SCRIPT_DIR}/../../../engine"
OUTPUT_DIR="${SCRIPT_DIR}/../Hako/Resources"

mkdir -p "${OUTPUT_DIR}"

cd "${ENGINE_DIR}"

./release-hako.sh "${OUTPUT_DIR}"