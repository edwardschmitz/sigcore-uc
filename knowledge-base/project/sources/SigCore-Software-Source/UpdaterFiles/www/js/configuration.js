/* Channel properties dialogs. Uses the existing PHP -> WebBridge API. */
(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const defs = [
    {kind:'relay', title:'Relays', prefix:'R', count:8, fields:[['name','Name'],['defaultState','Default state'],['failSafeState','Fail-safe state'],['invertedLogic','Inverted logic']]},
    {kind:'di', title:'Digital Inputs', prefix:'DI', count:8, fields:[['name','Name'],['debounce','Debounce'],['inverted','Inverted logic']]},
    {kind:'ai', title:'Analog Inputs', prefix:'AI', count:4, fields:[['name','Name'],['units','Units'],['avgSamples','Averaging'],['precision','Precision'],['calType','Cal Type'],['range','Range'],['display','Display'],['m','Slope (m)'],['b','Offset (b)'],['polyString','Polynomial calibration'],['pieceWiseString','Piecewise calibration']]},
    {kind:'ao', title:'Analog Outputs', prefix:'AO', count:4, fields:[['name','Name'],['units','Units'],['auto','Automatic Mode'],['precision','Precision'],['mode','Mode'],['display','Display'],['voltM','Voltage Scale (m)'],['voltB','Voltage Offset (b)']]},
    {kind:'pid', title:'PID Controllers', prefix:'PID', count:4, fields:[['title','Title'],['isEnabled','Enabled'],['autoMode','Automatic mode'],['kp','Kp'],['ki','Ki'],['kd','Kd'],['outputMin','Output minimum'],['outputMax','Output maximum'],['pvMin','PV minimum'],['pvMax','PV maximum'],['pvSource','PV source'],['outputDestination','Output destination'],['integralLimit','Integral limit'],['resetOnModeChange','Reset on mode change']]}
  ];
  const dialog=$('config-dialog'),form=$('config-form'),fields=$('config-fields'),save=$('config-save'),status=$('config-dialog-status');
  let active=null, original=null, dirty=false, busy=false, opening=0;
  const channelButtons = new Map();
  const locallyChanged = new Set();
  const channelKey = (kind, index) => `${kind}:${index}`;
  function displayChannelName(kind, index, name) {
    const button = channelButtons.get(channelKey(kind, index));
    if (!button) return;
    const subtitle = button.querySelector('.config-channel-name');
    const value = typeof name === 'string' ? name.trim() : '';
    subtitle.textContent = value;
    subtitle.hidden = !value;
    button.title = value ? `${button.dataset.channelLabel}: ${value}` : button.dataset.channelLabel;
  }
  // Background name retrieval is limited to two in-flight requests to avoid
  // flooding Core with heavy configuration transfers. Never poll these.
  async function loadChannelNames() {
    const jobs = defs.flatMap(spec => Array.from({length:spec.count}, (_,index) => ({spec,index})));
    let next = 0;
    async function worker() {
      while (next < jobs.length) {
        const {spec,index} = jobs[next++];
        const key = channelKey(spec.kind, index);
        try {
          const response = await fetch(api(spec.kind,index), {cache:'no-store'});
          const config = await jsonResponse(response);
          if (locallyChanged.has(key)) continue;
          displayChannelName(spec.kind, index, spec.kind==='pid' ? config.title : config.name);
        } catch (error) {
          // A failed lookup does not make the Properties page unusable.
          console.warn(`Unable to load name for ${spec.prefix}${index+1}:`, error);
        }
      }
    }
    await Promise.all([worker(), worker()]);
  }
  const setStatus=(message,error=false)=>{status.textContent=message;status.classList.toggle('is-error',error)};
  const api=(kind,index)=>`/api/config.php?kind=${encodeURIComponent(kind)}&channel=${index}`;
  async function jsonResponse(response){
    const text=await response.text();let data;
    try{data=JSON.parse(text)}catch{throw new Error(`Unexpected API response (HTTP ${response.status})`)}
    if(!response.ok)throw new Error(data.error||`HTTP ${response.status}`);
    return data;
  }
  for(const spec of defs){
    const section=document.createElement('section');section.className='panel config-section';
    const heading=document.createElement('h2');heading.textContent=spec.title;
    const grid=document.createElement('div');grid.className='config-channel-grid';
    for(let i=0;i<spec.count;i++){
      const button=document.createElement('button');button.type='button';button.className='config-channel';
      const number=document.createElement('span');number.textContent=spec.prefix+(i+1);
      const subtitle=document.createElement('small');subtitle.className='config-channel-name';subtitle.hidden=true;
      button.dataset.channelLabel=number.textContent;
      button.append(number,subtitle);
      channelButtons.set(channelKey(spec.kind,i),button);
      button.addEventListener('click',()=>openChannel(spec,i));grid.append(button);
    }
    section.append(heading,grid);$('config-sections').append(section);
  }
  void loadChannelNames();
  // Enum values come from SigCoreCommon A_IN/A_OUT definitions.
  const optionSets={
    ai:{
      display:[['FixedPoint',0],['Scientific',1]],
      range:[['Voltage256mV',0],['Voltage1024mV',1],['Voltage2048mV',2],['Voltage5V',3],['2.500V Reference',4],['Current',5],['GNDSense',6]],
      calType:[['None',0],['mXplusB',1],['MultiPoint',2],['Polynomial',3],['Log10',4]]
    },
    ao:{display:[['FixedPoint',0],['Scientific',1]],mode:[['Voltage',0],['PWM',1]]}
  };
  function addField(key,label,value){
    const wrap=document.createElement('div');wrap.className='config-field';
    const id='cfg-'+key;const caption=document.createElement('label');caption.htmlFor=id;caption.textContent=label;
    const opts=active && optionSets[active.spec.kind]?.[key];
    const isBoolean=typeof value==='boolean';let input;
    if(isBoolean){
      input=document.createElement('input');input.type='checkbox';input.checked=value;
      wrap.classList.add('config-checkbox');wrap.append(caption,input);
    } else if(opts && Number.isInteger(value)){
      input=document.createElement('select');
      for(const [name,num] of opts){const option=document.createElement('option');option.value=String(num);option.textContent=name;input.append(option)}
      if(!opts.some(([,num])=>num===value)){
        const unknown=document.createElement('option');unknown.value=String(value);unknown.textContent=`Unknown (${value})`;input.prepend(unknown);
      }
      input.value=String(value);wrap.append(caption,input);
    } else {
      input=(value!==null && typeof value==='object')?document.createElement('textarea'):document.createElement('input');
      if(typeof value==='number'){
        input.type='number';input.step=Number.isInteger(value)?'1':'any';
        if(['debounce','avgSamples','precision'].includes(key))input.min='0';
      }else if(input.tagName==='INPUT'){input.type='text';if(String(value).length>80)wrap.classList.add('is-wide')}
      if(input.tagName==='TEXTAREA')wrap.classList.add('is-wide');
      input.value=value==null?'':(typeof value==='object'?JSON.stringify(value,null,2):String(value));wrap.append(caption,input);
    }
    input.id=id;input.dataset.key=key;input.dataset.originalType=typeof value;
    input.addEventListener('input',()=>{dirty=true;save.disabled=false});
    input.addEventListener('change',()=>{dirty=true;save.disabled=false;if(key==='calType')updateCalibrationVisibility()});
    fields.append(wrap);
  }
  function updateCalibrationVisibility(){
    if(active?.spec.kind!=='ai')return;
    const cal=Number(fields.querySelector('[data-key="calType"]')?.value);
    const visibility={m:cal===1,b:cal===1,polyString:cal===3,pieceWiseString:cal===2};
    for(const [key,show] of Object.entries(visibility)){
      const el=fields.querySelector(`[data-key="${key}"]`);
      if(el)el.closest('.config-field').hidden=!show;
    }
  }
  async function openChannel(spec,index){
    if(dialog.open||busy)return;
    active={spec,index};original=null;dirty=false;fields.replaceChildren();save.disabled=true;
    $('config-dialog-title').textContent=`${spec.prefix}${index+1} Properties`;
    setStatus('Loading properties…');dialog.showModal();
    const requestId=++opening;
    try{
      const response=await fetch(api(spec.kind,index),{cache:'no-store'});
      const data=await jsonResponse(response);
      if(requestId!==opening||!dialog.open)return;
      if(!data||typeof data!=='object'||Array.isArray(data))throw new Error('Invalid configuration response');
      original=data;
      displayChannelName(spec.kind,index,spec.kind==='pid'?data.title:data.name);
      const missing=spec.fields.filter(([key])=>!Object.hasOwn(data,key)).map(([key])=>key);
      for(const [key,label] of spec.fields){
        if(Object.hasOwn(data,key))addField(key,label,data[key]);
      }
      if(missing.length){setStatus('Properties response is missing fields: '+missing.join(', '),true);save.disabled=true;return;}
      updateCalibrationVisibility();
      if(!fields.children.length)throw new Error('No editable fields found');
      setStatus('');save.disabled=false;
    }catch(error){setStatus('Unable to load properties: '+error.message,true);save.disabled=true;}
  }
  function closeDialog(){if(busy)return;if(dirty&&!confirm('Discard unsaved changes?'))return;opening++;dialog.close();active=null;original=null;dirty=false}
  $('config-close').addEventListener('click',closeDialog);
  $('config-cancel').addEventListener('click',closeDialog);
  dialog.addEventListener('cancel',event=>{event.preventDefault();closeDialog()});
  dialog.addEventListener('click',event=>{if(event.target===dialog)closeDialog()});
  function getEdited(){
    const updated=structuredClone(original);
    for(const input of fields.querySelectorAll('[data-key]')){
      const key=input.dataset.key, type=input.dataset.originalType;
      if(type==='boolean'){updated[key]=input.checked;continue;}
      if(type==='number'){
        if(input.value.trim()==='')throw new Error(`${key} is required`);
        const value=Number(input.value);
        if(!Number.isFinite(value)||!input.validity.valid)throw new Error(`Invalid number for ${key}`);
        updated[key]=value;continue;
      }
      if(type==='object' && original[key]!==null){try{updated[key]=JSON.parse(input.value)}catch{throw new Error(`Invalid JSON for ${key}`)};continue;}
      updated[key]=input.value;
    }
    if(active.spec.kind==='pid'){
      if(updated.outputMin>updated.outputMax)throw new Error('Output minimum must not exceed maximum');
      if(updated.pvMin>updated.pvMax)throw new Error('PV minimum must not exceed maximum');
    }
    return updated;
  }
  form.addEventListener('submit',async event=>{
    event.preventDefault();if(busy||!active||!original)return;
    let updated;
    try{updated=getEdited()}catch(error){setStatus(error.message,true);return;}
    busy=true;save.disabled=true;setStatus('Sending properties…');
    try{
      const response=await fetch(api(active.spec.kind,active.index),{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({config:updated}),cache:'no-store'});
      await jsonResponse(response);
      // The setter confirms transmission, not persistence. Do not read back.
      const savedKind=active.spec.kind, savedIndex=active.index;
      locallyChanged.add(channelKey(savedKind,savedIndex));
      displayChannelName(savedKind,savedIndex,savedKind==='pid'?updated.title:updated.name);
      dirty=false;setStatus('Properties command sent.');
      // No implicit GetConfig after saving. The library's setter only confirms transmission.
      dialog.close();active=null;original=null;
      $('config-page-status').textContent='Properties command sent.';
    }catch(error){setStatus('Save failed: '+error.message,true);}
    finally{busy=false;save.disabled=false;}
  });
})();
