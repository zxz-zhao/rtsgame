#!/usr/bin/env node

const fs = require('fs');
const path = require('path');
const {
  createStorageConfig,
  ensureDir,
  ensureMysqlStorage,
  fetchMysqlCollection,
  makeTimestamp,
  readJsonCollection,
  summarizeCollection,
  writeMysqlCollection
} = require('./storage-utils');

function printUsage() {
  console.log(`Usage: node scripts/migrate-json-to-mysql.js [options]

Options:
  --dry-run                 Show what would be written without changing MySQL.
  --force                   Overwrite different MySQL rows with JSON source data.
  --backup-file <path>      Backup overwritten MySQL rows to this file.
  --collection <names>      Comma-separated subset of collections to process.
  --help                    Show this help message.
`);
}

function parseArgs(argv) {
  const options = {
    dryRun: false,
    force: false,
    backupFile: '',
    collections: null
  };

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
    if (arg === '--dry-run') {
      options.dryRun = true;
      continue;
    }
    if (arg === '--force') {
      options.force = true;
      continue;
    }
    if (arg === '--backup-file') {
      options.backupFile = argv[i + 1] || '';
      i += 1;
      continue;
    }
    if (arg === '--collection') {
      options.collections = (argv[i + 1] || '')
        .split(',')
        .map(item => item.trim())
        .filter(Boolean);
      i += 1;
      continue;
    }
    if (arg === '--help' || arg === '-h') {
      printUsage();
      process.exit(0);
    }
    throw new Error(`Unknown argument: ${arg}`);
  }

  return options;
}

async function main() {
  const options = parseArgs(process.argv.slice(2));
  const config = createStorageConfig(path.join(__dirname, '..'));

  if (!config.useMysql) {
    console.log('[Migrate] DB_BACKEND=json, skipping MySQL bootstrap.');
    return;
  }

  const selectedCollections = options.collections && options.collections.length > 0
    ? config.collections.filter(name => options.collections.includes(name))
    : config.collections;

  if (selectedCollections.length === 0)
    throw new Error('No collections selected for migration.');

  const pool = await ensureMysqlStorage(config);
  const backup = {};
  const summary = {
    inserted: 0,
    unchanged: 0,
    skippedDifferent: 0,
    overwritten: 0
  };

  try {
    for (const name of selectedCollections) {
      const jsonData = readJsonCollection(config.dataDir, name);
      const jsonSummary = summarizeCollection(name, jsonData);
      const target = await fetchMysqlCollection(pool, config.mysqlTable, name);
      const mysqlSummary = summarizeCollection(name, target.data);

      if (!target.exists) {
        if (options.dryRun) {
          console.log(`[Migrate] INSERT ${name} entries=${jsonSummary.entries} hash=${jsonSummary.hash}`);
        } else {
          await writeMysqlCollection(pool, config.mysqlTable, name, jsonData);
          console.log(`[Migrate] Inserted ${name} entries=${jsonSummary.entries} hash=${jsonSummary.hash}`);
        }
        summary.inserted += 1;
        continue;
      }

      if (jsonSummary.hash === mysqlSummary.hash) {
        console.log(`[Migrate] Unchanged ${name} entries=${jsonSummary.entries} hash=${jsonSummary.hash}`);
        summary.unchanged += 1;
        continue;
      }

      if (!options.force) {
        console.warn(
          `[Migrate] Skipped ${name}: MySQL differs from JSON ` +
          `(json=${jsonSummary.hash.slice(0, 12)} mysql=${mysqlSummary.hash.slice(0, 12)}).`
        );
        summary.skippedDifferent += 1;
        continue;
      }

      backup[name] = target.data;
      if (options.dryRun) {
        console.log(
          `[Migrate] OVERWRITE ${name} json=${jsonSummary.hash.slice(0, 12)} ` +
          `mysql=${mysqlSummary.hash.slice(0, 12)}`
        );
      } else {
        await writeMysqlCollection(pool, config.mysqlTable, name, jsonData);
        console.log(`[Migrate] Overwrote ${name} entries=${jsonSummary.entries} hash=${jsonSummary.hash}`);
      }
      summary.overwritten += 1;
    }

    if (options.force && !options.dryRun && Object.keys(backup).length > 0) {
      let backupFile = options.backupFile;
      if (!backupFile) {
        const backupDir = path.join(config.appDir, 'backups');
        ensureDir(backupDir);
        backupFile = path.join(backupDir, `mysql-backup-before-force-${makeTimestamp()}.json`);
      }
      ensureDir(path.dirname(path.resolve(backupFile)));
      fs.writeFileSync(
        path.resolve(backupFile),
        JSON.stringify({
          createdAt: new Date().toISOString(),
          database: config.mysqlDatabase,
          table: config.mysqlTable,
          collections: backup
        }, null, 2)
      );
      console.log(`[Migrate] Backed up overwritten rows to ${path.resolve(backupFile)}`);
    }

    console.log(
      `[Migrate] Done inserted=${summary.inserted} unchanged=${summary.unchanged} ` +
      `skippedDifferent=${summary.skippedDifferent} overwritten=${summary.overwritten}`
    );
  } finally {
    await pool.end();
  }
}

main().catch(err => {
  console.error('[Migrate] Failed:', err.message);
  process.exit(1);
});
