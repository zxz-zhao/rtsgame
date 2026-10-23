@echo off
set DOCKER_EXE=D:\Program Files\Docker\resources\bin\docker.exe

for %%C in (rocketchat rocketchat-app) do (
    "%DOCKER_EXE%" ps --filter "name=%%C" --format "{{.Names}}" | findstr "%%C" >nul
    if not errorlevel 1 (
        echo [INFO] Found running container: %%C
        "%DOCKER_EXE%" exec -u 0 %%C chmod 666 /app/bundle/programs/server/app/app.js
        "%DOCKER_EXE%" exec -u 0 %%C node -e "const fs = require('fs'); const file = '/app/bundle/programs/server/app/app.js'; let c = fs.readFileSync(file, 'utf8'); c = c.replace(/const removePatchInfo = [^;]+;/, 'const removePatchInfo = version => \'8.8.1\';'); c = c.replace(/exports\\.Info\\s*=\\s*\\{\\s*\"version\":\\s*\"[^\"]+\"/, 'exports.Info = {\\n    \"version\": \"8.8.1\"'); fs.writeFileSync(file, c, 'utf8'); console.log('Successfully patched ' + file + ' to 8.8.1!');"
        "%DOCKER_EXE%" restart %%C
        echo [INFO] Restarted %%C successfully.
        goto :done
    )
)

echo [ERROR] No rocketchat container found running.
:done
