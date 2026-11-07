(() => {
const $ = id => document.getElementById('mp-' + id);
const escapeHtml = s => String(s ?? '').replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
let room = null, busy = false, stopped = false, offset = 0, gameKey = '', selected = null;
let waitingForHost = false;
let code = new URLSearchParams(location.search).get('code')?.trim().toUpperCase() || '';
function message(text) { $('message').textContent = text; }
function login() {
  if (/^[A-F0-9]{10}$/.test(code)) sessionStorage.setItem('husay-lobby', code);
  location.replace('/login.html');
}
async function api(path, body) {
  const response = await fetch('/api' + path, body === undefined ? { cache: 'no-store' } : { method: 'POST', headers: {'Content-Type':'application/json'}, body: JSON.stringify(body) });
  const result = await response.json().catch(() => ({}));
  if (response.status === 401) login();
  if (result.code === 'password_change_required') { if (code) sessionStorage.setItem('husay-lobby', code); location.replace('/login.html?changePassword=1'); }
  if (!response.ok) throw new Error(result.error || (response.status === 403 ? 'Only the host can do that.' : 'Request failed. Please retry.'));
  return result;
}
function settings() {
  const level = $('level').value;
  $('category').replaceChildren(...['All 4 Subjects (Final Mock Exam)', 'Numerical Ability','Verbal Ability',level === 'Professional' ? 'Analytical Ability' : 'Clerical Ability','General Information'].map(c => { const o = document.createElement('option'); o.textContent = c; return o; }));
}
function rules() {
  const speed = $('mode').value === 'speed';
  $('timer-label').textContent = speed ? 'Minutes per question' : 'Minutes for the entire test';
  $('rules').textContent = speed ? 'First correct answer received by the server wins the round. A wrong answer eliminates you for that round. Five seconds of review follow each round. Most rounds won wins the match; equal scores draw.' : 'Answer at your own pace before the shared timer ends. Most correct answers wins; equal scores draw. Answers are revealed only when the match ends.';
}
$('level').onchange = settings; $('mode').onchange = rules; settings(); rules();
async function action(work) {
  if (busy) return; busy = true; message('');
  try { await work(); } catch (e) { message(e.message); } finally { busy = false; }
}
function openRoom(data) {
  room = data; code = data.code; stopped = false; gameKey = ''; waitingForHost = false;
  history.replaceState(null, '', '/?view=multiplayer&code=' + code);
  $('setup').hidden = true; $('room').hidden = false;
  $('code').textContent = code; $('share-link').value = location.origin + '/?view=multiplayer&code=' + code;
  render(data);
}
$('create-form').onsubmit = e => { e.preventDefault(); action(async () => {
  const created = await api('/lobbies', { category: $('category').value, level: $('level').value, mode: $('mode').value, count: Number($('count').value), minutes: Number($('minutes').value) });
  code = created.code; openRoom(await api('/lobbies/' + code));
}); };
$('join-form').onsubmit = e => { e.preventDefault(); action(async () => { code = $('join-code').value.trim().toUpperCase(); openRoom(await api('/lobbies/' + code + '/join', {})); }); };
$('copy').onclick = () => action(async () => { try { await navigator.clipboard.writeText($('share-link').value); message('Lobby link copied.'); } catch { $('share-link').select(); message('Select and copy the lobby link above.'); } });
for (const id of ['ready','start','cancel','leave','replay']) $(id).onclick = () => action(async () => {
  if (id === 'replay') {
    if (room.host !== room.me) { waitingForHost = true; gameKey = ''; render(room); return; }
    render(await api('/lobbies/' + code + '/replay', { game: room.game }));
    return;
  }
  if (id === 'cancel' && !confirm('Close this lobby for everyone? Players will be returned to the main screen.')) return;
  if (id === 'leave' && room.status === 'finished' && room.host === room.me && !confirm('Leave and close the lobby? All players will be disconnected.')) return;
  const data = await api('/lobbies/' + code + '/' + id, id === 'ready' ? { ready: !room.ready } : {});
  if (id === 'leave' || (id === 'cancel' && data.status === 'cancelled')) {
    room = null; code = ''; gameKey = ''; stopped = true;
    history.replaceState(null,'','/?view=multiplayer'); $('room').hidden = true; $('setup').hidden = false;
  } else render(data);
});
function render(s) {
  if (room?.code === s.code && Date.parse(s.serverTime) < Date.parse(room.serverTime)) return;
  room = s; offset = Date.parse(s.serverTime) - Date.now();
  if (s.status !== 'finished') waitingForHost = false;
  const waiting = s.status === 'waiting', host = s.host === s.me;
  $('settings').textContent = `Game ${s.game} · ${s.level} · ${s.category} · ${s.count} questions · ${s.mode === 'speed' ? 'Speed rounds' : 'Timed test'} · ${s.minutes} min ${s.mode === 'speed' ? 'per question' : 'total'}`;
  $('breakdown').textContent = s.breakdown.map(x => `${x.category}: ${x.count}`).join(' · ');
  $('ready').hidden = !waiting; $('ready').textContent = s.ready ? 'Not ready' : 'I am ready';
  $('start').hidden = !waiting || !host; $('start').disabled = s.players.length < 2 || s.players.some(p => !p.ready);
  $('leave').hidden = s.status === 'playing' || s.status === 'cancelled';
  $('leave').textContent = s.status === 'finished' ? (host ? 'Close lobby & leave' : 'Leave lobby') : 'Leave lobby';
  $('cancel').hidden = !host || s.status === 'playing' || s.status === 'cancelled' || s.status === 'finished';
  $('replay').hidden = s.status !== 'finished' || waitingForHost;
  $('replay').textContent = host ? '🎮 New game · back to lobby' : '🔄 Back to lobby';
  $('players').innerHTML = s.players.map(p => `<p><strong>${escapeHtml(p.name)}</strong>${p.id === s.host ? ' · Host' : ''}${p.id === s.me ? ' · You' : ''} — ${waiting ? (p.ready ? 'Ready' : 'Not ready') : `${p.answered} answered${p.score === null ? '' : ` · ${p.score} points`}`}</p>`).join('');
  const key = [s.game, s.status, s.index, s.answered, s.nextRoundAt, waitingForHost].join('|');
  if (key === gameKey) return; gameKey = key; selected = null;
  if (waiting) { $('game').textContent = 'Share the link or code. The host can start when at least two players are ready.'; return; }
  if (s.status === 'cancelled') { $('game').innerHTML = '<h2 class="text-xl font-bold">Lobby closed</h2><a class="secondary mt-4" href="/?view=multiplayer">Create or join another lobby</a>'; return; }
  if (s.status === 'finished') {
    if (waitingForHost) { $('game').textContent = 'Waiting for the host to open the next game. Stay here — the same lobby link and players will be kept. You will need to mark yourself ready again.'; return; }
    const names = s.players.filter(p => s.winners.includes(p.id)).map(p => p.name).join(', ');
    $('game').innerHTML = `<h2 class="text-2xl font-bold">${s.winners.length === 0 ? 'Match finished — no winning score' : `${s.winners.length > 1 ? 'Draw' : 'Winner'}: ${escapeHtml(names)}`}</h2><p class="mt-4">${host ? '🎮 Use <strong>New game · back to lobby</strong> to restart with the same players and a reshuffled question set. Use <strong>Close lobby &amp; leave</strong> when done.' : '🔄 Choose <strong>Back to lobby</strong> to wait for the host to start another game, or <strong>Leave lobby</strong> to exit.'}</p><h3 class="font-bold mt-6">Your answer review</h3>` + s.results.map(r => `<details class="mt-4"><summary>${r.index + 1}. ${escapeHtml(r.question.prompt)}</summary><p>Your answer: ${r.choice === null ? 'Not answered' : escapeHtml(r.question.options[r.choice])}</p><p>Correct: ${escapeHtml(r.question.options[r.question.answer])}</p><p>${escapeHtml(r.question.explanation)}</p>${r.question.steps.map(step => `<p>${escapeHtml(step)}</p>`).join('')}</details>`).join(''); return;
  }
  if (s.review) {
    const q = s.review.question.question, winner = s.players.find(p => p.id === s.review.result.winner);
    $('game').innerHTML = `<p class="eyebrow">ROUND ${s.index + 1} COMPLETE</p><h2 class="text-xl font-bold mt-3">${winner ? escapeHtml(winner.name) + ' wins this round' : escapeHtml(s.review.result.reason)}</h2><p class="mt-4">${escapeHtml(q.prompt)}</p><p class="font-bold mt-3">Correct answer: ${escapeHtml(q.options[q.answer])}</p><p class="mt-3">${escapeHtml(q.explanation)}</p>${q.steps.map(step => `<p class="mt-2">${escapeHtml(step)}</p>`).join('')}<p class="mt-4">The next round starts automatically.</p>`; return;
  }
  if (!s.question) { $('game').textContent = 'You have finished. Waiting for other players or the timer to end.'; return; }
  $('game').innerHTML = `<p class="eyebrow">${escapeHtml(s.question.category)} · QUESTION ${s.index + 1}/${s.count}</p><h2 class="text-xl font-bold mt-4">${escapeHtml(s.question.prompt)}</h2><div class="space-y-3 mt-6">${s.question.options.map((o,i) => `<button type="button" class="option" data-choice="${i}" ${s.answered ? 'disabled' : ''}>${'ABCD'[i]}. ${escapeHtml(o)}</button>`).join('')}</div><button id="mp-submit-answer" class="primary mt-4" disabled>Submit answer</button><p class="mt-4">${s.answered ? 'Incorrect. You are out for this round; wait for the next one.' : 'You can submit only once. The server decides whether your answer arrived before time expired.'}</p>`;
  $('game').querySelectorAll('[data-choice]').forEach(button => button.onclick = () => { selected = Number(button.dataset.choice); $('game').querySelectorAll('[data-choice]').forEach(b => b.classList.toggle('selected', b === button)); $('submit-answer').disabled = false; });
  $('submit-answer').onclick = () => action(async () => {
    if (selected === null) return;
    $('submit-answer').disabled = true;
    try { render(await api('/lobbies/' + code + '/answer', { index: s.index, choice: selected, game: s.game })); }
    finally { if ($('submit-answer')) $('submit-answer').disabled = selected === null || room.answered; }
  });
}
setInterval(() => {
  if (!room) return;
  const end = room.nextRoundAt || room.deadline;
  const seconds = end ? Math.max(0, Math.ceil((Date.parse(end) - Date.now() - offset) / 1000)) : 0;
  $('timer').textContent = room.status !== 'playing' ? room.status.toUpperCase() : `${room.nextRoundAt ? 'Next round in ' : 'Time left '}${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2,'0')}`;
}, 250);
async function poll() {
  try { if (room && !busy && !stopped && !document.getElementById('multiplayer-view').classList.contains('hidden')) { render(await api('/lobbies/' + code)); if (room.status === 'cancelled') stopped = true; } }
  catch (e) { message('Connection interrupted. Reconnecting… ' + e.message); }
  finally { setTimeout(poll, 1500); }
}
let initialized = false;
window.HusayMultiplayer = {
  enter: async () => {
    if (initialized) {
      history.replaceState(null, '', '/?view=multiplayer' + (code ? '&code=' + code : ''));
      return;
    }
    initialized = true;
    $('setup').hidden = false;
    history.replaceState(null, '', '/?view=multiplayer' + (code ? '&code=' + encodeURIComponent(code) : ''));
    if (code) {
      $('join-code').value = code;
      await action(async () => openRoom(await api('/lobbies/' + encodeURIComponent(code) + '/join', {})));
    }
    poll();
  }
};

})();
