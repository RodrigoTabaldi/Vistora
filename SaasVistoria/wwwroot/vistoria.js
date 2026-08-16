/* Vistora — fluxo completo de vistoria: campo (medidores, chaves, inventário),
   validação de conclusão, laudo versionado, assinaturas, comparação e contestações.
   Carregado depois de app.js e reaproveita seus utilitários ($, api, esc, toast…).
   As primitivas de offline (fila e cópia local) ficam em app.js porque api() depende delas. */


// Registra o service worker e recarrega uma única vez quando uma versão nova assume o controle,
// para o usuário nunca ficar com metade do app desatualizada.
if ('serviceWorker' in navigator) {
  let reloading = false;
  navigator.serviceWorker.addEventListener('controllerchange', () => {
    if (reloading) return;
    reloading = true;
    location.reload();
  });
  navigator.serviceWorker.register('/sw.js').then(reg => reg.update()).catch(() => {});
}

/* ---------- HELPERS ---------- */
const CONDITIONS = ['Otimo','Bom','Regular','Ruim','Danificado','Inexistente','NaoAvaliado'];
const CONDITION_LABEL = { Otimo:'Ótimo', Bom:'Bom', Regular:'Regular', Ruim:'Ruim', Danificado:'Danificado', Inexistente:'Ausente', NaoAvaliado:'Não avaliado' };
const VERDICT_LABEL = { SemAlteracao:'Sem alteração', NovoDano:'Novo dano', Melhoria:'Melhoria', ItemRemovido:'Item removido', ItemAdicionado:'Item adicionado', AlteracaoNaoIdentificada:'Não identificada' };
const CONTESTATION_LABEL = { Aberta:'Aberta', EmAnalise:'Em análise', EvidenciaSolicitada:'Evidência solicitada', Aceita:'Aceita', Rejeitada:'Rejeitada', ParcialmenteAceita:'Parcialmente aceita', Resolvida:'Resolvida' };
const conditionOptions = selected => CONDITIONS.map(c => `<option value="${c}" ${c===selected?'selected':''}>${CONDITION_LABEL[c]}</option>`).join('');
const can = permission => (currentUser?.permissions || []).includes(permission);

/* ---------- PAINÉIS DA VISTORIA EM CAMPO ---------- */
async function renderInspectionExtras(id, inspection) {
  const host = $('#inspectionExtras');
  if (!host) return;
  const [meters, keys, inventory, validation, reports] = await Promise.all([
    api(`inspections/${id}/meters`), api(`inspections/${id}/keys`), api(`inspections/${id}/inventory`),
    api(`inspections/${id}/validacao`), api(`inspections/${id}/laudos`)
  ]).catch(() => [[], [], [], { issues: [], canComplete: true }, []]);

  const metersHtml = meters.length
    ? `<table class="data-table"><caption class="sr-only">Leituras de medidores</caption><thead><tr><th>Utilidade</th><th>Medidor</th><th>Leitura</th><th>Data</th></tr></thead><tbody>${meters.map(m => `<tr><td>${esc(m.kind)}</td><td>${esc(m.meterNumber)}</td><td>${m.value}</td><td>${dateOf(m.readAt)}</td></tr>`).join('')}</tbody></table>`
    : `<p class="field-hint">Nenhuma leitura registrada.</p>`;

  const keysHtml = keys.length
    ? `<table class="data-table"><caption class="sr-only">Chaves e controles</caption><thead><tr><th>Descrição</th><th>Qtd.</th><th>Estado</th></tr></thead><tbody>${keys.map(k => `<tr><td>${esc(k.description)}</td><td>${k.quantity}</td><td>${CONDITION_LABEL[k.condition]}</td></tr>`).join('')}</tbody></table>`
    : `<p class="field-hint">Nenhuma chave ou controle relacionado.</p>`;

  const inventoryHtml = inventory.length
    ? `<table class="data-table"><caption class="sr-only">Inventário de bens</caption><thead><tr><th>Ambiente</th><th>Bem</th><th>Série</th><th>Qtd.</th><th>Estado</th><th>Funciona</th></tr></thead><tbody>${inventory.map(a => `<tr><td>${esc(a.room)}</td><td>${esc(a.name)} ${esc(a.brand||'')}</td><td>${esc(a.serialNumber||'—')}</td><td>${a.quantity}</td><td>${CONDITION_LABEL[a.condition]}</td><td>${a.working?'Sim':'Não'}</td></tr>`).join('')}</tbody></table>`
    : `<p class="field-hint">Sem bens inventariados — use em imóveis mobiliados.</p>`;

  const issuesHtml = validation.issues.length
    ? `<ul class="issue-list">${validation.issues.map(i => `<li class="${i.blocking?'blocking':'warning'}"><b>${i.blocking?'Bloqueio':'Alerta'}</b> ${esc(i.message)}</li>`).join('')}</ul>`
    : `<p class="field-hint ok-hint">Nenhuma pendência de preenchimento. A vistoria pode ser concluída.</p>`;

  const reportsHtml = reports.length
    ? reports.map(r => `<div class="report-row"><div><strong>${esc(r.number)} · v${r.version}</strong><small>Emitido por ${esc(r.issuedBy)} em ${dateOf(r.issuedAt)} · integridade: ${esc(r.integrity)}</small><small class="mono">${esc(r.hash.slice(0,32))}…</small></div><div class="report-actions"><button type="button" class="ghost" data-open-report="${r.id}">Abrir / imprimir</button>${can('assinar')||can('aprovar')?`<button type="button" class="ghost" data-request-sign="${r.id}">Solicitar assinatura</button>`:''}</div>${r.signatures.length?`<ul class="sig-list">${r.signatures.map(s=>`<li>${esc(s.signerName)} · ${esc(s.role)} · ${s.refused?'recusou':dateOf(s.signedAt)}</li>`).join('')}</ul>`:''}</div>`).join('')
    : `<p class="field-hint">Nenhuma versão emitida ainda.</p>`;

  host.innerHTML = `
    <section class="form-card">
      <div class="card-heading"><div><p class="eyebrow">Campo</p><h3>Check-in e evidências de local</h3><p>${inspection.checkInAt ? `Check-in em ${dateOf(inspection.checkInAt)} ${timeOf(inspection.checkInAt)}` : 'Faça o check-in ao chegar no imóvel.'}</p></div></div>
      <div class="inline-actions">
        <button type="button" class="ghost" id="checkInBtn">${icon('local')} Check-in com GPS</button>
        <button type="button" class="ghost" id="checkOutBtn">${icon('relogio')} Check-out</button>
      </div>
    </section>

    <section class="form-card">
      <div class="card-heading"><div><p class="eyebrow">Medidores</p><h3>Leituras de água, energia e gás</h3></div></div>
      ${metersHtml}
      <div class="field-grid compact" style="margin-top:14px">
        <label>Utilidade<select id="meterKind"><option value="Agua">Água</option><option value="Energia">Energia</option><option value="Gas">Gás</option></select></label>
        <label>Nº do medidor<input id="meterNumber" placeholder="Ex.: A-99120"></label>
        <label>Leitura<input id="meterValue" type="number" step="0.01" min="0"></label>
      </div>
      <button type="button" class="ghost" id="addMeterBtn">${icon('mais')} Registrar leitura</button>
    </section>

    <section class="form-card">
      <div class="card-heading"><div><p class="eyebrow">Chaves</p><h3>Chaves, controles e acessos</h3></div></div>
      ${keysHtml}
      <div class="field-grid compact" style="margin-top:14px">
        <label>Descrição<input id="keyDescription" placeholder="Ex.: Controle do portão"></label>
        <label>Quantidade<input id="keyQuantity" type="number" min="1" value="1"></label>
        <label>Estado<select id="keyCondition">${conditionOptions('Bom')}</select></label>
      </div>
      <button type="button" class="ghost" id="addKeyBtn">${icon('mais')} Adicionar à relação</button>
    </section>

    <section class="form-card">
      <div class="card-heading"><div><p class="eyebrow">Inventário</p><h3>Bens do imóvel mobiliado</h3></div></div>
      ${inventoryHtml}
      <div class="field-grid compact" style="margin-top:14px">
        <label>Ambiente<input id="assetRoom" placeholder="Ex.: Cozinha"></label>
        <label>Bem<input id="assetName" placeholder="Ex.: Geladeira"></label>
        <label>Marca / modelo<input id="assetBrand" placeholder="Ex.: Brastemp BRM45"></label>
        <label>Nº de série<input id="assetSerial"></label>
        <label>Quantidade<input id="assetQuantity" type="number" min="1" value="1"></label>
        <label>Estado<select id="assetCondition">${conditionOptions('Bom')}</select></label>
        <label>Valor de referência (R$)<input id="assetValue" type="number" min="0" step="10" value="0"></label>
        <label>Funciona<select id="assetWorking"><option value="true">Sim</option><option value="false">Não</option></select></label>
      </div>
      <button type="button" class="ghost" id="addAssetBtn">${icon('mais')} Adicionar bem</button>
    </section>

    <section class="form-card">
      <div class="card-heading"><div><p class="eyebrow">Conferência</p><h3>Bloqueios e alertas</h3><p>Regras verificadas antes de emitir o laudo.</p></div></div>
      ${issuesHtml}
    </section>

    <section class="form-card">
      <div class="card-heading"><div><p class="eyebrow">Laudo</p><h3>Documento e assinaturas</h3><p>Cada emissão gera uma versão numerada com hash. Laudo emitido nunca é sobrescrito.</p></div></div>
      ${reportsHtml}
      <div class="inline-actions" style="margin-top:14px">
        <button type="button" class="ghost" data-preview-report="${id}">Ver prévia</button>
        ${can('aprovar') ? `<button type="button" class="primary" data-emit-report="${id}" ${validation.canComplete?'':'disabled title="Resolva os bloqueios acima"'}>Emitir laudo</button>` : ''}
        ${inspection.previousInspectionId ? `<button type="button" class="ghost" data-compare="${id}">Comparar com a entrada</button>` : ''}
        <button type="button" class="ghost" data-contest="${id}">Registrar contestação</button>
      </div>
    </section>`;
}

/* ---------- TELAS ---------- */
extraViews.contracts = async function () {
  const [contracts, people] = await Promise.all([api('contracts'), api('people')]);
  setHeader('Contratos e partes', `${contracts.length} contrato(s) · ${people.length} pessoa(s) cadastrada(s).`, 'CADASTROS / CONTRATOS',
    `<button class="primary" id="newPersonBtn"><span class="plus">+</span> Nova pessoa</button>`);
  $('#pageContent').innerHTML = `
    <section class="surface">
      <div class="section-head"><div><p class="eyebrow">Locação</p><h2>Contratos</h2></div>${can('criar') ? `<button class="text-btn" id="newContractBtn">Novo contrato</button>` : ''}</div>
      ${contracts.length ? `<table class="data-table"><thead><tr><th>Código</th><th>Imóvel</th><th>Locador</th><th>Locatário</th><th>Vigência</th><th>Aluguel</th><th>Garantia</th></tr></thead><tbody>${contracts.map(c => `<tr><td>${esc(c.code)}</td><td>${esc(c.property||'—')}</td><td>${esc(c.landlord||'—')}</td><td>${esc(c.tenant||'—')}</td><td>${dateOf(c.startsOn)} a ${dateOf(c.endsOn)}</td><td>${brl(c.rentValue)}</td><td>${esc(c.guarantee)}</td></tr>`).join('')}</tbody></table>` : `<p class="field-hint">Nenhum contrato cadastrado.</p>`}
    </section>
    <section class="surface" style="margin-top:20px">
      <div class="section-head"><div><p class="eyebrow">Partes</p><h2>Pessoas</h2></div></div>
      <table class="data-table"><thead><tr><th>Nome</th><th>Perfil</th><th>Documento</th><th>Contato</th></tr></thead>
      <tbody>${people.map(p => `<tr><td>${esc(p.name)}</td><td>${esc(p.role)}</td><td class="mono">${esc(p.document)}</td><td>${esc(p.email)} · ${esc(p.phone)}</td></tr>`).join('')}</tbody></table>
      <p class="field-hint">LGPD: documentos são exibidos mascarados; o número completo só é usado na emissão do laudo.</p>
    </section>`;
};

extraViews.reports = async function () {
  const all = await Promise.all(inspections.map(i => api(`inspections/${i.id}/laudos`)));
  const reports = all.flat();
  setHeader('Laudos', `${reports.length} documento(s) emitido(s).`, 'DOCUMENTOS / LAUDOS');
  $('#pageContent').innerHTML = `<section class="surface">
    ${reports.length ? reports.map(r => `<div class="report-row"><div><strong>${esc(r.number)} · v${r.version}</strong><small>${dateOf(r.issuedAt)} · ${esc(r.issuedBy)} · integridade: ${esc(r.integrity)}</small><small class="mono">${esc(r.hash.slice(0,40))}…</small></div><div class="report-actions"><button class="ghost" data-open-report="${r.id}">Abrir</button><button class="ghost" data-validate-report="${esc(r.number)}">Validar publicamente</button></div></div>`).join('') : `<p class="field-hint">Nenhum laudo emitido. Conclua uma vistoria e emita o documento na tela da vistoria.</p>`}
  </section>`;
};

extraViews.contestations = async function () {
  const list = await api('contestacoes');
  setHeader('Contestações', `${list.filter(c => !['Resolvida','Aceita','Rejeitada'].includes(c.status)).length} em aberto.`, 'DOCUMENTOS / CONTESTAÇÕES');
  $('#pageContent').innerHTML = `<section class="surface">${list.length ? list.map(c => `
    <article class="contestation">
      <header><strong>${esc(c.itemLabel)}</strong><span class="chip">${CONTESTATION_LABEL[c.status]}</span></header>
      <p>${esc(c.reason)}</p>
      <small>Aberta por ${esc(c.author)} em ${dateOf(c.openedAt)} · prazo de resposta até ${dateOf(c.deadline)}</small>
      <div class="thread">${c.messages.map(m => `<div><strong>${esc(m.author)}</strong> <time>${dateOf(m.sentAt)}</time><p>${esc(m.text)}</p></div>`).join('')}</div>
      ${can('editar') ? `<div class="field-grid compact">
        <label>Novo status<select data-cstatus="${c.id}">${Object.entries(CONTESTATION_LABEL).map(([v,l]) => `<option value="${v}" ${v===c.status?'selected':''}>${l}</option>`).join('')}</select></label>
        <label>Resposta<input data-cmsg="${c.id}" placeholder="Fundamento da decisão"></label>
      </div><button class="ghost" data-save-contestation="${c.id}">Registrar decisão</button>` : ''}
    </article>`).join('') : `<p class="field-hint">Nenhuma contestação registrada.</p>`}</section>`;
};

/* ---------- AÇÕES ---------- */
async function showComparison(id) {
  try {
    const c = await api(`inspections/${id}/comparacao`);
    const rows = c.lines.map(l => `<tr class="v-${l.verdict}"><td>${esc(l.room)}</td><td>${esc(l.item)}</td><td>${l.before?CONDITION_LABEL[l.before]:'—'}</td><td>${l.after?CONDITION_LABEL[l.after]:'—'}</td><td>${VERDICT_LABEL[l.verdict]}</td><td>${l.severity}</td><td>${esc(l.suggestedClass)}</td></tr>`).join('');
    $('#templateModalContent').innerHTML = `<p class="eyebrow">Comparação ${esc(c.entryCode)} × ${esc(c.exitCode)}</p>
      <h2>${esc(c.propertyName)}</h2>
      <p>${c.divergences} divergência(s) · estimativa de reparos ${brl(c.estimatedTotal)}</p>
      <table class="data-table"><thead><tr><th>Ambiente</th><th>Item</th><th>Entrada</th><th>Saída</th><th>Constatação</th><th>Severidade</th><th>Classificação sugerida</th></tr></thead><tbody>${rows}</tbody></table>
      <p class="field-hint">As classificações são sugestões automáticas. O desgaste natural não é imputável ao locatário (art. 23, III, Lei nº 8.245/1991) — a decisão final é do vistoriador e das partes.</p>
      <button class="primary wide" data-close="templateModal">Fechar</button>`;
    openModal('templateModal');
  } catch (err) { toast(err.message, 'warn'); }
}

/* O laudo é uma rota autenticada: abrir a URL direto numa aba nova não leva o token.
   Abrimos a janela no gesto do usuário (evita bloqueio de pop-up) e escrevemos o HTML buscado com o Bearer. */
async function openDocument(path, print) {
  const w = window.open('', '_blank');
  if (!w) return toast('Permita pop-ups para abrir o laudo.', 'warn');
  w.document.write('<p style="font:16px system-ui;padding:24px">Gerando documento…</p>');
  try {
    const r = await fetch('/api/' + path, { headers: { Authorization: `Bearer ${token.get()}` } });
    if (!r.ok) throw new Error('Não foi possível abrir o laudo.');
    const html = await r.text();
    w.document.open(); w.document.write(html); w.document.close();
    if (print) setTimeout(() => { try { w.print(); } catch {} }, 700);
  } catch (err) { w.close(); toast(err.message, 'warn'); }
}

async function requestSignature(reportId) {
  const name = prompt('Nome do signatário:');
  if (!name) return;
  const email = prompt('E-mail do signatário:');
  if (!email) return;
  const role = prompt('Perfil (Locador, Locatario, Fiador, Vistoriador, Corretor, Testemunha, Procurador):', 'Locatario') || 'Locatario';
  try {
    const r = await api(`laudos/${reportId}/assinaturas/solicitar`, { method:'POST', body: JSON.stringify({ signerName:name, signerEmail:email, role, method:'Otp' }) });
    $('#simpleModalContent').innerHTML = `<p class="eyebrow">Convite de assinatura</p><h2>Link gerado</h2>
      <p>Envie ao signatário. Válido até ${dateOf(r.expiresAt)}.</p>
      <div class="detail-box"><span>Link</span><strong class="mono break">${esc(r.link)}</strong><span>Código (OTP)</span><strong class="mono">${esc(r.otp || '—')}</strong></div>
      <p class="field-hint">Na demonstração o link e o código aparecem aqui; em produção seguem por e-mail, SMS ou WhatsApp.</p>
      <button class="primary wide" data-close="simpleModal">Entendido</button>`;
    openModal('simpleModal');
  } catch (err) { toast(err.message, 'warn'); }
}

async function openContestation(inspectionId) {
  const reason = prompt('Descreva o motivo da contestação:');
  if (!reason) return;
  try {
    await api('contestacoes', { method:'POST', body: JSON.stringify({ inspectionId, itemId:null, author: currentUser?.name || 'Parte interessada', reason }) });
    toast('Contestação registrada.', 'ok');
  } catch (err) { toast(err.message, 'warn'); }
}

document.addEventListener('click', async e => {
  const t = e.target.closest('button'); if (!t) return;
  const d = t.dataset, form = $('#fieldInspectionForm'), id = form?.dataset.id;

  if (t.id === 'checkInBtn') {
    let coords = null;
    try { coords = (await new Promise((res, rej) => navigator.geolocation.getCurrentPosition(res, rej, { timeout:6000 }))).coords; } catch {}
    await api(`inspections/${id}/check-in`, { method:'POST', body: JSON.stringify({ latitude:coords?.latitude ?? null, longitude:coords?.longitude ?? null }) });
    toast(coords ? 'Check-in registrado com geolocalização.' : 'Check-in registrado sem GPS.', 'ok');
    return showInspection(id);
  }
  if (t.id === 'checkOutBtn') { await api(`inspections/${id}/check-out`, { method:'POST' }); toast('Check-out registrado.', 'ok'); return showInspection(id); }

  if (t.id === 'addMeterBtn') {
    const number = $('#meterNumber').value.trim(), value = +$('#meterValue').value;
    if (!number || Number.isNaN(value)) return toast('Informe o número do medidor e a leitura.', 'warn');
    await api(`inspections/${id}/meters`, { method:'POST', body: JSON.stringify({ kind:$('#meterKind').value, meterNumber:number, value, photoUrl:null }) });
    toast('Leitura registrada.', 'ok'); return showInspection(id);
  }
  if (t.id === 'addKeyBtn') {
    const description = $('#keyDescription').value.trim();
    if (!description) return toast('Descreva a chave ou controle.', 'warn');
    await api(`inspections/${id}/keys`, { method:'POST', body: JSON.stringify({ description, quantity:+$('#keyQuantity').value || 1, condition:$('#keyCondition').value }) });
    toast('Chave adicionada à relação.', 'ok'); return showInspection(id);
  }
  if (t.id === 'addAssetBtn') {
    const name = $('#assetName').value.trim();
    if (!name) return toast('Informe o bem.', 'warn');
    await api(`inspections/${id}/inventory`, { method:'POST', body: JSON.stringify({
      room:$('#assetRoom').value.trim() || 'Geral', name, brand:$('#assetBrand').value.trim(), model:'',
      serialNumber:$('#assetSerial').value.trim(), quantity:+$('#assetQuantity').value || 1,
      condition:$('#assetCondition').value, referenceValue:+$('#assetValue').value || 0, working:$('#assetWorking').value === 'true' }) });
    toast('Bem inventariado.', 'ok'); return showInspection(id);
  }

  if (d.previewReport) return openDocument(`inspections/${d.previewReport}/laudos/previa`, false);
  if (d.emitReport) {
    try { const r = await api(`inspections/${d.emitReport}/laudos`, { method:'POST' }); toast(`Laudo ${r.number} v${r.version} emitido.`, 'ok'); return showInspection(d.emitReport); }
    catch (err) { return toast(err.message, 'warn'); }
  }
  if (d.openReport) return openDocument(`laudos/${d.openReport}/html`, true);
  if (d.validateReport) {
    try {
      const v = await api(`publico/laudos/${encodeURIComponent(d.validateReport)}`);
      $('#simpleModalContent').innerHTML = `<p class="eyebrow">Validação pública</p><h2>${esc(v.number)} · v${v.version}</h2>
        <div class="detail-box"><span>Emissora</span><strong>${esc(v.company)}</strong><span>Emitido em</span><strong>${dateOf(v.issuedAt)}</strong><span>Integridade</span><strong>${esc(v.integrity)}</strong><span>Assinaturas</span><strong>${v.signatures.length}</strong></div>
        <p class="field-hint mono break">${esc(v.hash)}</p><button class="primary wide" data-close="simpleModal">Fechar</button>`;
      return openModal('simpleModal');
    } catch (err) { return toast(err.message, 'warn'); }
  }
  if (d.requestSign) return requestSignature(d.requestSign);
  if (d.compare) return showComparison(d.compare);
  if (d.contest) return openContestation(d.contest);
  if (d.saveContestation) {
    const status = document.querySelector(`[data-cstatus="${d.saveContestation}"]`).value;
    const message = document.querySelector(`[data-cmsg="${d.saveContestation}"]`).value.trim();
    try { await api(`contestacoes/${d.saveContestation}`, { method:'PUT', body: JSON.stringify({ status, decision:message || null, message:message || null, author: currentUser?.name, attachmentUrl:null }) }); toast('Contestação atualizada.', 'ok'); return extraViews.contestations(); }
    catch (err) { return toast(err.message, 'warn'); }
  }

  if (t.id === 'newPersonBtn') return openPersonForm();
  if (t.id === 'newContractBtn') return openContractForm();
});

function openPersonForm() {
  $('#simpleModalContent').innerHTML = `<p class="eyebrow">Cadastro</p><h2>Nova pessoa</h2>
    <label>Nome completo<input id="personName" required></label>
    <label>CPF ou CNPJ<input id="personDocument" inputmode="numeric" placeholder="Somente números"></label>
    <div class="two-col"><label>E-mail<input id="personEmail" type="email"></label><label>Telefone<input id="personPhone"></label></div>
    <label>Perfil<select id="personRole"><option value="Locador">Locador</option><option value="Locatario">Locatário</option><option value="Fiador">Fiador</option><option value="Procurador">Procurador</option><option value="Corretor">Corretor</option><option value="Testemunha">Testemunha</option></select></label>
    <button class="primary wide" id="savePerson">Salvar pessoa ${icon('seta')}</button>`;
  openModal('simpleModal');
}

async function openContractForm() {
  const people = await api('people');
  const opts = role => people.filter(p => !role || p.role === role).map(p => `<option value="${p.id}">${esc(p.name)}</option>`).join('');
  $('#simpleModalContent').innerHTML = `<p class="eyebrow">Locação</p><h2>Novo contrato</h2>
    <label>Imóvel<select id="contractProperty">${properties.map(p => `<option value="${p.id}">${esc(p.title)}</option>`).join('')}</select></label>
    <div class="two-col"><label>Locador<select id="contractLandlord">${opts('Locador') || opts()}</select></label><label>Locatário<select id="contractTenant">${opts('Locatario') || opts()}</select></label></div>
    <label>Fiador (opcional)<select id="contractGuarantor"><option value="">Sem fiador</option>${opts()}</select></label>
    <div class="two-col"><label>Início<input id="contractStart" type="date" required></label><label>Fim<input id="contractEnd" type="date" required></label></div>
    <div class="two-col"><label>Aluguel (R$)<input id="contractRent" type="number" min="0" step="50"></label><label>Garantia<select id="contractGuarantee"><option>Fiador</option><option>Caução</option><option>Seguro-fiança</option><option>Título de capitalização</option><option>Sem garantia</option></select></label></div>
    <button class="primary wide" id="saveContract">Salvar contrato ${icon('seta')}</button>`;
  openModal('simpleModal');
}

document.addEventListener('click', async e => {
  if (e.target.id === 'savePerson') {
    const body = { name:$('#personName').value.trim(), document:$('#personDocument').value, email:$('#personEmail').value.trim(), phone:$('#personPhone').value.trim(), role:$('#personRole').value };
    if (!body.name) return toast('Informe o nome.', 'warn');
    try { await api('people', { method:'POST', body: JSON.stringify(body) }); closeModal('simpleModal'); toast('Pessoa cadastrada.', 'ok'); extraViews.contracts(); }
    catch (err) { toast(err.message, 'warn'); }
  }
  if (e.target.id === 'saveContract') {
    const body = {
      propertyId:$('#contractProperty').value, landlordId:$('#contractLandlord').value, tenantId:$('#contractTenant').value,
      guarantorId:$('#contractGuarantor').value || null, startsOn:$('#contractStart').value, endsOn:$('#contractEnd').value,
      rentValue:+$('#contractRent').value || 0, guarantee:$('#contractGuarantee').value
    };
    if (!body.startsOn || !body.endsOn) return toast('Informe a vigência do contrato.', 'warn');
    try { await api('contracts', { method:'POST', body: JSON.stringify(body) }); closeModal('simpleModal'); toast('Contrato cadastrado.', 'ok'); extraViews.contracts(); }
    catch (err) { toast(err.message, 'warn'); }
  }
});
