const $ = s => document.querySelector(s);
const $$ = s => [...document.querySelectorAll(s)];
let dashboard, properties = [], inspections = [], templates = [], currentView = 'dashboard', currentUser = null;
// Telas registradas por vistoria.js (contratos, laudos, comparação, contestações).
var extraViews = {};

const STATUS = { Rascunho:'Rascunho', Agendada:'Agendada', EmAndamento:'Em andamento', EmRevisao:'Em revisão', AguardandoAssinatura:'Aguardando assinatura', Concluida:'Concluída', Contestada:'Contestada' };
const COND_MAP = { Otimo:0, Bom:0, Regular:1, Ruim:2, Danificado:2, Inexistente:2, NaoAvaliado:-1 };
const COND_VALUE = ['Bom','Regular','Danificado'];

const token = { get:() => localStorage.getItem('vistora-token'), set:t => localStorage.setItem('vistora-token', t), clear:() => localStorage.removeItem('vistora-token') };

/* ---------- CÓPIA LOCAL DAS LEITURAS ----------
   Toda resposta GET fica guardada para que a vistoria abra e seja preenchida sem rede.
   Se o armazenamento encher, o cache é descartado — a fila de gravação tem prioridade. */
const CACHE_PREFIX = 'vistora-cache:';

function writeCache(path, data) {
  try { localStorage.setItem(CACHE_PREFIX + path, JSON.stringify(data)); }
  catch {
    Object.keys(localStorage).filter(k => k.startsWith(CACHE_PREFIX)).forEach(k => localStorage.removeItem(k));
  }
}

function readCache(path) {
  const raw = localStorage.getItem(CACHE_PREFIX + path);
  if (raw === null) return null;
  try { return JSON.parse(raw); } catch { return null; }
}

/* ---------- FILA OFFLINE ---------- */
const QUEUE_KEY = 'vistora-fila';
const readQueue = () => { try { return JSON.parse(localStorage.getItem(QUEUE_KEY)) || []; } catch { return []; } };
const writeQueue = q => localStorage.setItem(QUEUE_KEY, JSON.stringify(q));

function queueRequest(path, options) {
  const queue = readQueue();
  queue.push({ path, method: options.method || 'POST', body: options.body, at: Date.now() });
  writeQueue(queue);
  updateOfflineBadge();
  toast('Sem conexão — alteração salva no dispositivo e será sincronizada.', 'warn');
}

async function flushQueue() {
  const queue = readQueue();
  if (!queue.length || !navigator.onLine) return;
  const remaining = [];
  for (const entry of queue) {
    try {
      const headers = { 'content-type': 'application/json' };
      const t = token.get(); if (t) headers.Authorization = `Bearer ${t}`;
      const r = await fetch('/api/' + entry.path, { method: entry.method, headers, body: entry.body });
      // 4xx que não seja de autenticação é erro permanente: descarta para não travar a fila.
      if (!r.ok && (r.status === 401 || r.status >= 500)) remaining.push(entry);
    } catch { remaining.push(entry); }
  }
  writeQueue(remaining);
  updateOfflineBadge();
  if (queue.length !== remaining.length) toast(`${queue.length - remaining.length} alteração(ões) sincronizada(s).`, 'ok');
}

function updateOfflineBadge() {
  const pending = readQueue().length;
  let badge = document.querySelector('#offlineBadge');
  if (!badge) {
    badge = document.createElement('div');
    badge.id = 'offlineBadge';
    badge.setAttribute('role', 'status');
    document.body.appendChild(badge);
  }
  const offline = !navigator.onLine;
  badge.className = offline ? 'show offline' : pending ? 'show pending' : '';
  badge.textContent = offline
    ? `● Modo offline${pending ? ` · ${pending} pendente(s)` : ''}`
    : pending ? `↻ Sincronizando ${pending} alteração(ões)…` : '';
}

window.addEventListener('online', () => { updateOfflineBadge(); flushQueue(); });
window.addEventListener('offline', updateOfflineBadge);
updateOfflineBadge();
flushQueue();

async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  const t = token.get(); if (t) headers.Authorization = `Bearer ${t}`;
  if (options.body && !headers['content-type']) headers['content-type'] = 'application/json';
  // Offline: gravações entram na fila local; leituras usam a última cópia baixada.
  const method = (options.method || 'GET').toUpperCase();
  if (!navigator.onLine) {
    if (method !== 'GET') { queueRequest(path, options); return { queued:true }; }
    const cached = readCache(path);
    if (cached !== null) return cached;
    throw new Error('Sem conexão e sem cópia local desta informação.');
  }
  let r;
  try { r = await fetch('/api/' + path, { ...options, headers }); }
  catch (networkError) {
    // Caiu a rede no meio da visita: repete o comportamento offline em vez de quebrar a tela.
    if (method !== 'GET') { queueRequest(path, options); return { queued:true }; }
    const cached = readCache(path);
    if (cached !== null) return cached;
    throw new Error('Sem conexão e sem cópia local desta informação.');
  }
  if (r.status === 401) { signOut(); throw new Error('Sessão expirada.'); }
  if (!r.ok) { let m = 'Não foi possível concluir a operação.'; try { m = (await r.json()).message || m; } catch {} throw new Error(m); }
  if (r.status === 204) return null;
  const data = await r.json();
  if (method === 'GET') writeCache(path, data);
  return data;
}

const esc = v => String(v ?? '').replace(/[&<>'"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));
const brl = n => Number(n || 0).toLocaleString('pt-BR', { style:'currency', currency:'BRL', maximumFractionDigits:0 });
const timeOf = d => new Date(d).toLocaleTimeString('pt-BR', { hour:'2-digit', minute:'2-digit' });
const dateOf = d => new Date(d).toLocaleDateString('pt-BR');
const initials = n => (n||'').split(' ').filter(Boolean).slice(0,2).map(a => a[0]).join('').toUpperCase();

function toast(message, tone = '') { const el = $('#toast'); el.textContent = message; el.className = `show ${tone}`; clearTimeout(toast._t); toast._t = setTimeout(() => el.className = '', 4200); }
function setHeader(title, subtitle, crumb = 'Operação', side = '') { $('#pageTitle').textContent = title; $('#pageSubtitle').textContent = subtitle; $('#crumb').textContent = crumb; $('#headerSide').innerHTML = side; }
const statusChip = s => `<span class="chip ${String(s).toLowerCase()}">${STATUS[s] || s}</span>`;
// Ícone do sprite; sempre decorativo — o rótulo textual fica ao lado.
const icon = name => `<svg class="ico" aria-hidden="true" focusable="false"><use href="#i-${name}"/></svg>`;

/* ---------- DIÁLOGOS ----------
   Contrato de acessibilidade único: prende o foco enquanto aberto, fecha no Esc e
   devolve o foco a quem abriu (WCAG 2.4.3 e 2.1.2). */
const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
let openerElement = null;

function openModal(id) {
  const modal = $('#' + id);
  openerElement = document.activeElement;
  modal.hidden = false;
  modal.classList.add('show');
  document.body.style.overflow = 'hidden';
  const first = modal.querySelector(FOCUSABLE);
  (modal.querySelector('input:not([type=hidden]), select, textarea') || first)?.focus();
}

function closeModal(id) {
  const modal = $('#' + id);
  modal.classList.remove('show');
  modal.hidden = true;
  if (!$$('.modal.show').length) document.body.style.overflow = '';
  openerElement?.focus?.();
  openerElement = null;
}

function trapFocus(e) {
  const modal = $$('.modal.show').pop();
  if (!modal || e.key !== 'Tab') return;
  const items = [...modal.querySelectorAll(FOCUSABLE)].filter(el => el.offsetParent !== null);
  if (!items.length) return;
  const first = items[0], last = items[items.length - 1];
  if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
  else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
}

/* ---------- MENUS SUSPENSOS ---------- */
function toggleMenu(btn, menu, open) {
  const shouldOpen = open ?? menu.hidden;
  btn.setAttribute('aria-expanded', String(shouldOpen));
  menu.hidden = !shouldOpen;
  if (shouldOpen) menu.querySelector(FOCUSABLE)?.focus();
}
function closeAllMenus() {
  [['#moreBtn', '#moreMenu'], ['#accountBtn', '#accountMenu']].forEach(([b, m]) => {
    const btn = $(b), menu = $(m);
    if (btn && menu && !menu.hidden) { menu.hidden = true; btn.setAttribute('aria-expanded', 'false'); }
  });
}

/* ---------- NAVEGAÇÃO ----------
   A rota atual é marcada com aria-current (leitores de tela) além do destaque visual. */
const VIEW_LABEL = {
  dashboard: 'Painel', properties: 'Imóveis', inspections: 'Vistorias', agenda: 'Agenda',
  occurrences: 'Ocorrências', templates: 'Modelos de checklist', contracts: 'Contratos e partes',
  reports: 'Laudos', contestations: 'Contestações', team: 'Equipe e acesso', settings: 'Configurações'
};

function setActiveNav(view) {
  $$('[data-view]').forEach(el => {
    const current = el.dataset.view === view;
    el.classList.toggle('active', current);
    if (current) el.setAttribute('aria-current', 'page'); else el.removeAttribute('aria-current');
  });
  // O botão "Mais" acende quando a tela ativa mora dentro dele.
  const more = $('#moreBtn');
  if (more) more.classList.toggle('has-current', !!$(`#moreMenu [data-view="${view}"]`));
  const announcer = $('#viewAnnouncer');
  if (announcer) announcer.textContent = `${VIEW_LABEL[view] || 'Tela'} carregado.`;
}

/* ---------- ENTRADA AO ROLAR ----------
   Revela blocos uma única vez. Desligado quando o sistema pede menos movimento. */
const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
const revealObserver = 'IntersectionObserver' in window
  ? new IntersectionObserver(entries => entries.forEach(entry => {
      if (!entry.isIntersecting) return;
      entry.target.classList.add('is-in');
      revealObserver.unobserve(entry.target);
    }), { rootMargin: '0px 0px -40px 0px', threshold: .05 })
  : null;

function revealContent(scope) {
  if (prefersReducedMotion.matches || !revealObserver) return;
  scope.querySelectorAll(':scope > section, :scope > div, :scope > article').forEach((el, i) => {
    if (i === 0) return;              // o primeiro bloco já está visível: revelar atrasaria a leitura
    el.classList.add('reveal');
    revealObserver.observe(el);
  });
}

/* ---------- DASHBOARD ---------- */
// Cartão de imóvel é um <button>: clicável por mouse, teclado e leitor de tela.
function propertyCards(list = properties) {
  if (!list.length) return `<p class="field-hint">Nenhum imóvel encontrado com esse termo.</p>`;
  return `<section class="properties">${list.map(p => `<button type="button" class="property" data-property="${p.id}"><img src="${esc(p.imageUrl)}" alt="" loading="lazy"><span class="copy"><span class="tag">${esc(p.type)} · ${esc(p.occupancy)}</span><h3>${esc(p.title)}</h3><p>${esc(p.neighborhood)}</p><span class="footer-row"><span>${p.area} m² · ${p.bedrooms || '—'} dorm.</span><span>Ver detalhes ${icon('seta')}</span></span></span></button>`).join('')}</section>`;
}
function scheduleRows(items) {
  if (!items.length) return `<p class="field-hint">Nenhuma vistoria agendada.</p>`;
  return items.map(x => { const d = new Date(x.scheduledAt), hoje = d.toDateString() === new Date().toDateString();
    return `<button class="schedule-row" data-inspection="${x.id}"><div class="time">${timeOf(d)}<small>${hoje ? 'hoje' : d.toLocaleDateString('pt-BR',{day:'2-digit',month:'short'})}</small></div><div><strong>${esc(x.propertyName)}</strong><small>${esc(x.type)} · ${esc(x.inspector)} · ${x.completion}% preenchido</small></div><div class="row-end">${statusChip(x.status)}<span class="avatar">${initials(x.inspector)}</span></div></button>`; }).join('');
}
function occurrenceRows(items) {
  if (!items.length) return `<p class="field-hint">Nenhuma ocorrência aberta.</p>`;
  return items.map(x => { const p = x.priority === 'Alta' ? 'high' : x.priority === 'Baixa' ? 'low' : '';
    // A prioridade aparece escrita, não apenas pela cor do marcador (WCAG 1.4.1).
    return `<button type="button" class="issue" data-occurrence="${x.id}"><i class="priority ${p}" aria-hidden="true"></i><div><strong>${esc(x.title)}</strong><small>Prioridade ${esc(x.priority).toLowerCase()} · ${esc(x.propertyName)} · prazo ${dateOf(x.dueDate)} · ${brl(x.estimatedCost)}</small></div><span aria-hidden="true">${icon('seta')}</span></button>`; }).join('');
}
function renderDashboard() {
  const hora = new Date().getHours(), saud = hora < 12 ? 'Bom dia' : hora < 18 ? 'Boa tarde' : 'Boa noite';
  setHeader(`${saud}, ${(currentUser?.name || 'equipe').split(' ')[0]}`, new Date().toLocaleDateString('pt-BR',{weekday:'long',day:'2-digit',month:'long'}).replace(/^./, c=>c.toUpperCase()), 'Painel');
  const active = dashboard.inspections.find(i => i.status === 'EmAndamento') || dashboard.inspections[0];
  const spotlight = active ? `<button type="button" class="spotlight on-dark" data-inspection="${active.id}"><img src="${esc(active.coverUrl)}" alt="" loading="lazy"><span class="spotlight-body"><span class="status">${STATUS[active.status]||active.status}</span><h3>${esc(active.propertyName)}</h3><p>${esc(active.type)} · ${esc(active.code)}</p><span class="details"><span>${icon('relogio')}<b class="sr-only">Horário</b>${timeOf(active.scheduledAt)}</span><span>${icon('equipe')}<b class="sr-only">Vistoriador</b>${esc(active.inspector)}</span><span>${icon('alerta')}<b class="sr-only">Pendências</b>${active.pendingItems} pendência(s)</span></span></span><span class="progress-area"><span>Checklist</span><strong>${active.completion}%</strong><span class="bar"><i style="width:${active.completion}%"></i></span><small>Continuar em campo</small></span></button>` : '';
  const audit = dashboard.audit.slice(0,5).map(a => `<div class="ev"><b></b><div><strong>${esc(a.action)} · ${esc(a.entity)}</strong><small>${esc(a.detail)}</small><time>${esc(a.actor)} · ${timeOf(a.occurredAt)}</time></div></div>`).join('');
  $('#pageContent').innerHTML = `
    <section class="metrics" aria-label="Indicadores da operação">${dashboard.metrics.map((m,i) => `<article class="metric"><span>${esc(m.label)}</span><strong>${esc(m.value)}</strong><small class="${i===1?'warn':''}">${esc(m.trend)}</small></article>`).join('')}</section>
    <section aria-labelledby="tituloCampo">
      <div class="section-head"><div><p class="eyebrow">Em campo agora</p><h2 id="tituloCampo">Operação do dia</h2></div><button class="text-btn" data-view="agenda">Ver agenda</button></div>
      ${spotlight}
    </section>
    <section class="grid-two">
      <div class="surface"><div class="section-head"><div><p class="eyebrow">Próximas visitas</p><h2>Agenda</h2></div><button class="text-btn" data-view="inspections">Todas</button></div>${scheduleRows(inspections.slice(0,4))}</div>
      <div class="surface"><div class="section-head"><div><p class="eyebrow">Atenção</p><h2>Ocorrências</h2></div><button class="text-btn" data-view="occurrences">Ver todas</button></div>${occurrenceRows(dashboard.occurrences.filter(o=>o.status!=='Concluída'))}</div>
    </section>
    <div class="surface" style="margin-top:var(--sp-5)"><div class="section-head"><div><p class="eyebrow">Histórico</p><h2>Linha do tempo</h2></div></div><div class="timeline">${audit}</div></div>
    <section>
      <div class="section-head list-heading"><div><p class="eyebrow">Portfólio</p><h2>Imóveis</h2></div><button class="text-btn" data-view="properties">Todos</button></div>
      ${propertyCards(properties.slice(0,4))}
    </section>`;
  revealContent($('#pageContent'));
}

/* ---------- VIEWS ---------- */
async function renderView(view) {
  currentView = view;
  setActiveNav(view);
  closeAllMenus();
  setNavOpen(false);
  window.scrollTo({ top: 0, behavior: prefersReducedMotion.matches ? 'auto' : 'smooth' });

  if (view === 'dashboard') return renderDashboard();

  if (view === 'properties') {
    setHeader('Imóveis', `${properties.length} imóveis no portfólio.`, 'Cadastros', `<button class="primary" id="newPropertyBtn">${icon('mais')} Novo imóvel</button>`);
    $('#pageContent').innerHTML = `
      <div class="page-toolbar">
        <div class="search-field">
          ${icon('busca')}
          <label class="sr-only" for="propertySearch">Buscar imóvel por nome ou bairro</label>
          <input id="propertySearch" type="search" placeholder="Buscar por nome ou bairro">
        </div>
      </div>${propertyCards()}`;
    return;
  }

  if (view === 'inspections' || view === 'agenda') {
    const agenda = view === 'agenda';
    setHeader(agenda ? 'Agenda' : 'Vistorias', `${inspections.length} operações em acompanhamento.`, agenda ? 'Operação · Agenda' : 'Operação · Vistorias', `<button class="primary" id="inlineNewInspection">${icon('mais')} Nova vistoria</button>`);
    $('#pageContent').innerHTML = `<section class="surface list-surface">${scheduleRows(inspections)}</section>`;
    return;
  }

  if (view === 'occurrences') {
    const open = dashboard.occurrences.filter(o => o.status !== 'Concluída');
    setHeader('Ocorrências', `${open.length} pendências ativas — priorize o que precisa de atenção.`, 'Operação · Ocorrências');
    $('#pageContent').innerHTML = `<section class="surface list-surface">${occurrenceRows(open)}</section>`;
    return;
  }
  if (view === 'templates') return renderTemplates();
  // Telas de vistoria.js carregam dados por API: uma falha precisa aparecer, não sumir em silêncio.
  if (extraViews[view]) {
    $('#pageContent').innerHTML = `<p class="field-hint">Carregando…</p>`;
    return extraViews[view]().catch(err => {
      toast(err.message || 'Não foi possível carregar esta tela.', 'warn');
      $('#pageContent').innerHTML = `<section class="empty-state"><span>${icon('alerta')}</span><h2>Não foi possível carregar esta tela.</h2><p>${esc(err.message || '')}</p><button class="primary" data-view="${view}">Tentar novamente</button></section>`;
    });
  }
  const c = { reports:['Relatórios','Laudos e indicadores da operação.','Gere o laudo em PDF de cada vistoria concluída, com fotos, hash e assinatura das partes.'], team:['Equipe & acesso','Pessoas que fazem sua operação.','Convide vistoriadores e defina permissões por função.'], settings:['Configurações','Personalize sua operação Vistora.','Dados da empresa, marca do laudo e integrações.'] }[view] || ['Painel','',''];
  setHeader(c[0], c[1], 'Conta');
  $('#pageContent').innerHTML = `<section class="empty-state"><span>${icon('selo')}</span><h2>${c[2]}</h2><p>Esta área faz parte do plano e será liberada na configuração inicial da sua conta.</p><button class="primary" id="featureBtn">${view==='reports'?'Gerar laudo':'Configurar'} ${icon('seta')}</button></section>`;
}

/* ---------- MODELOS ---------- */
function renderTemplates() {
  const total = templates.reduce((s,t)=>s+t.rooms.reduce((a,r)=>a+r.topics.length,0),0);
  setHeader('Modelos de vistoria', `${templates.length} modelos · ${total} tópicos catalogados.`, 'Cadastros · Modelos', `<button class="primary" id="newTemplateBtn">${icon('mais')} Criar modelo</button>`);
  const cards = templates.map(t => { const n = t.rooms.reduce((s,r)=>s+r.topics.length,0);
    return `<article class="template-card"><span class="tag">${t.propertyType || 'Genérico'} · ${t.isSystem?'padrão':'personalizado'}</span><h3>${esc(t.name)}</h3><p>${esc(t.description)}</p><div class="template-rooms">${t.rooms.slice(0,5).map(r=>`<span>${esc(r.name)}</span>`).join('')}${t.rooms.length>5?`<span>+${t.rooms.length-5}</span>`:''}</div><footer><small>${t.rooms.length} ambientes · ${n} tópicos</small><span style="display:flex;gap:6px"><button class="text-btn" data-view-template="${t.id}">Ver<span class="sr-only"> o modelo ${esc(t.name)}</span></button>${t.isSystem?'':`<button class="mini-del" data-del-template="${t.id}"><span class="sr-only">Excluir o modelo ${esc(t.name)}</span><span aria-hidden="true">×</span></button>`}</span></footer></article>`; }).join('');
  $('#pageContent').innerHTML = `<section class="template-grid">${cards}</section>`;
}
function showTemplate(id) {
  const t = templates.find(x => x.id === id); if (!t) return;
  $('#templateModalContent').innerHTML = `<p class="eyebrow">${t.propertyType || 'Genérico'} · ${t.isSystem?'Modelo padrão':'Personalizado'}</p><h2>${esc(t.name)}</h2><p>${esc(t.description)}</p>${t.rooms.map(r=>`<div class="room-group" style="padding-top:16px"><div class="room-head"><h4>${esc(r.name)}</h4><span class="room-count">${r.topics.length} tópicos</span></div><div class="template-rooms" style="margin-top:10px">${r.topics.map(tp=>`<span>${esc(tp)}</span>`).join('')}</div></div>`).join('')}<button class="primary wide" data-close="templateModal">Fechar</button>`;
  openModal('templateModal');
}

let builderRooms = [];
function openTemplateBuilder() { builderRooms = [{ name:'', topics:[] }]; renderBuilder(); openModal('templateModal'); }
function renderBuilder() {
  $('#templateModalContent').innerHTML = `<p class="eyebrow">Novo modelo</p><h2>Criar modelo personalizado</h2><p>Defina os ambientes e os tópicos que serão inspecionados.</p>
    <label>Nome do modelo<input id="tplName" placeholder="Ex.: Apartamento 3 quartos — padrão da casa"></label>
    <div class="two-col"><label>Tipo de imóvel<select id="tplType"><option value="">Genérico</option><option>Apartamento</option><option>Casa</option><option>Comercial</option><option>Terreno</option><option>Condominio</option></select></label><label>Descrição<input id="tplDesc" placeholder="Curta descrição"></label></div>
    <div style="margin-top:20px" id="builderRooms">${builderRooms.map((r,i)=>roomBuilderHtml(r,i)).join('')}</div>
    <button class="ghost" id="addBuilderRoom">${icon('mais')} Adicionar ambiente</button>
    <button class="primary wide" id="saveTemplate">Salvar modelo ${icon('seta')}</button>`;
}
function roomBuilderHtml(room, i) {
  return `<div class="tpl-builder-room" data-room="${i}"><div class="rowhead"><input class="room-name" value="${esc(room.name)}" placeholder="Nome do ambiente (ex.: Cozinha)"><button class="mini-del" data-del-room="${i}"><span class="sr-only">Remover ambiente</span><span aria-hidden="true">×</span></button></div><div class="add-topic"><input class="topic-input" placeholder="Novo tópico (ex.: Bancada e cuba)"><button class="ghost" data-add-topic="${i}">Adicionar</button></div><div class="tpl-topics">${room.topics.map((t,j)=>`<span class="tt">${esc(t)}<button data-del-topic="${i}:${j}"><span class="sr-only">Remover tópico ${esc(t)}</span><span aria-hidden="true">×</span></button></span>`).join('')}</div></div>`;
}
function syncBuilderNames() { $$('#builderRooms .tpl-builder-room').forEach(el => { builderRooms[+el.dataset.room].name = el.querySelector('.room-name').value; }); }

/* ---------- VISTORIA EM CAMPO ---------- */
async function showInspection(id) {
  const item = inspections.find(x => x.id === id) || await api(`inspections/${id}`);
  const [items, evidence] = await Promise.all([api(`inspections/${id}/items`), api(`inspections/${id}/evidence`)]);
  const property = properties.find(p => p.id === item.propertyId);
  setActiveNav('inspections'); currentView = 'inspection-form';
  setHeader('Vistoria em campo', `${item.propertyName} · ${item.code}`, 'Vistorias · ' + item.code, `<button class="ghost" data-view="inspections">Voltar para vistorias</button>`);
  const rooms = [...new Set(items.map(i => i.room))];
  let n = 0;
  const roomsHtml = rooms.map(room => { const list = items.filter(i => i.room === room);
    const done = list.filter(i => i.condition !== 'NaoAvaliado').length;
    return `<div class="room-group"><div class="room-head"><h4>${esc(room)}</h4><span class="room-count">${done}/${list.length} avaliados</span></div>${list.map(c => itemHtml(c, ++n)).join('')}<div class="add-topic"><input class="new-topic-input" placeholder="Adicionar tópico em ${esc(room)}"><button type="button" class="ghost" data-add-item="${esc(room)}">${icon('mais')} Tópico</button></div></div>`;
  }).join('') || `<p class="field-hint">Nenhum tópico ainda. Adicione um ambiente abaixo para começar.</p>`;
  const evHtml = evidence.length ? `<div class="evidence-gallery">${evidence.map(e=>`<figure><img src="${esc(e.url)}" alt="" loading="lazy"><figcaption>${esc(e.room)} · ${timeOf(e.capturedAt)}</figcaption></figure>`).join('')}</div>` : `<p class="field-hint">Sem evidências ainda.</p>`;
  $('#pageContent').innerHTML = `<form id="fieldInspectionForm" class="inspection-form" data-id="${id}">
    <div class="inspection-top"><div><p class="eyebrow">${STATUS[item.status]||item.status}</p><h2>${esc(item.type)}</h2><p><strong>${esc(item.propertyName)}</strong> · ${esc(property?.address||'')}</p></div><div class="completion-chip"><strong id="checkProgress">${item.completion}%</strong><span>preenchido</span></div></div>
    <div class="inspection-toolbar"><span class="legend"><i></i>Bom<i class="attention"></i>Atenção<i class="bad"></i>Irregular</span><span class="field-hint" style="margin:0">Salvo automaticamente</span></div>
    <div class="inspection-layout">
      <div class="inspection-main">
        <section class="form-card"><div class="card-heading"><div><p class="eyebrow">Identificação</p><h3>Dados da vistoria</h3></div></div><div class="field-grid"><label>Data<input type="date" value="${new Date(item.scheduledAt).toISOString().slice(0,10)}"></label><label>Vistoriador<input value="${esc(item.inspector)}"></label><label>Tipo<input value="${esc(item.type)}"></label><label>Responsável presente<input placeholder="Nome completo"></label></div></section>
        <section class="form-card"><div class="card-heading"><div><p class="eyebrow">Checklist</p><h3>Condições por ambiente</h3><p>${items.length} tópicos em ${rooms.length} ambientes. Marque a condição e anexe fotos.</p></div></div>${roomsHtml}<div class="add-topic" style="margin-top:24px;border-top:1px solid var(--line);padding-top:20px"><input id="newRoomName" placeholder="Novo ambiente (ex.: Varanda gourmet)"><button type="button" class="ghost" id="addRoomBtn">${icon('mais')} Ambiente</button></div></section>
        <div id="inspectionExtras"></div>
        <section class="form-card final-card"><div><p class="eyebrow" style="color:var(--accent-2)">Conclusão</p><h3>Fechar vistoria</h3><p>Revise os itens de atenção antes de enviar para revisão.</p></div><button type="button" class="primary" id="completeBtn">Concluir vistoria ${icon('seta')}</button></section>
      </div>
      <aside class="inspection-side">
        <div class="side-card"><p class="eyebrow">Resumo</p><h3>${esc(item.propertyName)}</h3><p>${esc(property?.neighborhood||'')}</p><div class="kv"><span>Tipo</span><strong>${esc(property?.type||'—')}</strong><span>Área</span><strong>${property?.area||'—'} m²</strong><span>Código</span><strong>${esc(item.code)}</strong><span>Ambientes</span><strong>${rooms.length}</strong><span>Tópicos</span><strong>${items.length}</strong></div></div>
        <div class="side-card"><p class="eyebrow">Evidências</p><h3>Galeria (${evidence.length})</h3>${evHtml}</div>
        <div class="side-card side-help"><p class="eyebrow">Boa prática</p><strong>Duas fotos por divergência</strong><p>Registre uma foto ampla e uma de detalhe para cada divergência. A localização é gravada automaticamente junto ao hash da imagem.</p></div>
      </aside>
    </div></form>`;
  renderInspectionExtras(id, item);
}
const SEVERITY = ['Nenhuma','Baixa','Media','Alta','Critica'];
const ISSUE_CLASS = { NaoClassificado:'Não classificado', DesgasteNatural:'Desgaste natural', DanoAnterior:'Dano anterior', DanoLocatario:'Dano do locatário', VicioConstrutivo:'Vício construtivo', ManutencaoProprietario:'Manutenção do proprietário', UrgenciaSeguranca:'Urgência de segurança', Inconclusivo:'Inconclusivo' };
const TEST_OUTCOME = { NaoTestado:'Não testado', Aprovado:'Aprovado', Reprovado:'Reprovado', Parcial:'Parcial' };
const options = (map, selected) => Object.entries(map).map(([v,l]) => `<option value="${v}" ${v===selected?'selected':''}>${l}</option>`).join('');

function itemHtml(c, idx) {
  const sel = COND_MAP[c.condition];
  const radio = (v,l) => `<label><input type="radio" name="cond-${c.id}" value="${v}" ${sel===v?'checked':''}><span>${l}</span></label>`;
  return `<div class="room-item" data-item="${c.id}"><div class="room-number">${String(idx).padStart(2,'0')}</div><div class="room-content"><strong>${esc(c.name)}${c.required?' <em class="req" title="Item obrigatório">obrigatório</em>':''}<button type="button" class="item-del" data-del-item="${c.id}" title="Remover tópico" aria-label="Remover tópico ${esc(c.name)}">×</button></strong>
    <div class="condition-options" role="radiogroup" aria-label="Condição de ${esc(c.name)}">${radio(0,'Bom')}${radio(1,'Regular')}${radio(2,'Irregular')}</div>
    <textarea data-notes="${c.id}" aria-label="Observações sobre ${esc(c.name)}" placeholder="Observações sobre este item…">${esc(c.notes)}</textarea>
    <details class="item-detail"><summary>Severidade, classificação e responsabilidade</summary>
      <div class="field-grid compact">
        <label>Severidade<select data-field="severity" data-of="${c.id}">${SEVERITY.map(s=>`<option value="${s}" ${s===c.severity?'selected':''}>${s}</option>`).join('')}</select></label>
        <label>Classificação<select data-field="issueClass" data-of="${c.id}">${options(ISSUE_CLASS, c.issueClass)}</select></label>
        <label>Teste realizado<select data-field="test" data-of="${c.id}">${options(TEST_OUTCOME, c.test)}</select></label>
        <label>Responsável<input data-field="responsibleParty" data-of="${c.id}" value="${esc(c.responsibleParty||'')}" placeholder="Proprietário, locatário…"></label>
        <label>Prazo<input type="date" data-field="dueDate" data-of="${c.id}" value="${c.dueDate?String(c.dueDate).slice(0,10):''}"></label>
        <label>Custo estimado (R$)<input type="number" min="0" step="10" data-field="estimatedCost" data-of="${c.id}" value="${c.estimatedCost||0}"></label>
      </div>
      <label>Recomendação de manutenção<input data-field="recommendation" data-of="${c.id}" value="${esc(c.recommendation||'')}" placeholder="Ex.: refazer vedação da janela"></label>
      <label class="check-line"><input type="checkbox" data-field="required" data-of="${c.id}" ${c.required?'checked':''}> Item obrigatório para concluir a vistoria</label>
      <p class="field-hint">A classificação é uma constatação do vistoriador, não um diagnóstico técnico.</p>
    </details>
    <div class="evidence-row"><label class="photo-add">${icon('camera')} Tirar ou anexar foto<input type="file" accept="image/*" capture="environment" multiple hidden data-photo="${c.id}" data-room="${esc(c.room)}"></label><span id="photo-count-${c.id}">${c.photoCount} foto(s)</span></div>
    <div class="photo-preview" id="photos-${c.id}"></div></div></div>`;
}
function updateProgress() {
  const form = $('#fieldInspectionForm'); if (!form) return;
  const groups = [...new Set($$('#fieldInspectionForm input[type=radio]').map(r => r.name))];
  const done = groups.filter(n => form.querySelector(`input[name="${n}"]:checked`)).length;
  $('#checkProgress').textContent = groups.length ? `${Math.round(done/groups.length*100)}%` : '0%';
}
async function saveItem(id) {
  const form = $('#fieldInspectionForm'); if (!form) return;
  const checked = form.querySelector(`input[name="cond-${id}"]:checked`);
  const notes = form.querySelector(`textarea[data-notes="${id}"]`)?.value || '';
  const field = name => form.querySelector(`[data-field="${name}"][data-of="${id}"]`)?.value || '';
  const payload = {
    condition: checked ? COND_VALUE[+checked.value] : 'NaoAvaliado',
    notes,
    severity: field('severity') || 'Nenhuma',
    issueClass: field('issueClass') || 'NaoClassificado',
    test: field('test') || 'NaoTestado',
    recommendation: field('recommendation') || null,
    responsibleParty: field('responsibleParty') || null,
    dueDate: field('dueDate') || null,
    estimatedCost: +field('estimatedCost') || 0,
    required: !!form.querySelector(`[data-field="required"][data-of="${id}"]`)?.checked
  };
  try { await api(`inspections/${form.dataset.id}/items/${id}`, { method:'PUT', body: JSON.stringify(payload) }); } catch {}
}

/* ---------- DETALHES ---------- */
function showOccurrence(id) { const x = dashboard.occurrences.find(o=>o.id===id); if(!x) return; $('#simpleModalContent').innerHTML = `<p class="eyebrow">Ocorrência · prioridade ${esc(x.priority).toLowerCase()}</p><h2>${esc(x.title)}</h2><p>${esc(x.propertyName)}</p><div class="detail-box"><span>Prazo</span><strong>${dateOf(x.dueDate)}</strong><span>Estimativa</span><strong>${brl(x.estimatedCost)}</strong><span>Status</span><strong>${esc(x.status)}</strong></div><button class="primary wide" data-close="simpleModal">Entendido</button>`; openModal('simpleModal'); }
function showProperty(id) { const p = properties.find(x=>x.id===id); if(!p) return; $('#simpleModalContent').innerHTML = `<img class="modal-image" src="${esc(p.imageUrl)}" alt=""><p class="eyebrow">${esc(p.type)} · ${esc(p.occupancy)}</p><h2>${esc(p.title)}</h2><p>${esc(p.address)}<br>${esc(p.neighborhood)}</p><div class="detail-box"><span>Área</span><strong>${p.area} m²</strong><span>Quartos</span><strong>${p.bedrooms||'—'}</strong><span>Vagas</span><strong>${p.parkingSpaces||'—'}</strong><span>Proprietário</span><strong>${esc(p.owner)}</strong></div><button class="primary wide" data-close="simpleModal">Fechar</button>`; openModal('simpleModal'); }

/* ---------- DADOS ---------- */
async function loadData() {
  let me;
  [dashboard, properties, inspections, templates, me] = await Promise.all([api('dashboard'), api('properties'), api('inspections'), api('templates'), api('me')]);
  applySession(me);
  $('#propertyId').innerHTML = properties.map(p => `<option value="${p.id}">${esc(p.title)}</option>`).join('');
  $('#templateId').innerHTML = `<option value="">Começar em branco</option>` + templates.map(t => `<option value="${t.id}">${esc(t.name)}</option>`).join('');
  const emAberto = inspections.filter(i => i.status !== 'Concluida').length;
  $('#navInspCount').textContent = emAberto;
  $('#navInspCountLabel').textContent = `, ${emAberto} em aberto`;
  try {
    const contracts = await api('contracts');
    $('#contractId').innerHTML = `<option value="">Sem contrato vinculado</option>` + contracts.map(c => `<option value="${c.id}">${esc(c.code)} · ${esc(c.property || '')}</option>`).join('');
  } catch {}
  syncInspectionKind();
  prefetchForField();
}

// Baixa antecipadamente o conteúdo das vistorias em aberto para que elas possam ser abertas
// e preenchidas dentro do imóvel, mesmo sem sinal. Silencioso: é otimização, não fluxo crítico.
function prefetchForField() {
  if (!navigator.onLine) return;
  const abertas = inspections.filter(i => i.status !== 'Concluida').slice(0, 10);
  Promise.allSettled(abertas.flatMap(i => [
    api(`inspections/${i.id}/items`), api(`inspections/${i.id}/evidence`),
    api(`inspections/${i.id}/meters`), api(`inspections/${i.id}/keys`),
    api(`inspections/${i.id}/inventory`), api(`inspections/${i.id}/validacao`),
    api(`inspections/${i.id}/laudos`)
  ]));
}
function applySession(user) {
  currentUser = user;
  $('#profileInitials').textContent = initials(user.name);
  $('#profileName').textContent = user.name;
  $('#accountName').textContent = user.name;
  $('#accountRole').textContent = user.role || '';
}

function signOut() {
  token.clear();
  currentUser = null;
  closeAllMenus();
  setNavOpen(false);
  $('#appShell').classList.add('hidden');
  $('#loginScreen').classList.remove('hidden');
  $('#loginEmail').focus();
}

/* ---------- VALIDAÇÃO DE FORMULÁRIO ----------
   Mensagem por campo, ligada por aria-describedby, com aria-invalid e foco no primeiro erro
   (WCAG 3.3.1 e 3.3.3). O toast sozinho não servia: some antes de ser lido. */
function fieldError(input, message) {
  const box = document.querySelector(`[data-error-for="${input.id}"]`);
  input.setAttribute('aria-invalid', 'true');
  if (box) { box.textContent = message; box.classList.add('is-visible'); }
}

function clearFieldError(input) {
  const box = document.querySelector(`[data-error-for="${input.id}"]`);
  input.removeAttribute('aria-invalid');
  if (box) { box.textContent = ''; box.classList.remove('is-visible'); }
}

const MENSAGENS = {
  valueMissing: 'Preencha este campo para continuar.',
  typeMismatch: 'Confira o formato do que foi digitado.',
  rangeUnderflow: 'Informe um número maior.',
  badInput: 'Valor inválido.'
};

function validateForm(form) {
  let firstInvalid = null;
  form.querySelectorAll('input, select, textarea').forEach(input => {
    clearFieldError(input);
    if (input.checkValidity()) return;
    const key = Object.keys(MENSAGENS).find(k => input.validity[k]) || 'badInput';
    fieldError(input, MENSAGENS[key]);
    firstInvalid = firstInvalid || input;
  });
  if (firstInvalid) { firstInvalid.focus(); return false; }
  return true;
}

/* ---------- EVENTOS ---------- */
$('#loginForm').addEventListener('submit', async e => {
  e.preventDefault();
  const msg = $('#loginMessage'), btn = e.submitter;
  msg.textContent = '';
  if (!validateForm(e.target)) return;

  btn.disabled = true;
  btn.textContent = 'Entrando…';
  try {
    const r = await fetch('/api/auth/login', { method:'POST', headers:{'content-type':'application/json'}, body: JSON.stringify({ email:$('#loginEmail').value, password:$('#loginPassword').value }) });
    if (!r.ok) throw new Error();
    const data = await r.json(); token.set(data.accessToken); applySession(data.user);
    await loadData();
    $('#loginScreen').classList.add('hidden'); $('#appShell').classList.remove('hidden');
    setActiveNav('dashboard'); renderDashboard();
    $('#pageContent').focus({ preventScroll: true });
  } catch { msg.textContent = 'E-mail ou senha inválidos. Confira os dados e tente de novo.'; }
  finally { btn.disabled = false; btn.innerHTML = `Entrar ${icon('seta')}`; }
});

// Some com o erro assim que a pessoa corrige o campo
document.addEventListener('input', e => {
  if (e.target.matches('[aria-invalid="true"]')) clearFieldError(e.target);
});

document.addEventListener('click', async e => {
  // Clique fora fecha os menus suspensos abertos
  if (!e.target.closest('.nav-more, .account-wrap')) closeAllMenus();

  const t = e.target.closest('button,a'); if (!t) return;
  const d = t.dataset;
  if (d.view) { e.preventDefault(); return renderView(d.view); }
  if (d.close) return closeModal(d.close);
  if (d.viewTemplate) return showTemplate(d.viewTemplate);
  if (d.inspection) return showInspection(d.inspection);
  if (d.occurrence) return showOccurrence(d.occurrence);
  if (d.property) return showProperty(d.property);
  if (d.delTemplate) { if (!confirm('Excluir este modelo?')) return; try { await api(`templates/${d.delTemplate}`, { method:'DELETE' }); await loadData(); renderTemplates(); toast('Modelo excluído.','ok'); } catch(err){ toast(err.message,'warn'); } return; }
  if (t.id === 'newInspectionBtn' || t.id === 'inlineNewInspection' || t.id === 'dockCreate') return openModal('modal');
  if (t.id === 'newPropertyBtn') return openModal('propertyModal');
  if (t.id === 'newTemplateBtn') return openTemplateBuilder();
  if (t.id === 'searchBtn') { await renderView('properties'); return $('#propertySearch')?.focus(); }
  if (t.id === 'notificationsBtn') return toast('Você tem ocorrências que pedem atenção.','warn');
  if (t.id === 'featureBtn') return toast('Disponível na configuração da sua conta.');
  if (t.id === 'forgotBtn') return toast('Ambiente demonstrativo: as credenciais já estão preenchidas.');
  if (t.id === 'passwordToggle') {
    const campo = $('#loginPassword'), mostrando = campo.type === 'text';
    campo.type = mostrando ? 'password' : 'text';
    t.setAttribute('aria-pressed', String(!mostrando));
    t.querySelector('[data-toggle-label]').textContent = mostrando ? 'Mostrar senha' : 'Ocultar senha';
    t.querySelector('use').setAttribute('href', mostrando ? '#i-olho' : '#i-olho-off');
    campo.focus();
    return;
  }
  if (t.id === 'logoutBtn' || t.id === 'logoutBtnMobile') return signOut();
  if (t.id === 'moreBtn') { const menu = $('#moreMenu'); const abrir = menu.hidden; closeAllMenus(); return toggleMenu(t, menu, abrir); }
  if (t.id === 'accountBtn') { const menu = $('#accountMenu'); const abrir = menu.hidden; closeAllMenus(); return toggleMenu(t, menu, abrir); }
  if (t.id === 'mobileMenu') return setNavOpen(!document.body.classList.contains('nav-open'));

  if (d.delItem) { const f = $('#fieldInspectionForm'); await api(`inspections/${f.dataset.id}/items/${d.delItem}`, { method:'DELETE' }); await loadData(); return showInspection(f.dataset.id); }
  if (d.addItem !== undefined) { const f = $('#fieldInspectionForm'); const input = t.closest('.room-group').querySelector('.new-topic-input'); if(!input.value.trim()) return; await api(`inspections/${f.dataset.id}/items`, { method:'POST', body: JSON.stringify({ room:d.addItem, name:input.value.trim() }) }); await loadData(); return showInspection(f.dataset.id); }
  if (t.id === 'addRoomBtn') { const f = $('#fieldInspectionForm'); const name = $('#newRoomName').value.trim(); if(!name) return; await api(`inspections/${f.dataset.id}/items`, { method:'POST', body: JSON.stringify({ room:name, name:'Condição geral' }) }); await loadData(); return showInspection(f.dataset.id); }
  if (t.id === 'completeBtn') {
    const f = $('#fieldInspectionForm');
    try {
      // Bloqueios de negócio (itens obrigatórios, fotos, vínculo entrada/saída) impedem a conclusão.
      const v = await api(`inspections/${f.dataset.id}/validacao`);
      const blocker = v.issues.find(i => i.blocking);
      if (blocker) return toast(blocker.message, 'warn');
      await api(`inspections/${f.dataset.id}/complete`, { method:'POST' });
      await loadData(); toast('Vistoria enviada para revisão.','ok'); renderView('inspections');
    } catch(err){ toast(err.message,'warn'); }
    return;
  }

  if (t.id === 'addBuilderRoom') { syncBuilderNames(); builderRooms.push({ name:'', topics:[] }); return renderBuilder(); }
  if (d.delRoom !== undefined) { syncBuilderNames(); builderRooms.splice(+d.delRoom,1); if(!builderRooms.length) builderRooms.push({name:'',topics:[]}); return renderBuilder(); }
  if (d.addTopic !== undefined) { syncBuilderNames(); const input = t.closest('.tpl-builder-room').querySelector('.topic-input'); if(!input.value.trim()) return; builderRooms[+d.addTopic].topics.push(input.value.trim()); return renderBuilder(); }
  if (d.delTopic !== undefined) { syncBuilderNames(); const [i,j] = d.delTopic.split(':').map(Number); builderRooms[i].topics.splice(j,1); return renderBuilder(); }
  if (t.id === 'saveTemplate') {
    syncBuilderNames();
    const name = $('#tplName').value.trim(), rooms = builderRooms.filter(r => r.name.trim() && r.topics.length);
    if (!name || !rooms.length) return toast('Informe um nome e ao menos um ambiente com tópicos.','warn');
    try { await api('templates', { method:'POST', body: JSON.stringify({ name, description:$('#tplDesc').value, propertyType:$('#tplType').value||null, rooms }) }); closeModal('templateModal'); await loadData(); renderTemplates(); toast('Modelo criado.','ok'); } catch(err){ toast(err.message,'warn'); }
  }
});

document.addEventListener('change', async e => {
  if (e.target.matches('#fieldInspectionForm input[type=radio]')) { updateProgress(); await saveItem(e.target.name.replace('cond-','')); }
  if (e.target.matches('[data-field][data-of]')) return saveItem(e.target.dataset.of);
  if (e.target.matches('[data-photo]')) return handlePhotos(e.target);
});
document.addEventListener('input', e => {
  if (e.target.id === 'propertySearch') { const term = e.target.value.toLowerCase(); const box = document.querySelector('.properties'); const filtered = properties.filter(p => `${p.title} ${p.neighborhood}`.toLowerCase().includes(term)); if (box) box.outerHTML = propertyCards(filtered); }
});
document.addEventListener('focusout', e => { if (e.target.matches('[data-notes]')) saveItem(e.target.dataset.notes); });

/* ---------- TECLADO ----------
   Esc fecha o que estiver aberto, Tab fica preso no diálogo, Enter adiciona tópico
   sem enviar o formulário inteiro. */
document.addEventListener('keydown', e => {
  if (e.key === 'Escape') {
    const aberto = $$('.modal.show').pop();
    if (aberto) return closeModal(aberto.id);
    if ($$('.nav-more-menu:not([hidden]), .account-menu:not([hidden])').length) { const foco = document.activeElement; closeAllMenus(); foco?.blur?.(); return; }
    if (document.body.classList.contains('nav-open')) return setNavOpen(false);
  }
  if (e.key === 'Tab') trapFocus(e);
  if (e.key !== 'Enter') return;
  if (e.target.matches('.new-topic-input')) { e.preventDefault(); e.target.closest('.room-group').querySelector('[data-add-item]').click(); }
  if (e.target.matches('.topic-input')) { e.preventDefault(); e.target.closest('.tpl-builder-room').querySelector('[data-add-topic]').click(); }
  if (e.target.id === 'newRoomName') { e.preventDefault(); $('#addRoomBtn').click(); }
});

/* ---------- MENU DE CELULAR ---------- */
function setNavOpen(open) {
  const sheet = $('#mobileSheet'), btn = $('#mobileMenu');
  if (!sheet || !btn) return;
  document.body.classList.toggle('nav-open', open);
  sheet.hidden = !open;
  btn.setAttribute('aria-expanded', String(open));
  btn.querySelector('use').setAttribute('href', open ? '#i-fechar' : '#i-menu');
  btn.querySelector('.sr-only').textContent = open ? 'Fechar menu' : 'Abrir menu';
  if (open) sheet.querySelector('a')?.focus();
}

/* ---------- CABEÇALHO NO SCROLL ----------
   Ganha sombra ao sair do topo; nada se move, para não competir com a leitura. */
let ticking = false;
window.addEventListener('scroll', () => {
  if (ticking) return;
  ticking = true;
  requestAnimationFrame(() => {
    $('#topbar')?.classList.toggle('is-scrolled', window.scrollY > 8);
    ticking = false;
  });
}, { passive: true });

async function handlePhotos(input) {
  const id = input.dataset.photo, room = input.dataset.room, preview = $('#photos-' + id), form = $('#fieldInspectionForm');
  let coords = null;
  try { coords = (await new Promise((res,rej) => navigator.geolocation.getCurrentPosition(res, rej, { timeout:4000 }))).coords; } catch {}
  for (const file of input.files) {
    if (!file.type.startsWith('image/')) continue;
    const dataUrl = await new Promise(res => { const r = new FileReader(); r.onload = () => res(r.result); r.readAsDataURL(file); });
    preview.insertAdjacentHTML('beforeend', `<img src="${dataUrl}" alt="Evidência">`);
    try {
      await api(`inspections/${form.dataset.id}/evidence`, { method:'POST', body: JSON.stringify({ itemId:id, room, dataUrl, latitude:coords?.latitude, longitude:coords?.longitude, accuracy:coords?.accuracy }) });
      $('#photo-count-' + id).textContent = `${preview.children.length} foto(s)` + (coords ? ' · geolocalizada' : '');
    } catch(err){ toast(err.message,'warn'); }
  }
  input.value = '';
}

const KIND_LABEL = { Entrada:'Vistoria de entrada', Saida:'Vistoria de saída', Periodica:'Vistoria periódica', Manutencao:'Vistoria de manutenção', Recebimento:'Recebimento de chaves', PreCompraVenda:'Vistoria pré-compra/venda', Captacao:'Vistoria de captação', Temporada:'Vistoria de temporada', Sinistro:'Vistoria de sinistro', InspecaoPredial:'Inspeção predial técnica' };

// Vistoria de saída só é aceita com a vistoria de entrada do mesmo imóvel vinculada.
function syncInspectionKind() {
  const kind = $('#inspectionType').value, propertyId = $('#propertyId').value;
  const isExit = kind === 'Saida';
  $('#previousField').classList.toggle('hidden', !isExit);
  if (isExit) {
    const candidates = inspections.filter(i => i.propertyId === propertyId && i.kind === 'Entrada');
    $('#previousInspectionId').innerHTML = candidates.length
      ? candidates.map(i => `<option value="${i.id}">${esc(i.code)} · ${dateOf(i.scheduledAt)}</option>`).join('')
      : `<option value="">Nenhuma vistoria de entrada encontrada para este imóvel</option>`;
  }
  const hint = $('#kindHint');
  const notice = kind === 'InspecaoPredial' ? 'Inspeção predial exige profissional habilitado, com registro no conselho e ART/RRT quando aplicável.'
    : isExit ? 'A comparação entrada × saída usará a vistoria selecionada acima.' : '';
  hint.textContent = notice;
  hint.classList.toggle('hidden', !notice);
}
$('#inspectionType').addEventListener('change', syncInspectionKind);
$('#propertyId').addEventListener('change', syncInspectionKind);

$('#inspectionForm').addEventListener('submit', async e => {
  e.preventDefault();
  if (!validateForm(e.target)) return;
  const kind = $('#inspectionType').value;
  const previousInspectionId = kind === 'Saida' ? ($('#previousInspectionId').value || null) : null;
  if (kind === 'Saida' && !previousInspectionId) {
    fieldError($('#previousInspectionId'), 'Selecione a vistoria de entrada correspondente.');
    $('#previousInspectionId').focus();
    return;
  }
  try {
    const created = await api('inspections', { method:'POST', body: JSON.stringify({ propertyId:$('#propertyId').value, type:KIND_LABEL[kind], kind, contractId:$('#contractId').value||null, previousInspectionId, scheduledAt:$('#scheduledAt').value, inspector:$('#inspector').value, templateId:$('#templateId').value||null }) });
    await loadData(); closeModal('modal'); toast('Vistoria criada.','ok'); showInspection(created.id);
  } catch(err){ toast(err.message,'warn'); }
});
$('#templateId').addEventListener('change', e => {
  const t = templates.find(x => x.id === e.target.value);
  $('#templateHint').textContent = t ? `${t.rooms.length} ambientes e ${t.rooms.reduce((s,r)=>s+r.topics.length,0)} tópicos serão pré-carregados.` : 'Comece em branco e adicione os tópicos em campo.';
});
$('#propertyForm').addEventListener('submit', async e => {
  e.preventDefault();
  if (!validateForm(e.target)) return;
  try { await api('properties', { method:'POST', body: JSON.stringify({ title:$('#newPropertyTitle').value, type:$('#newPropertyType').value, address:$('#newPropertyAddress').value, neighborhood:$('#newPropertyNeighborhood').value, area:+$('#newPropertyArea').value, bedrooms:+$('#newPropertyBedrooms').value||0, parkingSpaces:0, owner:$('#newPropertyOwner').value }) }); await loadData(); closeModal('propertyModal'); e.target.reset(); renderView('properties'); toast('Imóvel adicionado.','ok'); }
  catch(err){ toast(err.message,'warn'); }
});
// Clique no fundo do diálogo fecha (o Esc é tratado no listener único de teclado)
$$('.modal').forEach(m => m.addEventListener('click', e => { if (e.target === m) closeModal(m.id); }));
$('#scheduledAt').value = new Date(Date.now() + 86400000).toISOString().slice(0,16);

(async () => {
  if (!token.get()) return;
  try { await loadData(); $('#loginScreen').classList.add('hidden'); $('#appShell').classList.remove('hidden'); setActiveNav('dashboard'); renderDashboard(); }
  catch { signOut(); }
})();
