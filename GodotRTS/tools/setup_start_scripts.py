with open(r"C:\Users\Administrator\.dsh\start.cmd", "w", encoding="gbk") as f:
    f.write('@echo off\nset "DSH_HOME=C:\\Users\\Administrator\\.dsh"\nset "DEEPSEEK_API_KEY=pfWhAHWl2Qa595Z0Mb04qt2ds6KmE8cQS33vrRuQCsYKAi1Q"\nset "DEEPSEEK_BASE_URL=http://127.0.0.1:3000"\nstart "" "H:\\deepseek\\DeepSeek Harness.exe"\n')

with open(r"C:\Users\Administrator\.dsh-acc2\start.cmd", "w", encoding="gbk") as f:
    f.write('@echo off\nset "DSH_HOME=C:\\Users\\Administrator\\.dsh-acc2"\nset "DEEPSEEK_API_KEY=pfWhAHWl2Qa595Z0Mb04qt2ds6KmE8cQS33vrRuQCsYKAi1Q"\nset "DEEPSEEK_BASE_URL=http://127.0.0.1:3000"\nstart "" "H:\\deepseek\\DeepSeek Harness.exe" --user-data-dir=C:\\Users\\Administrator\\.dsh-acc2\\userData\n')

print("Created start.cmd scripts successfully.")
