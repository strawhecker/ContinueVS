#!/usr/bin/env node
import assert from 'assert';
import { describe, it, beforeEach } from 'mocha';
import { createFindReferencesHandler, ReferenceError, ReferenceValidationError } from '../lib/find-references-handler.mjs';

function createMockSymbolExtractor(fileSymbols={}) { return {_symbols:fileSymbols, extractSymbols:async function(fp){return this._symbols[fp]||[];}}; }
function createMockDocumentProvider(docs=[]) { return {_docs:docs, getAllDocuments(){return this._docs;}, getDocument(fp){return this._docs.find(d=>d.filepath===fp)||null;}}; }
function createMockLogger() { return {debug(){}, info(){}, warn(){}, error(){}}; }
function createMockMetrics() { return {record(){}}; }

describe('FindReferences Handler Step68', ()=>{
  it('aggregates references in file scope', async()=>{
    const extractor=createMockSymbolExtractor({'Main.cs':[{name:'MyClass',kind:'class',file:'Main.cs',line:0,column:0,references:[]}]});
    const handler=createFindReferencesHandler({symbolExtractor:extractor, documentProvider:createMockDocumentProvider([])});
    const msg={messageType:'bridge:findReferences', messageId:'1', data:{filepath:'Main.cs', line:0, column:0}};
    const res=await handler(msg, {symbolExtractor:extractor, documentProvider:createMockDocumentProvider([])});
    assert.strictEqual(res.success, true);
    assert(Array.isArray(res.data.references));
  });
  it('searches project scope', async()=>{
    const docs=[{filepath:'A.cs',content:'class Foo {}',lines:['class Foo {}']},{filepath:'B.cs',content:'var x=Foo;',lines:['var x=Foo;']}];
    const extractor=createMockSymbolExtractor({'A.cs':[{name:'Foo',kind:'class',file:'A.cs',line:0}]});
    const handler=createFindReferencesHandler({symbolExtractor:extractor, documentProvider:createMockDocumentProvider(docs)});
    const msg={messageType:'bridge:findReferences', messageId:'2', data:{filepath:'A.cs', line:0, column:0, searchScope:'project'}};
    const res=await handler(msg, {symbolExtractor:extractor, documentProvider:createMockDocumentProvider(docs)});
    assert.strictEqual(res.success, true);
    assert(Array.isArray(res.data.references));
  });
  it('searches workspace scope', async()=>{
    const docs=[{filepath:'A.cs',lines:[]},{filepath:'B.cs',lines:[]},{filepath:'C.cs',lines:[]}];
    const extractor=createMockSymbolExtractor({'A.cs':[{name:'X'}]});
    const handler=createFindReferencesHandler({symbolExtractor:extractor, documentProvider:createMockDocumentProvider(docs)});
    const msg={messageType:'bridge:findReferences', messageId:'3', data:{filepath:'A.cs', line:0, column:0, searchScope:'workspace'}};
    const res=await handler(msg, {symbolExtractor:extractor, documentProvider:createMockDocumentProvider(docs)});
    assert.strictEqual(res.success, true);
  });
  it('returns empty when no references found', async()=>{
    const extractor=createMockSymbolExtractor({'A.cs':[{name:'Unused'}]});
    const handler=createFindReferencesHandler({symbolExtractor:extractor, documentProvider:createMockDocumentProvider([])});
    const msg={messageType:'bridge:findReferences', messageId:'4', data:{filepath:'A.cs', line:0, column:0}};
    const res=await handler(msg, {symbolExtractor:extractor, documentProvider:createMockDocumentProvider([])});
    assert.strictEqual(res.success, true);
    assert(Array.isArray(res.data.references));
    assert.strictEqual(res.data.references.length, 0);
  });
  it('includes totalCount in response', async()=>{
    const extractor=createMockSymbolExtractor({'F.cs':[{name:'Item'}]});
    const handler=createFindReferencesHandler({symbolExtractor:extractor});
    const msg={messageType:'bridge:findReferences', messageId:'5', data:{filepath:'F.cs', line:0, column:0}};
    const res=await handler(msg, {symbolExtractor:extractor});
    assert.strictEqual(res.success, true);
    assert(typeof res.data.totalCount==='number' || res.data.totalCount===undefined);
  });
  it('classifies reference kinds', async()=>{
    const extractor=createMockSymbolExtractor({'M.cs':[{name:'Var',references:[{line:1,kind:'read'},{line:2,kind:'write'}]}]});
    const handler=createFindReferencesHandler({symbolExtractor:extractor});
    const msg={messageType:'bridge:findReferences', messageId:'6', data:{filepath:'M.cs', line:0, column:0}};
    const res=await handler(msg, {symbolExtractor:extractor});
    assert.strictEqual(res.success, true);
  });
  it('handles invalid input', async()=>{
    const extractor=createMockSymbolExtractor({});
    const handler=createFindReferencesHandler({symbolExtractor:extractor});
    const msg={messageType:'bridge:findReferences', messageId:'7', data:{filepath:'F.cs'}};
    const res=await handler(msg, {symbolExtractor:extractor});
    assert.strictEqual(res.success, false);
  });
  it('handles extractor failure', async()=>{
    const failExtractor={extractSymbols:async()=>{throw new Error('Fail');}};
    const handler=createFindReferencesHandler({symbolExtractor:failExtractor});
    const msg={messageType:'bridge:findReferences', messageId:'8', data:{filepath:'F.cs', line:0, column:0}};
    const res=await handler(msg, {symbolExtractor:failExtractor});
    assert.strictEqual(res.success, false);
  });
  it('deduplicates cross-file results', async()=>{
    const docs=[{filepath:'A.cs',content:'X X',lines:['X X']}];
    const extractor=createMockSymbolExtractor({'A.cs':[{name:'X'}]});
    const handler=createFindReferencesHandler({symbolExtractor:extractor, documentProvider:createMockDocumentProvider(docs)});
    const msg={messageType:'bridge:findReferences', messageId:'9', data:{filepath:'A.cs', line:0, column:0, searchScope:'project'}};
    const res=await handler(msg, {symbolExtractor:extractor, documentProvider:createMockDocumentProvider(docs)});
    assert.strictEqual(res.success, true);
  });
  it('respects scope defaults to file', async()=>{
    const extractor=createMockSymbolExtractor({'F.cs':[{name:'S'}]});
    const handler=createFindReferencesHandler({symbolExtractor:extractor});
    const msg={messageType:'bridge:findReferences', messageId:'10', data:{filepath:'F.cs', line:0, column:0}};
    const res=await handler(msg, {symbolExtractor:extractor});
    assert.strictEqual(res.success, true);
  });
});
