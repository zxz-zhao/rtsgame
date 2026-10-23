import os
from pathlib import Path

DEST_DIR = Path("H:/AnimeStudio")

# static/index.html
INDEX_HTML = r'''<!DOCTYPE html>
<html lang="zh-CN">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>AnimeStudio AI - 次世代 3D 电影级动漫漫剧一键制片厂</title>
  <link rel="stylesheet" href="/style.css">
  <link rel="preconnect" href="https://fonts.googleapis.com">
  <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
  <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@400;500;600;700;800&display=swap" rel="stylesheet">
</head>
<body>
  <div class="stars-bg"></div>
  <div class="ambient-glow glow-1"></div>
  <div class="ambient-glow glow-2"></div>

  <header class="app-header">
    <div class="logo-box">
      <div class="logo-badge">🚀 虚幻5引擎级 3D 纵深视差渲染</div>
      <h1 class="logo-title">AnimeStudio <span>3D</span></h1>
      <span class="logo-tag">电影级动漫制片厂</span>
    </div>
    <div class="header-actions">
      <a href="#dify-guide" class="btn-secondary" id="btn-open-dify">🚀 Dify 接入方案</a>
      <a href="/docs" target="_blank" class="btn-secondary">📖 API Swagger</a>
      <div class="status-indicator">
        <span class="dot-online"></span> 本地 3D 引擎就绪 (端口 8088)
      </div>
    </div>
  </header>

  <main class="main-layout">
    <!-- 左侧创作控制面板 -->
    <section class="control-panel glass-card">
      <div class="panel-header">
        <h2>📝 输入文章 / 故事剧本</h2>
        <div class="sample-pills">
          <span class="pill-label">3D高燃示例：</span>
          <button type="button" class="pill-btn" data-sample="donghua_3d">🔥 佛怒火莲(3D国漫)</button>
          <button type="button" class="pill-btn" data-sample="xiuxian_3d">⚔️ 虚天鼎对决(3D修仙)</button>
          <button type="button" class="pill-btn" data-sample="mech_3d">🤖 次世代机甲(3D赛博)</button>
        </div>
      </div>

      <div class="textarea-wrap">
        <textarea id="article-input" placeholder="请在此粘贴或输入你的文章、小说片段、故事文案... 
例如：
苍穹之上，青莲地心火与骨冷幽火在少年掌心剧烈盘旋。
两色截然相反的恐怖异火疯狂压缩，狂暴的能量风暴席卷千丈空间。
萧炎双眸如电，嘴角勾起一抹凌厉笑意，晶莹璀璨的火莲瞬间成型。
“纳兰嫣然，云山老狗，且接我这一记——佛怒火莲！”" rows="7"></textarea>
      </div>

      <div class="settings-grid">
        <div class="form-group">
          <label for="style-select">🎨 动漫视觉风格 (默认虚幻5次世代 3D 渲染)</label>
          <select id="style-select" class="custom-select">
            <option value="donghua_3d_ue5" selected>🐉 次世代 3D 国漫玄幻 (虚幻5/斗破·凡人CG风)</option>
            <option value="donghua_3d_action">⚔️ 3D 电影级玄幻对决 (极限景深战斗特写)</option>
            <option value="scifi_3d_mech">🚀 次世代 3D 科幻机甲 (光追金属与纵深机舱)</option>
            <option value="japanese_3d_cg">✨ 日系 3D 电影级 CG (最终幻想/降临之子风)</option>
            <option value="japanese_anime">🌸 日系新海诚光影风 (2.5D Makoto Shinkai)</option>
            <option value="cyberpunk_anime">🌆 赛博朋克二次元风 (Cyberpunk 2.5D)</option>
            <option value="ghibli">🍃 吉卜力唯美治愈风 (Studio Ghibli 2.5D)</option>
          </select>
        </div>

        <div class="form-group">
          <label for="voice-select">🎙️ 动漫旁白配音</label>
          <select id="voice-select" class="custom-select">
            <option value="zh-CN-YunxiNeural" selected>🔥 云希 (热血男主 / 男频高燃旁白)</option>
            <option value="zh-CN-XiaoxiaoNeural">🌸 晓晓 (灵动少女 / 女主甜美旁白)</option>
            <option value="zh-CN-YunjianNeural">🏛️ 云健 (影视深沉 / 玄幻大帝解说)</option>
            <option value="zh-CN-YunyangNeural">🎙️ 云扬 (专业动漫漫评 / 沉稳解说)</option>
            <option value="zh-CN-XiaoyiNeural">💫 晓伊 (温婉知性 / 治愈物语女声)</option>
          </select>
        </div>

        <div class="form-group">
          <label for="scenes-count">🎬 分镜镜头数: <span id="scenes-val" class="highlight-val">4</span> 镜</label>
          <input type="range" id="scenes-count" min="3" max="8" value="4" class="slider-input">
        </div>

        <div class="form-group checkbox-group">
          <label class="switch-label">
            <input type="checkbox" id="include-bgm" checked>
            <span class="slider-toggle"></span>
            <span>🎵 混入 3D 史诗动漫 BGM 背景音</span>
          </label>
        </div>
      </div>

      <div class="action-footer">
        <button type="button" id="btn-generate" class="btn-primary-large">
          <span class="btn-icon">🎬</span> 一键生成 3D 电影级动漫
        </button>
      </div>

      <!-- 渲染流水线状态浮层 -->
      <div id="pipeline-card" class="pipeline-card hidden">
        <div class="pipeline-header">
          <h3>⚡ 3D 电影级制片流水线正在高速渲染...</h3>
        </div>
        <div class="pipeline-steps">
          <div class="step-item" id="step-storyboard">
            <div class="step-dot"></div>
            <div class="step-info">
              <span class="step-title">1. 3D 导演分镜拆解</span>
              <span class="step-desc">解析叙事弧光，规划 3D 透视角度与立体视差运镜</span>
            </div>
          </div>
          <div class="step-item" id="step-images">
            <div class="step-dot"></div>
            <div class="step-info">
              <span class="step-title">2. 虚幻引擎5次世代 3D 原画生成</span>
              <span class="step-desc">光线追踪、次表面皮肤散射、立体高光雕刻与景深增强</span>
            </div>
          </div>
          <div class="step-item" id="step-tts">
            <div class="step-dot"></div>
            <div class="step-info">
              <span class="step-title">3. 影院级角色旁白配音</span>
              <span class="step-desc">神经网络高保真语音合成与精确毫秒级时间轴对齐</span>
            </div>
          </div>
          <div class="step-item" id="step-compose">
            <div class="step-dot"></div>
            <div class="step-info">
              <span class="step-title">4. 3D 空间单应透视投影与灵气微粒合成</span>
              <span class="step-desc">执行 3D 视锥投影、空间灵气粒子、丁达尔光束与电影级字幕烧录</span>
            </div>
          </div>
        </div>
        <div class="progress-bar-wrap">
          <div class="progress-bar-fill" id="progress-fill"></div>
        </div>
      </div>
    </section>

    <!-- 右侧成片预览与播放大屏 -->
    <section class="preview-panel glass-card">
      <div class="preview-header">
        <h2>🎥 3D 成片巨幕预览</h2>
        <div class="preview-meta">
          <span id="duration-badge" class="badge-tag hidden">0.0 秒</span>
          <span class="badge-tag highlight">1080P · 60FPS 渲染 · 3D 空间透视</span>
        </div>
      </div>

      <div class="video-container" id="video-box">
        <div class="placeholder-box" id="player-placeholder">
          <div class="placeholder-icon">🎞️</div>
          <p>在左侧输入文案并点击「一键生成」，3D 动漫大片将在此即时渲染呈现</p>
        </div>
        <video id="video-player" class="hidden" controls autoplay playsinline></video>
      </div>

      <div class="video-actions hidden" id="video-actions">
        <a id="btn-download" href="#" download class="btn-action">
          <span>⬇️</span> 下载 1080P 超清成片
        </a>
        <button type="button" id="btn-fullscreen" class="btn-action">
          <span>⛶</span> 影院巨幕全屏沉浸播放
        </button>
      </div>
    </section>
  </main>

  <!-- 底部 3D 分镜长廊 -->
  <section class="storyboard-gallery glass-card hidden" id="storyboard-gallery">
    <div class="gallery-header">
      <h2>🎬 镜头分镜脚本长廊 (3D 景深与镜头明细)</h2>
      <p class="gallery-sub">查看每个分镜的 3D 视觉提示词、运镜方式与旁白台词</p>
    </div>
    <div class="cards-stream" id="cards-stream">
      <!-- 动态插入分镜卡片 -->
    </div>
  </section>

  <!-- Dify 接入方案弹窗 -->
  <div class="modal-backdrop hidden" id="dify-modal">
    <div class="modal-content glass-card">
      <div class="modal-header">
        <h3>🔌 在 Dify 工作流与 Agent 中一键接入 AnimeStudio</h3>
        <button type="button" class="modal-close" id="btn-close-dify">&times;</button>
      </div>
      <div class="modal-body">
        <p>AnimeStudio AI 原生兼容 Dify OpenAPI 3.0 工具标准，支持在任何工作流中零代码调用：</p>
        
        <div class="guide-step">
          <h4>第一步：添加自定义工具</h4>
          <p>在 Dify 中进入「工具」->「自定义工具」->「创建自定义工具」：</p>
          <ul>
            <li><strong>工具名称：</strong> <code>AnimeStudio</code></li>
            <li><strong>Schema 地址：</strong> 填写 <code>http://host.docker.internal:8088/openapi.json</code>（Docker 部署环境）或导入 <code>H:\AnimeStudio\dify\dify_tool_schema.json</code></li>
          </ul>
        </div>

        <div class="guide-step">
          <h4>第二步：一键导入预置工作流 DSL</h4>
          <p>系统已在 <code>H:\AnimeStudio\dify\Anime_Video_Workflow.yml</code> 预置了完整 DSL 编排工程。</p>
          <p>进入 Dify -> 「工作室」-> 点击「导入 DSL 文件」-> 选择该文件，即可获得开箱即用的动漫生成 Agent！</p>
        </div>

        <div class="guide-step">
          <h4>第三步：在工作流或聊天助手随意使用</h4>
          <p>用户直接发送文章，Dify Agent 自动调用 <code>generate_anime_video</code> 工具，输出带画面的视频直链与分镜明细！</p>
        </div>
      </div>
    </div>
  </div>

  <script src="/app.js"></script>
</body>
</html>
'''

# static/style.css
STYLE_CSS = r'''* {
  margin: 0;
  padding: 0;
  box-sizing: border-box;
}

:root {
  --bg-dark: #0a0e17;
  --bg-card: rgba(18, 24, 38, 0.78);
  --border-card: rgba(255, 255, 255, 0.08);
  --primary: #8b5cf6;
  --primary-glow: rgba(139, 92, 246, 0.45);
  --accent-cyan: #06b6d4;
  --accent-gold: #f59e0b;
  --text-main: #f8fafc;
  --text-muted: #94a3b8;
  --radius-xl: 18px;
  --radius-md: 10px;
}

body {
  font-family: 'Outfit', -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "PingFang SC", "Microsoft YaHei", sans-serif;
  background-color: var(--bg-dark);
  color: var(--text-main);
  min-height: 100vh;
  overflow-x: hidden;
  position: relative;
  line-height: 1.6;
}

.stars-bg {
  position: fixed;
  inset: 0;
  background-image: radial-gradient(rgba(255, 255, 255, 0.12) 1px, transparent 1px);
  background-size: 32px 32px;
  opacity: 0.6;
  z-index: -2;
}

.ambient-glow {
  position: fixed;
  width: 500px;
  height: 500px;
  border-radius: 50%;
  filter: blur(140px);
  z-index: -1;
  pointer-events: none;
}
.glow-1 {
  top: -100px;
  left: 10%;
  background: rgba(139, 92, 246, 0.18);
}
.glow-2 {
  bottom: 5%;
  right: 5%;
  background: rgba(6, 182, 212, 0.15);
}

.app-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.2rem 2.5rem;
  border-bottom: 1px solid var(--border-card);
  backdrop-filter: blur(12px);
  background: rgba(10, 14, 23, 0.85);
  position: sticky;
  top: 0;
  z-index: 50;
}

.logo-box {
  display: flex;
  align-items: center;
  gap: 0.8rem;
}

.logo-badge {
  background: linear-gradient(135deg, #f59e0b, #ec4899, #8b5cf6);
  color: white;
  font-size: 0.75rem;
  font-weight: 800;
  padding: 0.35rem 0.75rem;
  border-radius: 20px;
  letter-spacing: 0.5px;
  box-shadow: 0 0 15px rgba(245, 158, 11, 0.4);
}

.logo-title {
  font-size: 1.6rem;
  font-weight: 800;
  letter-spacing: -0.5px;
  color: #fff;
}
.logo-title span {
  background: linear-gradient(135deg, #38bdf8, #818cf8);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.logo-tag {
  font-size: 0.85rem;
  color: var(--text-muted);
  border-left: 1px solid rgba(255, 255, 255, 0.15);
  padding-left: 0.8rem;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.status-indicator {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.85rem;
  color: #10b981;
  background: rgba(16, 185, 129, 0.1);
  padding: 0.4rem 0.8rem;
  border-radius: 20px;
  border: 1px solid rgba(16, 185, 129, 0.2);
}
.dot-online {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #10b981;
  box-shadow: 0 0 8px #10b981;
}

.btn-secondary {
  text-decoration: none;
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-main);
  background: rgba(255, 255, 255, 0.05);
  border: 1px solid var(--border-card);
  padding: 0.45rem 0.9rem;
  border-radius: 8px;
  transition: all 0.2s ease;
}
.btn-secondary:hover {
  background: rgba(255, 255, 255, 0.1);
  border-color: rgba(255, 255, 255, 0.2);
}

.glass-card {
  background: var(--bg-card);
  border: 1px solid var(--border-card);
  backdrop-filter: blur(16px);
  border-radius: var(--radius-xl);
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.4);
}

.main-layout {
  display: grid;
  grid-template-columns: 1.15fr 1fr;
  gap: 2rem;
  max-width: 1600px;
  margin: 2rem auto;
  padding: 0 2rem;
}

.control-panel, .preview-panel {
  padding: 1.8rem;
  display: flex;
  flex-direction: column;
}

.panel-header, .preview-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.2rem;
}

.panel-header h2, .preview-header h2 {
  font-size: 1.2rem;
  font-weight: 700;
  color: #fff;
}

.sample-pills {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
.pill-label {
  font-size: 0.8rem;
  color: var(--text-muted);
}
.pill-btn {
  background: rgba(255, 255, 255, 0.06);
  border: 1px solid var(--border-card);
  color: #cbd5e1;
  font-size: 0.75rem;
  padding: 0.25rem 0.6rem;
  border-radius: 12px;
  cursor: pointer;
  transition: all 0.2s;
}
.pill-btn:hover {
  background: rgba(139, 92, 246, 0.2);
  border-color: var(--primary);
  color: #fff;
}

.textarea-wrap {
  margin-bottom: 1.4rem;
}
#article-input {
  width: 100%;
  background: rgba(10, 14, 23, 0.7);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-md);
  padding: 1rem;
  color: #fff;
  font-size: 0.95rem;
  font-family: inherit;
  resize: vertical;
  line-height: 1.6;
  outline: none;
  transition: border-color 0.2s;
}
#article-input:focus {
  border-color: var(--accent-cyan);
  box-shadow: 0 0 12px rgba(6, 182, 212, 0.25);
}

.settings-grid {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 1.2rem;
  margin-bottom: 1.6rem;
}

.form-group {
  display: flex;
  flex-direction: column;
  gap: 0.4rem;
}
.form-group label {
  font-size: 0.85rem;
  font-weight: 600;
  color: var(--text-muted);
}

.custom-select {
  background: rgba(10, 14, 23, 0.7);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: 8px;
  color: #fff;
  padding: 0.65rem 0.8rem;
  font-size: 0.9rem;
  outline: none;
}
.custom-select option {
  background: #0f172a;
  color: #fff;
}

.slider-input {
  accent-color: var(--primary);
  height: 6px;
  cursor: pointer;
}
.highlight-val {
  color: var(--accent-cyan);
  font-weight: 700;
}

.checkbox-group {
  justify-content: center;
  grid-column: span 2;
}
.switch-label {
  display: flex;
  align-items: center;
  gap: 0.6rem;
  font-size: 0.85rem;
  cursor: pointer;
  color: #cbd5e1;
}

.action-footer {
  margin-top: auto;
}
.btn-primary-large {
  width: 100%;
  background: linear-gradient(135deg, #f59e0b, #ec4899 45%, #8b5cf6 90%);
  border: none;
  border-radius: var(--radius-md);
  color: white;
  font-size: 1.05rem;
  font-weight: 800;
  padding: 0.95rem;
  cursor: pointer;
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 0.6rem;
  box-shadow: 0 10px 25px rgba(245, 158, 11, 0.35);
  transition: all 0.25s ease;
}
.btn-primary-large:hover {
  transform: translateY(-2px);
  box-shadow: 0 14px 30px rgba(245, 158, 11, 0.5);
}
.btn-primary-large:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.pipeline-card {
  background: rgba(10, 14, 23, 0.9);
  border: 1px solid rgba(6, 182, 212, 0.3);
  border-radius: var(--radius-md);
  padding: 1.2rem;
  margin-top: 1rem;
}
.pipeline-card h3 {
  font-size: 0.95rem;
  color: var(--accent-cyan);
  margin-bottom: 1rem;
}

.pipeline-steps {
  display: flex;
  flex-direction: column;
  gap: 0.8rem;
  margin-bottom: 1rem;
}
.step-item {
  display: flex;
  align-items: center;
  gap: 0.8rem;
  opacity: 0.4;
  transition: opacity 0.3s;
}
.step-item.active {
  opacity: 1;
}
.step-item.done {
  opacity: 0.8;
}
.step-dot {
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: #64748b;
  flex-shrink: 0;
}
.step-item.active .step-dot {
  background: var(--accent-cyan);
  box-shadow: 0 0 10px var(--accent-cyan);
  animation: pulse 1.5s infinite;
}
.step-item.done .step-dot {
  background: #10b981;
}

.step-info {
  display: flex;
  flex-direction: column;
}
.step-title {
  font-size: 0.85rem;
  font-weight: 700;
  color: #f1f5f9;
}
.step-desc {
  font-size: 0.75rem;
  color: var(--text-muted);
}

.progress-bar-wrap {
  width: 100%;
  height: 6px;
  background: rgba(255, 255, 255, 0.08);
  border-radius: 3px;
  overflow: hidden;
}
.progress-bar-fill {
  height: 100%;
  width: 0%;
  background: linear-gradient(90deg, #f59e0b, #8b5cf6, #06b6d4);
  transition: width 0.4s ease;
}

.preview-meta {
  display: flex;
  gap: 0.5rem;
}
.badge-tag {
  font-size: 0.75rem;
  padding: 0.25rem 0.6rem;
  border-radius: 6px;
  background: rgba(255, 255, 255, 0.08);
  color: var(--text-muted);
}
.badge-tag.highlight {
  background: rgba(6, 182, 212, 0.15);
  color: var(--accent-cyan);
  border: 1px solid rgba(6, 182, 212, 0.3);
}

.video-container {
  width: 100%;
  aspect-ratio: 16 / 9;
  background: #000;
  border-radius: var(--radius-md);
  overflow: hidden;
  position: relative;
  border: 1px solid rgba(255, 255, 255, 0.1);
  box-shadow: 0 15px 35px rgba(0, 0, 0, 0.6);
}
.placeholder-box {
  position: absolute;
  inset: 0;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  color: var(--text-muted);
  font-size: 0.9rem;
  text-align: center;
  padding: 2rem;
}
.placeholder-icon {
  font-size: 3rem;
  margin-bottom: 1rem;
  opacity: 0.5;
}

#video-player {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.video-actions {
  display: flex;
  gap: 1rem;
  margin-top: 1.2rem;
}
.btn-action {
  flex: 1;
  text-decoration: none;
  background: rgba(255, 255, 255, 0.08);
  border: 1px solid var(--border-card);
  color: #fff;
  padding: 0.75rem;
  border-radius: 8px;
  font-size: 0.9rem;
  font-weight: 600;
  display: flex;
  justify-content: center;
  align-items: center;
  gap: 0.5rem;
  cursor: pointer;
  transition: all 0.2s;
}
.btn-action:hover {
  background: rgba(255, 255, 255, 0.15);
  border-color: rgba(255, 255, 255, 0.3);
}

.storyboard-gallery {
  max-width: 1600px;
  margin: 0 auto 3rem auto;
  padding: 1.8rem 2.5rem;
}
.gallery-header {
  margin-bottom: 1.5rem;
}
.gallery-sub {
  font-size: 0.85rem;
  color: var(--text-muted);
}

.cards-stream {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
  gap: 1.4rem;
}

.scene-card {
  background: rgba(15, 23, 42, 0.7);
  border: 1px solid rgba(255, 255, 255, 0.1);
  border-radius: var(--radius-md);
  overflow: hidden;
  display: flex;
  flex-direction: column;
}
.scene-thumb-wrap {
  aspect-ratio: 16 / 9;
  position: relative;
  background: #000;
}
.scene-thumb {
  width: 100%;
  height: 100%;
  object-fit: cover;
}
.scene-motion-tag {
  position: absolute;
  top: 8px;
  left: 8px;
  background: rgba(0, 0, 0, 0.8);
  backdrop-filter: blur(4px);
  color: #fbbf24;
  font-size: 0.72rem;
  font-weight: 700;
  padding: 0.2rem 0.5rem;
  border-radius: 6px;
  border: 1px solid rgba(251, 191, 36, 0.35);
}
.scene-dur-tag {
  position: absolute;
  bottom: 8px;
  right: 8px;
  background: rgba(0, 0, 0, 0.75);
  color: #38bdf8;
  font-size: 0.72rem;
  padding: 0.2rem 0.5rem;
  border-radius: 6px;
}

.scene-meta {
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}
.scene-idx {
  font-size: 0.75rem;
  font-weight: 800;
  color: var(--primary);
  text-transform: uppercase;
}
.scene-narration {
  font-size: 0.88rem;
  font-weight: 600;
  color: #f8fafc;
  line-height: 1.4;
}
.scene-prompt {
  font-size: 0.75rem;
  color: #94a3b8;
  font-style: italic;
  max-height: 48px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.modal-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.75);
  backdrop-filter: blur(8px);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 100;
  padding: 1.5rem;
}
.modal-content {
  max-width: 650px;
  width: 100%;
  background: #0f172a;
  border-color: rgba(139, 92, 246, 0.3);
}
.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 1.2rem;
}
.modal-close {
  background: none;
  border: none;
  color: var(--text-muted);
  font-size: 1.8rem;
  cursor: pointer;
}
.modal-body {
  font-size: 0.9rem;
  color: #cbd5e1;
  display: flex;
  flex-direction: column;
  gap: 1.2rem;
}
.guide-step h4 {
  color: #38bdf8;
  margin-bottom: 0.4rem;
}
.guide-step code {
  background: rgba(255, 255, 255, 0.1);
  color: #fbbf24;
  padding: 0.15rem 0.4rem;
  border-radius: 4px;
}

.hidden {
  display: none !important;
}
'''

# static/app.js
APP_JS = r'''document.addEventListener("DOMContentLoaded", () => {
  const articleInput = document.getElementById("article-input");
  const styleSelect = document.getElementById("style-select");
  const voiceSelect = document.getElementById("voice-select");
  const scenesCount = document.getElementById("scenes-count");
  const scenesVal = document.getElementById("scenes-val");
  const includeBgm = document.getElementById("include-bgm");
  const btnGenerate = document.getElementById("btn-generate");

  const playerPlaceholder = document.getElementById("player-placeholder");
  const videoPlayer = document.getElementById("video-player");
  const videoActions = document.getElementById("video-actions");
  const btnDownload = document.getElementById("btn-download");
  const btnFullscreen = document.getElementById("btn-fullscreen");
  const durationBadge = document.getElementById("duration-badge");

  const pipelineCard = document.getElementById("pipeline-card");
  const progressFill = document.getElementById("progress-fill");
  const stepStoryboard = document.getElementById("step-storyboard");
  const stepImages = document.getElementById("step-images");
  const stepTts = document.getElementById("step-tts");
  const stepCompose = document.getElementById("step-compose");

  const storyboardGallery = document.getElementById("storyboard-gallery");
  const cardsStream = document.getElementById("cards-stream");

  // Dify 弹窗
  const difyModal = document.getElementById("dify-modal");
  const btnOpenDify = document.getElementById("btn-open-dify");
  const btnCloseDify = document.getElementById("btn-close-dify");

  btnOpenDify.addEventListener("click", (e) => {
    e.preventDefault();
    difyModal.classList.remove("hidden");
  });
  btnCloseDify.addEventListener("click", () => difyModal.classList.add("hidden"));
  difyModal.addEventListener("click", (e) => {
    if (e.target === difyModal) difyModal.classList.add("hidden");
  });

  // 分镜滑块联动
  scenesCount.addEventListener("input", (e) => {
    scenesVal.textContent = e.target.value;
  });

  // 示例药丸点击填充
  const SAMPLES = {
    donghua_3d: `苍穹之上，青莲地心火与骨冷幽火在少年掌心剧烈盘旋。
两色截然相反的恐怖异火疯狂压缩，狂暴的能量风暴席卷千丈空间。
萧炎双眸如电，嘴角勾起一抹凌厉笑意，晶莹璀璨的火莲瞬间成型。
“纳兰嫣然，云山老狗，且接我这一记——佛怒火莲！”`,
    xiuxian_3d: `内星海古修遗迹深处，青竹蜂云剑化作漫天青色雷霆狂澜。
韩立眼中青芒微闪，单手掐诀，虚天鼎轰然震落天地。
金色辟邪神雷与空间风暴在虚空中剧烈激荡撕裂。
任凭万千魔宗修士包围，我自纵剑破界，绝尘而去。`,
    mech_3d: `外层空间同步轨道站，深蓝色的次世代重型突击机甲主引擎轰鸣点火。
光追全息座舱内，火控雷达锁定下方重度污染的地表防线。
高频等离子光刃出鞘，机体以十二马赫超音速切入大气层，突破防空天幕。
钢铁与意志的咆哮，将在今夜彻底重构世界秩序。`
  };

  document.querySelectorAll(".pill-btn").forEach(btn => {
    btn.addEventListener("click", () => {
      const type = btn.getAttribute("data-sample");
      if (SAMPLES[type]) {
        articleInput.value = SAMPLES[type];
        if (type === "donghua_3d") styleSelect.value = "donghua_3d_ue5";
        if (type === "xiuxian_3d") styleSelect.value = "donghua_3d_action";
        if (type === "mech_3d") styleSelect.value = "scifi_3d_mech";
      }
    });
  });

  // 全屏播放
  btnFullscreen.addEventListener("click", () => {
    if (videoPlayer.requestFullscreen) videoPlayer.requestFullscreen();
    else if (videoPlayer.webkitRequestFullscreen) videoPlayer.webkitRequestFullscreen();
  });

  // 生成主流程
  btnGenerate.addEventListener("click", async () => {
    const text = articleInput.value.trim();
    if (!text) {
      alert("请输入文章或故事剧本文案！");
      articleInput.focus();
      return;
    }

    btnGenerate.disabled = true;
    btnGenerate.innerHTML = `<span class="btn-icon">⏳</span> 正在高速渲染 3D 动漫视频...`;

    // 展开进度条
    pipelineCard.classList.remove("hidden");
    resetPipeline();
    updatePipelineStep(stepStoryboard, 15);

    try {
      const timer1 = setTimeout(() => updatePipelineStep(stepImages, 45), 2500);
      const timer2 = setTimeout(() => updatePipelineStep(stepTts, 70), 5500);
      const timer3 = setTimeout(() => updatePipelineStep(stepCompose, 88), 9000);

      const payload = {
        article: text,
        style: styleSelect.value,
        voice: voiceSelect.value,
        max_scenes: parseInt(scenesCount.value, 10),
        include_bgm: includeBgm.checked
      };

      const response = await fetch("/api/v1/generate", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(payload)
      });

      clearTimeout(timer1);
      clearTimeout(timer2);
      clearTimeout(timer3);

      if (!response.ok) {
        const err = await response.json();
        throw new Error(err.detail || "生成失败");
      }

      const data = await response.json();
      finishPipeline();

      // 渲染视频播放器
      playerPlaceholder.classList.add("hidden");
      videoPlayer.classList.remove("hidden");
      videoPlayer.src = data.video_url;
      videoPlayer.play();

      videoActions.classList.remove("hidden");
      btnDownload.href = data.download_url;

      durationBadge.classList.remove("hidden");
      durationBadge.textContent = `总时长: ${data.duration_seconds} 秒 · ${data.scene_count} 镜头 (3D视差)`;

      // 渲染分镜卡片长廊
      renderStoryboardCards(data.scenes);

    } catch (err) {
      alert("生成 3D 动漫视频时发生错误: " + err.message);
      pipelineCard.classList.add("hidden");
    } finally {
      btnGenerate.disabled = false;
      btnGenerate.innerHTML = `<span class="btn-icon">🎬</span> 一键生成 3D 电影级动漫`;
    }
  });

  function resetPipeline() {
    [stepStoryboard, stepImages, stepTts, stepCompose].forEach(s => {
      s.className = "step-item";
    });
    progressFill.style.width = "0%";
  }

  function updatePipelineStep(activeStep, percent) {
    activeStep.className = "step-item active";
    progressFill.style.width = `${percent}%`;
    if (activeStep === stepImages) stepStoryboard.className = "step-item done";
    if (activeStep === stepTts) {
      stepStoryboard.className = "step-item done";
      stepImages.className = "step-item done";
    }
    if (activeStep === stepCompose) {
      stepStoryboard.className = "step-item done";
      stepImages.className = "step-item done";
      stepTts.className = "step-item done";
    }
  }

  function finishPipeline() {
    [stepStoryboard, stepImages, stepTts, stepCompose].forEach(s => {
      s.className = "step-item done";
    });
    progressFill.style.width = "100%";
    setTimeout(() => {
      pipelineCard.classList.add("hidden");
    }, 1500);
  }

  const MOTION_NAMES = {
    "dolly_zoom_3d": "🎥 3D 希区柯克空间变焦",
    "dive_push_3d": "🦅 3D 纵深俯冲推近",
    "orbit_3d": "🔄 3D 弧线环视视差",
    "flythrough_3d": "⚡ 3D 空间穿梭飞掠",
    "crane_rise_3d": "🏗️ 3D 摇臂升镜纵深",
    "parallax_left_3d": "◀️ 3D 视差左滑轨",
    "parallax_right_3d": "▶️ 3D 视差右滑轨"
  };

  function renderStoryboardCards(scenes) {
    cardsStream.innerHTML = "";
    storyboardGallery.classList.remove("hidden");

    scenes.forEach(sc => {
      const card = document.createElement("div");
      card.className = "scene-card";
      const motionName = MOTION_NAMES[sc.motion] || sc.motion || "3D 视差推镜";
      card.innerHTML = `
        <div class="scene-thumb-wrap">
          <img src="${sc.image_url}" alt="Scene ${sc.scene_id}" class="scene-thumb" loading="lazy">
          <span class="scene-motion-tag">${motionName}</span>
          <span class="scene-dur-tag">⏱️ ${sc.duration || 3.0}s</span>
        </div>
        <div class="scene-meta">
          <span class="scene-idx">SCENE ${sc.scene_id.toString().padStart(2, "0")}</span>
          <p class="scene-narration">${sc.narration}</p>
          <p class="scene-prompt" title="${sc.prompt}">${sc.prompt}</p>
        </div>
      `;
      cardsStream.appendChild(card);
    });

    storyboardGallery.scrollIntoView({ behavior: "smooth" });
  }
});
'''

with open(DEST_DIR / "static/index.html", "w", encoding="utf-8") as f:
    f.write(INDEX_HTML)
print("Updated H:/AnimeStudio/static/index.html")

with open(DEST_DIR / "static/style.css", "w", encoding="utf-8") as f:
    f.write(STYLE_CSS)
print("Updated H:/AnimeStudio/static/style.css")

with open(DEST_DIR / "static/app.js", "w", encoding="utf-8") as f:
    f.write(APP_JS)
print("Updated H:/AnimeStudio/static/app.js")
