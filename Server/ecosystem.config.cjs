module.exports = {
  apps: [
    {
      name: process.env.PM2_APP_NAME || 'unity-rts-server',
      script: './server.js',
      cwd: __dirname,
      instances: 1,
      exec_mode: 'fork',
      wait_ready: true,
      listen_timeout: Number(process.env.PM2_LISTEN_TIMEOUT_MS || 10000),
      kill_timeout: Number(process.env.SHUTDOWN_TIMEOUT_MS || 10000),
      max_memory_restart: process.env.PM2_MAX_MEMORY || '512M',
      env: {
        NODE_ENV: 'production'
      },
      out_file: './logs/server.out.log',
      error_file: './logs/server.err.log',
      merge_logs: true,
      time: true
    }
  ]
};
