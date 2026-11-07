// ── Utilities ─────────────────────────────────────────────────────────────────
const $ = id => document.getElementById(id);

const categories = [
  'All 4 Subjects (Final Mock Exam)',
  'Numerical Ability', 'Verbal Ability',
  'Analytical Ability', 'Clerical Ability', 'General Information'
];
const hints = [
  'Comprehensive 180-item exam covering all 4 subjects',
  'Numbers, patterns & word problems',
  'Words, grammar & comprehension',
  'Logic, analogies & conclusions',
  'Filing, spelling & attention to detail',
  'Public service, ethics & citizenship'
];

const escapeHtml = s => String(s).replace(/[&<>"']/g, c =>
  ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])
);

// ── App state ─────────────────────────────────────────────────────────────────
let category = categories[0];
let current, index = 0, choice = null, results = [], busy = false;
let aiConfig = { provider: 'AI provider', configured: false };

// ── Notice banner ─────────────────────────────────────────────────────────────
function notice(message) {
  $('notice').textContent = message;
  $('notice').classList.toggle('hidden', !message);
}

// ── API helper ────────────────────────────────────────────────────────────────
async function api(path, data) {
  let response;
  try {
    response = await fetch('/api' + path, data === undefined
      ? {}
      : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  } catch {
    throw new Error(
      'Cannot reach the reviewer server. ' +
      'Start dotnet watch --project CseReviewer run --launch-profile http from the solution folder, ' +
      'wait for "Now listening", then refresh this page.'
    );
  }

  const text = await response.text();
  let body;
  try { body = text ? JSON.parse(text) : null; } catch { body = null; }

  if (response.status === 401) location.replace('/login.html');
  if (body?.code === 'password_change_required') location.replace('/login.html?changePassword=1');
  if (!response.ok) {
    throw new Error(
      body?.error ||
      (response.status === 401 ? 'Your study session expired. Refresh this page.' : 'The request failed. Please try again.')
    );
  }
  return body;
}

// ── Dark mode (follows FGCI HRDIS pattern — class on <html>, persisted in localStorage) ──
function applyDarkMode(dark) {
  document.documentElement.classList.toggle('dark', dark);
  $('theme-toggle').textContent = dark ? '☀️' : '🌙';
  $('theme-toggle').setAttribute('aria-label', dark ? 'Switch to light mode' : 'Switch to dark mode');
  $('theme-toggle').setAttribute('title', dark ? 'Switch to light mode' : 'Switch to dark mode');
  try { localStorage.setItem('dark_mode', dark ? 'true' : 'false'); } catch (_) {}
}

function initDarkMode() {
  let dark = false;
  try { dark = localStorage.getItem('dark_mode') === 'true'; } catch (_) {}
  applyDarkMode(dark);
}

$('theme-toggle').onclick = () => {
  applyDarkMode(!document.documentElement.classList.contains('dark'));
};

// ── View switching ────────────────────────────────────────────────────────────
function view(name) {
  ['create', 'library', 'quiz', 'multiplayer', 'admin'].forEach(v =>
    $(v + '-view').classList.toggle('hidden', v !== name)
  );
  $('nav-create').classList.toggle('active', name === 'create' || name === 'quiz');
  $('nav-multiplayer').classList.toggle('active', name === 'multiplayer');
  if (name === 'multiplayer') window.HusayMultiplayer.enter();
  else if (name === 'admin') { history.replaceState(null, '', '/?view=admin'); window.HusayAdmin.enter(); }
  else if (new URLSearchParams(location.search).has('view')) history.replaceState(null, '', '/');
  $('admin-link').classList.toggle('active', name === 'admin');
  $('nav-library').classList.toggle('active', name === 'library');
  notice('');
}

// ── Category grid ─────────────────────────────────────────────────────────────
function renderCategories() {
  const level = $('level').value;
  const allowed = categories.filter(c =>
    !(level === 'Professional'    && c === 'Clerical Ability') &&
    !(level === 'Subprofessional' && c === 'Analytical Ability')
  );
  if (!allowed.includes(category)) category = allowed[0];

  $('categories').innerHTML = allowed.map(c => `
    <button type="button"
            class="subject ${c === category ? 'selected' : ''}"
            aria-pressed="${c === category}"
            data-category="${c}">
      <strong>${escapeHtml(c)}</strong>
      <span class="block text-xs text-slate-500 mt-2">${escapeHtml(hints[categories.indexOf(c)])}</span>
    </button>`
  ).join('');

  $('categories').querySelectorAll('button').forEach(b =>
    b.onclick = () => {
      category = b.dataset.category;
      if (category === 'All 4 Subjects (Final Mock Exam)') {
        $('count').value = '180';
      }
      renderCategories();
      modeChanged();
    }
  );
}

// ── Mode toggle ────────────────────────────────────────────────────────────────
let bankFilterKey = '', bankAvailable = null, bankRequestVersion = 0;
function selectedCount() { return Number(($('mode').value === 'bank' ? $('bank-count') : $('count')).value); }
async function updateBankCount() {
  const bank = $('mode').value === 'bank';
  $('bank-count').hidden = !bank; $('bank-count').disabled = !bank;
  $('count').hidden = bank; $('count').disabled = bank;
  $('bank-available').hidden = !bank;
  if (!bank) { bankRequestVersion++; bankFilterKey = ''; return; }
  const key = JSON.stringify([category, $('level').value, $('difficulty').value]);
  if (key === bankFilterKey) { $('generate').disabled = busy || !bankAvailable; return; }
  bankFilterKey = key; bankAvailable = null;
  const version = ++bankRequestVersion;
  $('generate').disabled = true; $('bank-count').disabled = true;
  $('bank-available').textContent = 'Checking available questions…';
  try {
    const result = await api('/bank/check', { category, level: $('level').value, difficulty: $('difficulty').value, count: 1 });
    if (version !== bankRequestVersion) return;
    bankAvailable = result.available;
    $('bank-count').max = String(bankAvailable);
    $('bank-count').value = String(Math.min(Math.max(1, Number($('bank-count').value) || 20), bankAvailable));
    $('bank-count').disabled = !bankAvailable;
    $('generate').disabled = busy || !bankAvailable;
    $('bank-available').textContent = bankAvailable ? `${bankAvailable.toLocaleString()} matching questions available. Enter 1–${bankAvailable.toLocaleString()}.` : 'No matching questions. Try Mixed or ask an administrator to import more.';
  } catch (e) {
    if (version !== bankRequestVersion) return;
    bankFilterKey = ''; $('bank-available').textContent = 'Unable to check availability. ' + e.message;
  }
}
function modeChanged() {
  const sample = $('mode').value === 'sample';
  const bank = $('mode').value === 'bank';
  const ai = $('mode').value === 'ai';

  $('focus').disabled = sample || bank;
  $('difficulty').disabled = sample;
  $('count').disabled = false;

  const opt180 = $('count').querySelector('option[value="180"]');
  if (opt180) opt180.disabled = ai;
  if (ai && Number($('count').value) > 30) $('count').value = '30';

  const count = selectedCount();
  const isLarge = count > 10;
  const isFinalExam = count >= 100 || category === 'All 4 Subjects (Final Mock Exam)';

  if (bank) {
    $('mode-note').textContent = isFinalExam
      ? `Database Final Exam (${count} questions): Randomly draws from your imported database across ${category === 'All 4 Subjects (Final Mock Exam)' ? 'all 4 subjects' : category}. You will be prompted if available questions are fewer than requested.`
      : 'Randomly selects the shared database questions by subject, level and difficulty. Save your progress when leaving an unfinished set. No AI call. Ask an administrator to add more questions if there are not enough matching items.';
  } else if (sample) {
    $('mode-note').textContent = isFinalExam
      ? `Built-in practice: ${count} questions (Final Exam). Comprehensive offline reviewer with randomized question and choice positions.`
      : `Built-in practice: ${count} questions from the bundled reviewer. This is not an official CSC question bank. Works offline without an API key.`;
  } else {
    $('mode-note').textContent = 'AI custom questions are not available yet. Please choose Custom questions · Database to start practicing.';
  }
  $('generate').disabled = ai || busy;
  $('focus').disabled = true;
  updateBankCount();

}

// ── Event wiring ──────────────────────────────────────────────────────────────
$('level').onchange = () => { renderCategories(); modeChanged(); };
$('difficulty').onchange = modeChanged;
$('bank-count').oninput = () => { $('generate').disabled = busy || !bankAvailable || !$('bank-count').checkValidity(); };
document.addEventListener('bank-updated', () => { bankFilterKey = ''; modeChanged(); });
$('mode').onchange  = modeChanged;
$('count').onchange = modeChanged;

$('nav-multiplayer').onclick = () => leaveQuiz('multiplayer');
$('admin-link').onclick = e => { e.preventDefault(); leaveQuiz('admin'); };
$('nav-create').onclick = $('back').onclick = () => leaveQuiz('create');
$('nav-library').onclick = () => leaveQuiz('library');

// ── Insufficient questions prompt dialog ──────────────────────────────────────
let pendingInsufficientAction = null;

function promptInsufficientQuestions(check) {
  const isFinalMock = category === 'All 4 Subjects (Final Mock Exam)';
  const label = isFinalMock ? 'the 4 exam subjects combined' : category;

  $('insufficient-title').textContent = 'Some questions are not enough';
  $('insufficient-message').textContent =
    `You requested ${check.requested} questions for ${label}, but only ${check.available} matching ${check.available === 1 ? 'question is' : 'questions are'} available in the database.`;

  if (check.breakdown && check.breakdown.length > 1) {
    $('insufficient-breakdown').classList.remove('hidden');
    $('insufficient-breakdown').innerHTML = '<strong>Subject breakdown:</strong><br>' +
      check.breakdown.map(b => `${escapeHtml(b.category)}: ${b.count} available`).join(' · ');
  } else {
    $('insufficient-breakdown').classList.add('hidden');
  }

  $('insufficient-continue').textContent = `Yes, continue with ${check.available} questions →`;

  pendingInsufficientAction = async () => {
    $('insufficient-dialog').close();
    await createSet({ count: check.available, allowPartial: true });
  };

  $('insufficient-dialog').showModal();
}

$('insufficient-continue').onclick = () => {
  if (pendingInsufficientAction) {
    const action = pendingInsufficientAction;
    pendingInsufficientAction = null;
    action();
  }
};

$('insufficient-cancel').onclick = () => {
  pendingInsufficientAction = null;
  $('insufficient-dialog').close();
};

// ── Create practice set ───────────────────────────────────────────────────────
async function createSet({ count, allowPartial }) {
  if (busy) return;
  busy = true;
  notice('');

  const mode = $('mode').value;
  if (mode === 'ai') { notice('AI custom questions are not available yet. Please choose Custom questions · Database.'); return; }
  const isSample = mode === 'sample';
  const isBank = mode === 'bank';

  $('generate').disabled = true;
  $('generate').textContent = count >= 100
    ? `Preparing ${count}-question Final Exam…`
    : (isSample || isBank ? `Preparing ${count} questions…` : (count > 10 ? `Generating ${count} questions via AI…` : 'Creating your practice set…'));

  try {
    const created = isBank
      ? await api('/bank/sets', {
          category,
          level: $('level').value,
          difficulty: isSample ? 'Easy' : $('difficulty').value,
          count,
          allowPartial: Boolean(allowPartial)
        })
      : await api('/sets', {
          category,
          level: $('level').value,
          mode,
          difficulty: isSample ? 'Easy' : $('difficulty').value,
          count,
          focus: $('focus').value
        });

    if (isBank) await library();
    await openSet(created.id);
  } catch (e) {
    notice(e.message);
  } finally {
    busy = false;
    $('generate').disabled = false;
    $('generate').textContent = 'Create practice set →';
  }
}

// ── Generate form ─────────────────────────────────────────────────────────────
$('generate-form').onsubmit = async e => {
  e.preventDefault();
  if (busy) return;
  notice('');

  const count = selectedCount();
  const mode = $('mode').value;

  if (mode === 'bank') {
    busy = true;
    $('generate').disabled = true;
    $('generate').textContent = 'Checking question database…';

    try {
      const check = await api('/bank/check', {
        category,
        level: $('level').value,
        difficulty: $('difficulty').value,
        count
      });

      if (!check.isEnough) {
        busy = false;
        $('generate').disabled = false;
        $('generate').textContent = 'Create practice set →';

        if (check.available === 0) {
          notice(`No matching questions found in your database for ${category} (${$('level').value}). Ask an administrator to add matching questions.`);
          return;
        }

        promptInsufficientQuestions(check);
        return;
      }

      busy = false;
      await createSet({ count, allowPartial: false });
    } catch (err) {
      notice(err.message);
      busy = false;
      $('generate').disabled = false;
      $('generate').textContent = 'Create practice set →';
    }
    return;
  }

  await createSet({ count, allowPartial: false });
};

// ── Library view ──────────────────────────────────────────────────────────────
async function library() {
  const sets = await api('/sets');
  $('saved-count').textContent = sets.length || '';
  $('library').innerHTML = sets.length
    ? sets.map(s => `
        <article class="panel p-6">
          <p class="eyebrow">${escapeHtml(s.source)} · ${s.count} QUESTIONS</p>
          <h2 class="text-xl font-bold mt-3">${escapeHtml(s.title)}</h2>
          <p class="text-sm text-slate-500 mt-2">${escapeHtml(s.level)} · ${escapeHtml(s.difficulty)}</p>
          <button class="secondary mt-5" data-set="${s.id}">Resume practice →</button>
        </article>`
      ).join('')
    : `<div class="panel p-8 col-span-full">
         <h2 class="text-xl font-bold">A fresh start for your library.</h2>
         <p class="text-slate-500 mt-3">Your unfinished sets appear here when you choose Save progress before leaving a quiz.</p>
       </div>`;

  $('library').querySelectorAll('[data-set]').forEach(b =>
    b.onclick = async () => { try { await openSet(b.dataset.set); } catch (e) { notice(e.message); } }
  );
}

// ── Open / quiz view ──────────────────────────────────────────────────────────
async function openSet(id) {
  current = await api('/sets/' + id);
  results = current.progress || []; index = results.length; choice = null; busy = false;
  view('quiz');
  $('quiz-title').textContent = current.title;
  $('quiz-meta').textContent  = `${current.level} · ${current.source} · ${current.difficulty}`;
  renderQuestion();
}

function renderQuestion() {
  choice = null;
  busy = false;
  $('progress-label').textContent = `Question ${index + 1} of ${current.questions.length}`;
  $('score').textContent = `${results.filter(r => r.correct).length} correct · ${results.length} answered`;
  $('progress').style.width = `${results.length / current.questions.length * 100}%`;

  const q = current.questions[index];
  $('question-card').innerHTML = `
    <p class="eyebrow">${escapeHtml(current.category)}</p>
    <h2 class="text-xl md:text-2xl font-semibold leading-relaxed mt-4">${escapeHtml(q.prompt)}</h2>
    <div id="options" class="space-y-3 mt-7">
      ${q.options.map((o, i) => `
        <button class="option" data-choice="${i}" aria-pressed="false">
          <span class="text-sm rounded-lg bg-slate-100 px-3 py-2 font-bold">${'ABCD'[i]}</span>
          <span>${escapeHtml(o)}</span>
        </button>`
      ).join('')}
    </div>
    <button id="check" class="primary mt-7" disabled>Check answer →</button>
    <div id="feedback" aria-live="polite"></div>`;

  $('options').querySelectorAll('button').forEach(b => b.onclick = () => {
    choice = Number(b.dataset.choice);
    $('options').querySelectorAll('button').forEach(o => {
      o.classList.toggle('selected', o === b);
      o.setAttribute('aria-pressed', String(o === b));
    });
    $('check').disabled = false;
  });

  $('check').onclick = submitAnswer;
}

async function submitAnswer() {
  if (choice === null || busy) return;
  busy = true;
  const selected = choice;
  $('check').disabled = true;
  $('options').querySelectorAll('button').forEach(b => b.disabled = true);

  try {
    const result = await api(`/sets/${current.id}/answer`, { index, choice: selected });
    results.push({ ...result, choice: selected });
    if (results.length === current.questions.length) {
      await api(`/sets/${current.id}/discard`, {});
      current.saved = false;
      await library();
    }

    $('options').querySelectorAll('button').forEach((b, i) => {
      b.classList.remove('selected');
      b.classList.toggle('correct', i === result.answer);
      b.classList.toggle('wrong',   i === selected && !result.correct);
    });

    $('check').classList.add('hidden');
    $('feedback').innerHTML = `
      <div class="mt-7 bg-slate-50 rounded-xl p-6">
        <h3 class="font-bold text-lg">${result.correct ? '✓ Correct. Nicely done!' : 'Keep going — this is how you learn.'}</h3>
        <p class="mt-3 font-semibold">Correct answer: ${'ABCD'[result.answer]}. ${escapeHtml(current.questions[index].options[result.answer])}</p>
        <p class="mt-3 text-slate-600 leading-7">${escapeHtml(result.explanation)}</p>
        ${result.steps.length
          ? `<h4 class="font-bold mt-5">Step-by-step solution</h4>
             <ol class="list-decimal pl-5 space-y-2 mt-3 text-slate-600">
               ${result.steps.map(s => `<li>${escapeHtml(s)}</li>`).join('')}
             </ol>`
          : ''}
      </div>
      <button id="next" class="primary mt-6">
        ${index + 1 === current.questions.length ? 'See my results' : 'Next question →'}
      </button>`;

    $('score').textContent = `${results.filter(r => r.correct).length} correct · ${results.length} answered`;
    $('progress').style.width = `${results.length / current.questions.length * 100}%`;
    $('next').onclick = () => {
      if (++index < current.questions.length) renderQuestion();
      else finish();
    };
  } catch (e) {
    notice(e.message);
    $('check').disabled = false;
    $('options').querySelectorAll('button').forEach(b => b.disabled = false);
  } finally {
    busy = false;
  }
}

function finish() {
  const correct = results.filter(r => r.correct).length;
  $('progress-label').textContent = 'Practice complete';
  $('question-card').innerHTML = `
    <p class="eyebrow">ONE STEP CLOSER</p>
    <h2 class="text-3xl font-bold mt-4">You showed up. That matters.</h2>
    <p class="text-5xl font-bold text-teal-800 mt-8">
      ${correct}<span class="text-2xl text-slate-400"> / ${results.length}</span>
    </p>
    <p class="text-slate-500 mt-4">
      ${Math.round(correct / results.length * 100)}% on this practice set. Review your answers below, then start a newly selected set.
    </p>
    <div class="mt-7 space-y-3">
      ${current.questions.map((q, i) => `
        <details class="border border-slate-200 rounded-xl p-4">
          <summary class="cursor-pointer font-semibold">
            ${results[i].correct ? '✓' : '○'} ${escapeHtml(q.prompt)}
          </summary>
          <p class="mt-3">Correct answer: ${escapeHtml(q.options[results[i].answer])}</p>
          <p class="text-slate-500 mt-2">${escapeHtml(results[i].explanation)}</p>
          ${results[i].steps.map(s => `<p class="text-sm mt-2">${escapeHtml(s)}</p>`).join('')}
        </details>`
      ).join('')}
    </div>
    <button id="retry" class="primary mt-7">Start next set →</button>`;

  $('retry').onclick = async () => {
    if (busy) return;
    busy = true; $('retry').disabled = true; notice('');
    try {
      const next = await api('/sets/' + current.id + '/next', {});
      await openSet(next.id);
    } catch (e) { notice(e.message); if ($('retry')) $('retry').disabled = false; }
    finally { busy = false; }
  };
}

// Leaving an unfinished quiz requires an explicit save/discard decision.
let destination = null;
async function leaveQuiz(name) {
  if (busy) { notice('Please wait for the current request to finish.'); return; }
  if (current && !$('quiz-view').classList.contains('hidden') && results.length < current.questions.length) {
    destination = name;
    $('leave-error').textContent = '';
    $('leave-dialog').showModal();
    return;
  }
  view(name);
  if (name === 'library') try { await library(); } catch (e) { notice(e.message); }
}
$('continue-quiz').onclick = () => $('leave-dialog').close();
$('leave-dialog').addEventListener('cancel', e => { if (busy) e.preventDefault(); });
async function confirmLeave(save) {
  if (busy) return;
  busy = true;
  const buttons = $('leave-dialog').querySelectorAll('button');
  buttons.forEach(b => b.disabled = true);
  try {
    await api('/sets/' + current.id + (save ? '/progress' : '/discard'), save ? { choices: results.map(r => r.choice) } : {});
    await library();
    $('leave-dialog').close();
    current = null; results = [];
    view(destination);
    notice(save ? 'Progress saved. Resume it from My saved sets.' : 'Practice progress discarded. Your question bank is unchanged.');
  } catch (e) { $('leave-error').textContent = e.message; }
  finally { busy = false; buttons.forEach(b => b.disabled = false); }
}
$('save-progress').onclick = () => confirmLeave(true);
$('discard-progress').onclick = () => confirmLeave(false);
window.addEventListener('beforeunload', e => {
  if (current && !$('quiz-view').classList.contains('hidden') && results.length < current.questions.length) {
    e.preventDefault(); e.returnValue = '';
  }
});

// ── Bootstrap ─────────────────────────────────────────────────────────────────
initDarkMode();
renderCategories();
modeChanged();
$('generate').disabled = true;

api('/session')
  .then(async session => {
    $('study-app').hidden = false;
    $('account-name').textContent = session.username;
    $('admin-import').hidden = !session.isAdmin;
    $('admin-link').hidden = !session.isAdmin;
    aiConfig = await api('/config');
    modeChanged();
    await library();
  })
  .then(() => { modeChanged(); document.dispatchEvent(new Event('study-ready')); const requestedView = new URLSearchParams(location.search).get('view'); if (['multiplayer', 'admin'].includes(requestedView)) view(requestedView); })
  .catch(e => notice(e.message));

$('logout').onclick = async () => {
  if (busy) return;
  if (current && results.length < current.questions.length && !$('quiz-view').classList.contains('hidden')) {
    notice('Use Back to create first to save or discard your progress, then sign out.'); return;
  }
  try { await api('/auth/logout', {}); current = null; location.replace('/login.html'); }
  catch (e) { notice(e.message); }
};
