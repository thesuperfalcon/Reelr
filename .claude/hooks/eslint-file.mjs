// PostToolUse hook: lints a frontend source file after Claude edits it.
// Exit code 2 sends ESLint's output back to Claude so it fixes the problems.
import { spawnSync } from "node:child_process";
import path from "node:path";

let input = "";
for await (const chunk of process.stdin) input += chunk;

const filePath = JSON.parse(input).tool_input?.file_path ?? "";
const frontend = path.resolve(import.meta.dirname, "../../frontend");
const relative = path.relative(frontend, path.resolve(filePath));

if (relative.startsWith("..") || !/^src[\\/].*\.tsx?$/.test(relative)) process.exit(0);

const eslint = path.join(frontend, "node_modules/eslint/bin/eslint.js");
const result = spawnSync(process.execPath, [eslint, relative], { cwd: frontend, encoding: "utf8" });

if (result.status !== 0) {
  process.stderr.write(`ESLint found problems in ${relative}:\n${result.stdout}${result.stderr}`);
  process.exit(2);
}
