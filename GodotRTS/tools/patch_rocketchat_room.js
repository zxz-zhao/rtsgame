const fs = require('fs');
const path = '/app/bundle/programs/server/app/app.js';
let c = fs.readFileSync(path, 'utf8');

const target = `      const argObject = this.eventNameArgumentsToObject(...arguments);
      const {
        event,
        message,
        room
      } = argObject;`;

const replacement = `      const argObject = this.eventNameArgumentsToObject(...arguments);
      let {
        event,
        message,
        room
      } = argObject;
      if (room && room.room) {
        room = room.room;
        argObject.room = room;
      }`;

if (c.includes(target)) {
    c = c.replace(target, replacement);
    fs.writeFileSync(path, c, 'utf8');
    console.log("Patched rocketchat app.js successfully with node!");
} else {
    console.log("Target block not found in app.js!");
}
