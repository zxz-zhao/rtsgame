import os

p1 = r'E:\dify\docker\nginx\conf.d\default.conf'
p2 = r'E:\dify\docker\nginx\conf.d\default.conf.template'

redirect_rules = '''    # Redirect embedded installed app requests to standalone web app pages
    location ~* ^/(explore/)?installed/(6299c648-4775-4d7c-a4fe-a4b57236b587|c271447f-5a9b-48d1-a403-af7b0306c7ee) {
      return 302 $scheme://$http_host/chat/k3XZzC40AdSgADAN;
    }
    location ~* ^/(explore/)?installed/(6e50e89f-af79-4ed6-8434-7a9fbf933df8|a7546eca-608d-4dc3-b09e-07e0660202e3) {
      return 302 $scheme://$http_host/chat/K2I75pwQFm4eitRi;
    }
    location ~* ^/(explore/)?installed/(d78a7aa4-d995-4169-b10f-04179692bfa3|511f67f3-4409-405b-9287-431afab50f22) {
      return 302 $scheme://$http_host/chat/aK4Z7N1mnGIqGxif;
    }
    location ~* ^/(explore/)?installed/(620d3349-8048-42ae-ba05-1c3f800efaac|ef72f8c8-a202-4a61-b756-3770808da851) {
      return 302 $scheme://$http_host/chat/E8W4zenZFZu1Clnq;
    }
    location ~* ^/(explore/)?installed/(f0ed8e6d-b258-4798-92f9-268cdd29c64e|65f8189c-59c5-41b7-9893-a638c7233eff) {
      return 302 $scheme://$http_host/chat/XGfFt2vOTQizDVnO;
    }
    location ~* ^/(explore/)?installed/(9a01866d-2fc3-4785-bace-dc1664619861|312a1b85-780c-4b72-8ae1-7e23edd3c542) {
      return 302 $scheme://$http_host/chat/bSqS0Xsz4CqBJZlW;
    }

    location /explore {'''

for path in [p1, p2]:
    if not os.path.exists(path):
        continue
    with open(path, 'r', encoding='utf-8') as f:
        data = f.read()

    # 1. Add /portal proxy if not present
    if 'location /portal' not in data:
        portal_block = '''    location /portal {
      proxy_pass http://host.docker.internal:8000/portal;
      include proxy.conf;
    }

    location /screenshots/ {'''
        data = data.replace('    location /screenshots/ {', portal_block)

    # 2. Add /portal/nav.js script injection to sub_filter if not present
    if '/portal/nav.js' not in data:
        data = data.replace(
            '</style></head>',
            '</style><script src="/portal/nav.js" defer></script></head>'
        )

    # 3. Add installed app redirects
    import re
    data = re.sub(r'location ~\*\s*\^\/\(explore\/\)\?installed\/.*?location \/explore \{', redirect_rules, data, flags=re.DOTALL)

    with open(path, 'w', encoding='utf-8') as f:
        f.write(data)
    print(f'Successfully updated: {path}')
