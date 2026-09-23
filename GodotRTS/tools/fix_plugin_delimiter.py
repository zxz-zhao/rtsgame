# -*- coding: utf-8 -*-
path = '/app/storage/cwd/langgenius/openai_api_compatible-0.0.66@51801b51069b2578c3fcf6d38edc45c45efd8b7ef4fdb25f096bd84d4f95a53d/.venv/lib/python3.12/site-packages/dify_plugin/interfaces/model/openai_compatible/llm.py'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

# Replace the whole delimiter logic with clean, bulletproof newline splitting
old_code = '''        delimiter = credentials.get("stream_mode_delimiter", "\\n\\n")
        delimiter = codecs.decode(delimiter, "unicode_escape")
        with open("/tmp/dify_plugin_debug.log", "a", encoding="utf-8") as f_dbg:
            f_dbg.write(f"--- STREAM START delimiter={repr(delimiter)} ---\\n")'''

new_code = '''        delimiter = "\\n\\n"
        with open("/tmp/dify_plugin_debug.log", "a", encoding="utf-8") as f_dbg:
            f_dbg.write(f"--- STREAM START bulletproof delimiter={repr(delimiter)} ---\\n")'''

if old_code in c:
    c = c.replace(old_code, new_code, 1)
    print("Replaced old_code successfully!")
else:
    # Try finding any delimiter lines
    import re
    c = re.sub(
        r'delimiter\s*=\s*credentials\.get\("stream_mode_delimiter"[^\n]*\n\s*delimiter\s*=\s*codecs\.decode[^\n]*',
        'delimiter = "\\n\\n"',
        c
    )
    print("Replaced regex successfully!")

with open(path, 'w', encoding='utf-8') as f:
    f.write(c)

print("Patching finished!")
