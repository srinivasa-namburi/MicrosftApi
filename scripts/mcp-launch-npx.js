#!/usr/bin/env node
// Cross-platform NPX launcher for MCP servers.
// Windows: wraps with `cmd /c`
// *nix: calls npx directly

const { spawn } = require("node:child_process");
const isWin = process.platform === "win32";

const argv = process.argv.slice(2);
if (argv.length === 0) {
  console.error("Usage: mcp-launch-npx <npm-package> [args...]");
  process.exit(64); // EX_USAGE
}

const cmd = isWin ? "cmd" : "npx";
const args = isWin ? ["/c", "npx", "-y", ...argv] : ["-y", ...argv];

const child = spawn(cmd, args, {
  stdio: "inherit",
  env: process.env,
  shell: false,
});

child.on("error", (err) => {
  console.error(`Failed to start process: ${err.message}`);
  process.exit(1);
});

const forward = (signal) => {
  if (child.pid) child.kill(signal);
};
process.on("SIGINT", () => forward("SIGINT"));
process.on("SIGTERM", () => forward("SIGTERM"));

child.on("exit", (code, signal) => {
  if (signal) {
    // Re-raise so parent exits with the same signal
    process.kill(process.pid, signal);
  } else {
    process.exit(code ?? 0);
  }
});
