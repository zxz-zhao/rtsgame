# -*- coding: utf-8 -*-
import subprocess

mongo_script = """
const script = `class Script {
  prepare_outgoing_request({ request }) {
    request.timeout = 180000;
    return request;
  }
  process_outgoing_response({ request, response }) {
    var data = JSON.parse(response.content);
    return {
      content: {
        text: data.text
      }
    };
  }
}`;

const scriptCompiled = `var Script=function(){function Script(){}var _proto=Script.prototype;_proto.prepare_outgoing_request=function prepare_outgoing_request(_ref){var request=_ref.request;request.timeout=180000;return request;};_proto.process_outgoing_response=function process_outgoing_response(_ref2){var request=_ref2.request,response=_ref2.response;var data=JSON.parse(response.content);return{content:{text:data.text}}};return Script}();`;

db.rocketchat_integrations.updateMany(
  { type: "webhook-outgoing" },
  { $set: { script: script, scriptCompiled: scriptCompiled } }
);
print("Integrations script updated with 180s timeout");
"""

with open("scratch_mongo_update.js", "w", encoding="utf-8") as f:
    f.write(mongo_script)

res = subprocess.run(["docker", "cp", "scratch_mongo_update.js", "rocketchat-mongo:/tmp/scratch_mongo_update.js"], capture_output=True, text=True)
print("docker cp:", res.returncode)

res = subprocess.run(["docker", "exec", "rocketchat-mongo", "mongo", "rocketchat", "/tmp/scratch_mongo_update.js"], capture_output=True, text=True)
print("mongo output:\n", res.stdout, res.stderr)
