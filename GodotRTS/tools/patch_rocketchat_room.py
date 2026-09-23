# -*- coding: utf-8 -*-
path = '/app/bundle/programs/server/app/app.js'
with open(path, 'r', encoding='utf-8') as f:
    c = f.read()

target = '''      const argObject = this.eventNameArgumentsToObject(...arguments);
      const {
        event,
        message,
        room
      } = argObject;'''

replacement = '''      const argObject = this.eventNameArgumentsToObject(...arguments);
      let {
        event,
        message,
        room
      } = argObject;
      if (room && room.room) {
        room = room.room;
        argObject.room = room;
      }'''

if target in c:
    c = c.replace(target, replacement, 1)
    with open(path, 'w', encoding='utf-8') as f:
        f.write(c)
    print("Patched rocketchat app.js successfully!")
else:
    print("Target block not found in app.js!")
