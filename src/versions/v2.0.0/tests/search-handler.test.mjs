#!/usr/bin/env node
import assert from 'assert';
import { describe, it, beforeEach } from 'mocha';
import { searchHandler, SearchError, SearchValidationError } from '../lib/search-handler.mjs';

function createMockDocumentProvider(docs=[]) { return {_docs:docs, getAllDocuments() { return this._docs; }, getDocument(fp) { return this._docs.find(d=>d.filepath===fp)||null; }, getDocumentMetadata(fp) { const d=this.getDocument(fp); return d?{filepath:d.filepath,language:d.language}:null; } }; }
function createMockLogger() { return {_calls:[], debug(m){this._calls.push({level:'debug',m});}, info(m){this._calls.push({level:'info',m});}, warn(m){this._calls.push({level:'warn',m});}, error(m){this._calls.push({level:'error',m});} }; }
function createMockMetrics() { return {_events:[], recordEvent(n,d){this._events.push({n,d});} }; }
function createMockContext(dp=null) { return {documentProvider:dp||createMockDocumentProvider([]), logger:createMockLogger(), metrics:createMockMetrics()}; }
function createTestDocument(fp,c,l='csharp') { return {filepath:fp, language:l, content:c, lines:c.split('\n'), isDirty:false}; }

describe('Search-Handler Step68 Tests', ()=>{
  it('basic substring search works', async()=>{
    const docs=[createTestDocument('F.cs', 'test line\nanother test')];
    const ctx=createMockContext(createMockDocumentProvider(docs));
    const msg={messageType:'bridge:search', messageId:'1', data:{query:'test'}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, true);
    assert(res.data.totalMatches>0);
  });
  it('regex search works', async()=>{
    const docs=[createTestDocument('M.cs', 'x=42\ny=100')];
    const ctx=createMockContext(createMockDocumentProvider(docs));
    const msg={messageType:'bridge:search', messageId:'2', data:{query:'\\d+', regex:true}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, true);
  });
  it('rejects invalid regex', async()=>{
    const ctx=createMockContext();
    const msg={messageType:'bridge:search', messageId:'3', data:{query:'[bad(', regex:true}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, false);
  });
  it('respects offset and limit', async()=>{
    const docs=[createTestDocument('L.cs', 'a\na\na\na\na')];
    const ctx=createMockContext(createMockDocumentProvider(docs));
    const msg={messageType:'bridge:search', messageId:'4', data:{query:'a', offset:2, limit:2}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.data.results.length, 2);
  });
  it('rejects empty query', async()=>{
    const ctx=createMockContext();
    const msg={messageType:'bridge:search', messageId:'5', data:{query:''}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, false);
  });
  it('case sensitive mode works', async()=>{
    const docs=[createTestDocument('C.cs', 'ABC\nabc')];
    const ctx=createMockContext(createMockDocumentProvider(docs));
    const msg={messageType:'bridge:search', messageId:'6', data:{query:'ABC', caseSensitive:true}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.data.totalMatches, 1);
  });
  it('handles empty documents gracefully', async()=>{
    const ctx=createMockContext(createMockDocumentProvider([]));
    const msg={messageType:'bridge:search', messageId:'7', data:{query:'test'}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.data.totalMatches, 0);
  });
  it('includes preview context', async()=>{
    const docs=[createTestDocument('P.cs', 'a\nb\nmatch\nd\ne')];
    const ctx=createMockContext(createMockDocumentProvider(docs));
    const msg={messageType:'bridge:search', messageId:'8', data:{query:'match', limit:1}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, true);
    assert(res.data.results[0].preview);
    assert(res.data.results[0].preview.length>=3);
  });
  it('truncates large results', async()=>{
    const docs=[createTestDocument('T.cs', 'x\n'.repeat(120))];
    const ctx=createMockContext(createMockDocumentProvider(docs));
    const msg={messageType:'bridge:search', messageId:'9', data:{query:'x', limit:50}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, true);
    assert.strictEqual(res.data.truncated, true);
  });
  it('tracks metrics', async()=>{
    const docs=[createTestDocument('M.cs', 'test')];
    const ctx=createMockContext(createMockDocumentProvider(docs));
    const msg={messageType:'bridge:search', messageId:'10', data:{query:'test'}};
    const res=await searchHandler(msg,ctx);
    assert.strictEqual(res.success, true);
    assert(ctx.metrics._events.length>0);
  });
});
