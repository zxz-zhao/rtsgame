file_path = r'H:\api\server.py'
with open(file_path, 'r', encoding='utf-8') as f:
    text = f.read()

target = '''                    except Exception as exc:
                        if not acc_marked:
                            is_auth = getattr(exc, "is_auth_error", False)
                            pool.mark_failure(acc_id, str(exc), is_auth_error=is_auth)
                            
                        err_chunk = {
                            "error": {
                                "message": f"ChatOS Stream Error: {str(exc)}",
                                "type": "chatos_error"
                            }
                        }
                        yield "data: " + json.dumps(err_chunk, ensure_ascii=False) + "\\n\\n"
                        yield "data: [DONE]\\n\\n"'''

replacement = '''                    except Exception as exc:
                        if full_content:
                            pool.mark_success(acc_id)
                            acc_marked = True
                            stop_resp = {
                                "id": f"chatcmpl-chatos-{uuid.uuid4().hex[:12]}",
                                "object": "chat.completion.chunk",
                                "created": int(time.time()),
                                "model": model_req,
                                "choices": [{
                                    "index": 0,
                                    "delta": {},
                                    "finish_reason": "stop"
                                }]
                            }
                            yield f"data: {json.dumps(stop_resp, ensure_ascii=False)}\\n\\n"
                            yield "data: [DONE]\\n\\n"
                            return
                            
                        if not acc_marked:
                            is_auth = getattr(exc, "is_auth_error", False)
                            pool.mark_failure(acc_id, str(exc), is_auth_error=is_auth)
                            
                        err_chunk = {
                            "error": {
                                "message": f"ChatOS Stream Error: {str(exc)}",
                                "type": "chatos_error"
                            }
                        }
                        yield "data: " + json.dumps(err_chunk, ensure_ascii=False) + "\\n\\n"
                        yield "data: [DONE]\\n\\n"'''

if target in text:
    new_text = text.replace(target, replacement)
    with open(file_path, 'w', encoding='utf-8') as f:
        f.write(new_text)
    print("server.py patched successfully!")
else:
    print("Target block not found in server.py")
