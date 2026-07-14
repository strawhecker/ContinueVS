#!/usr/bin/env node
import { bootstrapHandler } from '../handlers/bootstrap-handler.js';
import { getEditorStateHandler } from './get-editor-state-handler.mjs';
import { searchHandler } from './search-handler.mjs';
import { createGoToDefinitionHandler } from './go-to-definition-handler.mjs';
import { createFindReferencesHandler } from './find-references-handler.mjs';
import { createCodeCompletionHandler } from './code-completion-handler.mjs';
import { createHoverInfoHandler } from './hover-info-handler.mjs';
import { createTestExplorerHandler } from './test-explorer-handler.mjs';
import { DebugSessionHandler } from './debug-session-handler.mjs';
export class HandlerRegistryError extends Error {
  constructor(message, code = 'REGISTRY_ERROR', details = null) {
    super(message); this.name = 'HandlerRegistryError'; this.code = code; this.details = details;
  }
}
export class HandlerNotFoundError extends Error {
  constructor(messageType) {
    super(`Handler not found for message type: ${messageType}`);
    this.name = 'HandlerNotFoundError'; this.messageType = messageType;
  }
}
const HANDLER_REGISTRY = [{messageType:'bridge:bootstrap',handler:bootstrapHandler,isFactory:false,timeoutPolicy:'medium',stabilityTier:'core',description:'Gateway handler',relatedSteps:[46,71],dependencies:[45]},{messageType:'bridge:getEditorState',handler:getEditorStateHandler,isFactory:false,timeoutPolicy:'fast',stabilityTier:'core',description:'Retrieves editor state',relatedSteps:[50,71],dependencies:[48,49]},{messageType:'bridge:onEditorStateChange',handler:async(m,c)=>({success:true,data:{subscriptionId:'sub'+Date.now()}}),isFactory:false,timeoutPolicy:'fast',stabilityTier:'core',description:'Subscribes to editor changes',relatedSteps:[51,71],dependencies:[49]},{messageType:'bridge:search',handler:searchHandler,isFactory:false,timeoutPolicy:'medium',stabilityTier:'core',description:'Full-text search',relatedSteps:[55,71],dependencies:[52]},{messageType:'bridge:goToDefinition',handler:createGoToDefinitionHandler,isFactory:true,timeoutPolicy:'medium',stabilityTier:'core',description:'Navigate to definition',relatedSteps:[56,71],dependencies:[53]},{messageType:'bridge:findReferences',handler:createFindReferencesHandler,isFactory:true,timeoutPolicy:'medium',stabilityTier:'core',description:'Find references',relatedSteps:[57,71],dependencies:[53]},{messageType:'bridge:codeCompletion',handler:createCodeCompletionHandler,isFactory:true,timeoutPolicy:'fast',stabilityTier:'core',description:'Code completion',relatedSteps:[58,71],dependencies:[50]},{messageType:'bridge:hoverInfo',handler:createHoverInfoHandler,isFactory:true,timeoutPolicy:'fast',stabilityTier:'core',description:'Hover information',relatedSteps:[59,71],dependencies:[53]},{messageType:'bridge:testExplorer',handler:createTestExplorerHandler,isFactory:true,timeoutPolicy:'medium',stabilityTier:'experimental',description:'Test explorer',relatedSteps:[60,71],dependencies:[84]},{messageType:'bridge:debugSession',handler:DebugSessionHandler,isFactory:false,timeoutPolicy:'slow',stabilityTier:'experimental',description:'Debug session',relatedSteps:[61,71],dependencies:[82]}];
function validateRegistry(){const t=new Set(),p=['fast','medium','slow'],s=['core','experimental','deprecated'];for(let i=0;i<HANDLER_REGISTRY.length;i++){const e=HANDLER_REGISTRY[i];if(!e.messageType)throw new HandlerRegistryError('missing');if(!e.handler)throw new HandlerRegistryError('no handler');if(typeof e.handler!=='function')throw new HandlerRegistryError('not callable');if(!p.includes(e.timeoutPolicy))throw new HandlerRegistryError('bad timeout');if(!s.includes(e.stabilityTier))throw new HandlerRegistryError('bad tier');if(t.has(e.messageType))throw new HandlerRegistryError('duplicate');t.add(e.messageType);}}
try{validateRegistry();}catch(e){console.error('[Registry]',e.message);throw e;}
export function getAllHandlers(){return[...HANDLER_REGISTRY];}
export function getHandlerMetadata(m){const e=HANDLER_REGISTRY.find(h=>h.messageType===m);if(!e)throw new HandlerNotFoundError(m);return e;}
export function getHandlersByStabilityTier(t){return HANDLER_REGISTRY.filter(h=>h.stabilityTier===t);}
export function getHandlersByTimeoutPolicy(p){return HANDLER_REGISTRY.filter(h=>h.timeoutPolicy===p);}
export function hasHandler(m){return HANDLER_REGISTRY.some(h=>h.messageType===m);}
export default {getAllHandlers,getHandlerMetadata,getHandlersByStabilityTier,getHandlersByTimeoutPolicy,hasHandler,HandlerRegistryError,HandlerNotFoundError};
