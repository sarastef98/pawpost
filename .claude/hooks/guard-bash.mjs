import { execSync } from 'node:child_process';

let data = '';
for await (const chunk of process.stdin) data += chunk;
const input = JSON.parse(data);

const command = input.tool_input?.command ?? '';
const cwd = input.cwd ?? process.cwd();

function block(reason) {
  console.error(`Blocked by hook: ${reason}`);
  process.exit(2);
}

function currentBranch() {
  try {
    return execSync('git branch --show-current', { cwd, encoding: 'utf8' }).trim();
  } catch {
    return '';
  }
}

const tokens = command.split(/\s+/);
const isPush = /\bgit\s+push\b/.test(command);
const isCommit = /\bgit\s+commit\b/.test(command);
const protectedBranches = ['main', 'master'];

if (isPush && (/\s--force\b/.test(command) || /\s-f\b/.test(command) || /--force-with-lease/.test(command))) {
  block('force-push is not allowed.');
}

if (isPush) {
  const namesProtected = tokens.some(
    (t) => protectedBranches.includes(t) || /:(main|master)$/.test(t)
  );
  if (namesProtected || protectedBranches.includes(currentBranch())) {
    block('never push to main. Push a feature branch instead.');
  }
}

if (isCommit && protectedBranches.includes(currentBranch())) {
  block('never commit on main. Create a feature branch first with git switch -c feature/<issue>-<slug>, as a separate command.');
}

if (/\bgit\s+reset\s+--hard\b/.test(command)) {
  block('git reset --hard can destroy work. Ask the user first.');
}

if (/\bgit\s+clean\s+-[a-zA-Z]*f/.test(command)) {
  block('git clean -f deletes files for good. Ask the user first.');
}

if (/\brm\s+-[a-zA-Z]*([rR][a-zA-Z]*f|f[a-zA-Z]*[rR])/.test(command)) {
  block('rm -rf is not allowed. Ask the user to delete things.');
}

if (/\bdotnet\s+ef\s+database\s+drop\b/.test(command)) {
  block('dropping the database is not allowed. Ask the user.');
}

if (/\b(cat|type|more|less|head|tail)\s+[^|;&]*\.env\b/.test(command)) {
  block('do not read .env files. They contain secrets.');
}

process.exit(0);
