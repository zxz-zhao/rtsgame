# -*- coding: utf-8 -*-
from core.agent.output_parser.cot_output_parser import CotAgentOutputParser
from graphon.model_runtime.entities.llm_entities import LLMResultChunk, LLMResultChunkDelta
from graphon.model_runtime.entities.message_entities import AssistantPromptMessage
from core.agent.entities import AgentScratchpadUnit

raw_text = """Thought: 用户询问我的核心定位。
Action:
```json
{
  "action": "Final Answer",
  "action_input": "我是特工"
}
```"""

def gen():
    for char in raw_text:
        yield LLMResultChunk(
            model="test",
            prompt_messages=[],
            system_fingerprint="",
            delta=LLMResultChunkDelta(index=0, message=AssistantPromptMessage(content=char), usage=None)
        )

usage_dict = {}
chunks = list(CotAgentOutputParser.handle_react_stream_output(gen(), usage_dict))
print("CHUNKS COUNT:", len(chunks))
for c in chunks:
    print("CHUNK TYPE:", type(c), "VAL:", repr(c))
    if isinstance(c, AgentScratchpadUnit.Action):
        print("ACTION FOUND!", c.action_name, c.action_input)
