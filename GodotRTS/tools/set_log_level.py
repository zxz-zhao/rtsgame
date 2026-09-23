import subprocess

cmd = ['docker', 'exec', '-i', 'rocketchat-mongo', 'mongo', 'rocketchat', '--eval', 'db.rocketchat_settings.updateOne({_id: "Log_Level"}, {$set: {value: "2"}});']
proc = subprocess.Popen(cmd, stdout=subprocess.PIPE)
print(proc.communicate()[0].decode())
