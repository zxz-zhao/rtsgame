with open(r'H:\deepseek\resources\app\lib\main.js', 'r', encoding='utf-8') as f:
    text = f.read()

patch_code = """
const _origQuit = app.quit.bind(app);
const _origExit = app.exit.bind(app);
app.quit = function(...args) {
    try { appendFileSync("H:\\\\deepseek\\\\quit_reason.log", new Date().toISOString() + " app.quit called:\\n" + new Error().stack + "\\n\\n"); } catch (_) {}
    return _origQuit(...args);
};
app.exit = function(...args) {
    try { appendFileSync("H:\\\\deepseek\\\\quit_reason.log", new Date().toISOString() + " app.exit called:\\n" + new Error().stack + "\\n\\n"); } catch (_) {}
    return _origExit(...args);
};
"""

if '_origQuit' not in text:
    anchor = 'if (typeof process !== "undefined" && process.env && !process.env.DSH_PROFILE) process.env.DSH_PROFILE = "desktop";'
    new_text = text.replace(anchor, anchor + "\n" + patch_code)
    with open(r'H:\deepseek\resources\app\lib\main.js', 'w', encoding='utf-8') as f:
        f.write(new_text)
    print("Patched successfully!")
else:
    print("Already patched!")
