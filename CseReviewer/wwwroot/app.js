// ── Utilities ─────────────────────────────────────────────────────────────────
const $ = id => document.getElementById(id);

const categories = [
  'Numerical Ability', 'Verbal Ability',
  'Analytical Ability', 'Clerical Ability', 'General Information'
];
const hints = [
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
  ['create', 'library', 'quiz'].forEach(v =>
    $(v + '-view').classList.toggle('hidden', v !== name)
  );
  $('nav-create').classList.toggle('active', name !== 'library');
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
      <strong>${c}</strong>
      <span class="block text-xs text-slate-500 mt-2">${hints[categories.indexOf(c)]}</span>
    </button>`
  ).join('');

  $('categories').querySelectorAll('button').forEach(b =>
    b.onclick = () => { category = b.dataset.category; renderCategories(); }
  );
}

// ── Mode toggle ────────────────────────────────────────────────────────────────
function modeChanged() {
  const sample = $('mode').value === 'sample';
  const bank = $('mode').value === 'bank';
  // Focus is only for AI prompting; count is always available (5, 10, 20, 30)
  $('focus').disabled = sample || bank;
  $('difficulty').disabled = sample;
  $('count').disabled = false;

  const count = Number($('count').value);
  const isLarge = count > 10;

  if (bank) {
    $('mode-note').textContent = 'Randomly selects your imported SQL Server questions by subject, level and difficulty. Save your progress when leaving an unfinished set. No AI call. Import more questions if there are not enough matching items.';
  } else if (sample) {
    $('mode-note').textContent =
      `Built-in practice: ${count} questions from the bundled reviewer. This is not an official CSC question bank. Works offline without an API key.`;
  } else if (!aiConfig.configured) {
    $('mode-note').textContent =
      `${aiConfig.provider} needs a server-side API key. Follow the README setup instructions, then restart the app. Reviewer Bank mode is available right now.`;
  } else {
    const quota = aiConfig.provider === 'Groq' ? ' Free-tier limits apply.' : '';
    const chunked = isLarge
      ? ` Large sets (${count} questions) are generated in chunks of 10 and saved progressively.`
      : '';
    $('mode-note').textContent =
      `${aiConfig.provider} generates your questions.${quota}${chunked} Keep this page open while the set is created.`;
  }
}

// ── Event wiring ──────────────────────────────────────────────────────────────
$('level').onchange = renderCategories;
$('mode').onchange  = modeChanged;
$('count').onchange = modeChanged;

$('nav-create').onclick = $('back').onclick = () => leaveQuiz('create');
$('nav-library').onclick = async () => {
  view('library');
  try { await library(); } catch (e) { notice(e.message); }
};

// ── Generate form ─────────────────────────────────────────────────────────────
$('generate-form').onsubmit = async e => {
  e.preventDefault();
  if (busy) return;
  busy = true;
  notice('');

  const count = Number($('count').value);
  const isSample = $('mode').value === 'sample';
  $('generate').disabled = true;
  $('generate').textContent = isSample
    ? `Preparing ${count} questions from Reviewer Bank…`
    : (count > 10 ? `Generating ${count} questions via AI…` : 'Creating your practice set…');

  try {
    const created = await api($('mode').value === 'bank' ? '/bank/sets' : '/sets', {
      category,
      level:      $('level').value,
      mode:       $('mode').value,
      difficulty: isSample ? 'Easy' : $('difficulty').value,
      count,
      focus:      $('focus').value
    });
    if ($('mode').value === 'bank') await library();
    await openSet(created.id);
  } catch (e) {
    notice(e.message);
  } finally {
    busy = false;
    $('generate').disabled = false;
    $('generate').textContent = 'Create practice set →';
  }
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

api('/session', {})
  .then(async () => {
    aiConfig = await api('/config');
    modeChanged();
    await library();
  })
  .then(() => { $('generate').disabled = false; document.dispatchEvent(new Event('study-ready')); })
  .catch(e => notice(e.message));
