env_files = [
    r"C:\Users\Administrator\.dsh\.env",
    r"C:\Users\Administrator\.dsh-acc2\.env"
]

for p in env_files:
    with open(p, "w", encoding="utf-8") as f:
        f.write("DEEPSEEK_API_KEY=pfWhAHWl2Qa595Z0Mb04qt2ds6KmE8cQS33vrRuQCsYKAi1Q\n")
    print(f"Cleaned {p}: only DEEPSEEK_API_KEY")
