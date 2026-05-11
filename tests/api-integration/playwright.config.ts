import { defineConfig } from '@playwright/test';
import { execSync } from 'node:child_process';
import { mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import path from 'node:path';

const authDir = path.join(__dirname, '.auth');
const tokenFile = path.join(authDir, 'token.json');
const seederProject = path.resolve(__dirname, '../../src/Spydersoft.AuditSeeder');
const appHostProject = path.resolve(__dirname, '../../src/Spydersoft.Audit.AppHost');
const baseUrl = process.env.AUDIT_BASE_URL ?? 'http://localhost:5296';

function getToken(): string {
  if (process.env.AUDIT_TEST_TOKEN) {
    return process.env.AUDIT_TEST_TOKEN;
  }
  if (!existsSync(tokenFile)) {
    try {
      const output = execSync(`dotnet run --project "${seederProject}" -- --token-only`, {
        encoding: 'utf-8',
        stdio: ['ignore', 'pipe', 'ignore'],
        timeout: 60_000,
      });
      const json = output.split('\n').map(l => l.trim()).find(l => l.startsWith('{')) ?? '{}';
      const token = (JSON.parse(json) as { token?: string }).token ?? '';
      mkdirSync(authDir, { recursive: true });
      writeFileSync(tokenFile, JSON.stringify({ token }));
      return token;
    } catch {
      return '';
    }
  }
  try {
    return JSON.parse(readFileSync(tokenFile, 'utf-8')).token ?? '';
  } catch {
    return '';
  }
}

const token = getToken();

export default defineConfig({
  testDir: './tests',
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: 'html',
  use: {
    baseURL: baseUrl,
    extraHTTPHeaders: {
      Authorization: `Bearer ${token}`,
    },
  },
  webServer: {
    command: `dotnet run --project "${appHostProject}" --launch-profile Testing`,
    url: `${baseUrl}/livez`,
    timeout: 180_000,
    reuseExistingServer: !process.env.CI,
  },
});
