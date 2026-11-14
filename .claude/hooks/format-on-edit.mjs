import { execFileSync } from 'node:child_process';
import path from 'node:path';

let data = '';
for await (const chunk of process.stdin) data += chunk;
const input = JSON.parse(data);

const filePath = input.tool_input?.file_path;
if (!filePath || !filePath.endsWith('.cs')) process.exit(0);

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? input.cwd ?? process.cwd();
const rel = path.relative(projectDir, path.resolve(projectDir, filePath)).replaceAll('\\', '/');

try {
  execFileSync('dotnet', ['format', 'whitespace', '.', '--folder', '--include', rel], {
    cwd: projectDir,
    stdio: 'ignore',
    timeout: 60000,
  });
} catch {
  // Formatting is a convenience. Never block work because of it.
}

process.exit(0);
