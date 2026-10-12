import OpenAI from "openai";
import { describe, expect, it } from "vitest";
import { OpenAiTwoStageNpcResponseGenerator, classificationInstructions } from "../src/two-stage-generator.js";
import type { NpcGenerationRequest } from "../src/generator.js";
import { InMemoryConversationSessionStore, SessionConversationService } from "../src/sessions.js";

const request: NpcGenerationRequest = {
  character: { characterId:"guard", displayName:"Guard", personality:"PRIVATE_PERSONA", speechStyle:"Brief.",
    exampleDialogue:"Hello.",defaultEmotion:"neutral" },
  history:[{role:"user",content:"A prior proposal."},{role:"assistant",content:"A prior reply."}],
  userText:"Carry out that proposal.",
  triggers:[{triggerId:"open_gate",conditionDescription:"A current request to open the gate."}],
  grounding:{revision:"test",background:"PRIVATE_CANON",goalsAndValues:"Protect.",behavioralRules:[],dialogueExamples:[],
    facts:[{factId:"permission",kind:"observation",statement:"PRIVATE_PERMISSION",priority:10}]},
};
const dialogue={dialogue:"I will check the gate.",emotion:"neutral",gesture:"none"};

/** Wraps controlled model data in a complete Responses result without making a network call. */
function response(value: unknown, status="completed") {
  return {id:"test-response",status,output:[{type:"message",role:"assistant",status:"completed",content:[{type:"output_text",parsed:value,text:JSON.stringify(value)}]}],
    usage:{input_tokens:10,output_tokens:5,total_tokens:15}};
}

/** Captures immutable request bodies while returning one explicitly scripted result per SDK invocation. */
function generator(values: unknown[], captured: any[]=[]) {
  const client={responses:{parse:async(body:unknown)=>{captured.push(body);const value=values.shift();if(value instanceof Error)throw value;return value;}}} as unknown as OpenAI;
  return new OpenAiTwoStageNpcResponseGenerator({apiKey:"fake",model:"gpt-5.6-luna",timeoutMs:30000},client);
}

/** Creates the unchanged bounded session store for atomicity and recovery checks. */
function store() { return new InMemoryConversationSessionStore({maxTurns:8,maxContextBytes:16384,idleTtlMs:1800000,maxSessions:8}); }

describe("two-stage generation",()=>{
  // Both calls receive the same historical pairs; classification receives no trusted persona or world state.
  it("separates inputs and merges only classified IDs",async()=>{
    const captured:any[]=[];
    const result=await generator([response({decisions:["match"]}),response(dialogue)],captured).generate(request,new AbortController().signal);
    expect(result.result.matchedTriggerIds).toEqual(["open_gate"]);
    expect(captured).toHaveLength(2);
    expect(captured[0].instructions).not.toMatch(/PRIVATE_PERSONA|PRIVATE_CANON|PRIVATE_PERMISSION/);
    expect(captured[0].input.slice(0,2)).toEqual(captured[1].input.slice(0,2));
    expect(captured[1].input.find((m:any)=>m.role==="system").content).toContain("PRIVATE_PERMISSION");
    expect(Object.keys(captured[1].text.format.schema.properties)).toEqual(["dialogue","emotion","gesture"]);
    expect(result.telemetry.totalTokens).toBe(30);
    expect(result.telemetry.stages).toHaveLength(2);
    expect(captured.every(body=>body.max_output_tokens===256 && body.store===false)).toBe(true);
  });
  // Malformed or overlong decisions fail before a second model request.
  it.each([[],["match","match"],["unknown"],null])("rejects invalid decisions %j",async values=>{
    const captured:any[]=[];
    await expect(generator([response({decisions:values}),response(dialogue)],captured).generate(request,new AbortController().signal))
      .rejects.toMatchObject({code:"upstream_invalid_response"});
    expect(captured).toHaveLength(1);
  });
  // Unknown keys cannot silently acquire authority or be stripped into a valid classification.
  it.each([{decisions:["match"],reason:"private"},{other:["match"]}])("rejects extra or missing fields",async value=>{
    await expect(generator([response(value)]).generate(request,new AbortController().signal)).rejects.toMatchObject({code:"upstream_invalid_response"});
  });
  // Uncertain classifications become a clarification hint and never an action ID.
  it("preserves uncertainty without matching a trigger",async()=>{
    const captured:any[]=[];
    const result=await generator([response({decisions:["uncertain"]}),response(dialogue)],captured).generate(request,new AbortController().signal);
    expect(result.result.matchedTriggerIds).toEqual([]);
    expect(captured[1].input.find((m:any)=>m.role==="system").content).toContain('"unresolvedReference":true');
  });
  // No-trigger NPCs generate only one presentation response.
  it("skips classification with zero triggers",async()=>{
    const captured:any[]=[];
    const result=await generator([response(dialogue)],captured).generate({...request,triggers:[]},new AbortController().signal);
    expect(captured).toHaveLength(1);expect(result.result.matchedTriggerIds).toEqual([]);
  });
  // The dialogue stage cannot replace validated IDs by adding a fourth field.
  it("rejects presentation-stage trigger injection",async()=>{
    await expect(generator([response({decisions:["no_match"]}),response({...dialogue,matchedTriggerIds:["open_gate"]})])
      .generate(request,new AbortController().signal)).rejects.toMatchObject({code:"upstream_invalid_response"});
  });
  // Incomplete output is never interpreted as a valid classification.
  it("rejects output-limit completion before dialogue",async()=>{
    const captured:any[]=[];
    await expect(generator([response({decisions:["match"]},"incomplete")],captured).generate(request,new AbortController().signal)).rejects.toThrow();
    expect(captured).toHaveLength(1);
  });
  // A failed second stage leaves no partial history and the next turn can use the same session.
  it("aborts memory on stage-two failure and recovers",async()=>{
    const memory=store();
    const service=new SessionConversationService(memory,generator([
      response({decisions:["match"]}),response({...dialogue,bad:true}),
      response({decisions:["no_match"]}),response(dialogue)]));
    const input={...request,sessionId:"two-stage-session"};
    await expect(service.respond(input,new AbortController().signal)).rejects.toThrow();
    const lease=memory.begin(input.sessionId,input.character.characterId);
    expect(lease.history).toHaveLength(0);memory.abort(lease);
    await service.respond(input,new AbortController().signal);
    const next=memory.begin(input.sessionId,input.character.characterId);
    expect(next.history).toHaveLength(1);memory.abort(next);
  });
  // The session remains busy between stages, including reset and duplicate submissions.
  it("keeps the session lease across both stages",async()=>{
    let release!:()=>void;
    const pending=new Promise<void>(resolve=>{release=resolve;});
    let calls=0;
    const client={responses:{parse:async()=>{calls++;if(calls===1)return response({decisions:["match"]});await pending;return response(dialogue);}}} as unknown as OpenAI;
    const memory=store();
    const service=new SessionConversationService(memory,new OpenAiTwoStageNpcResponseGenerator({apiKey:"fake",model:"test",timeoutMs:30000},client));
    const input={...request,sessionId:"busy"};
    const active=service.respond(input,new AbortController().signal);
    await Promise.resolve();await Promise.resolve();
    expect(()=>service.reset({sessionId:"busy",characterId:"guard"})).toThrow();
    await expect(service.respond(input,new AbortController().signal)).rejects.toMatchObject({code:"session_busy"});
    release();await active;expect(calls).toBe(2);
  });
  // One deadline covers both calls instead of starting another timeout for the second stage.
  it("shares a single deadline and never retries",async()=>{
    let calls=0;
    const client={responses:{parse:async(_:unknown,options:{signal:AbortSignal})=>{
      calls++; if(calls===1)return response({decisions:["match"]});
      return new Promise((_,reject)=>options.signal.addEventListener("abort",()=>reject(new Error("cancelled")),{once:true}));
    }}} as unknown as OpenAI;
    const value=new OpenAiTwoStageNpcResponseGenerator({apiKey:"fake",model:"test",timeoutMs:25},client);
    await expect(value.generate(request,new AbortController().signal)).rejects.toMatchObject({code:"upstream_timeout"});
    expect(calls).toBe(2);
  });
  // A cancellation immediately after classification prevents the second request.
  it("honors cancellation at the phase boundary",async()=>{
    const caller=new AbortController();let calls=0;
    const client={responses:{parse:async()=>{calls++;caller.abort();return response({decisions:["match"]});}}} as unknown as OpenAI;
    const value=new OpenAiTwoStageNpcResponseGenerator({apiKey:"fake",model:"test",timeoutMs:30000},client);
    await expect(value.generate(request,caller.signal)).rejects.toThrow();expect(calls).toBe(1);
  });
  // Persona and current facts never enter the classifier through instruction construction.
  it("uses identity rather than the persona profile",()=>{
    expect(classificationInstructions(request)).toContain('"displayName":"Guard"');
    expect(classificationInstructions(request)).not.toContain("PRIVATE_");
  });
  // SDK parsing must not hide duplicate or escaped duplicate keys in the model's raw JSON.
  it.each(['{"decisions":["match"],"decisions":["no_match"]}', '{"decisions":["match"],"decis\\u0069ons":["no_match"]}'])("rejects duplicate raw classification keys",async text=>{
    const raw=response({decisions:["match"]});raw.output[0]!.content[0]!.text=text;
    const captured:any[]=[];
    await expect(generator([raw],captured).generate(request,new AbortController().signal)).rejects.toMatchObject({code:"upstream_invalid_response"});
    expect(captured).toHaveLength(1);
  });
  // Both stages use the same caller snapshot even if a consumer mutates the original during classification.
  it("freezes the full generation input across awaits",async()=>{
    const original=structuredClone(request) as any,captured:any[]=[];
    const client={responses:{parse:async(body:any)=>{
      captured.push(body);
      if(captured.length===1){original.userText="MUTATED";original.history[0].content="MUTATED";original.triggers[0].triggerId="bad";original.grounding.facts[0].statement="MUTATED";return response({decisions:["match"]});}
      return response(dialogue);
    }}} as unknown as OpenAI;
    const value=new OpenAiTwoStageNpcResponseGenerator({apiKey:"fake",model:"test",timeoutMs:30000},client);
    const result=await value.generate(original,new AbortController().signal);
    expect(JSON.stringify(captured)).not.toContain("MUTATED");
    expect(result.result.matchedTriggerIds).toEqual(["open_gate"]);
  });
  // A failed second call retains counts and its failure stage without retaining raw exceptions or conversation text.
  it("reports safe request-scoped diagnostics for a failed stage",async()=>{
    let failed:any;
    try{await generator([response({decisions:["match"]}),new Error("PRIVATE_UPSTREAM_DETAIL")]).generate(request,new AbortController().signal);}
    catch(error){failed=error;}
    expect(failed.inferenceDiagnostics.modelCalls).toBe(2);
    expect(failed.inferenceDiagnostics.failureStage).toBe("npc_dialogue");
    expect(failed.inferenceDiagnostics.stages[0].inputTokens).toBe(10);
    expect(failed.inferenceDiagnostics.stages[1].inputTokens).toBe(-1);
    expect(JSON.stringify(failed.inferenceDiagnostics)).not.toMatch(/PRIVATE_|prior proposal|Carry out/);
    expect(Object.isFrozen(failed.inferenceDiagnostics.stages[0])).toBe(true);
  });
  // Missing upstream usage is explicitly unavailable and cannot appear as a measured zero-token stage.
  it("does not invent zero usage when the provider omits telemetry",async()=>{
    const raw=response(dialogue) as any;delete raw.usage;
    const result=await generator([raw]).generate({...request,triggers:[]},new AbortController().signal);
    expect(result.telemetry.inputTokens).toBe(-1);expect(result.telemetry.totalTokens).toBe(-1);
    expect(result.telemetry.inferenceDiagnostics?.modelCalls).toBe(1);
  });
});
