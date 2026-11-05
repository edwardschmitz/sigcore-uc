(() => {
  'use strict';
  const el = id => document.getElementById(id);
  const colorsFallback = ['#2563eb', '#dc2626', '#16a34a', '#a855f7'];
  const historyPath = '/api/chart-history.php';
  const configPath = '/api/chart-config.php';
  const statePath = '/api/state.php'; // Existing website API, unchanged.
  const maxSamples = 2000;
  let config, chart, data, startMs, durationMs, visibleSamples = 800;
  let live = true, dragX = null, updating = false, refreshPending = false;
  let liveTimer = null, liveTickActive = false;

  function errorMessage(err) { el('status').textContent = 'Error: ' + err.message; }
  function checkedNumber(id, label, min = -Infinity, max = Infinity, whole = false) {
    const field = el(id), raw = field.value.trim(), value = Number(raw);
    if (!raw || !Number.isFinite(value) || !field.checkValidity() || value < min ||
        value > max || (whole && (!Number.isInteger(value) || !Number.isSafeInteger(value)))) {
      field.focus(); field.select();
      throw new Error(`${label} must be ${whole ? 'a whole number' : 'a number'}` +
        (Number.isFinite(min) && Number.isFinite(max) ? ` from ${min} to ${max}.` : '.'));
    }
    return value;
  }
  async function request(url, options) {
    const response = await fetch(url, { cache: 'no-store', ...options });
    const json = await response.json();
    if (!response.ok) throw new Error(json.error || 'HTTP ' + response.status);
    return json;
  }
  function updateWindowLabel() {
    const a = new Date(startMs).toLocaleString();
    const b = new Date(startMs + durationMs).toLocaleString();
    el('window-label').textContent = `${a} to ${b}`;
    el('live').classList.toggle('selected', live);
  }
  function sampleCount() {
    const width = el('chart').clientWidth || 800;
    return Math.max(100, Math.min(maxSamples, Math.floor(width)));
  }
  function chartSeries() {
    return [ {label: 'Time'}, ...[0,1,2,3].map(i => ({
      label: `AI ${i+1}`,
      stroke: config.display.colors[i] || colorsFallback[i],
      width: 1.8,
      // A single fresh sample must be visible even before a second point exists.
      points: { show: true, size: 3, width: 0 },
      show: config.display.traces[i],
      scale: 'y',
      value: (u,v) => v == null ? 'N/A' : Number(v).toPrecision(6)
    }))];
  }
  function makeChart() {
    if (!window.uPlot) { el('status').textContent = 'uPlot library missing. Download the two local uPlot files first.'; return; }
    if (chart) chart.destroy();
    const width = Math.max(300, el('chart').clientWidth);
    const height = Math.max(280, Math.min(570, Math.round(window.innerHeight*0.55)));
    const opts = {
      width, height,
      cursor: { drag: { setScale: false, x: false, y: false } },
      scales: { x: { time:true, auto:false }, y: { auto: Boolean(config.display.autoScale) } },
      axes: [{label:'Time'}, {label:'Value', size:75}],
      series: chartSeries(),
      legend: {show:true, live:true}
    };
    chart = new uPlot(opts, data, el('chart'));
    chart.setScale('x', {min:startMs/1000, max:(startMs+durationMs)/1000});
    if (!config.display.autoScale) chart.setScale('y', {min:config.display.min,max:config.display.max});
    attachDrag();
  }
  function setDisplay() {
    config.display.windowSeconds = durationMs/1000;
    el('autoscale').checked = config.display.autoScale;
    el('scale-min').value = config.display.min;
    el('scale-max').value = config.display.max;
    el('scale-min').disabled = config.display.autoScale;
    el('scale-max').disabled = config.display.autoScale;
    el('apply-scale').disabled = config.display.autoScale;
    el('record-interval').value = config.recordIntervalSec;
    el('history-memory').value = config.maxMemoryMiB;
    const seconds = String(config.display.windowSeconds);
    if ([...el('window').options].some(o=>o.value===seconds)) el('window').value=seconds;
    let row = '';
    for (let i=0;i<4;i++) row += `<label class="trace"><input type="checkbox" data-trace="${i}" ${config.display.traces[i]?'checked':''}>AI ${i+1} <input type="color" data-color="${i}" value="${config.display.colors[i]}"></label>`;
    el('traces').innerHTML = row;
  }
  let saveTimer = null;
  function saveDisplaySoon() {
    clearTimeout(saveTimer);
    saveTimer = setTimeout(async () => {
      try { config = await request(configPath, {method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({display:config.display})}); }
      catch(e){ errorMessage(e); }
    }, 300);
  }
  async function loadHistory() {
    if (updating) { refreshPending = true; return; }
    updating = true;
    try {
      visibleSamples = sampleCount();
      const params = new URLSearchParams({start:String(Math.floor(startMs)),length:String(Math.round(durationMs)),samples:String(visibleSamples)});
      const response = await request(`${historyPath}?${params}`);
      data = response.data.map(a => a.map(v => v === null ? null : v));
      if (!chart) makeChart();
      else {
        chart.setData(data, true); // Refresh the automatic Y range as data arrives.
        chart.setScale('x', {min:startMs/1000,max:(startMs+durationMs)/1000});
        if (!config.display.autoScale) chart.setScale('y',{min:config.display.min,max:config.display.max});
      }
      updateWindowLabel();
      el('status').textContent = response.lastRecordedMs == null ? 'Recording starting' : `History: ${new Date(response.firstRecordedMs).toLocaleString()} to ${new Date(response.lastRecordedMs).toLocaleString()}`;
    } catch(err) { errorMessage(err); }
    finally { updating=false; if (refreshPending) {refreshPending=false; loadHistory();} }
  }
  function moveLive() {
    live = true;
    startMs = Date.now() - durationMs * 0.9;
    updateWindowLabel();
    loadHistory();
  }
  function move(delta) {
    live = false;
    startMs += durationMs * delta;
    updateWindowLabel();
    loadHistory();
  }
  function zoom(factor) {
    const center = live ? Date.now() : startMs + durationMs*0.5;
    durationMs = Math.min(10*365*86400000, Math.max(1000, durationMs * factor));
    startMs = live ? Date.now() - durationMs*0.9 : center - durationMs*0.5;
    el('window').value=String(durationMs/1000);
    config.display.windowSeconds = durationMs/1000;
    saveDisplaySoon();
    loadHistory();
  }
  function attachDrag() {
    const surface = chart.root.querySelector('.u-over');
    if (!surface) return;
    surface.style.cursor = 'grab';
    surface.addEventListener('pointerdown', ev => {dragX=ev.clientX;surface.setPointerCapture(ev.pointerId);surface.style.cursor='grabbing';});
    surface.addEventListener('pointerup', ev => {
      if (dragX == null) return;
      const dx=ev.clientX-dragX;dragX=null;surface.style.cursor='grab';
      if (Math.abs(dx)>4) move(-dx / Math.max(1,chart.bbox.width / (window.devicePixelRatio || 1)) );
    });
    surface.addEventListener('pointercancel',()=>{dragX=null;surface.style.cursor='grab';});
    surface.addEventListener('wheel', ev => {ev.preventDefault();zoom(ev.deltaY<0?.8:1.25);},{passive:false});
  }
  function insertLiveSample(series, index, value) {
    if (!Number.isFinite(value)) {
      series[index] = null;
      return;
    }
    // A 5-minute frame may have subsecond bins but live state arrives once
    // per second. Interpolate intervening empty bins to keep each trace joined.
    let previous = index - 1;
    while (previous >= 0 && !Number.isFinite(series[previous])) previous--;
    if (previous >= 0) {
      const previousValue = series[previous];
      const distance = index - previous;
      for (let j = previous + 1; j < index; j++) {
        // Preserve the measurements already supplied by WebBridge.
        if (!Number.isFinite(series[j]))
          series[j] = previousValue + (value - previousValue) * ((j - previous) / distance);
      }
    }
    series[index] = value; // Latest live value wins in its output bin.
  }

  // Use WebBridge's actual recording interval for the browser refresh rate.
  // Re-arm this timer whenever the recording interval is changed.
  function startLiveTimer() {
    if (liveTimer !== null) clearInterval(liveTimer);
    const intervalMs = Math.max(100, Math.round(Number(config.recordIntervalSec) * 1000));
    liveTimer = setInterval(liveTick, intervalMs);
  }

  async function liveTick() {
    if (!live || !data || !chart || updating || liveTickActive) return;
    const now = Date.now();
    if (now > startMs + durationMs) {
      // Advance the WHOLE fixed window 10% at a time, then fetch a fresh frame.
      startMs += Math.ceil((now - startMs - durationMs) / (durationMs * 0.1)) * durationMs * 0.1;
      loadHistory();
      return;
    }
    liveTickActive = true;
    try {
      const state = await request(statePath);
      if (state.connection && state.connection.connected === false) {
        el('status').textContent = 'Live: Core disconnected';
        return;
      }
      if (!Array.isArray(state.analogInputs) || state.analogInputs.length<4) {
        el('status').textContent = 'Live: Waiting for analog input data';
        return;
      }
      const index = Math.floor((now-startMs) / (durationMs / visibleSamples));
      if (index<0 || index>=visibleSamples) return;
      for (let i = 0; i < 4; i++) {
        insertLiveSample(data[i+1], index, Number(state.analogInputs[i]));
      }
      // New measurements must repaint the traces even while the time window stays fixed.
      // uPlot setData(..., false) does not update auto-scaled Y ranges.
      // Always refresh the ranges, then restore our stationary time window.
      chart.setData(data, true);
      chart.setScale('x', { min: startMs / 1000, max: (startMs + durationMs) / 1000 });
      if (!config.display.autoScale) {
        chart.setScale('y', { min: config.display.min, max: config.display.max });
      }
      // Confirm the live data path is running even when all four inputs are constant.
      const time = new Date(now).toLocaleTimeString();
      const values = state.analogInputs.slice(0,4).map((value,i) => {
        const n = Number(value);
        return `AI${i+1}: ${Number.isFinite(n) ? n.toPrecision(5) : 'N/A'}`;
      });
      el('status').textContent = `Live ${time} | ${values.join(' | ')}`;
    } catch(err) { errorMessage(err); }
    finally { liveTickActive = false; }
  }
  async function start() {
    config = await request(configPath);
    durationMs = config.display.windowSeconds * 1000;
    setDisplay();
    el('live').onclick=moveLive;
    el('earlier').onclick=()=>move(-.5);
    el('later').onclick=()=>move(.5);
    el('zoom-in').onclick=()=>zoom(.5);
    el('zoom-out').onclick=()=>zoom(2);
    el('window').onchange=e=>{durationMs=Number(e.target.value)*1000; config.display.windowSeconds=durationMs/1000; saveDisplaySoon(); if(live)moveLive();else loadHistory();};
    el('traces').addEventListener('change',e=>{
      if(e.target.dataset.trace!==undefined) config.display.traces[Number(e.target.dataset.trace)]=e.target.checked;
      if(e.target.dataset.color!==undefined) config.display.colors[Number(e.target.dataset.color)]=e.target.value;
      saveDisplaySoon();makeChart();
    });
    el('autoscale').onchange=e=>{config.display.autoScale=e.target.checked;setDisplay();saveDisplaySoon();makeChart();};
    el('apply-scale').onclick=()=>{
      try {
        const min=checkedNumber('scale-min','Scale minimum');
        const max=checkedNumber('scale-max','Scale maximum');
        if(min>=max)throw new Error('Scale minimum must be less than scale maximum.');
        config.display.min=min;config.display.max=max;saveDisplaySoon();makeChart();
      }catch(err){el('status').textContent='Error: '+err.message;}
    };
    el('apply-recorder').onclick=async()=>{
      try {
        const interval=checkedNumber('record-interval','Recording interval',0.1,60);
        const mib=checkedNumber('history-memory','History memory (MiB)',16,1024,true);
        if(interval!==config.recordIntervalSec || mib!==config.maxMemoryMiB) {
          if(!confirm('Changing recording settings will erase all current chart history. Continue?'))return;
        }
        config=await request(configPath,{method:'PUT',headers:{'Content-Type':'application/json'},body:JSON.stringify({recordIntervalSec:interval,maxMemoryMiB:mib})});
        startLiveTimer();
        el('recorder-status').textContent='Recorder settings saved.';
        await loadHistory();
      } catch(err){el('recorder-status').textContent=err.message;}
    };
    const observer=new ResizeObserver(()=>{
      if(!data||!chart)return;
      const width=el('chart').clientWidth;
      if(Math.abs(width-chart.width)>8){makeChart();loadHistory();}
    });
    observer.observe(el('chart'));
    moveLive();
    startLiveTimer();
  }
  start().catch(errorMessage);
})();
