import subprocess

patch_script = """
const fs = require('fs');

const fClient = '/app/targets/next/web/.next/static/chunks/1nzosu_ex_lib.js';
const fServer = '/app/targets/next/web/.next/server/chunks/ssr/web_app_components_20bd3x8._.js';

const targetMapCode = `function(id, appId) {
    var u = {
        'c271447f-5a9b-48d1-a403-af7b0306c7ee': '/chat/k3XZzC40AdSgADAN',
        '6299c648-4775-4d7c-a4fe-a4b57236b587': '/chat/k3XZzC40AdSgADAN',
        'a7546eca-608d-4dc3-b09e-07e0660202e3': '/chat/K2I75pwQFm4eitRi',
        '6e50e89f-af79-4ed6-8434-7a9fbf933df8': '/chat/K2I75pwQFm4eitRi',
        '511f67f3-4409-405b-9287-431afab50f22': '/chat/aK4Z7N1mnGIqGxif',
        'd78a7aa4-d995-4169-b10f-04179692bfa3': '/chat/aK4Z7N1mnGIqGxif',
        'ef72f8c8-a202-4a61-b756-3770808da851': '/chat/E8W4zenZFZu1Clnq',
        '620d3349-8048-42ae-ba05-1c3f800efaac': '/chat/E8W4zenZFZu1Clnq',
        '65f8189c-59c5-41b7-9893-a638c7233eff': '/chat/XGfFt2vOTQizDVnO',
        'f0ed8e6d-b258-4798-92f9-268cdd29c64e': '/chat/XGfFt2vOTQizDVnO',
        '312a1b85-780c-4b72-8ae1-7e23edd3c542': '/chat/bSqS0Xsz4CqBJZlW',
        '9a01866d-2fc3-4785-bace-dc1664619861': '/chat/bSqS0Xsz4CqBJZlW'
    };
    return u[appId] || u[id] || ('/chat/' + id);
}`;

// 1. Patch Client
if (fs.existsSync(fClient)) {
    let c = fs.readFileSync(fClient, 'utf8');
    const start = c.indexOf('function I({app:');
    const end = c.indexOf('let _=', start);
    if (start !== -1 && end !== -1) {
        const oldBlock = c.slice(start, end);
        console.log('Old client block found, len:', oldBlock.length);
        
        // Construct new block that uses native <a target="_blank" rel="noopener noreferrer">
        const newBlock = `function I({app:e,isSelected:s,onTogglePin:a,onDelete:i}){` +
            `let[n,r]=(0,h.useState)(!1),` +
            `{id:l,is_pinned:o,uninstallable:c,app:{name:d,icon_type:p,icon:u,icon_background:m,icon_url:x}}=e,` +
            `g=(${targetMapCode})(l,e.app?.id);` +
            `return(0,t.jsxs)("div",{className:"group flex h-7 items-center justify-between gap-2 rounded-lg py-0.5 pr-0.5 pl-2 transition-colors not-has-[>a[aria-current=page]]:hover:bg-state-base-hover has-[>a:focus-visible]:inset-ring-2 has-[>a:focus-visible]:inset-ring-state-accent-solid has-[>a[aria-current=page]]:bg-state-base-active",children:[` +
                `(0,t.jsxs)("a",{href:g,target:"_blank",rel:"noopener noreferrer",className:"flex min-w-0 flex-1 items-center gap-2 outline-hidden",children:[` +
                    `(0,t.jsx)(y.default,{decorative:!0,size:"tiny",className:"size-5 rounded-md text-sm",iconType:p,icon:u??void 0,background:m,imageUrl:x}),` +
                    `(0,t.jsx)("div",{className:"min-w-0 flex-1 truncate py-1 pr-1 system-sm-regular",title:d,children:d})` +
                `]}),` +
                `(0,t.jsx)("div",{className:"h-6 shrink-0",children:(0,t.jsx)(A,{itemName:d,isPinned:o,togglePin:()=>a(l,!o),isShowDelete:!c&&!s,onDelete:()=>i(l)})})` +
            `]},l)}`;
            
        c = c.slice(0, start) + newBlock + c.slice(end);
        fs.writeFileSync(fClient, c);
        console.log('Successfully patched client chunk:', fClient);
    } else {
        console.log('Client block markers not found');
    }
}

// 2. Patch Server
if (fs.existsSync(fServer)) {
    let c = fs.readFileSync(fServer, 'utf8');
    const start = c.indexOf('function C({app:');
    const end = c.indexOf('let D=', start);
    if (start !== -1 && end !== -1) {
        const oldBlock = c.slice(start, end);
        console.log('Old server block found, len:', oldBlock.length);
        
        const newBlock = `function C({app:a,isSelected:c,onTogglePin:d,onDelete:e}){` +
            `let[f,g]=(0,o.useState)(!1),` +
            `{id:h,is_pinned:i,uninstallable:j,app:{name:k,icon_type:l,icon:m,icon_background:n,icon_url:p}}=a,` +
            `q=(${targetMapCode})(h,a.app?.id);` +
            `return(0,b.jsxs)("div",{className:"group flex h-7 items-center justify-between gap-2 rounded-lg py-0.5 pr-0.5 pl-2 transition-colors not-has-[>a[aria-current=page]]:hover:bg-state-base-hover has-[>a:focus-visible]:inset-ring-2 has-[>a:focus-visible]:inset-ring-state-accent-solid has-[>a[aria-current=page]]:bg-state-base-active",children:[` +
                `(0,b.jsxs)("a",{href:q,target:"_blank",rel:"noopener noreferrer",className:"flex min-w-0 flex-1 items-center gap-2 outline-hidden",children:[` +
                    `(0,b.jsx)(u.default,{decorative:!0,size:"tiny",className:"size-5 rounded-md text-sm",iconType:l,icon:m??void 0,background:n,imageUrl:p}),` +
                    `(0,b.jsx)("div",{className:"min-w-0 flex-1 truncate py-1 pr-1 system-sm-regular",title:k,children:k})` +
                `]}),` +
                `(0,b.jsx)("div",{className:"h-6 shrink-0",children:(0,b.jsx)(A,{itemName:k,isPinned:i,togglePin:()=>d(h,!i),isShowDelete:!j&&!c,onDelete:()=>e(h)})})` +
            `]},h)}`;
            
        c = c.slice(0, start) + newBlock + c.slice(end);
        fs.writeFileSync(fServer, c);
        console.log('Successfully patched server chunk:', fServer);
    } else {
        console.log('Server block markers not found');
    }
}
"""

with open('scratch/patch_script.js', 'w', encoding='utf-8') as f:
    f.write(patch_script)

print("Created scratch/patch_script.js")
cmd_cp = ['docker', 'cp', 'scratch/patch_script.js', 'docker-web-1:/tmp/patch_script.js']
subprocess.run(cmd_cp, check=True)
print("Copied patch_script.js to docker-web-1")

cmd_run = ['docker', 'exec', 'docker-web-1', 'node', '/tmp/patch_script.js']
res = subprocess.run(cmd_run, capture_output=True, text=True, encoding='utf-8')
print("STDOUT:", res.stdout)
print("STDERR:", res.stderr)
