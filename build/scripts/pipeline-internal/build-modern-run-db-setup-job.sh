#!/usr/bin/env bash
set -euo pipefail

# Run the DB SetupManager as a one-shot Kubernetes Job from inside the cluster context.
# Usage: build-modern-run-db-setup-job.sh <namespace> [sleep_before_job_seconds]

NS=${1:-greenlight-dev}
SLEEP_BEFORE_JOB=${2:-45}
SA_NAME=${WORKLOAD_IDENTITY_SERVICE_ACCOUNT:-greenlight-app}

echo "[dbjob] Namespace: $NS | ServiceAccount: $SA_NAME | Sleep: ${SLEEP_BEFORE_JOB}s"

# Ensure kubectl context is present prior to this step in the pipeline.

# Back off briefly to let WI/roles/ports settle
echo "[dbjob] Sleeping ${SLEEP_BEFORE_JOB}s before starting job"
sleep "$SLEEP_BEFORE_JOB" || true

# Scale any lingering deployment to 0
kubectl -n "$NS" scale deployment/db-setupmanager-deployment --replicas=0 2>/dev/null || true

# Determine image from the deployed deployment (authoritative)
IMAGE=$(kubectl -n "$NS" get deploy db-setupmanager-deployment -o jsonpath='{.spec.template.spec.containers[0].image}' 2>/dev/null || true)
if [[ -z "$IMAGE" ]]; then
  echo "[dbjob] ERROR: Could not resolve db-setupmanager image from deployment" >&2
  exit 1
fi
echo "[dbjob] Using image: $IMAGE"

run_job() {
  local suffix="$1"
  local name="db-setupmanager-job-${suffix}"
  cat <<EOF | kubectl apply -f -
apiVersion: batch/v1
kind: Job
metadata:
  name: $name
  namespace: $NS
spec:
  backoffLimit: 0
  ttlSecondsAfterFinished: 1800
  template:
    metadata:
      labels:
        app: aspire
        component: db-setupmanager
        azure.workload.identity/use: "true"
    spec:
      serviceAccountName: $SA_NAME
      restartPolicy: Never
      containers:
        - name: db-setupmanager
          image: "$IMAGE"
          imagePullPolicy: IfNotPresent
          envFrom:
            - configMapRef:
                name: db-setupmanager-config
            - secretRef:
                name: db-setupmanager-secrets
            - configMapRef:
                name: workload-identity-env
EOF

  echo "[dbjob] Waiting for job completion (up to 15m)"
  if ! kubectl -n "$NS" wait --for=condition=complete job/$name --timeout=15m; then
    echo "[dbjob] ERROR: Job did not complete"
    kubectl -n "$NS" describe job $name | tail -n 80 || true
    kubectl -n "$NS" logs job/$name --tail=200 || true
    return 1
  fi

  local logs
  logs=$(kubectl -n "$NS" logs job/$name --tail=500 || true)
  echo "$logs" | tail -n 200
  if echo "$logs" | grep -qiE "SqlException.*Login failed for user|Error Number:18456"; then
    echo "[dbjob] Detected transient SQL login failure; will retry"
    return 2
  fi
  echo "[dbjob] Job completed successfully"
  return 0
}

RETRIES=5
DELAY=90
ATTEMPT=1
echo "[dbjob] Starting DB SetupManager with retry logic: max $RETRIES attempts, ${DELAY}s between retries"
while : ; do
  TS=$(date +%Y%m%d%H%M%S)
  echo "[dbjob] === Attempt $ATTEMPT/$RETRIES ==="
  run_job "$TS"
  rc=$?
  if [[ $rc -eq 0 ]]; then
    echo "[dbjob] ✅ DB SetupManager completed successfully on attempt $ATTEMPT"
    break
  elif [[ $rc -eq 2 && $ATTEMPT -lt $RETRIES ]]; then
    echo "[dbjob] ⚠️ Transient failure detected on attempt $ATTEMPT/$RETRIES"
    echo "[dbjob] Waiting ${DELAY}s before retry (workload identity token propagation)"
    sleep "$DELAY" || true
    ATTEMPT=$((ATTEMPT+1))
  else
    echo "[dbjob] ❌ SetupManager job failed permanently after $ATTEMPT attempts (rc=$rc)"
    exit 1
  fi
done

echo "[dbjob] Completed"

