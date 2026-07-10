#!/usr/bin/env node

const path = require('path');
const {
  createStorageConfig,
  ensureMysqlStorage,
  fetchMysqlCollection,
  readJsonCollection,
  summarizeCollection
} = require('./storage-utils');

function printUsage() {
  console.log(`Usage: node scripts/verify-json-to-mysql.js [options]

Options:
  --collection <names>      Comma-separated subset of collections to compare.
  --help                    Show this help message.
`);
}

function parseArgs(argv) {
  const options = {
    collections: null
  };

  for (let i = 0; i < argv.length; i += 1) {
    const arg = argv[i];
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

  if (!config.useMysql)
    throw new Error('DB_BACKEND=json. Verification only applies to MySQL deployments.');

  const selectedCollections = options.collections && options.collections.length > 0
    ? config.collections.filter(name => options.collections.includes(name))
    : config.collections;

  if (selectedCollections.length === 0)
    throw new Error('No collections selected for verification.');

  const pool = await ensureMysqlStorage(config);
  let mismatches = 0;

  try {
    for (const name of selectedCollections) {
      const jsonData = readJsonCollection(config.dataDir, name);
      const jsonSummary = summarizeCollection(name, jsonData);
      const target = await fetchMysqlCollection(pool, config.mysqlTable, name);

      if (!target.exists) {
        mismatches += 1;
        console.error(`[Verify] MISSING ${name} in MySQL`);
        continue;
      }

      const mysqlSummary = summarizeCollection(name, target.data);
      if (jsonSummary.hash !== mysqlSummary.hash) {
        mismatches += 1;
        console.error(
          `[Verify] MISMATCH ${name} ` +
          `json(entries=${jsonSummary.entries}, hash=${jsonSummary.hash}) ` +
          `mysql(entries=${mysqlSummary.entries}, hash=${mysqlSummary.hash})`
        );
        continue;
      }

      console.log(`[Verify] OK ${name} entries=${jsonSummary.entries} hash=${jsonSummary.hash}`);
    }
  } finally {
    await pool.end();
  }

  if (mismatches > 0) {
    console.error(`[Verify] Failed with ${mismatches} mismatch(es).`);
    process.exit(1);
  }

  console.log('[Verify] JSON and MySQL collections match.');
}

main().catch(err => {
  console.error('[Verify] Failed:', err.message);
  process.exit(1);
});
