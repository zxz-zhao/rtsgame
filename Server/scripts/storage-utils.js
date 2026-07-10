const crypto = require('crypto');
const fs = require('fs');
const path = require('path');
const mysql = require('mysql2/promise');

const COLLECTIONS = ['users', 'friends', 'rooms', 'match_queue', 'pending_matches', 'invites'];

function loadEnvFile(filePath) {
  if (!fs.existsSync(filePath)) return;
  const lines = fs.readFileSync(filePath, 'utf8').split(/\r?\n/);
  for (const raw of lines) {
    const line = raw.trim();
    if (!line || line.startsWith('#')) continue;
    const eq = line.indexOf('=');
    if (eq <= 0) continue;
    const key = line.slice(0, eq).trim();
    let value = line.slice(eq + 1).trim();
    if ((value.startsWith('"') && value.endsWith('"')) || (value.startsWith("'") && value.endsWith("'")))
      value = value.slice(1, -1);
    if (!process.env[key]) process.env[key] = value;
  }
}

function createStorageConfig(appDir = path.join(__dirname, '..')) {
  loadEnvFile(path.join(appDir, '.env'));

  const dbBackend = (process.env.DB_BACKEND || 'mysql').toLowerCase();
  const mysqlDatabase = process.env.MYSQL_DATABASE || process.env.DB_NAME || 'unity_rts';
  const mysqlTable = process.env.MYSQL_TABLE || 'rts_kv_store';

  return {
    appDir,
    dataDir: path.join(appDir, 'data'),
    dbBackend,
    useMysql: dbBackend !== 'json',
    mysqlDatabase,
    mysqlTable,
    collections: COLLECTIONS.slice(),
    mysqlConfig: {
      host: process.env.MYSQL_HOST || process.env.DB_HOST || '127.0.0.1',
      port: Number(process.env.MYSQL_PORT || process.env.DB_PORT || 3306),
      user: process.env.MYSQL_USER || process.env.DB_USER || 'root',
      password: process.env.MYSQL_PASSWORD || process.env.DB_PASSWORD || '',
      database: mysqlDatabase,
      waitForConnections: true,
      connectionLimit: Number(process.env.MYSQL_CONNECTION_LIMIT || 10),
      charset: 'utf8mb4'
    }
  };
}

function normalizeCollection(value) {
  if (!value || typeof value !== 'object' || Array.isArray(value))
    return {};
  return value;
}

function readJsonCollection(dataDir, name) {
  const filePath = path.join(dataDir, `${name}.json`);
  if (!fs.existsSync(filePath)) return {};
  const raw = fs.readFileSync(filePath, 'utf8').trim();
  if (!raw) return {};
  return normalizeCollection(JSON.parse(raw));
}

function sortJson(value) {
  if (Array.isArray(value))
    return value.map(sortJson);
  if (value && typeof value === 'object') {
    const out = {};
    for (const key of Object.keys(value).sort())
      out[key] = sortJson(value[key]);
    return out;
  }
  return value;
}

function stableStringify(value) {
  return JSON.stringify(sortJson(value));
}

function summarizeCollection(name, data) {
  const json = stableStringify(normalizeCollection(data));
  return {
    name,
    entries: Object.keys(normalizeCollection(data)).length,
    bytes: Buffer.byteLength(json, 'utf8'),
    hash: crypto.createHash('sha256').update(json).digest('hex')
  };
}

function parseMysqlJson(value) {
  if (!value) return {};
  if (typeof value === 'object') return normalizeCollection(value);
  return normalizeCollection(JSON.parse(value));
}

async function ensureMysqlStorage(config) {
  const bootstrapConfig = { ...config.mysqlConfig };
  delete bootstrapConfig.database;

  const bootstrap = await mysql.createConnection(bootstrapConfig);
  await bootstrap.query(
    `CREATE DATABASE IF NOT EXISTS \`${config.mysqlDatabase}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`
  );
  await bootstrap.end();

  const pool = await mysql.createPool(config.mysqlConfig);
  await pool.query(`
    CREATE TABLE IF NOT EXISTS \`${config.mysqlTable}\` (
      name VARCHAR(64) NOT NULL PRIMARY KEY,
      data JSON NOT NULL,
      updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  `);
  return pool;
}

async function fetchMysqlCollection(pool, tableName, name) {
  const [rows] = await pool.query(
    `SELECT data, updated_at FROM \`${tableName}\` WHERE name = ? LIMIT 1`,
    [name]
  );
  if (rows.length === 0)
    return { exists: false, data: {}, updatedAt: null };

  return {
    exists: true,
    data: parseMysqlJson(rows[0].data),
    updatedAt: rows[0].updated_at || null
  };
}

async function writeMysqlCollection(pool, tableName, name, data) {
  await pool.query(
    `INSERT INTO \`${tableName}\` (name, data) VALUES (?, ?)
     ON DUPLICATE KEY UPDATE data = VALUES(data)`,
    [name, JSON.stringify(normalizeCollection(data))]
  );
}

function ensureDir(dirPath) {
  fs.mkdirSync(dirPath, { recursive: true });
}

function makeTimestamp() {
  const now = new Date();
  const pad = value => String(value).padStart(2, '0');
  return [
    now.getFullYear(),
    pad(now.getMonth() + 1),
    pad(now.getDate()),
    '-',
    pad(now.getHours()),
    pad(now.getMinutes()),
    pad(now.getSeconds())
  ].join('');
}

module.exports = {
  COLLECTIONS,
  createStorageConfig,
  ensureDir,
  ensureMysqlStorage,
  fetchMysqlCollection,
  loadEnvFile,
  makeTimestamp,
  readJsonCollection,
  stableStringify,
  summarizeCollection,
  writeMysqlCollection
};
