const express = require('express');
const cors    = require('cors');
const jwt     = require('jsonwebtoken');
const bcrypt  = require('bcryptjs');
const { v4: uuidv4 } = require('uuid');
const fs      = require('fs');
const path    = require('path');
const mysql   = require('mysql2/promise');
const packageInfo = require('./package.json');

loadEnvFile(path.join(__dirname, '.env'));

const app    = express();
const PORT   = Number(process.env.PORT || 8080);
const SECRET = process.env.JWT_SECRET || process.env.SECRET || 'dev-only-change-me';
const IS_PRODUCTION = process.env.NODE_ENV === 'production';
const WS_PORT = Number(process.env.WS_PORT || 8081);
const APP_VERSION = packageInfo.version || '0.0.0';
const BUILD_REVISION = process.env.BUILD_REVISION || process.env.GIT_COMMIT || 'local';
const STARTED_AT = new Date();
const DB_DIR = path.join(__dirname, 'data');
const DB_BACKEND = (process.env.DB_BACKEND || 'mysql').toLowerCase();
const USE_MYSQL = DB_BACKEND !== 'json';
const MYSQL_DB = process.env.MYSQL_DATABASE || process.env.DB_NAME || 'unity_rts';
const MYSQL_TABLE = process.env.MYSQL_TABLE || 'rts_kv_store';
const MYSQL_CONNECT_RETRIES = Math.max(1, Number(process.env.MYSQL_CONNECT_RETRIES || 10));
const MYSQL_CONNECT_RETRY_DELAY_MS = Math.max(250, Number(process.env.MYSQL_CONNECT_RETRY_DELAY_MS || 3000));
const MYSQL_CONFIG = {
  host: process.env.MYSQL_HOST || process.env.DB_HOST || '127.0.0.1',
  port: Number(process.env.MYSQL_PORT || process.env.DB_PORT || 3306),
  user: process.env.MYSQL_USER || process.env.DB_USER || 'root',
  password: process.env.MYSQL_PASSWORD || process.env.DB_PASSWORD || '',
  database: MYSQL_DB,
  waitForConnections: true,
  connectionLimit: Number(process.env.MYSQL_CONNECTION_LIMIT || 10),
  charset: 'utf8mb4'
};
const COLLECTIONS = ['users', 'friends', 'rooms', 'match_queue', 'pending_matches', 'invites', 'orders'];
const memDB = {};
let mysqlPool = null;

if (IS_PRODUCTION && SECRET === 'dev-only-change-me') {
  throw new Error('JWT_SECRET must be set when NODE_ENV=production.');
}

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

// ── 中间件 ─────────────────────────────────────────────────
const corsOrigins = (process.env.CORS_ORIGINS || '')
  .split(',')
  .map(origin => origin.trim())
  .filter(Boolean);
app.use(cors(corsOrigins.length > 0 ? { origin: corsOrigins } : undefined));
app.use(express.json());

app.get('/health', (req, res) => {
  res.json({
    ok: true,
    version: APP_VERSION,
    revision: BUILD_REVISION,
    storage: USE_MYSQL ? 'mysql' : 'json',
    httpPort: PORT,
    wsPort: WS_PORT,
    pid: process.pid,
    startedAt: STARTED_AT.toISOString(),
    uptimeSec: Math.round(process.uptime())
  });
});

// ── JSON 文件数据库 ─────────────────────────────────────────
if (!fs.existsSync(DB_DIR)) fs.mkdirSync(DB_DIR);

function loadDB(name) {
  if (USE_MYSQL) {
    if (!memDB[name]) memDB[name] = {};
    return memDB[name];
  }
  const p = path.join(DB_DIR, name + '.json');
  if (!fs.existsSync(p)) fs.writeFileSync(p, '{}');
  return JSON.parse(fs.readFileSync(p, 'utf8'));
}
function saveDB(name, data) {
  if (USE_MYSQL) {
    memDB[name] = data || {};
    persistCollection(name).catch(err => console.error(`[DB] save ${name} failed:`, err.message));
    return;
  }
  fs.writeFileSync(path.join(DB_DIR, name + '.json'), JSON.stringify(data, null, 2));
}

function readJsonCollection(name) {
  const p = path.join(DB_DIR, name + '.json');
  if (!fs.existsSync(p)) return {};
  try {
    const parsed = JSON.parse(fs.readFileSync(p, 'utf8'));
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch (err) {
    console.warn(`[DB] Ignore invalid JSON file ${p}: ${err.message}`);
    return {};
  }
}

function parseMysqlJson(v) {
  if (!v) return {};
  if (typeof v === 'object') return v;
  try {
    const parsed = JSON.parse(v);
    return parsed && typeof parsed === 'object' ? parsed : {};
  } catch {
    return {};
  }
}

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

async function connectMysqlWithRetry(config) {
  let lastError = null;
  for (let attempt = 1; attempt <= MYSQL_CONNECT_RETRIES; attempt++) {
    try {
      return await mysql.createConnection(config);
    } catch (err) {
      lastError = err;
      if (attempt >= MYSQL_CONNECT_RETRIES)
        break;
      const delay = MYSQL_CONNECT_RETRY_DELAY_MS * attempt;
      console.warn(
        `[DB] MySQL connection attempt ${attempt}/${MYSQL_CONNECT_RETRIES} failed: ${err.message}. ` +
        `Retrying in ${delay}ms...`
      );
      await sleep(delay);
    }
  }
  throw lastError;
}

async function initMysqlStorage() {
  const bootstrapConfig = { ...MYSQL_CONFIG };
  delete bootstrapConfig.database;
  const bootstrap = await connectMysqlWithRetry(bootstrapConfig);
  await bootstrap.query(
    `CREATE DATABASE IF NOT EXISTS \`${MYSQL_DB}\` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`
  );
  await bootstrap.end();

  mysqlPool = await mysql.createPool(MYSQL_CONFIG);
  await mysqlPool.query(`
    CREATE TABLE IF NOT EXISTS \`${MYSQL_TABLE}\` (
      name VARCHAR(64) NOT NULL PRIMARY KEY,
      data JSON NOT NULL,
      updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
  `);

  for (const name of COLLECTIONS) {
    const [rows] = await mysqlPool.query(`SELECT data FROM \`${MYSQL_TABLE}\` WHERE name = ? LIMIT 1`, [name]);
    if (rows.length > 0) {
      memDB[name] = parseMysqlJson(rows[0].data);
      continue;
    }
    memDB[name] = readJsonCollection(name);
    await persistCollection(name);
  }
}

async function persistCollection(name) {
  if (!USE_MYSQL || !mysqlPool) return;
  const json = JSON.stringify(memDB[name] || {});
  await mysqlPool.query(
    `INSERT INTO \`${MYSQL_TABLE}\` (name, data) VALUES (?, ?)
     ON DUPLICATE KEY UPDATE data = VALUES(data)`,
    [name, json]
  );
}

async function initStorage() {
  if (!USE_MYSQL) {
    COLLECTIONS.forEach(n => {
      const p = path.join(DB_DIR, n + '.json');
      if (!fs.existsSync(p)) fs.writeFileSync(p, '{}');
    });
    console.log('[DB] Using JSON file storage.');
    return;
  }

  await initMysqlStorage();
  console.log(`[DB] Using MySQL storage: ${MYSQL_CONFIG.host}:${MYSQL_CONFIG.port}/${MYSQL_DB}.${MYSQL_TABLE}`);
}

// 大厅在线心跳（内存）
const presence = {};

function todayStr() {
  return new Date().toISOString().slice(0, 10);
}

function ensureUserDefaults(u) {
  if (!u) return u;
  if (u.gold == null || u.gold === undefined) u.gold = 1000;
  if (u.gems == null || u.gems === undefined) u.gems = 100;
  if (!u.guildName) {
    u.guildName = "第一游骑兵团";
    u.guildLevel = 3;
  }
  if (!u.lobbyState || typeof u.lobbyState !== 'object') {
    u.lobbyState = {
      day: todayStr(),
      dailyLoginClaimed: false,
      win3Claimed: false,
      destroyClaimed: false,
      winsToday: 0,
      killsToday: 0
    };
  }
  return u;
}

function ensureLobbyDay(u) {
  const t = todayStr();
  if (u.lobbyState.day !== t) {
    u.lobbyState.day = t;
    u.lobbyState.dailyLoginClaimed = false;
    u.lobbyState.win3Claimed = false;
    u.lobbyState.destroyClaimed = false;
    u.lobbyState.winsToday = 0;
    u.lobbyState.killsToday = 0;
  }
}

function rankTitleFrom(u) {
  ensureUserDefaults(u);
  const w = u.wins || 0, l = u.losses || 0, lv = u.level || 1;
  const tot = w + l;
  const wr = tot === 0 ? 0 : w / tot;
  if (w >= 50 && wr >= 0.55) return '最强王者';
  if (w >= 30) return '钻石 I';
  if (w >= 15) return '黄金 II';
  if (w >= 5) return '白银 I';
  if (lv >= 25) return '上校 I';
  if (lv >= 15) return '少校 III';
  if (lv >= 8) return '中尉 II';
  return '列兵';
}

function friendStatus(uid) {
  const queue = loadDB('match_queue');
  if (queue[uid]) return '游戏中';
  const rooms = loadDB('rooms');
  for (const r of Object.values(rooms)) {
    if (r.players && r.players.includes(uid) && r.status === 'playing') return '游戏中';
  }
  if (presence[uid] && Date.now() - presence[uid] < 45000) return '在线';
  return '离线';
}

// 初始化空数据文件
if (!USE_MYSQL) {
  COLLECTIONS.forEach(n => {
    const p = path.join(DB_DIR, n + '.json');
    if (!fs.existsSync(p)) fs.writeFileSync(p, '{}');
  });
}

// ── JWT 验证中间件 ──────────────────────────────────────────
function normalizeRoomMaxPlayers(value) {
  const parsed = Number.parseInt(value, 10);
  if (!Number.isFinite(parsed)) return 2;
  return Math.min(100, Math.max(2, parsed));
}

function detachPlayerFromWaitingRooms(userId, exceptRoomId = '') {
  const rooms = loadDB('rooms');
  let changed = false;
  for (const [id, room] of Object.entries(rooms)) {
    if (!room || room.status !== 'waiting' || id === exceptRoomId || !Array.isArray(room.players) || !room.players.includes(userId))
      continue;

    room.players = room.players.filter(playerId => playerId !== userId);
    if (room.players.length === 0) {
      delete rooms[id];
    } else if (room.hostId === userId) {
      room.hostId = room.players[0];
    }
    changed = true;
  }

  if (changed)
    saveDB('rooms', rooms);
  return rooms;
}

function authMiddleware(req, res, next) {
  const token = req.headers['authorization']?.split(' ')[1];
  if (!token) return res.status(401).json({ error: '未授权' });
  try {
    req.user = jwt.verify(token, SECRET);
    next();
  } catch {
    res.status(401).json({ error: 'Token 无效' });
  }
}

function makeToken(userId) {
  return jwt.sign({ userId }, SECRET, { expiresIn: '7d' });
}

// ════════════════════════════════════════════════════════════
//  AUTH 辅助与路由
// ════════════════════════════════════════════════════════════

const blockedNames = [
  "傻逼", "煞笔", "沙比", "操你妈", "肏", "妈的", "特么的", "王八蛋", "滚蛋", "垃圾", "废柴", "混蛋", "二百五", "婊子", "贱人",
  "fuck", "bitch", "shit", "asshole", "bastard", "sb", "wocao", "caonima"
];

function getNameWeight(str) {
  if (!str) return 0;
  let w = 0;
  for (let i = 0; i < str.length; i++) {
    w += str.charCodeAt(i) > 127 ? 2 : 1;
  }
  return w;
}

function containsBlockedName(str) {
  if (!str) return false;
  const lower = str.toLowerCase();
  return blockedNames.some(word => lower.includes(word));
}

app.post('/api/register', (req, res) => {
  const { username, password } = req.body;
  if (!username) return res.json({ success: false, error: '用户名不能为空' });
  const trimmedUser = username.trim();
  const weight = getNameWeight(trimmedUser);
  if (weight < 4 || weight > 14) {
    return res.json({ success: false, error: '账号长度不符合要求（中文字符算2，英文算1，要求4-14）' });
  }
  if (containsBlockedName(trimmedUser)) {
    return res.json({ success: false, error: '账号包含敏感词或不当言论' });
  }
  if (!password || password.length < 6) return res.json({ success: false, error: '密码至少6个字符' });
  const users = loadDB('users');
  if (Object.values(users).find(u => u.username === trimmedUser))
    return res.json({ success: false, error: '用户名已存在' });
  const id   = uuidv4();
  const hash = bcrypt.hashSync(password, 8);
  users[id]  = { id, username: trimmedUser, password: hash, isGuest: false, level: 1, wins: 0, losses: 0, gold: 1000, gems: 100,
    lobbyState: { day: todayStr(), dailyLoginClaimed: false, win3Claimed: false, destroyClaimed: false, winsToday: 0, killsToday: 0 },
    createdAt: Date.now() };
  saveDB('users', users);
  res.json({ success: true, token: makeToken(id), userId: id, username: trimmedUser, level: 1, gold: 1000, gems: 100, rankTitle: '列兵' });
});

app.post('/api/login', (req, res) => {
  const { username, password } = req.body;
  const users = loadDB('users');
  const user  = Object.values(users).find(u => u.username === username && !u.isGuest);
  if (!user) return res.json({ success: false, error: '账号不存在' });
  if (!bcrypt.compareSync(password, user.password)) return res.json({ success: false, error: '密码错误' });
  ensureUserDefaults(user);
  saveDB('users', users);
  res.json({ success: true, token: makeToken(user.id), userId: user.id,
             username: user.username, level: user.level, wins: user.wins, losses: user.losses,
             gold: user.gold, gems: user.gems, guildName: user.guildName, guildLevel: user.guildLevel, rankTitle: rankTitleFrom(user), isGuest: false });
});

app.post('/api/guest', (req, res) => {
  let guestName = '';
  try {
    if (req.body && req.body.displayName)
      guestName = String(req.body.displayName).trim().replace(/[<>'"]/g, '');
  } catch (_) {}
  if (guestName && (containsBlockedName(guestName) || getNameWeight(guestName) > 14 || getNameWeight(guestName) < 4)) {
    guestName = '';
  }
  if (!guestName || guestName.length < 2)
    guestName = '游客' + Math.floor(Math.random() * 9000 + 1000);
  if (guestName.length > 14) guestName = guestName.slice(0, 14);
  const id  = uuidv4();
  const users = loadDB('users');
  users[id] = { id, username: guestName, isGuest: true, level: 1, wins: 0, losses: 0, gold: 500, gems: 50,
    guildName: "第一游骑兵团", guildLevel: 3,
    lobbyState: { day: todayStr(), dailyLoginClaimed: false, win3Claimed: false, destroyClaimed: false, winsToday: 0, killsToday: 0 },
    createdAt: Date.now() };
  saveDB('users', users);
  res.json({ success: true, token: makeToken(id), userId: id, username: guestName, level: 1, isGuest: true,
             gold: 500, gems: 50, guildName: "第一游骑兵团", guildLevel: 3, rankTitle: '列兵' });
});

app.get('/api/profile', authMiddleware, (req, res) => {
  const users = loadDB('users');
  const user  = users[req.user.userId];
  if (!user) return res.json({ success: false, error: '用户不存在' });
  ensureUserDefaults(user);
  const { password: _, ...safe } = user;
  safe.rankTitle = rankTitleFrom(user);
  res.json({ success: true, ...safe });
});

// 大厅数据聚合（货币、任务、科技）
app.get('/api/lobby', authMiddleware, (req, res) => {
  const users = loadDB('users');
  const u = users[req.user.userId];
  if (!u) return res.json({ success: false, error: '用户不存在' });
  ensureUserDefaults(u);
  ensureLobbyDay(u);

  if (u.activeTech && u.activeTech.endAt <= Date.now()) {
    u.activeTech = null;
  }

  const ls = u.lobbyState;
  const wt = ls.winsToday || 0;
  const kt = ls.killsToday || 0;
  const tasks = [
    { id: 'daily_login', title: '每日登录', cur: 1, max: 1, claimed: !!ls.dailyLoginClaimed, canClaim: !ls.dailyLoginClaimed },
    { id: 'win3', title: '赢得3场战斗', cur: Math.min(wt, 3), max: 3, claimed: !!ls.win3Claimed, canClaim: wt >= 3 && !ls.win3Claimed },
    { id: 'destroy20', title: '摧毁敌方单位', cur: Math.min(kt, 20), max: 20, claimed: !!ls.destroyClaimed, canClaim: kt >= 20 && !ls.destroyClaimed }
  ];

  let techName = null, techDesc = null, techEndAt = 0, techTotalSec = 0, techId = null;
  if (u.activeTech && u.activeTech.endAt > Date.now()) {
    techId = u.activeTech.id;
    techName = u.activeTech.name;
    techDesc = u.activeTech.desc;
    techEndAt = u.activeTech.endAt;
    techTotalSec = u.activeTech.totalSec;
  }

  saveDB('users', users);
  res.json({
    success: true,
    gold: u.gold,
    gems: u.gems,
    rankTitle: rankTitleFrom(u),
    tasks,
    techId, techName, techDesc, techEndAt, techTotalSec
  });
});

app.post('/api/presence', authMiddleware, (req, res) => {
  presence[req.user.userId] = Date.now();
  res.json({ success: true });
});

app.post('/api/tasks/claim', authMiddleware, (req, res) => {
  const { taskId } = req.body || {};
  const users = loadDB('users');
  const u = users[req.user.userId];
  if (!u) return res.json({ success: false, error: '用户不存在' });
  ensureUserDefaults(u);
  ensureLobbyDay(u);
  const ls = u.lobbyState;

  if (taskId === 'daily_login') {
    if (ls.dailyLoginClaimed) return res.json({ success: false, error: '已领取' });
    ls.dailyLoginClaimed = true;
    u.gold = (u.gold || 0) + 100;
  } else if (taskId === 'win3') {
    if (ls.win3Claimed) return res.json({ success: false, error: '已领取' });
    if ((ls.winsToday || 0) < 3) return res.json({ success: false, error: '进度不足' });
    ls.win3Claimed = true;
    u.gold = (u.gold || 0) + 200;
  } else if (taskId === 'destroy20') {
    if (ls.destroyClaimed) return res.json({ success: false, error: '已领取' });
    if ((ls.killsToday || 0) < 20) return res.json({ success: false, error: '进度不足' });
    ls.destroyClaimed = true;
    u.gold = (u.gold || 0) + 150;
    u.gems = (u.gems || 0) + 5;
  } else return res.json({ success: false, error: '未知任务' });

  saveDB('users', users);
  res.json({ success: true, gold: u.gold, gems: u.gems });
});

app.post('/api/tech/start', authMiddleware, (req, res) => {
  const users = loadDB('users');
  const u = users[req.user.userId];
  if (!u) return res.json({ success: false, error: '用户不存在' });
  ensureUserDefaults(u);
  if (u.activeTech && u.activeTech.endAt > Date.now())
    return res.json({ success: false, error: '已有进行中的研究' });
  const totalSec = 2 * 60 * 60;
  u.activeTech = {
    id: 'tank_armor_1',
    name: '战车装甲强化 I',
    desc: '提升战车生命值5%',
    totalSec,
    endAt: Date.now() + totalSec * 1000
  };
  saveDB('users', users);
  res.json({ success: true, tech: { id: u.activeTech.id, name: u.activeTech.name, desc: u.activeTech.desc, endAt: u.activeTech.endAt, totalSec: u.activeTech.totalSec } });
});

app.post('/api/tech/speedup', authMiddleware, (req, res) => {
  const users = loadDB('users');
  const u = users[req.user.userId];
  if (!u) return res.json({ success: false, error: '用户不存在' });
  if (!u.activeTech || u.activeTech.endAt <= Date.now())
    return res.json({ success: false, error: '没有进行中的研究' });
  const cost = 10;
  if ((u.gems || 0) < cost) return res.json({ success: false, error: '钻石不足' });
  u.gems -= cost;
  u.activeTech.endAt = Math.max(Date.now(), u.activeTech.endAt - 10 * 60 * 1000);
  saveDB('users', users);
  res.json({ success: true, gold: u.gold, gems: u.gems, endAt: u.activeTech.endAt });
});

// ════════════════════════════════════════════════════════════
//  好友路由
// ════════════════════════════════════════════════════════════

app.get('/api/friends', authMiddleware, (req, res) => {
  const friends = loadDB('friends');
  const users   = loadDB('users');
  const myList  = (friends[req.user.userId] || [])
    .map(fid => {
      const u = users[fid];
      if (!u) return null;
      ensureUserDefaults(u);
      return { id: u.id, username: u.username, level: u.level, rank: rankTitleFrom(u), status: friendStatus(fid) };
    })
    .filter(Boolean);
  res.json({ success: true, friends: myList });
});

app.get('/api/guild/members', authMiddleware, (req, res) => {
  const users = loadDB('users');
  const currentUser = users[req.user.userId];
  if (!currentUser) return res.json({ success: false, error: '用户不存在' });

  ensureUserDefaults(currentUser);
  const guildName = currentUser.guildName || '无工会';
  if (guildName === '无工会') {
    return res.json({ success: true, members: [] });
  }

  const members = Object.values(users)
    .filter(u => u && u.id !== currentUser.id && u.guildName === guildName)
    .map(u => {
      ensureUserDefaults(u);
      return {
        id: u.id,
        username: u.username,
        level: u.level || 1,
        rank: rankTitleFrom(u),
        status: friendStatus(u.id)
      };
    });

  res.json({ success: true, members });
});

app.get('/api/leaderboard', authMiddleware, (req, res) => {
  const users = loadDB('users');
  const currentUserId = req.user.userId;
  const scoreFor = u => {
    ensureUserDefaults(u);
    const wins = u.wins || 0;
    const losses = u.losses || 0;
    const level = u.level || 1;
    const kills = u.stats && u.stats.kills ? u.stats.kills : 0;
    return wins * 30 + Math.max(0, level) * 8 + kills - losses * 6;
  };

  const ranked = Object.values(users)
    .filter(u => u && u.id && u.username)
    .map(u => {
      ensureUserDefaults(u);
      return {
        id: u.id,
        username: u.username,
        level: u.level || 1,
        wins: u.wins || 0,
        losses: u.losses || 0,
        score: scoreFor(u),
        rankTitle: rankTitleFrom(u),
        status: friendStatus(u.id)
      };
    })
    .sort((a, b) => {
      if (b.score !== a.score) return b.score - a.score;
      if (b.wins !== a.wins) return b.wins - a.wins;
      return a.username.localeCompare(b.username);
    })
    .map((u, index) => ({ ...u, place: index + 1, isCurrent: u.id === currentUserId }));

  let mine = ranked.find(u => u.id === currentUserId) || null;
  let leaderboard = ranked.slice(0, 20);
  if (mine && !leaderboard.some(u => u.id === mine.id)) {
    leaderboard = leaderboard.slice(0, 19);
    leaderboard.push(mine);
  }

  res.json({
    success: true,
    season: 'S3 东线军演赛季',
    resetText: '每周一 05:00 结算军功与战绩',
    leaderboard,
    current: mine
  });
});

app.post('/api/friends/add', authMiddleware, (req, res) => {
  const { friendName } = req.body;
  const users   = loadDB('users');
  const friend  = Object.values(users).find(u => u.username === friendName);
  if (!friend) return res.json({ success: false, error: '用户不存在' });
  if (friend.id === req.user.userId) return res.json({ success: false, error: '不能添加自己' });
  const friends = loadDB('friends');
  if (!friends[req.user.userId]) friends[req.user.userId] = [];
  if (!friends[friend.id])        friends[friend.id]       = [];
  if (!friends[req.user.userId].includes(friend.id)) friends[req.user.userId].push(friend.id);
  if (!friends[friend.id].includes(req.user.userId)) friends[friend.id].push(req.user.userId);
  saveDB('friends', friends);
  res.json({ success: true });
});

// 发送房间邀请给好友
app.post('/api/friends/invite', authMiddleware, (req, res) => {
  const { friendName, roomId } = req.body;
  const users   = loadDB('users');
  const rooms   = loadDB('rooms');
  const sender  = users[req.user.userId];
  if (!sender) return res.json({ success: false, error: '发送者不存在' });

  // 查找目标好友
  const target = Object.values(users).find(u => u.username === friendName);
  if (!target) return res.json({ success: false, error: `找不到用户: ${friendName}` });

  // 验证目标好友在线状态
  const targetStatus = friendStatus(target.id);
  if (targetStatus === '离线') {
    return res.json({ success: false, error: '用户已下线，无法接受邀请' });
  }

  // 验证房间是否有效
  const room = rooms[roomId];
  if (roomId && !room)
    return res.json({ success: false, error: 'ROOM_NOT_FOUND' });
  if (roomId && room && room.status !== 'waiting')
    return res.json({ success: false, error: '该房间已无法加入' });

  // 写入邀请记录
  const invites = loadDB('invites');
  if (!invites[target.id]) invites[target.id] = [];
  const invite = {
    id:         uuidv4(),
    from:       sender.username,
    fromId:     req.user.userId,
    roomId:     roomId || '',
    mapName:    room?.mapName || '',
    maxPlayers: room?.maxPlayers || 2,
    playerCount: room?.players?.length || 0,
    createdAt:  Date.now()
  };
  // 去重：若已有同一 sender+room 的未处理邀请则覆盖
  invites[target.id] = invites[target.id].filter(
    i => !(i.fromId === req.user.userId && i.roomId === (roomId || ''))
  );
  invites[target.id].push(invite);
  // 只保留最近 20 条
  if (invites[target.id].length > 20)
    invites[target.id] = invites[target.id].slice(-20);
  saveDB('invites', invites);
  res.json({ success: true, inviteId: invite.id });
});

// 查询当前用户的待处理邀请
app.get('/api/invites', authMiddleware, (req, res) => {
  const invites = loadDB('invites');
  const users = loadDB('users');
  const rooms = loadDB('rooms');
  const myInvites = (invites[req.user.userId] || [])
    .filter(i => {
      // 过了10分钟邀请链接失效
      const timeValid = Date.now() - i.createdAt < 10 * 60 * 1000;
      if (!timeValid) return false;

      // 房主退出了，房间自动失效
      if (i.roomId) {
        const room = rooms[i.roomId];
        if (!room || room.status !== 'waiting') return false;
        if (room.hostId !== i.fromId) return false;
        if (!room.players || !room.players.includes(i.fromId)) return false;
      }
      return true;
    })
    .map(i => {
      const sender = users[i.fromId];
      let level = 1;
      let rank = '列兵';
      if (sender) {
        ensureUserDefaults(sender);
        level = sender.level || 1;
        rank = rankTitleFrom(sender);
      }
      return {
        ...i,
        level,
        rank
      };
    });
  res.json({ success: true, invites: myInvites });
});

// 接受/拒绝邀请
app.post('/api/invites/respond', authMiddleware, (req, res) => {
  const { inviteId, accept } = req.body;
  const invites = loadDB('invites');
  const myList  = invites[req.user.userId] || [];
  const idx     = myList.findIndex(i => i.id === inviteId);
  if (idx < 0) return res.json({ success: false, error: '邀请不存在' });
  const invite  = myList[idx];

  if (accept && invite.roomId) {
    // 验证超时 (10分钟)
    if (Date.now() - invite.createdAt > 10 * 60 * 1000) {
      return res.json({ success: false, error: '邀请已过期失效' });
    }

    const rooms = loadDB('rooms');
    const room = rooms[invite.roomId];

    // 房主退出了，房间自动失效
    if (!room || room.status !== 'waiting' || room.hostId !== invite.fromId || !room.players.includes(invite.fromId)) {
      return res.json({ success: false, error: '该房间已失效或无法加入' });
    }

    myList.splice(idx, 1);
    invites[req.user.userId] = myList;
    saveDB('invites', invites);

    return res.json({
      success: true,
      roomId: invite.roomId,
      mapName: room?.mapName || invite.mapName,
      maxPlayers: room?.maxPlayers || invite.maxPlayers || 2,
      playerCount: room?.players?.length || invite.playerCount || 0
    });
  }

  myList.splice(idx, 1);
  invites[req.user.userId] = myList;
  saveDB('invites', invites);
  res.json({ success: true });
});

// ════════════════════════════════════════════════════════════
//  房间路由
// ════════════════════════════════════════════════════════════

app.get('/api/rooms', authMiddleware, (req, res) => {
  const rooms   = loadDB('rooms');
  const cutoff  = Date.now() - 24 * 60 * 60 * 1000;
  let   changed = false;
  for (const id of Object.keys(rooms)) {
    if (rooms[id].status !== 'waiting' || rooms[id].createdAt < cutoff)
      { delete rooms[id]; changed = true; }
  }
  if (changed) saveDB('rooms', rooms);
  const list = Object.values(rooms).slice(-20);
  res.json({ success: true, rooms: list });
});

app.post('/api/rooms/create', authMiddleware, (req, res) => {
  const { mapName = '沙漠绿洲', roomName } = req.body;
  const users = loadDB('users');
  const user  = users[req.user.userId];
  const rooms = detachPlayerFromWaitingRooms(req.user.userId);
  const id    = uuidv4();
  const maxPlayers = normalizeRoomMaxPlayers(req.body?.maxPlayers);
  const defaultName = (user?.username || '玩家') + '的房间';
  rooms[id]   = { id, name: (roomName && roomName.trim()) ? roomName.trim() : defaultName, mapName,
                  hostId: req.user.userId, status: 'waiting', players: [req.user.userId],
                  maxPlayers, createdAt: Date.now() };
  saveDB('rooms', rooms);
  res.json({ success: true, roomId: id, mapName, maxPlayers, playerCount: 1 });
});

app.post('/api/rooms/join', authMiddleware, (req, res) => {
  const { roomId } = req.body;
  const rooms = detachPlayerFromWaitingRooms(req.user.userId, roomId);
  const room  = rooms[roomId];
  if (!room || room.status !== 'waiting') return res.json({ success: false, error: '房间不存在或已开始' });
  if (room.players.length >= room.maxPlayers) return res.json({ success: false, error: '房间已满' });
  if (!room.players.includes(req.user.userId)) room.players.push(req.user.userId);
  saveDB('rooms', rooms);
  res.json({
    success: true,
    roomId,
    mapName: room.mapName,
    maxPlayers: room.maxPlayers || 2,
    playerCount: room.players.length
  });
});

app.post('/api/rooms/leave', authMiddleware, (req, res) => {
  const { roomId } = req.body;
  const rooms = loadDB('rooms');
  const room  = rooms[roomId];
  if (room) {
    if (room.hostId === req.user.userId) {
      // 房主退出了，房间自动失效
      delete rooms[roomId];
    } else {
      room.players = room.players.filter(p => p !== req.user.userId);
      if (room.players.length === 0) {
        delete rooms[roomId];
      }
    }
    saveDB('rooms', rooms);
  }
  res.json({ success: true });
});

// ════════════════════════════════════════════════════════════
//  匹配路由
// ════════════════════════════════════════════════════════════

app.post('/api/match/join', authMiddleware, (req, res) => {
  const { mapName = '沙漠绿洲' } = req.body;
  const uid = req.user.userId;

  // 先检查是否已有为本玩家存好的匹配结果（由对方触发）
  const pending = loadDB('pending_matches');
  if (pending[uid]) {
    const { roomId, mapName: mName } = pending[uid];
    delete pending[uid];
    saveDB('pending_matches', pending);
    return res.json({ success: true, matched: true, roomId, mapName: mName });
  }

  const queue = loadDB('match_queue');
  queue[uid] = { userId: uid, mapName, joinedAt: Date.now() };

  // 寻找同地图的对手
  const other = Object.values(queue).find(q => q.userId !== uid && q.mapName === mapName);
  if (other) {
    const roomId = uuidv4();
    const rooms  = loadDB('rooms');
    rooms[roomId] = { id: roomId, name: '匹配房间', mapName,
                      hostId: uid, status: 'playing',
                      players: [uid, other.userId], maxPlayers: 2, createdAt: Date.now() };
    saveDB('rooms', rooms);
    delete queue[uid]; delete queue[other.userId];
    saveDB('match_queue', queue);
    // 为先等待的玩家存储匹配结果，供其下次轮询时领取
    pending[other.userId] = { roomId, mapName };
    saveDB('pending_matches', pending);
    return res.json({ success: true, matched: true, roomId, mapName });
  }
  saveDB('match_queue', queue);
  const queueSize = Object.values(queue).filter(q => q.mapName === mapName).length;
  res.json({ success: true, matched: false, queueSize });
});

app.post('/api/match/cancel', authMiddleware, (req, res) => {
  const queue = loadDB('match_queue');
  delete queue[req.user.userId];
  saveDB('match_queue', queue);
  res.json({ success: true });
});

// ── 启动 ───────────────────────────────────────────────────

// ── 对局结果上报 ─────────────────────────────────────────
app.post('/api/result', authMiddleware, (req, res) => {
  const { win, kills, duration } = req.body;
  const users = loadDB('users');
  const uid   = req.user.userId;
  if (!users[uid]) return res.status(404).json({ success: false });
  const u = users[uid];
  ensureUserDefaults(u);
  ensureLobbyDay(u);
  if (!u.stats) u.stats = { wins: 0, losses: 0, kills: 0 };
  if (parseInt(win)) { u.stats.wins++; u.wins = (u.wins || 0) + 1; u.lobbyState.winsToday = (u.lobbyState.winsToday || 0) + 1; }
  else { u.stats.losses++; u.losses = (u.losses || 0) + 1; }
  u.stats.kills = (u.stats.kills || 0) + (parseInt(kills) || 0);
  u.lobbyState.killsToday = (u.lobbyState.killsToday || 0) + (parseInt(kills) || 0);
  u.gold = (u.gold || 0) + (parseInt(win) ? 80 : 25);
  saveDB('users', users);
  res.json({ success: true });
});

// ════════════════════════════════════════════════════════════
//  支付与兑换路由
// ════════════════════════════════════════════════════════════

const GEM_PRODUCTS = [
  { id: 'gem_60', name: '60 钻石', amountFen: 600, gems: 60, bonusGems: 0 },
  { id: 'gem_300', name: '330 钻石', amountFen: 3000, gems: 300, bonusGems: 30 },
  { id: 'gem_680', name: '760 钻石', amountFen: 6800, gems: 680, bonusGems: 80 },
  { id: 'gem_1280', name: '1480 钻石', amountFen: 12800, gems: 1280, bonusGems: 200 }
];

app.get('/api/payments/catalog', authMiddleware, (req, res) => {
  res.json({ success: true, products: GEM_PRODUCTS });
});

app.post('/api/payments/create', authMiddleware, (req, res) => {
  const { productId, provider } = req.body;
  const product = GEM_PRODUCTS.find(p => p.id === productId);
  if (!product) return res.json({ success: false, error: '商品不存在' });

  const users = loadDB('users');
  const user = users[req.user.userId];
  if (!user) return res.json({ success: false, error: '用户不存在' });

  const orderId = uuidv4();
  const orders = loadDB('orders');

  const qrCodeUrl = provider === 'alipay'
    ? `alipay://platformapi/startapp?saId=10000007&qrcode=http://127.0.0.1:8080/mock/pay/${orderId}`
    : `weixin://wxpay/bizpayurl?pr=mock_${orderId}`;

  const order = {
    id: orderId,
    userId: req.user.userId,
    productId: product.id,
    productName: product.name,
    currency: 'CNY',
    status: 'pending',
    provider: provider || 'alipay',
    amountFen: product.amountFen,
    gems: product.gems,
    bonusGems: product.bonusGems,
    qrCodeUrl: qrCodeUrl,
    createdAt: Date.now()
  };

  orders[orderId] = order;
  saveDB('orders', orders);

  res.json({ success: true, order });
});

app.post('/api/payments/confirm', authMiddleware, (req, res) => {
  const { orderId } = req.body;
  const orders = loadDB('orders');
  const order = orders[orderId];
  if (!order) return res.json({ success: false, error: '订单不存在' });
  if (order.userId !== req.user.userId) return res.json({ success: false, error: '无权操作此订单' });

  const users = loadDB('users');
  const user = users[req.user.userId];
  if (!user) return res.json({ success: false, error: '用户不存在' });

  ensureUserDefaults(user);
  if (order.status !== 'paid') {
    const addGems = (order.gems || 0) + (order.bonusGems || 0);
    user.gems = (user.gems || 0) + addGems;
    order.status = 'paid';
    order.paidAt = Date.now();
    saveDB('orders', orders);
    saveDB('users', users);
  }

  res.json({ success: true, gems: user.gems, gold: user.gold });
});

app.post('/api/gold/buy', authMiddleware, (req, res) => {
  const { gems } = req.body;
  const gemsToConvert = parseInt(gems, 10);
  if (isNaN(gemsToConvert) || gemsToConvert <= 0) {
    return res.json({ success: false, error: '无效的兑换数额' });
  }

  const users = loadDB('users');
  const user = users[req.user.userId];
  if (!user) return res.json({ success: false, error: '用户不存在' });

  ensureUserDefaults(user);
  if ((user.gems || 0) < gemsToConvert) {
    return res.json({ success: false, error: '钻石不足，无法兑换金币' });
  }

  const goldGained = gemsToConvert * 100;
  user.gems -= gemsToConvert;
  user.gold = (user.gold || 0) + goldGained;

  saveDB('users', users);

  res.json({ success: true, gems: user.gems, gold: user.gold, goldGained });
});

let httpServer = null;

function startHttpServer() {
  return new Promise((resolve, reject) => {
    httpServer = app.listen(PORT, '0.0.0.0', () => {
      console.log(`HTTP server listening: http://0.0.0.0:${PORT}`);
      console.log(`Local address: http://127.0.0.1:${PORT}`);
      console.log(`LAN address: use ipconfig to find your IP, port ${PORT}`);
      resolve();
    });
    httpServer.once('error', reject);
  });
}

// ════════════════════════════════════════════════════════════
//  WebSocket 游戏内中继 (port 8081)
//  协议: { type, token, roomId, data }
//  角色: 先连接者 = host (蓝方), 后连接者 = guest (红方)
// ════════════════════════════════════════════════════════════
const { WebSocketServer } = require('ws');
let wss = null;

// roomId -> { host: ws, hostId, guest: ws, guestId }
const gameRooms = {};

function startWebSocketServer() {
  return new Promise((resolve, reject) => {
    wss = new WebSocketServer({ port: WS_PORT });
    wss.once('listening', () => {
      console.log(`WebSocket relay listening: ws://0.0.0.0:${WS_PORT}`);
      resolve();
    });
    wss.once('error', reject);

    wss.on('connection', ws => {
  let userId = null, roomId = null, role = null;

  function send(target, obj) {
    if (target && target.readyState === 1)
      target.send(JSON.stringify(obj));
  }

  ws.on('message', raw => {
    let msg;
    try { msg = JSON.parse(raw.toString()); } catch { return; }

    // ── 加入游戏房间 ──
    if (msg.type === 'join') {
      try {
        const payload = jwt.verify(msg.token, SECRET);
        userId = payload.userId;
        roomId = msg.roomId || 'default';

        if (!gameRooms[roomId])
          gameRooms[roomId] = { host: null, hostId: null, guest: null, guestId: null };

        const room = gameRooms[roomId];
        if (!room.hostId) {
          room.host = ws; room.hostId = userId; role = 'host';
          send(ws, { type: 'role', role: 'host', seed: room.seed = Math.floor(Math.random() * 99999) });
        } else if (!room.guestId) {
          room.guest = ws; room.guestId = userId; role = 'guest';
          send(ws,   { type: 'role', role: 'guest', seed: room.seed });
          send(room.host, { type: 'peer_joined', peerId: userId });
          send(ws,        { type: 'peer_joined', peerId: room.hostId });
        } else {
          send(ws, { type: 'error', msg: '房间已满' }); ws.close(); return;
        }
        console.log(`[WS] ${userId} joined room ${roomId} as ${role}`);
      } catch(e) {
        send(ws, { type: 'error', msg: 'Invalid token' }); ws.close();
      }
      return;
    }

    // ── 游戏指令中继 ──
    if (msg.type === 'cmd' && roomId && gameRooms[roomId]) {
      const room = gameRooms[roomId];
      const target = role === 'host' ? room.guest : room.host;
      send(target, { type: 'cmd', data: msg.data, from: role });
      return;
    }

    // ── 心跳 ──
    if (msg.type === 'ping') { send(ws, { type: 'pong' }); return; }
  });

  ws.on('close', () => {
    if (!roomId || !gameRooms[roomId]) return;
    const room = gameRooms[roomId];
    const other = role === 'host' ? room.guest : room.host;
    send(other, { type: 'peer_left' });
    delete gameRooms[roomId];
    console.log(`[WS] Room ${roomId} closed (${role} disconnected)`);
  });

  ws.on('error', err => console.error('[WS] Error:', err.message));
    });
  });
}

let shuttingDown = false;

function closeHttpServer() {
  if (!httpServer) return Promise.resolve();
  return new Promise(resolve => {
    httpServer.close(err => {
      if (err) console.error('[Lifecycle] HTTP close failed:', err.message);
      resolve();
    });
  });
}

function closeWebSocketServer(signal) {
  if (!wss) return Promise.resolve();
  const restartMessage = JSON.stringify({ type: 'server_restarting', signal });
  for (const client of wss.clients) {
    try {
      if (client.readyState === 1) client.send(restartMessage);
      client.close(1012, 'Service restart');
    } catch (err) {
      console.error('[Lifecycle] WS client close failed:', err.message);
    }
  }
  return new Promise(resolve => {
    wss.close(err => {
      if (err) console.error('[Lifecycle] WS close failed:', err.message);
      resolve();
    });
  });
}

async function closeStorage() {
  if (!mysqlPool) return;
  await mysqlPool.end();
}

async function shutdown(signal) {
  if (shuttingDown) return;
  shuttingDown = true;
  console.log(`[Lifecycle] ${signal} received, shutting down gracefully...`);
  const forceExit = setTimeout(() => {
    console.error('[Lifecycle] Graceful shutdown timed out.');
    process.exit(1);
  }, Number(process.env.SHUTDOWN_TIMEOUT_MS || 10000));
  forceExit.unref();

  await Promise.allSettled([
    closeWebSocketServer(signal),
    closeHttpServer(),
    closeStorage()
  ]);
  clearTimeout(forceExit);
  console.log('[Lifecycle] Shutdown complete.');
  process.exit(0);
}

process.once('SIGINT', () => shutdown('SIGINT'));
process.once('SIGTERM', () => shutdown('SIGTERM'));

async function main() {
  try {
    await initStorage();
    await Promise.all([startHttpServer(), startWebSocketServer()]);
    if (typeof process.send === 'function') process.send('ready');
  } catch (err) {
    console.error('[Startup] Failed to start server:', err.message);
    console.error('[DB] Check MYSQL_HOST / MYSQL_PORT / MYSQL_USER / MYSQL_PASSWORD / MYSQL_DATABASE, or set DB_BACKEND=json temporarily.');
    process.exit(1);
  }
}

main();
