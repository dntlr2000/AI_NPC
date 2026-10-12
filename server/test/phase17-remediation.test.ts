import OpenAI from 'openai';
import {describe,expect,it} from 'vitest';
import {OpenAiTwoStageNpcResponseGenerator} from '../src/two-stage-generator.js';
import {semanticTriggerSchema} from '../src/contracts/v4.js';
import type {NpcGenerationRequest} from '../src/generator.js';

/** Produces complete fake Responses output without a key, SDK transport or gameplay effects. */
function complete(value:unknown){
  return {id:'fixed-remediation-response',status:'completed',output:[{type:'message',role:'assistant',status:'completed',
    content:[{type:'output_text',parsed:value,text:JSON.stringify(value)}]}],usage:{input_tokens:1,output_tokens:1,total_tokens:2}};
}

describe('Phase 17 remediation contract compatibility',()=>{
  // Declaration order may affect a model, but correctly classified positions must never map to a different authored ID.
  it.each([false,true])('maps sixteen opaque maximum-length IDs in actual declaration order (reverse=%s)',async reverse=>{
    const triggers=Array.from({length:16},(_,index)=>({triggerId:'signal_'+String(index).padStart(2,'0')+'x'.repeat(55),conditionDescription:'An authored synthetic condition.'}));
    if(reverse)triggers.reverse();
    const values=triggers.map(trigger=>/^signal_(03|14)/.test(trigger.triggerId)?'match':'uncertain');
    const captured:any[]=[];
    const client={responses:{parse:async(body:any)=>{captured.push(body);return complete(captured.length===1?{decisions:values}:{dialogue:'I will check.',emotion:'neutral',gesture:'none'});}}} as unknown as OpenAI;
    const request:NpcGenerationRequest={character:{characterId:'opaque',displayName:'NPC',personality:'Friendly.',speechStyle:'Brief.',exampleDialogue:'Hello.',defaultEmotion:'neutral'},
      userText:'A synthetic compound request.',history:[],triggers};
    const generator=new OpenAiTwoStageNpcResponseGenerator({apiKey:'unused-fake',model:'synthetic',timeoutMs:30000},client);
    const result=await generator.generate(request,new AbortController().signal);
    expect(result.result.matchedTriggerIds?.slice().sort()).toEqual(['signal_03'+'x'.repeat(55),'signal_14'+'x'.repeat(55)]);
    expect(captured).toHaveLength(2);
    expect(captured[0].text.format.schema.properties.decisions.minItems).toBe(16);
    expect(captured[0].text.format.schema.properties.decisions.maxItems).toBe(16);
    expect(captured[0].max_output_tokens).toBe(256);
    expect(captured[1].text.format.schema.properties).not.toHaveProperty('matchedTriggerIds');
  });
  // Author-supplied target aliases cannot bypass the pre-existing byte limit used by Unity and Backend.
  it('keeps the authored target description UTF-8 boundary unchanged',()=>{
    const conditionDescription='가'.repeat(170)+'ab';
    expect(semanticTriggerSchema.safeParse({triggerId:'opaque_target',conditionDescription}).success).toBe(true);
    expect(semanticTriggerSchema.safeParse({triggerId:'opaque_target',conditionDescription:conditionDescription+'c'}).success).toBe(false);
  });
});
