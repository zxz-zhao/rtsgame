Set WshShell = CreateObject("WScript.Shell")
WshShell.Environment("PROCESS")("DEEPSEEK_BASE_URL") = ""
WshShell.Environment("PROCESS")("DEEPSEEK_API_KEY") = ""
WshShell.Run """H:\deepseek\DeepSeek Harness.exe""", 1, False
