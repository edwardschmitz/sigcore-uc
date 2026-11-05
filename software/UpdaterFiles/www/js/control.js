/* SigCore web HMI. Commands use the existing PHP/WebBridge route; state comes from Core subscriptions. */
(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const refs = {relay:[],di:[],ai:[],ao:[]};
  const cfg = {relay:Array(8).fill(null),di:Array(8).fill(null),ai:Array(4).fill(null),ao:Array(4).fill(null)};
  const msg = $('control-message');
  let connected = false, activeAO = -1, busyAO = false;
  let fastPollUntil = 0;
  const relaySnapshot = Array(8).fill(undefined);
  const el = (tag, className, text) => {const n=document.createElement(tag);if(className)n.className=className;if(text!==undefined)n.textContent=text;return n;};
  const info = (text,bad=false) => {msg.textContent=text;msg.classList.toggle('error',bad);};
  const field = (c,key,fallback='') => c?.[key] ?? fallback;
  const shortName = (kind,i) => kind.toUpperCase()+(i+1);
  const displayName = (kind,i) => {const name=field(cfg[kind][i],'name','');return typeof name==='string'?name.trim():'';};
  const prec = (kind,i) => {const p=Number(cfg[kind][i]?.precision);return Number.isInteger(p)&&p>=0&&p<=9?p:3;};
  const units = (kind,i) => {const u=field(cfg[kind][i],'units','V');return String(u||'V');};
  const format=(v,kind,i)=>typeof v==='number'&&Number.isFinite(v)?v.toFixed(prec(kind,i)):'—';
  const parseResponse=async r=>{const raw=await r.text();let data;try{data=JSON.parse(raw);}catch{throw new Error(`HTTP ${r.status}: unexpected response`);}if(!r.ok)throw new Error(data.error||`HTTP ${r.status}`);return data;};
  async function send(operation,payload) {
    if(!connected)throw new Error('Core disconnected');
    const r=await fetch(`/api/control.php?operation=${encodeURIComponent(operation)}`,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(payload),cache:'no-store'});
    await parseResponse(r);
    // No optimistic UI change; subscription state is the authority.
  }
  for(let i=0;i<8;i++){
    const card=el('div','hmi-relay');const contacts=el('div','hmi-relay-contacts');
    const no=el('span','hmi-led dark'),nc=el('span','hmi-led dark');
    for(const [led,text] of [[no,'N/O'],[nc,'N/C']]){const row=el('div','hmi-contact');row.append(led,el('span','',text));contacts.append(row);}
    const toggle=el('button','hmi-toggle','—');toggle.type='button';toggle.disabled=true;
    const name=el('div','hmi-name','');
    let sending=false;
    toggle.addEventListener('click',async()=>{
      const ref=refs.relay[i];
      if(sending||!connected||typeof ref.state!=='boolean')return;
      // Mouse clicks must not leave a temporary focus ring or change button opacity.
      toggle.blur();
      sending=true;
      fastPollUntil=Math.max(fastPollUntil,performance.now()+2500);
      const start=performance.now();
      try{
        await send('relay',{channel:i,state:!ref.state});
        console.debug(`[HMI] R${i+1} command HTTP ${(performance.now()-start).toFixed(0)} ms`);
      }catch(e){info('Relay command failed: '+e.message,true);}
      finally{sending=false;}
    });
    card.append(contacts,toggle,el('div','hmi-number',`R${i+1}`),name);$('control-relays').append(card);
    refs.relay.push({toggle,no,nc,name,state:null});
  }
  for(let i=0;i<8;i++){
    const row=el('div','hmi-row'),led=el('span','hmi-led dark'),name=el('span','hmi-channel-name');row.append(led,name);$('control-di').append(row);refs.di.push({led,name});
  }
  for(const kind of ['ai','ao']){
    for(let i=0;i<4;i++){
      const row=el('div','hmi-row');const value=el(kind==='ao'?'button':'span','hmi-value','—');
      if(kind==='ao'){value.type='button';value.disabled=true;value.setAttribute('aria-label',`Set AO${i+1}`);value.addEventListener('click',()=>openAO(i));}
      const name=el('span','hmi-channel-name');row.append(value,name);$('control-'+kind).append(row);refs[kind].push({value,name});
    }
  }
  function updateLabels(){
    for(const kind of ['relay','di','ai','ao'])for(let i=0;i<cfg[kind].length;i++){
      const label=displayName(kind,i),id=shortName(kind,i);
      if(kind==='relay')refs.relay[i].name.textContent=label;
      else refs[kind][i].name.textContent=kind==='ai'||kind==='ao'?`${id}${label?' · '+label:''} (${units(kind,i)})`:`${id}${label?' · '+label:''}`;
    }
  }
  async function loadConfigurations(){
    // One-time, on-entry loads only. No configuration polling or write readbacks.
    const tasks=[];for(const kind of ['relay','di','ai','ao'])for(let i=0;i<cfg[kind].length;i++)tasks.push({kind,i});
    // Sequential requests avoid a burst of heavy configuration messages on Core's TCP channel.
    for(const {kind,i} of tasks){
      try{
        const r=await fetch(`/api/config.php?kind=${kind}&channel=${i}`,{cache:'no-store'});
        cfg[kind][i]=await parseResponse(r);
        updateLabels();
        // The first state poll may finish before relay configuration loads.
        // Redraw the contacts when invertedLogic becomes known, even if the
        // relay has not changed state since that first poll.
        if(kind==='relay' && typeof refs.relay[i].state==='boolean')
          contactLeds(refs.relay[i],refs.relay[i].state,i);
      }
      catch(e){console.warn(`Configuration ${kind}${i+1} unavailable:`,e.message);}
    }
  }
  const dialog=$('ao-dialog'),entry=$('ao-entry');
  let anchorAO=null;
  function positionEditor(){
    if(!anchorAO||!dialog.open)return;
    const r=anchorAO.getBoundingClientRect();
    const w=dialog.offsetWidth||220,h=dialog.offsetHeight||100,m=8;
    const x=Math.max(m,Math.min(r.left,window.innerWidth-w-m));
    const y=r.bottom+h+4<=window.innerHeight-m?r.bottom+4:Math.max(m,r.top-h-4);
    dialog.style.left=x+'px';dialog.style.top=y+'px';
  }
  window.addEventListener('resize',positionEditor);
  window.addEventListener('scroll',positionEditor,true);
  document.addEventListener('pointerdown',e=>{
    if(dialog.open&&!dialog.contains(e.target)&&!anchorAO?.contains(e.target))dialog.close();
  });
  function openAO(i){
    if(!connected||busyAO)return;
    activeAO=i;
    $('ao-dialog-title').textContent=displayName('ao',i)||`AO${i+1}`;
    $('ao-units').textContent=units('ao',i);
    const shown=refs.ao[i].value.textContent;
    entry.value=shown==='—'?'':shown;
    anchorAO=refs.ao[i].value;
    dialog.show();positionEditor();entry.focus();entry.select();
  }
  $('ao-cancel').addEventListener('click',()=>dialog.close());
  dialog.addEventListener('close',()=>{activeAO=-1;anchorAO=null;});
  $('ao-form').addEventListener('submit',async event=>{
    event.preventDefault();if(activeAO<0||busyAO)return;
    if(entry.value.trim()===''||!entry.checkValidity()||!Number.isFinite(Number(entry.value))){entry.reportValidity();return;}
    const target=activeAO;busyAO=true;$('ao-set').disabled=true;
    try{await send('analog-output',{channel:target,value:Number(entry.value)});dialog.close();}
    catch(e){info('Analog output command failed: '+e.message,true);}
    finally{busyAO=false;$('ao-set').disabled=false;}
  });
  function contactLeds(ref,state,i){
    // Relay state from the subscription is the authority; don't infer state from a click.
    // N/O and N/C represent mutually exclusive contact positions.
    const inverted=cfg.relay[i]?.invertedLogic===true;
    const energized=inverted?!state:state;
    ref.no.className='hmi-led '+(energized?'green':'dark');ref.nc.className='hmi-led '+(energized?'dark':'red');
  }
  let pending=false;
  async function poll(){
    if(pending)return;pending=true;
    try{
      const r=await fetch('/api/state.php',{cache:'no-store'});const s=await parseResponse(r);connected=s.connection?.connected===true;
      refs.relay.forEach((ref,i)=>{
        const v=s.relays?.[i];const next=typeof v==='boolean'?v:null;
        ref.state=next;
        if(relaySnapshot[i]!==next){
          relaySnapshot[i]=next;
          ref.toggle.textContent=next===null?'—':next?'ON':'OFF';
          ref.toggle.classList.toggle('on',next===true);
          ref.toggle.classList.toggle('off',next===false);
          if(next===null){ref.no.className=ref.nc.className='hmi-led dark';}
          else contactLeds(ref,next,i);
        }
        // Do not disable on click: this caused a transient visual flash.
        ref.toggle.disabled=!connected||next===null;
      });
      refs.di.forEach((ref,i)=>{const cls='hmi-led '+(s.digitalInputs?.[i]===true?'green':'dark');if(ref.led.className!==cls)ref.led.className=cls;});
      refs.ai.forEach((ref,i)=>{const value=format(s.analogInputs?.[i],'ai',i);if(ref.value.textContent!==value)ref.value.textContent=value;});
      refs.ao.forEach((ref,i)=>{const value=format(s.analogOutputs?.values?.[i],'ao',i);if(ref.value.textContent!==value)ref.value.textContent=value;ref.value.disabled=!connected||!Number.isFinite(s.analogOutputs?.values?.[i]);});
      if(!connected)info('Core disconnected. Controls are disabled.',true);
      else if(msg.classList.contains('error')||msg.textContent==='Connecting to Core…')info('Connected to Core.');
    }catch(e){connected=false;info('State unavailable: '+e.message,true);refs.relay.forEach(ref=>ref.toggle.disabled=true);refs.ao.forEach(ref=>ref.value.disabled=true);}
    finally{pending=false;setTimeout(poll,performance.now()<fastPollUntil?150:450);}
  }
  updateLabels();poll();loadConfigurations();
})();
