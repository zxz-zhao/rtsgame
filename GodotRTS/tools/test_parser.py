# -*- coding: utf-8 -*-
import re
import json

code_block = '''```json
{
  "action": "Final Answer",
  "action_input": "测试输出"
}
```'''

blocks = re.findall(r"```[json]*\s*([\[{].*[]}])\s*```", code_block, re.DOTALL | re.IGNORECASE)
print("blocks:", blocks)
for block in blocks:
    json_text = re.sub(r"^[a-zA-Z]+\n", "", block.strip(), flags=re.MULTILINE)
    print("json_text:", repr(json_text))
    print("parsed:", json.loads(json_text))
