import { execSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';

const projectDir = process.env.CLAUDE_PROJECT_DIR ?? process.cwd();

function git(args) {
  try {
    return execSync(`git ${args}`, { cwd: projectDir, encoding: 'utf8' }).trim();
  } catch {
    return '';
  }
}

const branch = git('branch --show-current') || '(none)';
const changed = git('status --short').split('\n').filter(Boolean).length;

const lines = [`Current branch: ${branch}`, `Uncommitted files: ${changed}`];

const plansDir = path.join(projectDir, 'docs', 'plans');
if (fs.existsSync(plansDir)) {
  for (const file of fs.readdirSync(plansDir).filter((f) => f.endsWith('.md'))) {
    const text = fs.readFileSync(path.join(plansDir, file), 'utf8');
    const status = text.match(/^Status:\s*(.+)$/m)?.[1] ?? 'unknown';
    lines.push(`Plan ${file}: ${status}`);
  }
}

if (['main', 'master'].includes(branch)) {
  lines.push('Reminder: you are on main. Create a feature branch before writing code. Never commit on main.');
}

console.log(lines.join('\n'));
process.exit(0);
