#!/bin/sh

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ENGINE_DIR="${SCRIPT_DIR}/../../../engine"
HAKO_MODULE="${SCRIPT_DIR}/../Hako/Resources/hako.wasm"
OUT_FILE="${SCRIPT_DIR}/../Hako/Host/HakoRegistry.cs"

# Check dependencies
if ! command -v bun >/dev/null 2>&1; then
    echo "Error: bun not found in PATH"
    exit 1
fi

if ! command -v wasm-objdump >/dev/null 2>&1; then
    echo "Error: wasm-objdump not found in PATH"
    exit 1
fi

# Check if required files exist
if [ ! -f "${HAKO_MODULE}" ]; then
    echo "Error: hako.wasm not found at ${HAKO_MODULE}"
    exit 1
fi

if [ ! -f "${ENGINE_DIR}/hako.h" ]; then
    echo "Error: hako.h not found at ${ENGINE_DIR}/hako.h"
    exit 1
fi

if [ ! -f "${ENGINE_DIR}/codegen.ts" ]; then
    echo "Error: codegen.ts not found at ${ENGINE_DIR}/codegen.ts"
    exit 1
fi

# Create temp directory
WORK_DIR=$(mktemp -d)
if [ ! -d "${WORK_DIR}" ]; then
    echo "Error: Could not create temp dir"
    exit 1
fi

# Cleanup trap
cleanup() {
    rm -rf "${WORK_DIR}"
}
trap cleanup EXIT

# Generate bindings
bun "${ENGINE_DIR}/codegen.ts" parse "${HAKO_MODULE}" "${ENGINE_DIR}/hako.h" "${WORK_DIR}/bindings.json"

# Generate C# code  
bun "${ENGINE_DIR}/codegen.ts" generate csharp "${WORK_DIR}/bindings.json" "${OUT_FILE}"