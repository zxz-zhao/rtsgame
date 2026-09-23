import subprocess

cmd = [
    "docker", "exec", "rocketchat-mongo", "mongosh", "rocketchat", "--eval",
    'db.rocketchat_settings.updateOne({_id: "Message_MaxAllowedSize"}, {$set: {value: 65535, packageValue: 65535, _updatedAt: new Date()}})'
]
res = subprocess.run(cmd, capture_output=True, text=True)
print("STDOUT:", res.stdout)
print("STDERR:", res.stderr)
