/* Device settings: supported SigCoreCommon operations through PHP/WebBridge. */
(() => {
  'use strict';
  const $ = id => document.getElementById(id);
  const norm = s => String(s).replace(/[^a-z0-9]/gi,'').toLowerCase();
  const findItem = (status, labels) => {
    for (const section of status.sections || []) for (const item of section.items || []) {
      if (labels.some(label=>norm(label)===norm(item.label))) return item.data;
    }
    return undefined;
  };
  const statusText=(id, v) => { if ($(id)) $(id).textContent = v == null || v === '' ? 'Unavailable' : String(v); };
  const settingsUrl=kind=>`/api/device-settings.php?kind=${kind}`;
  const fetchJson=async (url, opts={})=>{
    const r=await fetch(url,{cache:'no-store',...opts});
    const text=await r.text(); let data;
    try {data=JSON.parse(text);} catch {throw Error(`Unexpected response (HTTP ${r.status})`);}
    if (!r.ok) throw Error(data.error || `HTTP ${r.status}`);
    return data;
  };
  const val = id => $(id).value.trim();
  const bool = id => $(id).checked;
  const fill=(id,value)=>{if($(id)&&value!==undefined&&value!==null)$(id).value=String(value);};
  const check=(id,value)=>{if($(id))$(id).checked=value===true;};
  const message=(id,text,error=false)=>{const el=$(id);el.textContent=text;el.style.color=error?'#992020':'#21783e';};
  const num = (id,min,max,integer=false) => {
    const text=val(id),number=Number(text);
    if (text==='' || !Number.isFinite(number) || number<min || number>max || (integer && !Number.isInteger(number))) throw Error(`Invalid ${$(id).id} value.`);
    return number;
  };
  const original={};
  async function refreshStatus(){
    const button=$('refresh-status'); button.disabled=true;
    try {
      const status=await fetchJson('/api/status.php');
      statusText('update-version',findItem(status,['Version','Software Version']));
      statusText('update-revision',findItem(status,['Revision','Hardware Revision']));
      statusText('update-status',findItem(status,['Update Status']));
      statusText('update-available',findItem(status,['Available Version']));
      $('device-error').hidden=true;
      statusText('device-last-updated','Updated '+new Date().toLocaleTimeString());
    } catch(e){$('device-error').textContent='Device status unavailable: '+e.message;$('device-error').hidden=false;}
    finally{button.disabled=false;}
  }
  const loadSettings=async kind=>{
    try {
      const d=await fetchJson(settingsUrl(kind));original[kind]=d;
      if(kind==='system'){
        fill('system-name',d.systemName);fill('system-sampling',d.analogSamplingRate);fill('system-pwm',d.pwm_frequency);
      } else if(kind==='network'){
        fill('eth-ip',d.ethernet?.ipAddress);fill('eth-mask',d.ethernet?.subnetMask);
        fill('eth-gateway',d.ethernet?.gateway);fill('eth-dns',d.ethernet?.dns);
        check('eth-dhcp',d.ethernet?.dhcpEnabled);
        fill('wifi-ssid',d.wifi?.ssid);fill('wifi-ip',d.wifi?.ipAddress);
        fill('wifi-mask',d.wifi?.subnetMask);fill('wifi-gateway',d.wifi?.gateway);
        fill('wifi-dns',d.wifi?.dns);check('wifi-dhcp',d.wifi?.dhcpEnabled);
        // Never fill a secret from the API. Blank means preserve existing PSK.
        fill('wifi-password', d.wifi?.psk ?? ''); networkEditable();
      } else if(kind==='logger'){
        check('logger-enabled',d.enabled);fill('logger-interval',d.intervalSec);
        fill('logger-server',d.server);fill('logger-port',d.port);
        fill('logger-db',d.database);fill('logger-user',d.user);
        fill('logger-table',d.tableName);fill('logger-conn',d.connectionTimeoutSec);
        fill('logger-command',d.commandTimeoutSec);fill('logger-password',d.password ?? '');
      }
      message(kind+'-result','');
    } catch(e){message(kind+'-result','Unable to load settings: '+e.message,true);}
  };
  function networkEditable(){
    for(const prefix of ['eth','wifi']) {
      const dhcp=bool(prefix+'-dhcp');
      const fields=$(prefix+'-static-fields');
      fields.hidden=dhcp;
      fields.style.display=dhcp?'none':'';
      for(const item of ['ip','mask','gateway','dns']) {
        $(prefix+'-'+item).disabled=dhcp;
      }
    }
  }
  async function save(kind,changes){
    const form=$(kind+'-form'),button=form.querySelector('button[type="submit"]');
    button.disabled=true;message(kind+'-result','Sending settings…');
    try {
      const response=await fetchJson(settingsUrl(kind),{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify(changes)});
      if(!response.accepted)throw Error('Command was not accepted.');
      message(kind+'-result','Command transmitted (not yet verified by Core).');
    }catch(e){message(kind+'-result',e.message,true);}
    finally{button.disabled=false;}
  }
  $('system-form').addEventListener('submit',e=>{
    e.preventDefault();
    try{save('system',{systemName:val('system-name'),analogSamplingRate:num('system-sampling',0,2,true),pwm_frequency:num('system-pwm',1,1000000,true)});}
    catch(err){message('system-result',err.message,true);}
  });
  for(const id of ['eth-dhcp','wifi-dhcp'])$(id).addEventListener('change',networkEditable);
  $('wifi-password-toggle').addEventListener('click',()=>{
    const password=$('wifi-password'), button=$('wifi-password-toggle');
    const showing=password.type==='text';
    password.type=showing?'password':'text';
    button.textContent=showing?'Show':'Hide';
  });
  $('network-form').addEventListener('submit',e=>{
    e.preventDefault();
    if(!confirm('Applying network settings may disconnect this website. Continue?'))return;
    const eth={dhcpEnabled:bool('eth-dhcp'),ipAddress:val('eth-ip'),subnetMask:val('eth-mask'),gateway:val('eth-gateway'),dns:val('eth-dns')};
    const wifi={ssid:val('wifi-ssid'),dhcpEnabled:bool('wifi-dhcp'),ipAddress:val('wifi-ip'),subnetMask:val('wifi-mask'),gateway:val('wifi-gateway'),dns:val('wifi-dns')};
    if(val('wifi-password'))wifi.psk=val('wifi-password');
    // Preserve hostname, which is a distinct identity managed by the existing system.
    save('network',{ethernet:eth,wifi});
  });
  $('logger-password-toggle').addEventListener('click',()=>{
    const password=$('logger-password'), button=$('logger-password-toggle');
    const showing=password.type==='text';
    password.type=showing?'password':'text';
    button.textContent=showing?'Show':'Hide';
  });
  $('logger-form').addEventListener('submit',e=>{
    e.preventDefault();
    try {
      const changes={enabled:bool('logger-enabled'),intervalSec:num('logger-interval',0.001,1e8),server:val('logger-server'),port:num('logger-port',1,65535,true),database:val('logger-db'),user:val('logger-user'),tableName:val('logger-table'),connectionTimeoutSec:num('logger-conn',1,86400,true),commandTimeoutSec:num('logger-command',1,86400,true)};
      if(val('logger-password'))changes.password=val('logger-password');
      save('logger',changes);
    }catch(err){message('logger-result',err.message,true);}
  });
  $('refresh-status').addEventListener('click',refreshStatus);
  void refreshStatus();
  void loadSettings('system');void loadSettings('network');void loadSettings('logger');
})();
