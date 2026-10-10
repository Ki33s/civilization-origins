import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
const root=path.resolve(process.argv[2]||'Builds'),port=Number(process.argv[3]||5081);
const mime={'.html':'text/html; charset=utf-8','.js':'text/javascript','.json':'application/json','.png':'image/png','.txt':'text/plain','.unityweb':'application/octet-stream','.wasm':'application/wasm'};
http.createServer((request,response)=>{
  let file;
  try { file=path.resolve(root,'.'+decodeURIComponent(new URL(request.url,'http://localhost').pathname)); } catch {response.writeHead(400);response.end();return;}
  if(file!==root && !file.startsWith(root+path.sep)){response.writeHead(403);response.end();return;}
  if(fs.existsSync(file) && fs.statSync(file).isDirectory()) file=path.join(file,'index.html');
  if(!fs.existsSync(file) || !fs.statSync(file).isFile()){response.writeHead(404);response.end('Not found');return;}
  response.writeHead(200,{'Content-Type':mime[path.extname(file)]||'application/octet-stream','Cache-Control':'no-cache'});
  fs.createReadStream(file).pipe(response);
}).listen(port,'127.0.0.1',()=>console.log('WEBGL PREVIEW: http://localhost:'+port+'/WebGLItch/index.html'));
