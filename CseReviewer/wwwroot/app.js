const $ = id => document.getElementById(id);
const categories = ['Numerical Ability', 'Verbal Ability', 'Analytical Ability', 'Clerical Ability', 'General Information'];
const hints = ['Numbers, patterns & word problems', 'Words, grammar & comprehension', 'Logic, analogies & conclusions', 'Filing, spelling & attention to detail', 'Public service, ethics & citizenship'];
let category = categories[0], current, index = 0, choice = null, results = [], busy = false;
const escapeHtml = s => String(s).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
function notice(message) { $('notice').textContent = message; $('notice').classList.toggle('hidden', !message); }
async function api(path, data) {
 const response = await fetch('/api' + path, data === undefined ? {} : {method:'POST', headers:{'Content-Type':'application/json'}, body:JSON.stringify(data)});
 const text = await response.text(); let body; try { body = text ? JSON.parse(text) : null; } catch { body = null; }
 if (!response.ok) throw new Error(body?.error || (response.status === 401 ? 'Your study session expired. Refresh this page.' : 'The request failed. Please try again.'));
 return body;
}
function view(name) {
 ['create','library','quiz'].forEach(v => $(v+'-view').classList.toggle('hidden', v !== name));
 $('nav-create').classList.toggle('active',name !== 'library'); $('nav-library').classList.toggle('active',name === 'library'); notice('');
}
function renderCategories() {
 const allowed = categories.filter(c => !($('level').value === 'Professional' && c === 'Clerical Ability') && !($('level').value === 'Subprofessional' && c === 'Analytical Ability'));
 if (!allowed.includes(category)) category = allowed[0];
 $('categories').innerHTML = allowed.map(c => `<button type="button" class="subject ${c===category?'selected':''}" aria-pressed="${c===category}" data-category="${c}"><strong>${c}</strong><span class="block text-xs text-slate-500 mt-2">${hints[categories.indexOf(c)]}</span></button>`).join('');
 $('categories').querySelectorAll('button').forEach(b => b.onclick = () => {category=b.dataset.category; renderCategories();});
}
function modeChanged() {
 const sample = $('mode').value === 'sample';
 ['focus','difficulty','count'].forEach(id => $(id).disabled = sample);
 if (sample) { $('count').value = '5'; $('difficulty').value = 'Easy'; }
 $('mode-note').textContent = sample ? 'Sample mode: 5 introductory questions. Custom focus and difficulty are available in AI mode.' : 'AI mode uses your local Ollama model. Generation may take a few minutes. Keep this page open.';
}
$('level').onchange = renderCategories; $('mode').onchange = modeChanged;
$('nav-create').onclick = $('back').onclick = () => view('create');
$('nav-library').onclick = async () => {view('library'); try { await library(); } catch(e) {notice(e.message);} };
$('generate-form').onsubmit = async e => {
 e.preventDefault(); if (busy) return; busy=true; notice(''); $('generate').disabled=true; $('generate').textContent='Creating your practice set…';
 try {
  const created = await api('/sets', {category, level:$('level').value, mode:$('mode').value, difficulty:$('mode').value==='sample'?'Easy':$('difficulty').value, count:Number($('count').value), focus:$('focus').value});
  await openSet(created.id);
 } catch(e) {notice(e.message);} finally {busy=false; $('generate').disabled=false; $('generate').textContent='Create practice set →';}
};
async function library() {
 const sets=await api('/sets'); $('saved-count').textContent=sets.length || '';
 $('library').innerHTML=sets.length ? sets.map(s=>`<article class="panel p-6"><p class="eyebrow">${escapeHtml(s.source)} · ${s.count} QUESTIONS</p><h2 class="text-xl font-bold mt-3">${escapeHtml(s.title)}</h2><p class="text-sm text-slate-500 mt-2">${escapeHtml(s.level)} · ${escapeHtml(s.difficulty)}</p><button class="secondary mt-5" data-set="${s.id}">Practice this set →</button></article>`).join('') : '<div class="panel p-8 col-span-full"><h2 class="text-xl font-bold">A fresh start for your library.</h2><p class="text-slate-500 mt-3">Create a practice set, then choose “Save this set” to keep it here.</p></div>';
 $('library').querySelectorAll('[data-set]').forEach(b=>b.onclick=async()=>{try {await openSet(b.dataset.set);} catch(e){notice(e.message);}});
}
async function openSet(id) {
 current=await api('/sets/'+id); index=0; choice=null; results=[];
 view('quiz'); $('quiz-title').textContent=current.title; $('quiz-meta').textContent=`${current.level} · ${current.source} · ${current.difficulty}`;
 $('save').textContent=current.saved?'✓ Saved to library':'♡ Save this set'; $('save-form').classList.add('hidden'); renderQuestion();
}
function renderQuestion() {
 choice=null;
 $('progress-label').textContent=`Question ${index+1} of ${current.questions.length}`;
 $('score').textContent=`${results.filter(r=>r.correct).length} correct · ${results.length} answered`;
 $('progress').style.width=`${results.length/current.questions.length*100}%`;
 const q=current.questions[index];
 $('question-card').innerHTML=`<p class="eyebrow">${escapeHtml(current.category)}</p><h2 class="text-xl md:text-2xl font-semibold leading-relaxed mt-4">${escapeHtml(q.prompt)}</h2><div id="options" class="space-y-3 mt-7">${q.options.map((o,i)=>`<button class="option" data-choice="${i}" aria-pressed="false"><span class="text-sm rounded-lg bg-slate-100 px-3 py-2 font-bold">${'ABCD'[i]}</span><span>${escapeHtml(o)}</span></button>`).join('')}</div><button id="check" class="primary mt-7" disabled>Check answer →</button><div id="feedback" aria-live="polite"></div>`;
 $('options').querySelectorAll('button').forEach(b=>b.onclick=()=>{
  choice=Number(b.dataset.choice); $('options').querySelectorAll('button').forEach(o=>{o.classList.toggle('selected',o===b);o.setAttribute('aria-pressed',String(o===b));}); $('check').disabled=false;
 });
 $('check').onclick=submitAnswer;
}
async function submitAnswer() {
 if(choice===null)return; const selected=choice; $('check').disabled=true;
 $('options').querySelectorAll('button').forEach(b=>b.disabled=true);
 try {
  const result=await api(`/sets/${current.id}/answer`,{index,choice:selected}); results.push({...result,choice:selected});
  $('options').querySelectorAll('button').forEach((b,i)=>{b.classList.remove('selected');b.classList.toggle('correct',i===result.answer);b.classList.toggle('wrong',i===selected&&!result.correct);});
  $('check').classList.add('hidden');
  $('feedback').innerHTML=`<div class="mt-7 bg-slate-50 rounded-xl p-6"><h3 class="font-bold text-lg">${result.correct?'✓ Correct. Nicely done!':'Keep going — this is how you learn.'}</h3><p class="mt-3 font-semibold">Correct answer: ${'ABCD'[result.answer]}. ${escapeHtml(current.questions[index].options[result.answer])}</p><p class="mt-3 text-slate-600 leading-7">${escapeHtml(result.explanation)}</p>${result.steps.length?`<h4 class="font-bold mt-5">Step-by-step solution</h4><ol class="list-decimal pl-5 space-y-2 mt-3 text-slate-600">${result.steps.map(s=>`<li>${escapeHtml(s)}</li>`).join('')}</ol>`:''}</div><button id="next" class="primary mt-6">${index+1===current.questions.length?'See my results':'Next question →'}</button>`;
  $('score').textContent=`${results.filter(r=>r.correct).length} correct · ${results.length} answered`; $('progress').style.width=`${results.length/current.questions.length*100}%`;
  $('next').onclick=()=>{if(++index<current.questions.length)renderQuestion();else finish();};
 }catch(e){notice(e.message);$('check').disabled=false; $('options').querySelectorAll('button').forEach(b=>b.disabled=false);}
}
function finish() {
 const correct=results.filter(r=>r.correct).length;
 $('progress-label').textContent='Practice complete';
 $('question-card').innerHTML=`<p class="eyebrow">ONE STEP CLOSER</p><h2 class="text-3xl font-bold mt-4">You showed up. That matters.</h2><p class="text-5xl font-bold text-teal-800 mt-8">${correct}<span class="text-2xl text-slate-400"> / ${results.length}</span></p><p class="text-slate-500 mt-4">${Math.round(correct/results.length*100)}% on this practice set. Save it above to revisit the questions.</p><div class="mt-7 space-y-3">${current.questions.map((q,i)=>`<details class="border border-slate-200 rounded-xl p-4"><summary class="cursor-pointer font-semibold">${results[i].correct?'✓':'○'} ${escapeHtml(q.prompt)}</summary><p class="mt-3">Correct answer: ${escapeHtml(q.options[results[i].answer])}</p><p class="text-slate-500 mt-2">${escapeHtml(results[i].explanation)}</p>${results[i].steps.map(s=>`<p class="text-sm mt-2">${escapeHtml(s)}</p>`).join('')}</details>`).join('')}</div><button id="retry" class="primary mt-7">Practice again</button>`;
 $('retry').onclick=()=>{index=0;results=[];renderQuestion();};
}
$('save').onclick=()=>{$('save-form').classList.toggle('hidden');$('save-title').value=current.title;$('save-title').focus();};
$('save-confirm').onclick=async()=>{
 $('save-confirm').disabled=true;
 try {await api(`/sets/${current.id}/save`,{title:$('save-title').value});current.saved=true;current.title=$('save-title').value.trim();$('quiz-title').textContent=current.title;$('save').textContent='✓ Saved to library';$('save-form').classList.add('hidden');await library();notice('Saved. You can find this set in My saved sets.');}catch(e){notice(e.message);}finally{$('save-confirm').disabled=false;}
};
renderCategories();modeChanged();
$('generate').disabled=true;
api('/session',{}).then(library).then(()=>{$('generate').disabled=false;}).catch(e=>notice(e.message));
