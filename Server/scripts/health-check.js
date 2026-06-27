#!/usr/bin/env node
const fs = require('fs');
const http = require('http');
const path = require('path');

loadEnv(path.join(__dirname, '..', '.env'));

const healthUrl = process.env.HEALTH_URL || `http://127.0.0.1:${process.env.PORT || 8080}/health`;
const attempts = Number(process.env.HEALTH_ATTEMPTS || 30);
const intervalMs = Number(process.env.HEALTH_INTERVAL_MS || 1000);

function loadEnv(filePath) {
  if (!fs.existsSync(filePath)) return;
  for (const raw of fs.readFileSync(filePath, 'utf8').split(/\r?\n/)) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq <= 0) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'"))) {
      value = value.slice(1, -1);
    }
    if (!process.env[key]) process.env[key] = value;
  }
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function requestHealth() {
  return new Promise((resolve, reject) => {
    const req = http.get(healthUrl, res => {
      let body = '';
      res.setEncoding('utf8');
      res.on('data', chunk => { body += chunk; });
      res.on('end', () => {
        if (res.statusCode !== 200) {
          reject(new Error(`HTTP ${res.statusCode}: ${body.slice(0, 120)}`));
          return;
        }
        try {
          const json = JSON.parse(body);
          if (!json.ok) {
            reject(new Error(`Health response is not ok: ${body.slice(0, 120)}`));
            return;
          }
          resolve(json);
        } catch (err) {
          reject(new Error(`Invalid JSON: ${err.message}`));
        }
      });
    });
    req.setTimeout(5000, () => {
      req.destroy(new Error('timeout'));
    });
    req.on('error', reject);
  });
}

(async () => {
  let lastError = null;
  for (let i = 1; i <= attempts; i += 1) {
    try {
      const health = await requestHealth();
      console.log(`Health OK: ${healthUrl}`);
      console.log(JSON.stringify(health, null, 2));
      return;
    } catch (err) {
      lastError = err;
      console.log(`Health check ${i}/${attempts} failed: ${err.message}`);
      await sleep(intervalMs);
    }
  }
  console.error(`Health check failed after ${attempts} attempts: ${lastError ? lastError.message : 'unknown error'}`);
  process.exit(1);
})();
