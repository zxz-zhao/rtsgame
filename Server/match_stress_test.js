const http = require('http');

const SERVER_PORT = 8080;
const SERVER_HOST = '127.0.0.1';
const CONCURRENT_USERS = 50;

function postJson(path, body, token = null) {
  return new Promise((resolve, reject) => {
    const payload = JSON.stringify(body);
    const options = {
      hostname: SERVER_HOST,
      port: SERVER_PORT,
      path: path,
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        'Content-Length': Buffer.byteLength(payload)
      }
    };
    if (token) {
      options.headers['Authorization'] = `Bearer ${token}`;
    }

    const req = http.request(options, (res) => {
      let data = '';
      res.on('data', chunk => data += chunk);
      res.on('end', () => {
        try {
          resolve({
            statusCode: res.statusCode,
            body: JSON.parse(data)
          });
        } catch (e) {
          resolve({
            statusCode: res.statusCode,
            body: { success: false, error: 'Invalid JSON response' }
          });
        }
      });
    });

    req.on('error', reject);
    req.write(payload);
    req.end();
  });
}

async function runTest() {
  console.log(`=== 开始高并发匹配压力测试 (模拟 ${CONCURRENT_USERS} 名并发玩家) ===`);

  // 1. 并发注册登录
  console.log('\n[步骤 1] 正在并发登录游客账号...');
  const loginStart = Date.now();
  const loginPromises = [];
  for (let i = 0; i < CONCURRENT_USERS; i++) {
    loginPromises.push(
      postJson('/api/guest', { displayName: `Tester_${i}` })
        .then(res => ({ id: i, res }))
        .catch(err => ({ id: i, err }))
    );
  }

  const loginResults = await Promise.all(loginPromises);
  const loginDuration = Date.now() - loginStart;

  let loginSuccess = 0;
  const activePlayers = [];

  for (const item of loginResults) {
    if (item.err) {
      console.error(`玩家 ${item.id} 登录失败:`, item.err.message);
    } else if (item.res.statusCode === 200 && item.res.body.success) {
      loginSuccess++;
      activePlayers.push({
        id: item.id,
        token: item.res.body.token,
        userId: item.res.body.userId
      });
    } else {
      console.warn(`玩家 ${item.id} 登录返回错误:`, item.res.body);
    }
  }

  console.log(`-> 登录结果: 成功 ${loginSuccess}/${CONCURRENT_USERS}，总耗时: ${loginDuration}ms (平均每个 ${Math.round(loginDuration / CONCURRENT_USERS)}ms)`);

  if (activePlayers.length < 2) {
    console.error('成功登录玩家过少，无法测试匹配。请确保本地服务器启动在 127.0.0.1:8080');
    return;
  }

  // 2. 并发匹配
  console.log('\n[步骤 2] 正在模拟高并发加入匹配队列...');
  const matchStart = Date.now();
  const matchPromises = [];

  for (const player of activePlayers) {
    matchPromises.push(
      postJson('/api/match/join', { mapName: '沙漠绿洲' }, player.token)
        .then(res => ({ id: player.id, res }))
        .catch(err => ({ id: player.id, err }))
    );
  }

  const matchResults = await Promise.all(matchPromises);
  const matchDuration = Date.now() - matchStart;

  let matchedCount = 0;
  let waitingCount = 0;
  let failedCount = 0;
  const roomIds = new Set();
  const queueSizes = [];

  for (const item of matchResults) {
    if (item.err) {
      failedCount++;
    } else if (item.res.statusCode === 200 && item.res.body.success) {
      const body = item.res.body;
      if (body.matched) {
        matchedCount++;
        roomIds.add(body.roomId);
      } else {
        waitingCount++;
        queueSizes.push(body.queueSize);
      }
    } else {
      failedCount++;
    }
  }

  console.log(`-> 匹配结果:`);
  console.log(`   - 匹配成功人数: ${matchedCount}`);
  console.log(`   - 生成对局房间数: ${roomIds.size}`);
  console.log(`   - 仍在排队中人数: ${waitingCount}`);
  console.log(`   - 接口请求失败数: ${failedCount}`);
  console.log(`   - 排队中返回的 queueSize 采样: [${queueSizes.slice(0, 10).join(', ')}${queueSizes.length > 10 ? '...' : ''}]`);
  console.log(`   - 总耗时: ${matchDuration}ms (平均每个请求 ${Math.round(matchDuration / activePlayers.length)}ms)`);
  console.log(`\n=== 压力测试完成 ===`);
}

runTest().catch(console.error);
