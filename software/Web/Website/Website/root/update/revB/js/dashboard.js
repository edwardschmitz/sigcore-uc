/* Dashboard: Core status via the established Apache/PHP/WebBridge path. No I/O polling. */
(() => {
  const $ = id => document.getElementById(id);
  const show = (id, value) => { const el=$(id); if(el) el.textContent = display(value); };
  const display = value => value===undefined || value===null || value==='' ? '—' :
    Array.isArray(value) ? value.map(display).join(', ') :
    typeof value==='object' ? '—' : String(value);
  const norm = str => String(str).replace(/[^a-z0-9]/gi,'').toLowerCase();
  // Match names case-insensitively and tolerate PascalCase, camelCase, and snake_case.
  const property = (obj, name) => obj && typeof obj==='object' && !Array.isArray(obj) ?
    obj[Object.keys(obj).find(key=>norm(key)===norm(name))] : undefined;
  const path = (obj, text) => text.split('.').reduce((node,key)=>property(node,key),obj);
  const first = (obj, paths) => {
    for(const p of paths){const v=path(obj,p);if(v!==undefined && v!==null && v!=='')return v;}
    return undefined;
  };
  // Core status is section-oriented: {sections:[{section:"SigCore",items:[{label:"Serial Number",data:"..."}]}]}.
  // Keep support for ordinary object-shaped status responses as well.
  // Core uses named sections, but their names are presentation details.
  // Find a matching label in any section, rather than guessing its section name.
  const sectionItem = (data, sectionNames, labels) => {
    const sections = property(data, 'sections');
    if (!Array.isArray(sections)) return undefined;
    const wanted = labels.map(norm);
    for (const group of sections) {
      const items = property(group, 'items');
      if (!Array.isArray(items)) continue;
      for (const item of items) {
        const label = norm(property(item, 'label') ?? '');
        if (!wanted.includes(label)) continue;
        const value = property(item, 'data');
        if (value !== undefined && value !== null && value !== '') return value;
      }
    }
    return undefined;
  };
  const field = (data,groups,names,paths) => sectionItem(data,groups,names) ?? first(data,paths);
  const shared = (...names) => names.flatMap(n=>[n,'system.'+n,'device.'+n,'status.'+n,'systemInfo.'+n,'deviceInfo.'+n]);
  const lookup = (data, ...names) => first(data,shared(...names));
  const network = (data,...names) => first(data,names.flatMap(n=>['network.'+n,'networkInfo.'+n,'status.network.'+n,'system.network.'+n,n]));
  const scalar = (value, suffix='') => {
    if(value==null || value==='')return undefined;
    if(typeof value==='object')return undefined;
    return suffix && typeof value==='number'?`${value}${suffix}`:value;
  };
  const uptime = value => {
    if(value==null || value==='')return undefined;
    if(typeof value==='string'){
      if(!/^\d+(?:\.\d+)?$/.test(value))return value;
    }
    const seconds=Number(value);if(!Number.isFinite(seconds))return undefined;
    const days=Math.floor(seconds/86400),hrs=Math.floor(seconds%86400/3600),mins=Math.floor(seconds%3600/60);
    return `${days?days+'d ':''}${hrs}h ${mins}m`;
  };
  const bytes = value => {
    if(value==null || value==='')return undefined;
    if(typeof value==='string')return value;
    const n=Number(value);if(!Number.isFinite(n))return undefined;
    if(n<1024)return `${n} B`;
    const u=['KB','MB','GB','TB'];let v=n,i=-1;
    do{v/=1024;i++;}while(v>=1024&&i<u.length-1);
    return `${v.toFixed(1)} ${u[i]}`;
  };
  const metric = (data, bases, amountNames) => {
    const v=first(data,bases);
    if(v==null)return undefined;
    if(typeof v!=='object')return v;
    const total=first(v,['total','totalBytes','size',...amountNames]);
    const used=first(v,['used','usedBytes','usedSpace']);
    const available=first(v,['available','availableBytes','free','freeBytes']);
    if(total!==undefined && used!==undefined)return `${bytes(used)} / ${bytes(total)}`;
    if(total!==undefined && available!==undefined)return `${bytes(available)} free / ${bytes(total)}`;
    return undefined;
  };
  const healthItem = (data, names) => sectionItem(data, [], names);
  // Status item values are frequently strings already formatted by Core.
  const setHealth = (data) => {
    const temp=field(data,[],['CPU Temperature','CPU Temp','CPU Temperature (°C)','CPU Temperature (C)','Temperature'],shared('cpuTemperature','cpuTemp','temperature'));
    show('health-temperature',scalar(temp, ' °C'));
    const load=field(data,[],['CPU Load','CPU Usage','Processor Load','Load Average','Load'],shared('cpuLoad','cpuUsage','processorLoad'));
    show('health-load',scalar(load, typeof load==='number'?'%':''));
    const mem=healthItem(data,['Memory','Memory Usage','RAM','Memory Used','Available Memory','Free Memory']);
    show('health-memory', mem ?? metric(data,shared('memory','memoryUsage','ram'),['sizeBytes']) ?? lookup(data,'memoryUsed','memoryAvailable','memoryFree'));
    const disk=healthItem(data,['Disk','Disk Usage','Disk Space','Disk Free','Available Disk','Free Disk','Storage']);
    show('health-disk', disk ?? metric(data,shared('disk','diskUsage','storage'),['sizeBytes']) ?? lookup(data,'diskAvailable','diskFree'));
    show('health-model',field(data,[],['Model','Pi Model','Raspberry Pi Model','Hardware Model','Device Model'],shared('model','piModel','raspberryPiModel')));
    show('health-kernel',field(data,[],['Kernel','Kernel Version','Linux Kernel'],shared('kernel','kernelVersion')));
  };
  // Display the *active* Ethernet network reported by GetStatusAsync.
  // GetNetworkConfigAsync contains saved configuration and may report a different
  // address while DHCP is in use. Never use it for Dashboard network status.
  const activeEthernet = data => {
    const sections = property(data, 'sections');
    if (!Array.isArray(sections)) return undefined;
    const eth = sections.find(group => {
      const section = String(property(group, 'section') ?? '').toLowerCase();
      return /\beth0\b/.test(section);
    });
    if (!eth) return undefined;
    const items = property(eth, 'items');
    if (!Array.isArray(items)) return undefined;
    const read = (...labels) => {
      const wanted = labels.map(norm);
      const item = items.find(item => wanted.includes(norm(property(item, 'label') ?? '')));
      return item ? property(item, 'data') : undefined;
    };
    return {
      hostname: read('Hostname','Host Name'),
      ip: read('IP Address','IPv4 Address'),
      subnet: read('Subnet Mask','Subnet'),
      gateway: read('Gateway','Default Gateway'),
      dns: read('DNS','DNS Server','DNS Servers'),
      mac: read('MAC Address','MAC'),
      mode: read('Mode','Network Mode'),
    };
  };
  const setNetwork = status => {
    const eth = activeEthernet(status);
    show('net-hostname',eth?.hostname);
    show('net-ip',eth?.ip);
    show('net-subnet',eth?.subnet);
    show('net-gateway',eth?.gateway);
    show('net-dns',eth?.dns);
    show('net-mac',eth?.mac);
    show('net-mode',eth?.mode);
  };
  let connected = null;
  const setStatus = (status) => {
    const serial=field(status,['SigCore'],['Serial Number','Serial'],shared('serialNumber','serial'));
    show('device-serial',serial);
    const rev=field(status,['SigCore'],['Revision','Hardware Revision'],shared('hardwareRevision','revision','hwRevision'));
    show('device-revision',rev===undefined?'Hardware revision unavailable':`Hardware Rev ${rev}`);
    show('device-software',field(status,['SigCore'],['Version','Software Version'],shared('softwareVersion','version','releaseVersion')));
    show('device-uptime',uptime(field(status,['System','Raspberry Pi','OS','Runtime','Health'],['Uptime','Up Time','System Uptime','OS Uptime','Uptime (Seconds)','Uptime (s)'],shared('uptimeSeconds','uptime','upTime'))));
    setHealth(status);
    setNetwork(status);
    const ready=field(status,['SigCore','System'],['Ready','Is Ready'],shared('ready','isReady'));
    const readiness=typeof ready==='string' ? ready.toLowerCase()==='true' : ready;
    const state=$('device-ready');
    state.classList.toggle('healthy',readiness===true || (ready===undefined && connected===true));
    state.classList.toggle('unhealthy',readiness===false || (ready===undefined && connected===false));
    show('device-ready',readiness===true?'Ready':readiness===false?'Not Ready':connected===true?'Connected':connected===false?'Disconnected':'Status Available');
    show('device-connection',connected===true?'Core connection available':connected===false?'Core connection unavailable':'Device responding');
  };
  async function get(url){const r=await fetch(url,{cache:'no-store'});if(!r.ok)throw new Error(`HTTP ${r.status}`);return r.json();}
  let refreshTimer = null;
  let inFlight = false;
  async function refresh(){
    if (document.hidden || inFlight) return;
    inFlight = true;
    const alert=$('dashboard-alert');
    try{
      const result=await Promise.allSettled([get('/api/status.php'),get('/api/connection.php')]);
      if(result[1].status==='fulfilled'){
        const c=result[1].value;connected=c.connected===true || String(c.state).toLowerCase()==='connected';
      } else connected=null;
      if(result[0].status!=='fulfilled')throw result[0].reason;
      setStatus(result[0].value);
      alert.hidden=true;
      show('dashboard-updated','Updated '+new Date().toLocaleTimeString());
    }catch(error){
      alert.hidden=false;alert.textContent='Device status unavailable: '+error.message;
      show('device-ready',connected===false?'Disconnected':'Unavailable');
      $('device-ready').classList.remove('healthy');$('device-ready').classList.add('unhealthy');
      show('dashboard-updated','Status unavailable');
    }finally{
      inFlight = false;
      if (!document.hidden) refreshTimer = setTimeout(refresh,5000);
    }
  }
  document.addEventListener("visibilitychange", () => {
    clearTimeout(refreshTimer);
    if (!document.hidden && !inFlight) refresh();
  });
  if (!document.hidden) refresh();
})();
