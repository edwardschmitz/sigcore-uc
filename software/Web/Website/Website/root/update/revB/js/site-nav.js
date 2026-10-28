/* Shared header, navigation and footer for every device-site page.
   This is the sole source for site-wide navigation and branding.
   No external assets and no Core dependency for rendering the shell. */
(() => {
  'use strict';
  const links = [
    ['Dashboard', 'index.html'],
    ['Logs', 'logviewer.php'],
    ['Control', 'control.html'],
    ['Properties', 'configuration.html'],
    ['Device', 'device.html'],
    ['Support', 'support.html'],
    ['Contact', 'contact.html']
  ];
  const current = location.pathname.split('/').pop() || 'index.html';
  const header = document.querySelector('[data-site-header]');
  if (header) {
    header.replaceChildren();
    const brand = document.createElement('a');
    brand.className = 'site-brand';
    brand.href = 'index.html';
    brand.setAttribute('aria-label','SigCore UC Dashboard');
    const logo = document.createElement('img');
    logo.src = '/images/en-z-em-logo.png';
    logo.alt = 'en Z em';
    const details = document.createElement('span');
    details.className = 'site-brand-details';
    const name = document.createElement('span');
    name.className = 'site-brand-label';
    name.textContent = 'SigCore UC';
    name.id = 'site-device-name';
    const serial = document.createElement('span');
    serial.className = 'site-brand-serial';
    serial.id = 'site-device-serial';
    serial.hidden = true; // No fabricated serial number while status is unavailable.
    details.append(name,serial);
    brand.append(logo,details);
    const nav = document.createElement('nav');
    nav.className = 'site-nav';
    nav.setAttribute('aria-label','Device navigation');
    for (const [label,url] of links) {
      const a = document.createElement('a');
      a.href = url;
      a.textContent = label;
      if (current === url) a.setAttribute('aria-current','page');
      nav.append(a);
    }
    header.append(brand,nav);
  }
  const footer = document.querySelector('[data-site-footer]');
  if (footer) footer.textContent = `© ${new Date().getFullYear()} en Z em | SigCore UC`;

  // Status is optional: the common shell always works even when Core is offline.
  // One lightweight status request on page load, not ongoing configuration polling.
  if (header) {
    fetch('/api/status.php', {cache:'no-store'})
      .then(r => { if(!r.ok) throw new Error('Status unavailable'); return r.json(); })
      .then(data => {
        const normalized = key => String(key).replace(/[^a-z0-9]/gi,'').toLowerCase();
        const prop = (object,key) => object && typeof object === 'object' && !Array.isArray(object)
          ? object[Object.keys(object).find(k => normalized(k) === normalized(key))] : undefined;
        const pick = (object, names) => {
          for(const scope of [object,prop(object,'system'),prop(object,'device'),prop(object,'status'),prop(object,'systemInfo'),prop(object,'deviceInfo')]) {
            for(const key of names) {
              const v = prop(scope,key);
              if (typeof v === 'string' && v.trim()) return v.trim();
              if (typeof v === 'number') return String(v);
            }
          }
          return null;
        };
        const sections = Array.isArray(data.sections) ? data.sections : [];
        const sigcore = sections.find(s => normalized(s.section) === 'sigcore');
        const getSectionValue = label => {
          const item = (Array.isArray(sigcore?.items) ? sigcore.items : [])
            .find(i => normalized(i.label) === normalized(label));
          return item?.data == null ? null : String(item.data).trim();
        };
        const deviceName = getSectionValue('Name') || pick(data,['systemName','deviceName','name']);
        const serialNumber = getSectionValue('Serial Number') || pick(data,['serialNumber','serial']);
        if(deviceName) document.getElementById('site-device-name').textContent = deviceName;
        if(serialNumber) { const element=document.getElementById('site-device-serial');element.textContent=serialNumber;element.hidden=false; }
      })
      .catch(() => {});
  }
})();
