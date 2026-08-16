/* Página pública de assinatura do laudo (/assinar?token=…).
   Independente do app: não usa token de sessão, só o convite do link.

   Acessibilidade: além do desenho no canvas — que não é operável por teclado nem por
   leitor de tela — existe a alternativa de assinar digitando o nome, gerando a mesma
   imagem de assinatura. Erros são anunciados por role="alert" junto ao campo. */

const $ = s => document.querySelector(s);
const params = new URLSearchParams(location.search);
const invite = params.get('token');

const pad = $('#pad');
const ctx = pad.getContext('2d');
let drawing = false, hasDrawn = false, opener = null;

/* ---------- assinatura desenhada ---------- */
function sizeCanvas() {
  const ratio = window.devicePixelRatio || 1;
  const rect = pad.getBoundingClientRect();
  if (!rect.width) return;
  const snapshot = hasDrawn ? pad.toDataURL() : null;
  pad.width = Math.round(rect.width * ratio);
  pad.height = Math.round(rect.height * ratio);
  ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
  ctx.lineWidth = 2.4;
  ctx.lineCap = 'round';
  ctx.lineJoin = 'round';
  ctx.strokeStyle = '#14201c';
  if (snapshot) {
    const img = new Image();
    img.onload = () => ctx.drawImage(img, 0, 0, rect.width, rect.height);
    img.src = snapshot;
  }
}

const pointOf = e => {
  const r = pad.getBoundingClientRect();
  const p = e.touches ? e.touches[0] : e;
  return { x: p.clientX - r.left, y: p.clientY - r.top };
};

pad.addEventListener('pointerdown', e => {
  e.preventDefault();
  pad.setPointerCapture?.(e.pointerId);
  drawing = true; hasDrawn = true;
  clearError();
  const p = pointOf(e);
  ctx.beginPath();
  ctx.moveTo(p.x, p.y);
});
pad.addEventListener('pointermove', e => {
  if (!drawing) return;
  e.preventDefault();
  const p = pointOf(e);
  ctx.lineTo(p.x, p.y);
  ctx.stroke();
});
['pointerup', 'pointercancel', 'pointerleave'].forEach(ev => pad.addEventListener(ev, () => { drawing = false; }));
window.addEventListener('resize', sizeCanvas);

$('#clearBtn').addEventListener('click', () => {
  ctx.clearRect(0, 0, pad.width, pad.height);
  hasDrawn = false;
  $('#typedName').value = '';
  clearError();
});

/* ---------- alternativa por digitação ---------- */
$('#typedBtn').addEventListener('click', () => {
  $('#typedField').classList.remove('hidden');
  $('#typedName').focus();
});

$('#typedName').addEventListener('input', e => {
  const nome = e.target.value.trim();
  ctx.clearRect(0, 0, pad.width, pad.height);
  hasDrawn = false;
  if (!nome) return;
  const rect = pad.getBoundingClientRect();
  ctx.font = '600 34px Fraunces, Georgia, serif';
  ctx.fillStyle = '#14201c';
  ctx.textBaseline = 'middle';
  ctx.fillText(nome, 24, rect.height / 2);
  hasDrawn = true;
  clearError();
});

/* ---------- erros ---------- */
const clearError = () => { $('#padErro').textContent = ''; };
function showError(el, message) {
  const target = typeof el === 'string' ? $(el) : el;
  target.textContent = message;
}

/* ---------- carga do convite ---------- */
async function load() {
  const intro = $('#intro');
  if (!invite) { intro.textContent = 'Link inválido: o convite não traz o código de assinatura.'; return; }

  let r, data;
  try {
    r = await fetch('/api/publico/assinaturas/' + encodeURIComponent(invite));
    data = await r.json();
  } catch {
    intro.textContent = 'Não foi possível verificar o convite. Verifique sua conexão e recarregue a página.';
    return;
  }
  if (!r.ok) { intro.textContent = data.message || 'Link inválido ou expirado.'; return; }

  intro.textContent = `Olá, ${data.signerName}. Confira os dados e assine o documento.`;
  $('#details').innerHTML = `
    <div><dt>Documento</dt><dd>${data.report}</dd></div>
    <div><dt>Imóvel</dt><dd>${data.inspection ?? '—'}</dd></div>
    <div><dt>Seu papel</dt><dd>${data.role}</dd></div>
    <div><dt>Válido até</dt><dd>${new Date(data.expiresAt).toLocaleDateString('pt-BR')}</dd></div>`;
  if (data.requiresOtp) $('#otpField').classList.remove('hidden');
  $('#signBox').classList.remove('hidden');
  sizeCanvas();
}

/* ---------- envio ---------- */
async function send(refused, reason) {
  if (!refused && !hasDrawn) {
    showError('#padErro', 'Desenhe ou digite sua assinatura antes de concluir.');
    pad.focus?.();
    return;
  }
  const otpField = $('#otp');
  if (!$('#otpField').classList.contains('hidden') && otpField.value.trim().length !== 6) {
    showError('#otpErro', 'Informe os 6 dígitos do código enviado a você.');
    otpField.focus();
    return;
  }

  let coords = null;
  try { coords = (await new Promise((res, rej) => navigator.geolocation.getCurrentPosition(res, rej, { timeout: 6000 }))).coords; } catch {}

  const body = {
    token: invite,
    otp: otpField.value || null,
    imageDataUrl: refused ? null : pad.toDataURL('image/png'),
    latitude: coords?.latitude ?? null,
    longitude: coords?.longitude ?? null,
    refused,
    refusalReason: refused ? reason : null
  };

  const botao = refused ? $('#refuseForm button[type=submit]') : $('#signBtn');
  botao.disabled = true;
  botao.dataset.rotulo = botao.textContent;
  botao.textContent = 'Registrando…';

  try {
    const r = await fetch('/api/publico/assinaturas', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
    const data = await r.json();
    if (!r.ok) {
      showError(refused ? '#refuseReasonErro' : '#otpErro', data.message || 'Não foi possível registrar a assinatura.');
      return;
    }
    document.querySelector('.sign-page').innerHTML = `
      <div class="sign-done">
        <svg class="sign-done-ico" aria-hidden="true" focusable="false"><use href="#i-${refused ? 'contestacao' : 'selo'}"/></svg>
        <h1>${refused ? 'Recusa registrada' : 'Assinatura concluída'}</h1>
        <p>${data.message}</p>
        <dl class="detail-box">
          <div><dt>Registro</dt><dd>${new Date(data.signedAt).toLocaleString('pt-BR')}</dd></div>
          <div><dt>Hash</dt><dd class="mono break">${data.hash}</dd></div>
        </dl>
        <p class="legal">Guarde este comprovante. A validade do documento pode ser conferida a qualquer momento pelo número do laudo.</p>
      </div>`;
    document.querySelector('.sign-page h1').focus?.();
  } catch {
    showError(refused ? '#refuseReasonErro' : '#otpErro', 'Sem conexão. Tente novamente em instantes.');
  } finally {
    if (botao.isConnected) { botao.disabled = false; botao.textContent = botao.dataset.rotulo; }
  }
}

$('#signBtn').addEventListener('click', () => send(false));

/* ---------- recusa (diálogo acessível) ---------- */
const refuseModal = $('#refuseModal');
function openRefuse() {
  opener = document.activeElement;
  refuseModal.hidden = false;
  refuseModal.classList.add('show');
  $('#refuseReason').focus();
  document.addEventListener('keydown', onModalKey);
}
function closeRefuse() {
  refuseModal.classList.remove('show');
  refuseModal.hidden = true;
  document.removeEventListener('keydown', onModalKey);
  opener?.focus();
}
function onModalKey(e) {
  if (e.key === 'Escape') return closeRefuse();
  if (e.key !== 'Tab') return;
  const focusables = refuseModal.querySelectorAll('button, input, textarea, select, a[href]');
  const first = focusables[0], last = focusables[focusables.length - 1];
  if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
  else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
}
$('#refuseBtn').addEventListener('click', openRefuse);
refuseModal.addEventListener('click', e => { if (e.target === refuseModal) closeRefuse(); });
$('[data-close="refuseModal"]').addEventListener('click', closeRefuse);
$('#refuseForm').addEventListener('submit', e => {
  e.preventDefault();
  const reason = $('#refuseReason').value.trim();
  if (!reason) { showError('#refuseReasonErro', 'Descreva o motivo para registrar a recusa.'); $('#refuseReason').focus(); return; }
  closeRefuse();
  send(true, reason);
});

load();
