import assert from "node:assert/strict";
import {createHash,randomBytes} from "node:crypto";
import {readFileSync} from "node:fs";
import vm from "node:vm";
const source=readFileSync(new URL("../../RemoteWork.Server/remotework/static/file_hash.js",import.meta.url),"utf8");
const context=vm.createContext({ArrayBuffer,Uint8Array,Uint32Array,DOMException,setTimeout});
vm.runInContext(source,context);
const {sha256,MAX_BYTES}=context.RemoteWorkFileHash;
const expected=b=>createHash("sha256").update(b).digest("hex");
let count=0;
for(const length of [0,1,2,3,55,56,57,63,64,65,119,120,127,128,129,16384,262144,262145,1048576,MAX_BYTES]){
 const bytes=randomBytes(length);assert.equal(await sha256(bytes),expected(bytes));count++;
}
for(const text of ["abc","한글 파일 확인 🌏","a".repeat(1000000)]){
 const b=Buffer.from(text);assert.equal(await sha256(b),expected(b));count++;
}
const backing=randomBytes(400), sliced=new Uint8Array(backing.buffer,backing.byteOffset+9,271);
assert.equal(await sha256(sliced),expected(sliced));count++;
assert.equal(await sha256(new DataView(sliced.buffer,sliced.byteOffset,79)),expected(sliced.subarray(0,79)));count++;
const array=new Uint8Array([0,128,255]).buffer;assert.equal(await sha256(array),expected(new Uint8Array(array)));count++;
await assert.rejects(sha256(new Uint8Array(MAX_BYTES+1)),{name:"RangeError"});count++;
await assert.rejects(sha256("abc"),{name:"TypeError"});count++;
const pre=new AbortController();pre.abort();await assert.rejects(sha256(new Uint8Array(0),{signal:pre.signal}),{name:"AbortError"});count++;
const during=new AbortController();const pending=sha256(new Uint8Array(2*1048576),{signal:during.signal});setTimeout(()=>during.abort(),0);await assert.rejects(pending,{name:"AbortError"});count++;
const mutable=randomBytes(1048576), original=expected(mutable), snapshot=sha256(mutable);mutable.fill(0);assert.equal(await snapshot,original);count++;
assert.throws(()=>vm.runInContext(source,context),/already installed/);count++;
assert.equal(context.crypto,undefined);assert.equal(context.fetch,undefined);
console.log("PASS "+count+" SHA-256 checks; Node crypto oracle; no SubtleCrypto/network/DB; includes25MiB, slice, mutation and cancellation.");
