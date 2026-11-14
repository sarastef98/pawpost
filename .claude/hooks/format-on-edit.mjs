import { execFileSync, execSync } from 'node:child_process';
import path from 'node:path';

let data = '';
for await (const chunk of process.stdin) data += chunk;
const input = JSON.parse(data);

const filePath = input.tool_input?.file_path;
if (!filePath) process.exit(0);

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? input.cwd ?? process.cwd();
const rel = path.relative(projectDir, path.resolve(projectDir, filePath)).replaceAll('\\', '/');

try {
  if (rel.endsWith('.cs')) {
    execFileSync('dotnet', ['format', 'whitespace', '.', '--folder', '--include', rel], {
      cwd: projectDir,
      stdio: 'ignore',
      timeout: 60000,
    });
  } else if (
    rel.startsWith('PawsPost.Web/') &&
    /\.(ts|tsx|css|json)$/.test(rel) &&
    !rel.endsWith('schema.d.ts') &&
    !rel.endsWith('package-lock.json')
  ) {
    const inWeb = rel.slice('PawsPost.Web/'.length);
    execSync(`npx prettier --write "${inWeb}"`, {
      cwd: path.join(projectDir, 'PawsPost.Web'),
      stdio: 'ignore',
      timeout: 60000,
    });
  }
} catch {
  // Formatting is a convenience. Never block work because of it.
}

process.exit(0);
