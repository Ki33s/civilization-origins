import fs from 'node:fs/promises';
import path from 'node:path';
const root=process.cwd(),destination=path.join(root,'Online','Deployment');
await fs.mkdir(destination,{recursive:true});
async function copy(relative) { await fs.mkdir(path.dirname(path.join(destination,relative)),{recursive:true}); await fs.cp(path.join(root,relative),path.join(destination,relative),{recursive:true}); }
await copy('Assets/Scripts/Core'); await copy('Assets/Resources/EarlyEraContent.json'); await copy('Assets/Resources/DemoBalance.json');
for(const file of ['Program.cs','RoomStore.cs','Origins.Server.csproj','Dockerfile']) await copy('Online/Server/'+file);
// Unity uses content hashes: discard obsolete build files in this generated mirror.
const obsoleteBuild=path.resolve(destination,'Online/Server/wwwroot/Build');
if(!obsoleteBuild.startsWith(path.resolve(destination)+path.sep)) throw Error('Deployment build path escaped its checkout');
await fs.rm(obsoleteBuild,{recursive:true,force:true});
await copy('Online/Server/wwwroot'); await copy('render.yaml'); await copy('.dockerignore');
await fs.writeFile(path.join(destination,'.gitignore'),'**/bin/\n**/obj/\n**/data/\n');
await fs.writeFile(path.join(destination,'README.md'),'# Истоки — онлайн-демка\n\nГотовая Unity WebGL-сборка и сервер на общих правилах C#. Два племени, одна карта, одновременное разрешение приказов.\n\nРазвёртывание через Render Blueprint из `render.yaml`: сервер и PostgreSQL только на Free. Бесплатная база действует 30 дней; сохранения можно экспортировать из игры.\n\nПолный Unity-проект: https://github.com/Ki33s/civilization-origins\n');
let bytes=0,count=0;
async function inspect(dir) { for(const item of await fs.readdir(dir,{withFileTypes:true})) { if(item.name==='.git') continue; const full=path.join(dir,item.name); if(item.isDirectory()) await inspect(full); else {const stat=await fs.stat(full);if(stat.size>99*1024*1024)throw Error('GitHub file limit exceeded: '+full);bytes+=stat.size;count++;} } }
await inspect(destination); console.log(JSON.stringify({destination,files:count,bytes}));
