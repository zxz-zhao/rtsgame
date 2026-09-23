# -*- coding: utf-8 -*-
path = '/app/storage/cwd/langgenius/openai_api_compatible-0.0.66@51801b51069b2578c3fcf6d38edc45c45efd8b7ef4fdb25f096bd84d4f95a53d/.venv/lib/python3.12/site-packages/dify_plugin/interfaces/model/openai_compatible/llm.py'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

target = '        delimiter = credentials.get("stream_mode_delimiter", "\\n\\n")\n        delimiter = codecs.decode(delimiter, "unicode_escape")'

replacement = '''        delimiter = credentials.get("stream_mode_delimiter", "\\n\\n")
        delimiter = codecs.decode(delimiter, "unicode_escape")
        print(f"[DIFY_DELIMITER_DEBUG] delimiter={repr(delimiter)}", flush=True)'''

if target in c:
    c = c.replace(target, replacement, 1)

target2 = '                try:\n                    chunk_json: dict = TypeAdapter(dict[str, Any]).validate_json(\n                        decoded_chunk,\n                    )'
replacement2 = '''                try:
                    chunk_json: dict = TypeAdapter(dict[str, Any]).validate_json(
                        decoded_chunk,
                    )
                except ValidationError as ve:
                    print(f"[DIFY_VALIDATION_ERROR] {ve} on chunk: {repr(decoded_chunk)}", flush=True)
                    yield self._create_final_llm_result_chunk(
                        index=chunk_index + 1,
                        message=AssistantPromptMessage(content=""),
                        finish_reason="Non-JSON encountered.",
                        usage=usage,
                        model=model,
                        credentials=credentials,
                        prompt_messages=prompt_messages,
                        full_content=full_assistant_content,
                    )
                    break'''

target_block = '''                try:
                    chunk_json: dict = TypeAdapter(dict[str, Any]).validate_json(
                        decoded_chunk,
                    )
                # stream ended
                except ValidationError:
                    yield self._create_final_llm_result_chunk(
                        index=chunk_index + 1,
                        message=AssistantPromptMessage(content=""),
                        finish_reason="Non-JSON encountered.",
                        usage=usage,
                        model=model,
                        credentials=credentials,
                        prompt_messages=prompt_messages,
                        full_content=full_assistant_content,
                    )
                    break'''

if target_block in c:
    c = c.replace(target_block, replacement2, 1)
    print("Patched target_block successfully!")

with open(path, 'w', encoding='utf-8') as f:
    f.write(c)
print("Finished patching llm.py!")
