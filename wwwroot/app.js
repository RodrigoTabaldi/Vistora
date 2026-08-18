const $ = s => document.querySelector(s);
const $$ = s => [...document.querySelectorAll(s)];
let dashboard, properties = [], inspections = [], templates = [], currentView = 'dashboard', currentUser = null;
let authMode = 'login', occurrenceContext = null;

const STATUS = { Rascunho:'Rascunho', Agendada:'Agendada', EmAndamento:'Em andamento', EmRevisao:'Em revisão', AguardandoAssinatura:'Aguardando assinatura', Concluida:'Concluída', Contestada:'Contestada' };
const COND_MAP = { Otimo:0, Bom:0, Regular:1, Ruim:2, Danificado:2, Inexistente:2, NaoAvaliado:-1 };
const COND_VALUE = ['Bom','Regular','Danificado'];

const token = { get:() => localStorage.getItem('vistora-token'), set:t => localStorage.setItem('vistora-token', t), clear:() => localStorage.removeItem('vistora-token') };

async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  const t = token.get(); if (t) headers.Authorization = `Bearer ${t}`;
  if (options.body && !headers['content-type']) headers['content-type'] = 'application/json';
  const r = await fetch('/api/' + path, { ...options, headers });
  if (r.status === 401) { signOut(); throw new Error('Sessão expirada.'); }
  if (!r.ok) { let m = 'Não foi possível concluir a operação.'; try { m = (await r.json()).message || m; } catch {} throw new Error(m); }
  return r.status === 204 ? null : r.json();
}

const esc = v => String(v ?? '').replace(/[&<>'"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;',"'":'&#39;','"':'&quot;'}[c]));
const brl = n => Number(n || 0).toLocaleString('pt-BR', { style:'currency', currency:'BRL', maximumFractionDigits:0 });
const timeOf = d => new Date(d).toLocaleTimeString('pt-BR', { hour:'2-digit', minute:'2-digit' });
const dateOf = d => new Date(d).toLocaleDateString('pt-BR');
const initials = n => (n||'').split(' ').filter(Boolean).slice(0,2).map(a => a[0]).join('').toUpperCase();

function toast(message, tone = '') { const el = $('#toast'); el.textContent = message; el.className = `show ${tone}`; clearTimeout(toast._t); toast._t = setTimeout(() => el.className = '', 3400); }
function openModal(id) { $('#' + id).classList.add('show'); }
function closeModal(id) { $('#' + id).classList.remove('show'); }
function setHeader(title, subtitle, crumb = 'OPERAÇÃO', side = '') { $('#pageTitle').textContent = title; $('#pageSubtitle').textContent = subtitle; $('#crumb').textContent = crumb; $('#headerSide').innerHTML = side; }
const statusChip = s => `<span class="chip ${String(s).toLowerCase()}">${STATUS[s] || s}</span>`;

/* ---------- NAV: indicador animado ---------- */
function moveIndicator() {
  const nav = $('#topnav'), ind = $('#navIndicator');
  const active = nav?.querySelector('a.active');
  if (!nav || !ind) return;
  if (!active) { ind.style.width = '0px'; return; }
  ind.style.left = active.offsetLeft + 'px';
  ind.style.width = active.offsetWidth + 'px';
}
window.addEventListener('resize', moveIndicator);

function setActiveNav(view) {
  $$('[data-view]').forEach(a => a.classList.toggle('active', a.dataset.view === view));
  requestAnimationFrame(moveIndicator);
}

/* ---------- DASHBOARD ---------- */
function propertyCards(list = properties) {
  if (!list.length) return `<p class="field-hint">Nenhum imóvel encontrado.</p>`;
  return `<section class="properties">${list.map(p => `<article class="property clickable" data-property="${p.id}"><img src="${esc(p.imageUrl)}" alt="${esc(p.title)}" loading="lazy"><div class="copy"><span class="tag">${esc(p.type)} · ${esc(p.occupancy)}</span><h3>${esc(p.title)}</h3><p>${esc(p.neighborhood)}</p><footer><span>${p.area} m² · ${p.bedrooms || '—'} dorm.</span><span>Ver →</span></footer></div></article>`).join('')}</section>`;
}
function scheduleRows(items) {
  if (!items.length) return `<p class="field-hint">Nenhuma vistoria agendada.</p>`;
  return items.map(x => { const d = new Date(x.scheduledAt), hoje = d.toDateString() === new Date().toDateString();
    return `<button class="schedule-row" data-inspection="${x.id}"><div class="time">${timeOf(d)}<small>${hoje ? 'hoje' : d.toLocaleDateString('pt-BR',{day:'2-digit',month:'short'})}</small></div><div><strong>${esc(x.propertyName)}</strong><small>${esc(x.type)} · ${esc(x.inspector)} · ${x.completion}% preenchido</small></div><div class="row-end">${statusChip(x.status)}<span class="avatar">${initials(x.inspector)}</span></div></button>`; }).join('');
}
function occurrenceRows(items) {
  if (!items.length) return `<p class="field-hint">Nenhuma ocorrência aberta.</p>`;
  return items.map(x => { const p = x.priority === 'Alta' ? 'high' : x.priority === 'Baixa' ? 'low' : '';
    return `<button class="issue" data-occurrence="${x.id}"><i class="priority ${p}"></i><div><strong>${esc(x.title)}</strong><small>${esc(x.propertyName)} · prazo ${dateOf(x.dueDate)} · ${brl(x.estimatedCost)}</small></div><span>→</span></button>`; }).join('');
}
function renderDashboard() {
  const hora = new Date().getHours(), saud = hora < 12 ? 'Bom dia' : hora < 18 ? 'Boa tarde' : 'Boa noite';
  setHeader(`${saud}, ${(currentUser?.name || 'equipe').split(' ')[0]}`, new Date().toLocaleDateString('pt-BR',{weekday:'long',day:'2-digit',month:'long'}).replace(/^./, c=>c.toUpperCase()), 'PAINEL');
  const active = dashboard.inspections.find(i => i.status === 'EmAndamento') || dashboard.inspections[0];
  const spotlight = active ? `<div class="spotlight clickable" data-inspection="${active.id}"><img src="${esc(active.coverUrl)}" alt=""><div class="spotlight-body"><span class="status">● ${STATUS[active.status]||active.status}</span><h3>${esc(active.propertyName)}</h3><p>${esc(active.type)} · ${esc(active.code)}</p><div class="details"><span><b>◷</b>${timeOf(active.scheduledAt)}</span><span><b>◈</b>${esc(active.inspector)}</span><span><b>◭</b>${active.pendingItems} pendência(s)</span></div></div><div class="progress-area"><span>CHECKLIST</span><strong>${active.completion}%</strong><div class="bar"><i style="width:${active.completion}%"></i></div><small>Toque para continuar em campo</small></div></div>` : '';
  const audit = dashboard.audit.slice(0,5).map(a => `<div class="ev"><b></b><div><strong>${esc(a.action)} · ${esc(a.entity)}</strong><small>${esc(a.detail)}</small><time>${esc(a.actor)} · ${timeOf(a.occurredAt)}</time></div></div>`).join('');
  $('#pageContent').innerHTML = `
    <section class="metrics">${dashboard.metrics.map((m,i) => `<article class="metric"><span>${esc(m.label)}</span><strong>${esc(m.value)}</strong><small class="${i===1?'warn':''}">${esc(m.trend)}</small></article>`).join('')}</section>
    <div class="section-head"><div><span class="eyebrow">EM CAMPO AGORA</span><h2>Operação do dia</h2></div><button class="text-btn" data-view="agenda">Ver agenda →</button></div>
    ${spotlight}
    <section class="grid-two">
      <div class="surface"><div class="section-head"><div><span class="eyebrow">PRÓXIMAS VISITAS</span><h2>Agenda</h2></div><button class="text-btn" data-view="inspections">Todas →</button></div>${scheduleRows(inspections.slice(0,4))}</div>
      <div class="surface"><div class="section-head"><div><span class="eyebrow">ATENÇÃO</span><h2>Ocorrências</h2></div><button class="text-btn" data-view="occurrences">Ver todas</button></div>${occurrenceRows(dashboard.occurrences.filter(o=>o.status!=='Concluída'))}</div>
    </section>
    <div class="surface" style="margin-top:20px"><div class="section-head"><div><span class="eyebrow">HISTÓRICO</span><h2>Linha do tempo</h2></div></div><div class="timeline">${audit}</div></div>
    <section class="section-head list-heading"><div><span class="eyebrow">PORTFÓLIO</span><h2>Imóveis</h2></div><button class="text-btn" data-view="properties">Todos →</button></section>
    ${propertyCards(properties.slice(0,4))}`;
}

/* ---------- VIEWS ---------- */
async function renderView(view) {
  currentView = view;
  setActiveNav(view);
  document.body.classList.remove('nav-open');
  window.scrollTo({ top:0, behavior:'smooth' });
  if (view === 'dashboard') return renderDashboard();
  if (view === 'properties') { setHeader('Imóveis', `${properties.length} imóveis no portfólio.`, 'PORTFÓLIO', `<button class="primary" id="newPropertyBtn"><span class="plus">+</span> Novo imóvel</button>`); $('#pageContent').innerHTML = `<div class="page-toolbar"><div class="search-field">⌕ <input id="propertySearch" placeholder="Buscar por nome ou bairro"></div></div>${propertyCards()}`; return; }
  if (view === 'inspections' || view === 'agenda') { setHeader(view==='agenda'?'Agenda':'Vistorias', `${inspections.length} operações em acompanhamento.`, view==='agenda'?'OPERAÇÃO / AGENDA':'OPERAÇÃO / VISTORIAS', `<button class="primary" id="inlineNewInspection"><span class="plus">+</span> Nova vistoria</button>`); $('#pageContent').innerHTML = `<section class="surface list-surface">${scheduleRows(inspections)}</section>`; return; }
  if (view === 'assinaturas') { const pending = inspections.filter(i => i.status === 'AguardandoAssinatura'); setHeader('Assinaturas', `${pending.length} vistoria(s) aguardando assinatura.`, 'OPERAÇÃO / ASSINATURAS'); $('#pageContent').innerHTML = `<section class="surface list-surface">${scheduleRows(pending)}</section>`; return; }
  if (view === 'occurrences') { const open = dashboard.occurrences.filter(o=>o.status!=='Concluída'); setHeader('Ocorrências', `${open.length} pendências ativas — priorize o que precisa de atenção.`, 'OPERAÇÃO / OCORRÊNCIAS'); $('#pageContent').innerHTML = `<section class="surface list-surface">${occurrenceRows(open)}</section>`; return; }
  if (view === 'templates') return renderTemplates();
  const c = { reports:['Relatórios','Laudos e indicadores da operação.','Gere o laudo em PDF de cada vistoria concluída, com fotos, hash e assinatura das partes.'], team:['Equipe & acesso','Pessoas que fazem sua operação.','Convide vistoriadores e defina permissões por função.'], settings:['Configurações','Personalize sua operação Vistora.','Dados da empresa, marca do laudo e integrações.'] }[view] || ['Painel','',''];
  setHeader(c[0], c[1], 'GESTÃO');
  $('#pageContent').innerHTML = `<section class="empty-state"><span>◆</span><h2>${c[2]}</h2><p>Esta área faz parte do plano e será liberada na configuração inicial da sua conta.</p><button class="primary" id="featureBtn">${view==='reports'?'Gerar laudo':'Configurar'} →</button></section>`;
}

/* ---------- MODELOS ---------- */
function renderTemplates() {
  const total = templates.reduce((s,t)=>s+t.rooms.reduce((a,r)=>a+r.topics.length,0),0);
  setHeader('Modelos de vistoria', `${templates.length} modelos · ${total} tópicos catalogados.`, 'OPERAÇÃO / MODELOS', `<button class="primary" id="newTemplateBtn"><span class="plus">+</span> Criar modelo</button>`);
  const cards = templates.map(t => { const n = t.rooms.reduce((s,r)=>s+r.topics.length,0);
    return `<article class="template-card"><span class="tag">${t.propertyType || 'Genérico'} · ${t.isSystem?'padrão':'personalizado'}</span><h3>${esc(t.name)}</h3><p>${esc(t.description)}</p><div class="template-rooms">${t.rooms.slice(0,5).map(r=>`<span>${esc(r.name)}</span>`).join('')}${t.rooms.length>5?`<span>+${t.rooms.length-5}</span>`:''}</div><footer><small>${t.rooms.length} ambientes · ${n} tópicos</small><span style="display:flex;gap:6px"><button class="text-btn" data-view-template="${t.id}">Ver</button>${t.isSystem?'':`<button class="mini-del" data-del-template="${t.id}" title="Excluir">×</button>`}</span></footer></article>`; }).join('');
  $('#pageContent').innerHTML = `<section class="template-grid">${cards}</section>`;
}
function showTemplate(id) {
  const t = templates.find(x => x.id === id); if (!t) return;
  $('#templateModalContent').innerHTML = `<span class="eyebrow">${t.propertyType || 'GENÉRICO'} · ${t.isSystem?'MODELO PADRÃO':'PERSONALIZADO'}</span><h2>${esc(t.name)}</h2><p>${esc(t.description)}</p>${t.rooms.map(r=>`<div class="room-group" style="padding-top:16px"><div class="room-head"><h4>▤ ${esc(r.name)}</h4><span class="room-count">${r.topics.length} tópicos</span></div><div class="template-rooms" style="margin-top:10px">${r.topics.map(tp=>`<span>${esc(tp)}</span>`).join('')}</div></div>`).join('')}<button class="primary wide" data-close="templateModal">Fechar</button>`;
  openModal('templateModal');
}

let builderRooms = [];
function openTemplateBuilder() { builderRooms = [{ name:'', topics:[] }]; renderBuilder(); openModal('templateModal'); }
function renderBuilder() {
  $('#templateModalContent').innerHTML = `<span class="eyebrow">NOVO MODELO</span><h2>Criar modelo personalizado</h2><p>Defina os ambientes e os tópicos que serão inspecionados.</p>
    <label>Nome do modelo<input id="tplName" placeholder="Ex.: Apartamento 3 quartos — padrão da casa"></label>
    <div class="two-col"><label>Tipo de imóvel<select id="tplType"><option value="">Genérico</option><option>Apartamento</option><option>Casa</option><option>Comercial</option><option>Terreno</option><option>Condominio</option></select></label><label>Descrição<input id="tplDesc" placeholder="Curta descrição"></label></div>
    <div style="margin-top:20px" id="builderRooms">${builderRooms.map((r,i)=>roomBuilderHtml(r,i)).join('')}</div>
    <button class="ghost" id="addBuilderRoom">+ Adicionar ambiente</button>
    <button class="primary wide" id="saveTemplate">Salvar modelo <span>→</span></button>`;
}
function roomBuilderHtml(room, i) {
  return `<div class="tpl-builder-room" data-room="${i}"><div class="rowhead"><input class="room-name" value="${esc(room.name)}" placeholder="Nome do ambiente (ex.: Cozinha)"><button class="mini-del" data-del-room="${i}" title="Remover ambiente">×</button></div><div class="add-topic"><input class="topic-input" placeholder="Novo tópico (ex.: Bancada e cuba)"><button class="ghost" data-add-topic="${i}">Adicionar</button></div><div class="tpl-topics">${room.topics.map((t,j)=>`<span class="tt">${esc(t)}<button data-del-topic="${i}:${j}">×</button></span>`).join('')}</div></div>`;
}
function syncBuilderNames() { $$('#builderRooms .tpl-builder-room').forEach(el => { builderRooms[+el.dataset.room].name = el.querySelector('.room-name').value; }); }

/* ---------- VISTORIA EM CAMPO ---------- */
async function showInspection(id) {
  const item = inspections.find(x => x.id === id) || await api(`inspections/${id}`);
  const [items, evidence] = await Promise.all([api(`inspections/${id}/items`), api(`inspections/${id}/evidence`)]);
  const property = properties.find(p => p.id === item.propertyId);
  setActiveNav('inspections'); currentView = 'inspection-form';
  setHeader('Vistoria em campo', `${item.propertyName} · ${item.code}`, 'VISTORIAS / ' + item.code, `<button class="ghost" data-view="inspections">← Voltar</button>`);
  const rooms = [...new Set(items.map(i => i.room))];
  let n = 0;
  const roomsHtml = rooms.map(room => { const list = items.filter(i => i.room === room);
    const done = list.filter(i => i.condition !== 'NaoAvaliado').length;
    return `<div class="room-group"><div class="room-head"><h4>▤ ${esc(room)}</h4><span class="room-count">${done}/${list.length} avaliados</span><button type="button" class="text-btn" data-report-occurrence="${esc(room)}">◭ Ocorrência</button></div>${list.map(c => itemHtml(c, ++n)).join('')}<div class="add-topic"><input class="new-topic-input" placeholder="Adicionar tópico em ${esc(room)}"><button type="button" class="ghost" data-add-item="${esc(room)}">+ Tópico</button></div></div>`;
  }).join('') || `<p class="field-hint">Nenhum tópico ainda. Adicione um ambiente abaixo para começar.</p>`;
  const evHtml = evidence.length ? `<div class="evidence-gallery">${evidence.map(e=>`<figure><img src="${esc(e.url)}" alt="" loading="lazy"><figcaption>${esc(e.room)} · ${timeOf(e.capturedAt)}</figcaption></figure>`).join('')}</div>` : `<p class="field-hint">Sem evidências ainda.</p>`;
  const inspectionOccurrences = dashboard.occurrences.filter(o => o.inspectionId === id);
  const occHtml = inspectionOccurrences.length
    ? inspectionOccurrences.map(o => `<div class="ev"><b></b><div><strong>${esc(o.title)}${o.room ? ' · ' + esc(o.room) : ''}</strong><small>${esc(o.priority)} · prazo ${dateOf(o.dueDate)} · ${brl(o.estimatedCost)}</small></div></div>`).join('')
    : `<p class="field-hint">Nenhuma ocorrência registrada nesta vistoria.</p>`;
  const finalCard = item.status === 'Concluida'
    ? `<section class="form-card final-card"><div><span class="eyebrow" style="color:var(--gold-2)">CONCLUÍDA</span><h3>Vistoria assinada e concluída</h3><p>${item.signedBy ? `Assinada por ${esc(item.signedBy)}${item.signedAt ? ' em ' + dateOf(item.signedAt) : ''}.` : 'Esta vistoria já foi concluída.'}</p></div></section>`
    : item.status === 'AguardandoAssinatura'
    ? `<section class="form-card final-card"><div><span class="eyebrow" style="color:var(--gold-2)">ASSINATURA</span><h3>Assinar e concluir vistoria</h3><p>O checklist já foi preenchido. Confirme a assinatura para concluir definitivamente esta vistoria.</p></div><button type="button" class="primary" id="signBtn">Assinar e concluir <span>→</span></button></section>`
    : `<section class="form-card final-card"><div><span class="eyebrow" style="color:var(--gold-2)">CONCLUSÃO</span><h3>Fechar vistoria</h3><p>Revise os itens de atenção antes de enviar para assinatura.</p></div><button type="button" class="primary" id="completeBtn">Concluir vistoria <span>→</span></button></section>`;
  $('#pageContent').innerHTML = `<form id="fieldInspectionForm" class="inspection-form" data-id="${id}">
    <div class="inspection-top"><div><span class="eyebrow">${STATUS[item.status]||item.status}</span><h2>${esc(item.type)}</h2><p><strong>${esc(item.propertyName)}</strong> · ${esc(property?.address||'')}</p></div><div class="completion-chip"><strong id="checkProgress">${item.completion}%</strong><span>preenchido</span></div></div>
    <div class="inspection-toolbar"><span class="legend"><i></i>Bom<i class="attention"></i>Atenção<i class="bad"></i>Irregular</span><span class="field-hint" style="margin:0">Salvo automaticamente</span></div>
    <div class="inspection-layout">
      <div class="inspection-main">
        <section class="form-card"><div class="card-heading"><div><span class="eyebrow">IDENTIFICAÇÃO</span><h3>Dados da vistoria</h3></div></div><div class="field-grid"><label>Data<input type="date" value="${new Date(item.scheduledAt).toISOString().slice(0,10)}"></label><label>Vistoriador<input value="${esc(item.inspector)}"></label><label>Tipo<input value="${esc(item.type)}"></label><label>Responsável presente<input placeholder="Nome completo"></label></div></section>
        <section class="form-card"><div class="card-heading"><div><span class="eyebrow">CHECKLIST</span><h3>Condições por ambiente</h3><p>${items.length} tópicos em ${rooms.length} ambientes. Marque a condição e anexe fotos.</p></div></div>${roomsHtml}<div class="add-topic" style="margin-top:24px;border-top:1px solid var(--line);padding-top:20px"><input id="newRoomName" placeholder="Novo ambiente (ex.: Varanda gourmet)"><button type="button" class="ghost" id="addRoomBtn">+ Ambiente</button></div></section>
        ${finalCard}
      </div>
      <aside class="inspection-side">
        <div class="side-card"><span class="eyebrow">RESUMO</span><h3>${esc(item.propertyName)}</h3><p>${esc(property?.neighborhood||'')}</p><div class="kv"><span>Tipo</span><strong>${esc(property?.type||'—')}</strong><span>Área</span><strong>${property?.area||'—'} m²</strong><span>Código</span><strong>${esc(item.code)}</strong><span>Ambientes</span><strong>${rooms.length}</strong><span>Tópicos</span><strong>${items.length}</strong></div></div>
        <div class="side-card"><span class="eyebrow">EVIDÊNCIAS</span><h3>Galeria (${evidence.length})</h3>${evHtml}</div>
        <div class="side-card"><span class="eyebrow">OCORRÊNCIAS</span><h3>Nesta vistoria (${inspectionOccurrences.length})</h3>${occHtml}</div>
        <div class="side-card side-help"><strong style="font-size:16px">Boa vistoria ◆</strong><p>Registre uma foto ampla e uma de detalhe para cada divergência. A localização é gravada automaticamente junto ao hash da imagem.</p></div>
      </aside>
    </div></form>`;
}
function itemHtml(c, idx) {
  const sel = COND_MAP[c.condition];
  const radio = (v,l) => `<label><input type="radio" name="cond-${c.id}" value="${v}" ${sel===v?'checked':''}><span>${l}</span></label>`;
  return `<div class="room-item" data-item="${c.id}"><div class="room-number">${String(idx).padStart(2,'0')}</div><div class="room-content"><strong>${esc(c.name)}<button type="button" class="item-del" data-del-item="${c.id}" title="Remover tópico">×</button></strong>
    <div class="condition-options">${radio(0,'Bom')}${radio(1,'Regular')}${radio(2,'Irregular')}</div>
    <textarea data-notes="${c.id}" placeholder="Observações sobre este item…">${esc(c.notes)}</textarea>
    <div class="evidence-row"><label class="photo-add">◈ Tirar / anexar foto<input type="file" accept="image/*" capture="environment" multiple hidden data-photo="${c.id}" data-room="${esc(c.room)}"></label><span id="photo-count-${c.id}">${c.photoCount} foto(s)</span></div>
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
  try { await api(`inspections/${form.dataset.id}/items/${id}`, { method:'PUT', body: JSON.stringify({ condition: checked ? COND_VALUE[+checked.value] : 'NaoAvaliado', notes }) }); } catch {}
}

/* ---------- DETALHES ---------- */
function showOccurrence(id) { const x = dashboard.occurrences.find(o=>o.id===id); if(!x) return; $('#simpleModalContent').innerHTML = `<span class="eyebrow">OCORRÊNCIA · ${esc(x.priority).toUpperCase()}</span><h2>${esc(x.title)}</h2><p>${esc(x.propertyName)}</p><div class="detail-box"><span>Prazo</span><strong>${dateOf(x.dueDate)}</strong><span>Estimativa</span><strong>${brl(x.estimatedCost)}</strong><span>Status</span><strong>${esc(x.status)}</strong></div><button class="primary wide" data-close="simpleModal">Entendido</button>`; openModal('simpleModal'); }
function showProperty(id) { const p = properties.find(x=>x.id===id); if(!p) return; $('#simpleModalContent').innerHTML = `<img class="modal-image" src="${esc(p.imageUrl)}" alt=""><span class="eyebrow">${esc(p.type).toUpperCase()} · ${esc(p.occupancy).toUpperCase()}</span><h2>${esc(p.title)}</h2><p>${esc(p.address)}<br>${esc(p.neighborhood)}</p><div class="detail-box"><span>Área</span><strong>${p.area} m²</strong><span>Quartos</span><strong>${p.bedrooms||'—'}</strong><span>Vagas</span><strong>${p.parkingSpaces||'—'}</strong><span>Proprietário</span><strong>${esc(p.owner)}</strong></div><button class="primary wide" data-close="simpleModal">Fechar</button>`; openModal('simpleModal'); }

/* ---------- DADOS ---------- */
async function loadData() {
  [dashboard, properties, inspections, templates] = await Promise.all([api('dashboard'), api('properties'), api('inspections'), api('templates')]);
  $('#propertyId').innerHTML = properties.map(p => `<option value="${p.id}">${esc(p.title)}</option>`).join('');
  $('#templateId').innerHTML = `<option value="">Começar em branco</option>` + templates.map(t => `<option value="${t.id}">${esc(t.name)}</option>`).join('');
  $('#navInspCount').textContent = inspections.filter(i => i.status !== 'Concluida').length;
  $('#navSignCount').textContent = inspections.filter(i => i.status === 'AguardandoAssinatura').length;
}
function applySession(user) { currentUser = user; $('#profileInitials').textContent = initials(user.name); }
function signOut() { token.clear(); currentUser = null; $('#appShell').classList.add('hidden'); $('#loginScreen').classList.remove('hidden'); document.body.classList.remove('nav-open'); setAuthMode('login'); }

/* ---------- CADASTRO ---------- */
function setAuthMode(mode) {
  authMode = mode;
  $('#loginForm').classList.toggle('hidden', mode !== 'login');
  $('#registerForm').classList.toggle('hidden', mode !== 'register');
  $('#authTitle').textContent = mode === 'login' ? 'Entre na sua operação.' : 'Crie sua conta.';
  $('#authSubtitle').textContent = mode === 'login' ? 'Imóveis, vistorias e laudos em um só lugar.' : 'Cadastre-se e comece a usar a Vistora agora — sem depender da conta demonstrativa.';
  $('#demoNoteText').textContent = mode === 'login' ? 'Ambiente demonstrativo · credenciais preenchidas' : 'Já tem uma conta?';
  $('#authToggleBtn').textContent = mode === 'login' ? 'Cadastre-se' : 'Entrar';
}

/* ---------- EVENTOS ---------- */
$('#loginForm').addEventListener('submit', async e => {
  e.preventDefault(); const msg = $('#loginMessage'), btn = e.submitter;
  btn.disabled = true; btn.textContent = 'Entrando...'; msg.textContent = '';
  try {
    const r = await fetch('/api/auth/login', { method:'POST', headers:{'content-type':'application/json'}, body: JSON.stringify({ email:$('#loginEmail').value, password:$('#loginPassword').value }) });
    if (!r.ok) throw new Error();
    const data = await r.json(); token.set(data.accessToken); applySession(data.user);
    await loadData();
    $('#loginScreen').classList.add('hidden'); $('#appShell').classList.remove('hidden');
    setActiveNav('dashboard'); renderDashboard();
  } catch { msg.textContent = 'E-mail ou senha inválidos. Tente novamente.'; }
  finally { btn.disabled = false; btn.innerHTML = 'Entrar na Vistora <span>→</span>'; }
});

$('#registerForm').addEventListener('submit', async e => {
  e.preventDefault(); const msg = $('#registerMessage'), btn = e.submitter;
  btn.disabled = true; btn.textContent = 'Criando conta...'; msg.textContent = '';
  try {
    const r = await fetch('/api/auth/register', { method:'POST', headers:{'content-type':'application/json'}, body: JSON.stringify({ name:$('#registerName').value, email:$('#registerEmail').value, password:$('#registerPassword').value }) });
    const data = await r.json().catch(() => ({}));
    if (!r.ok) throw new Error(data.message || 'Não foi possível criar a conta.');
    token.set(data.accessToken); applySession(data.user);
    await loadData();
    $('#loginScreen').classList.add('hidden'); $('#appShell').classList.remove('hidden');
    setActiveNav('dashboard'); renderDashboard();
    e.target.reset();
  } catch (err) { msg.textContent = err.message; }
  finally { btn.disabled = false; btn.innerHTML = 'Criar conta <span>→</span>'; }
});

document.addEventListener('click', async e => {
  const t = e.target.closest('button,a,.clickable'); if (!t) return;
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
  if (t.id === 'forgotBtn') return toast('Use as credenciais demonstrativas preenchidas.');
  if (t.id === 'passwordToggle') { const f = $('#loginPassword'); f.type = f.type==='password'?'text':'password'; return; }
  if (t.id === 'registerPasswordToggle') { const f = $('#registerPassword'); f.type = f.type==='password'?'text':'password'; return; }
  if (t.id === 'authToggleBtn') return setAuthMode(authMode === 'login' ? 'register' : 'login');
  if (t.id === 'accountBtn' || t.id === 'logoutBtnMobile') return signOut();
  if (t.id === 'mobileMenu') return document.body.classList.toggle('nav-open');

  if (d.delItem) { const f = $('#fieldInspectionForm'); await api(`inspections/${f.dataset.id}/items/${d.delItem}`, { method:'DELETE' }); await loadData(); return showInspection(f.dataset.id); }
  if (d.addItem !== undefined) { const f = $('#fieldInspectionForm'); const input = t.closest('.room-group').querySelector('.new-topic-input'); if(!input.value.trim()) return; await api(`inspections/${f.dataset.id}/items`, { method:'POST', body: JSON.stringify({ room:d.addItem, name:input.value.trim() }) }); await loadData(); return showInspection(f.dataset.id); }
  if (t.id === 'addRoomBtn') { const f = $('#fieldInspectionForm'); const name = $('#newRoomName').value.trim(); if(!name) return; await api(`inspections/${f.dataset.id}/items`, { method:'POST', body: JSON.stringify({ room:name, name:'Condição geral' }) }); await loadData(); return showInspection(f.dataset.id); }
  if (t.id === 'completeBtn') { const f = $('#fieldInspectionForm'); try { await api(`inspections/${f.dataset.id}/complete`, { method:'POST' }); await loadData(); toast('Vistoria concluída. Aguardando assinatura.','ok'); renderView('assinaturas'); } catch(err){ toast(err.message,'warn'); } return; }
  if (t.id === 'signBtn') { const f = $('#fieldInspectionForm'); try { await api(`inspections/${f.dataset.id}/sign`, { method:'POST' }); await loadData(); toast('Vistoria assinada e concluída.','ok'); renderView('assinaturas'); } catch(err){ toast(err.message,'warn'); } return; }
  if (d.reportOccurrence !== undefined) {
    const f = $('#fieldInspectionForm');
    occurrenceContext = { inspectionId: f.dataset.id, room: d.reportOccurrence };
    $('#occurrenceRoomHint').textContent = `Ambiente: ${d.reportOccurrence}`;
    $('#occDueDate').value = new Date(Date.now() + 7*86400000).toISOString().slice(0,10);
    openModal('occurrenceModal');
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
  if (e.target.matches('[data-photo]')) return handlePhotos(e.target);
});
document.addEventListener('input', e => {
  if (e.target.id === 'propertySearch') { const term = e.target.value.toLowerCase(); const box = document.querySelector('.properties'); const filtered = properties.filter(p => `${p.title} ${p.neighborhood}`.toLowerCase().includes(term)); if (box) box.outerHTML = propertyCards(filtered); }
});
document.addEventListener('focusout', e => { if (e.target.matches('[data-notes]')) saveItem(e.target.dataset.notes); });
// Enter adiciona tópico sem enviar o formulário
document.addEventListener('keydown', e => {
  if (e.key !== 'Enter') return;
  if (e.target.matches('.new-topic-input')) { e.preventDefault(); e.target.closest('.room-group').querySelector('[data-add-item]').click(); }
  if (e.target.matches('.topic-input')) { e.preventDefault(); e.target.closest('.tpl-builder-room').querySelector('[data-add-topic]').click(); }
  if (e.target.id === 'newRoomName') { e.preventDefault(); $('#addRoomBtn').click(); }
});

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

$('#inspectionForm').addEventListener('submit', async e => {
  e.preventDefault();
  try {
    const created = await api('inspections', { method:'POST', body: JSON.stringify({ propertyId:$('#propertyId').value, type:$('#inspectionType').value, scheduledAt:$('#scheduledAt').value, inspector:$('#inspector').value, templateId:$('#templateId').value||null }) });
    await loadData(); closeModal('modal'); toast('Vistoria criada.','ok'); showInspection(created.id);
  } catch(err){ toast(err.message,'warn'); }
});
$('#templateId').addEventListener('change', e => {
  const t = templates.find(x => x.id === e.target.value);
  $('#templateHint').textContent = t ? `${t.rooms.length} ambientes e ${t.rooms.reduce((s,r)=>s+r.topics.length,0)} tópicos serão pré-carregados.` : 'Comece em branco e adicione os tópicos em campo.';
});
$('#propertyForm').addEventListener('submit', async e => {
  e.preventDefault();
  try { await api('properties', { method:'POST', body: JSON.stringify({ title:$('#newPropertyTitle').value, type:$('#newPropertyType').value, address:$('#newPropertyAddress').value, neighborhood:$('#newPropertyNeighborhood').value, area:+$('#newPropertyArea').value, bedrooms:+$('#newPropertyBedrooms').value||0, parkingSpaces:0, owner:$('#newPropertyOwner').value }) }); await loadData(); closeModal('propertyModal'); e.target.reset(); renderView('properties'); toast('Imóvel adicionado.','ok'); }
  catch(err){ toast(err.message,'warn'); }
});
$('#occurrenceForm').addEventListener('submit', async e => {
  e.preventDefault();
  if (!occurrenceContext) return;
  try {
    await api('occurrences', { method:'POST', body: JSON.stringify({ inspectionId:occurrenceContext.inspectionId, room:occurrenceContext.room, title:$('#occTitle').value, priority:$('#occPriority').value, dueDate:$('#occDueDate').value, estimatedCost:+$('#occCost').value||0 }) });
    closeModal('occurrenceModal'); e.target.reset(); toast('Ocorrência registrada.','ok');
    await loadData();
    if (occurrenceContext.inspectionId) await showInspection(occurrenceContext.inspectionId);
    occurrenceContext = null;
  } catch(err){ toast(err.message,'warn'); }
});
['modal','propertyModal','simpleModal','templateModal','occurrenceModal'].forEach(id => $('#'+id).addEventListener('click', e => { if (e.target.id === id) closeModal(id); }));
document.addEventListener('keydown', e => { if (e.key === 'Escape') $$('.modal.show').forEach(m => m.classList.remove('show')); });
$('#scheduledAt').value = new Date(Date.now() + 86400000).toISOString().slice(0,16);
setAuthMode('login');

// Lê nome/função direto do payload do JWT (não confia nele para autorização — só para exibir)
function userFromToken() {
  try { const p = JSON.parse(atob(token.get().split('.')[1].replace(/-/g,'+').replace(/_/g,'/'))); return { name:p.name, email:p.email, role:p.role }; }
  catch { return { name:'Usuário', role:'' }; }
}
(async () => {
  if (!token.get()) return;
  try { await loadData(); applySession(userFromToken()); $('#loginScreen').classList.add('hidden'); $('#appShell').classList.remove('hidden'); setActiveNav('dashboard'); renderDashboard(); }
  catch { signOut(); }
})();
