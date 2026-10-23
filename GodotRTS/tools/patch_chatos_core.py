import sys

file_path = r'H:\api\chatos_core.py'
with open(file_path, 'r', encoding='utf-8') as f:
    text = f.read()

target = '''    async with httpx.AsyncClient(timeout=120.0, trust_env=False) as client:
        async with client.stream("GET", event_url, headers=headers) as stream_resp:
            buffer = ""
            async for chunk in stream_resp.aiter_text():
                buffer += chunk
                while "\\n" in buffer:
                    line, buffer = buffer.split("\\n", 1)
                    line = line.strip()
                    if not line or not line.startswith("data:"):
                        continue
                    data_json = line[5:].strip()
                    try:
                        msg_obj = json.loads(data_json)
                        delta_text = msg_obj.get("Data", "")
                        status = msg_obj.get("Status", "")
                        
                        if delta_text:
                            chunk_resp = {
                                "id": completion_id,
                                "object": "chat.completion.chunk",
                                "created": created_ts,
                                "model": model_req,
                                "choices": [{
                                    "index": 0,
                                    "delta": {"content": delta_text},
                                    "finish_reason": None
                                }]
                            }
                            yield f"data: {json.dumps(chunk_resp, ensure_ascii=False)}\\n\\n"
                            
                        if status == "stop":
                            stop_resp = {
                                "id": completion_id,
                                "object": "chat.completion.chunk",
                                "created": created_ts,
                                "model": model_req,
                                "choices": [{
                                    "index": 0,
                                    "delta": {},
                                    "finish_reason": "stop"
                                }]
                            }
                            yield f"data: {json.dumps(stop_resp)}\\n\\n"
                            yield "data: [DONE]\\n\\n"
                            return
                    except Exception:
                        pass
                        
    yield "data: [DONE]\\n\\n"'''

replacement = '''    has_content = False
    try:
        async with httpx.AsyncClient(timeout=120.0, trust_env=False) as client:
            async with client.stream("GET", event_url, headers=headers) as stream_resp:
                buffer = ""
                async for chunk in stream_resp.aiter_text():
                    buffer += chunk
                    while "\\n" in buffer:
                        line, buffer = buffer.split("\\n", 1)
                        line = line.strip()
                        if not line or not line.startswith("data:"):
                            continue
                        data_json = line[5:].strip()
                        try:
                            msg_obj = json.loads(data_json)
                            delta_text = msg_obj.get("Data", "")
                            status = msg_obj.get("Status", "")
                            
                            if delta_text:
                                has_content = True
                                chunk_resp = {
                                    "id": completion_id,
                                    "object": "chat.completion.chunk",
                                    "created": created_ts,
                                    "model": model_req,
                                    "choices": [{
                                        "index": 0,
                                        "delta": {"content": delta_text},
                                        "finish_reason": None
                                    }]
                                }
                                yield f"data: {json.dumps(chunk_resp, ensure_ascii=False)}\\n\\n"
                                
                            if status == "stop":
                                stop_resp = {
                                    "id": completion_id,
                                    "object": "chat.completion.chunk",
                                    "created": created_ts,
                                    "model": model_req,
                                    "choices": [{
                                        "index": 0,
                                        "delta": {},
                                        "finish_reason": "stop"
                                    }]
                                }
                                yield f"data: {json.dumps(stop_resp)}\\n\\n"
                                yield "data: [DONE]\\n\\n"
                                return
                        except Exception:
                            pass
    except Exception as exc:
        err_msg = str(exc).lower()
        if has_content or "incomplete chunked read" in err_msg or "peer closed connection" in err_msg:
            stop_resp = {
                "id": completion_id,
                "object": "chat.completion.chunk",
                "created": created_ts,
                "model": model_req,
                "choices": [{
                    "index": 0,
                    "delta": {},
                    "finish_reason": "stop"
                }]
            }
            yield f"data: {json.dumps(stop_resp)}\\n\\n"
            yield "data: [DONE]\\n\\n"
            return
        raise
                        
    yield "data: [DONE]\\n\\n"'''

if target in text:
    new_text = text.replace(target, replacement)
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(new_text)
    print("chatos_core.py patched successfully!")
else:
    print("Target block not found in chatos_core.py")
