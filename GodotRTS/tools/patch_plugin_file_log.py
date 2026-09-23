# -*- coding: utf-8 -*-
path = '/app/storage/cwd/langgenius/openai_api_compatible-0.0.66@51801b51069b2578c3fcf6d38edc45c45efd8b7ef4fdb25f096bd84d4f95a53d/.venv/lib/python3.12/site-packages/dify_plugin/interfaces/model/openai_compatible/llm.py'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

target = '        delimiter = credentials.get("stream_mode_delimiter", "\\n\\n")\n        delimiter = codecs.decode(delimiter, "unicode_escape")'

replacement = '''        delimiter = credentials.get("stream_mode_delimiter", "\\n\\n")
        delimiter = codecs.decode(delimiter, "unicode_escape")
        with open("/tmp/dify_plugin_debug.log", "a", encoding="utf-8") as f_dbg:
            f_dbg.write(f"--- STREAM START delimiter={repr(delimiter)} ---\\n")'''

if target in c:
    c = c.replace(target, replacement, 1)

target_loop = '        for raw_chunk in response.iter_lines(decode_unicode=True, delimiter=delimiter):'
replacement_loop = '''        for raw_chunk in response.iter_lines(decode_unicode=True, delimiter=delimiter):
            with open("/tmp/dify_plugin_debug.log", "a", encoding="utf-8") as f_dbg:
                f_dbg.write(f"RAW CHUNK: {repr(raw_chunk)}\\n")'''

if target_loop in c:
    c = c.replace(target_loop, replacement_loop, 1)

target_val = '                except ValidationError:'
replacement_val = '''                except ValidationError as ve:
                    with open("/tmp/dify_plugin_debug.log", "a", encoding="utf-8") as f_dbg:
                        f_dbg.write(f"VALIDATION ERROR: {ve} on chunk: {repr(decoded_chunk)}\\n")'''

if target_val in c:
    c = c.replace(target_val, replacement_val, 1)

with open(path, 'w', encoding='utf-8') as f:
    f.write(c)
print("File logger patched successfully!")
