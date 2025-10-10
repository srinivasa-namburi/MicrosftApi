#!/usr/bin/env bash
# Copyright (c) Microsoft Corporation. All rights reserved.
set -euo pipefail

# Test script for KUBERNETES_RESOURCES_CONFIG parsing

echo "Testing KUBERNETES_RESOURCES_CONFIG parsing logic"
echo "=================================================="
echo ""

# Test with object format (your example)
echo "Test 1: Object format"
export KUBERNETES_RESOURCES_CONFIG='{"api-main":{"requests":{"cpu":"500m","memory":"512Mi"},"limits":{"cpu":"1","memory":"1Gi"},"replicas":{"min":2,"max":4}},"web-docgen":{"requests":{"cpu":"250m","memory":"256Mi"},"limits":{"cpu":"500m","memory":"512Mi"},"replicas":{"min":1,"max":3}},"silo":{"requests":{"cpu":"2","memory":"2Gi"},"limits":{"cpu":"4","memory":"4Gi"},"replicas":{"min":2,"max":10}},"mcp-server":{"requests":{"cpu":"250m","memory":"256Mi"},"limits":{"cpu":"500m","memory":"512Mi"},"replicas":{"min":1,"max":1}},"db-setupmanager":{"requests":{"cpu":"250m","memory":"256Mi"},"limits":{"cpu":"500m","memory":"512Mi"}}}'

IS_ARRAY=$(echo "${KUBERNETES_RESOURCES_CONFIG}" | jq 'if type == "array" then "true" else "false" end' -r)
echo "  Format detected: $([ "$IS_ARRAY" == "true" ] && echo "Array" || echo "Object")"

for service in "api-main" "silo" "web-docgen"; do
  if [[ "$IS_ARRAY" == "true" ]]; then
    config=$(echo "${KUBERNETES_RESOURCES_CONFIG}" | jq -r ".[] | select(.name == \"${service}\" or .name == \"$(echo $service | tr '-' '_')\")")
  else
    config=$(echo "${KUBERNETES_RESOURCES_CONFIG}" | jq -r ".\"${service}\" // .\"$(echo $service | tr '-' '_')\" // empty")
  fi

  if [[ -n "$config" ]] && [[ "$config" != "null" ]]; then
    req_cpu=$(echo "$config" | jq -r '.requests.cpu // empty')
    req_mem=$(echo "$config" | jq -r '.requests.memory // empty')
    echo "  $service: cpu=$req_cpu, mem=$req_mem"
  else
    echo "  $service: NOT FOUND"
  fi
done

echo ""

# Test with array format (pipeline default)
echo "Test 2: Array format"
export KUBERNETES_RESOURCES_CONFIG='[{"name":"silo","requests":{"cpu":"500m","memory":"512Mi"},"limits":{"cpu":"2000m","memory":"2048Mi"},"replicas":{"min":2,"max":10}},{"name":"api-main","requests":{"cpu":"250m","memory":"256Mi"},"limits":{"cpu":"1000m","memory":"1024Mi"},"replicas":{"min":2,"max":10}},{"name":"web-docgen","requests":{"cpu":"250m","memory":"256Mi"},"limits":{"cpu":"1000m","memory":"1024Mi"},"replicas":{"min":2,"max":10}}]'

IS_ARRAY=$(echo "${KUBERNETES_RESOURCES_CONFIG}" | jq 'if type == "array" then "true" else "false" end' -r)
echo "  Format detected: $([ "$IS_ARRAY" == "true" ] && echo "Array" || echo "Object")"

for service in "api-main" "silo" "web-docgen"; do
  if [[ "$IS_ARRAY" == "true" ]]; then
    config=$(echo "${KUBERNETES_RESOURCES_CONFIG}" | jq -r ".[] | select(.name == \"${service}\" or .name == \"$(echo $service | tr '-' '_')\")")
  else
    config=$(echo "${KUBERNETES_RESOURCES_CONFIG}" | jq -r ".\"${service}\" // .\"$(echo $service | tr '-' '_')\" // empty")
  fi

  if [[ -n "$config" ]] && [[ "$config" != "null" ]]; then
    req_cpu=$(echo "$config" | jq -r '.requests.cpu // empty')
    req_mem=$(echo "$config" | jq -r '.requests.memory // empty')
    echo "  $service: cpu=$req_cpu, mem=$req_mem"
  else
    echo "  $service: NOT FOUND"
  fi
done

echo ""
echo "Tests complete!"
