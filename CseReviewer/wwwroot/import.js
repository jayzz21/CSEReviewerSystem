// Uploaded content is handled as text, never as active HTML.
let pendingImport = null, importVersion = 0;
async function refreshImportedBank() {
  const rows = await api('/bank');
  document.dispatchEvent(new Event('bank-updated'));
  $('bank-total').textContent = rows.reduce((total, row) => total + row.count, 0).toLocaleString();
  const summary = $('bank-summary');
  summary.replaceChildren();
  if (!rows.length) { summary.textContent = 'Your imported bank is empty. Choose a file to preview it.'; return; }
  const difficulties = ['Easy', 'Moderate', 'Hard'];
  for (const row of rows) if (!difficulties.includes(row.difficulty)) difficulties.push(row.difficulty);
  const subjects = new Map();
  for (const row of rows) {
    if (!subjects.has(row.category)) subjects.set(row.category, new Map());
    const counts = subjects.get(row.category);
    counts.set(row.difficulty, (counts.get(row.difficulty) || 0) + row.count);
  }
  const table = document.createElement('table');
  table.className = 'bank-table';
  const caption = table.createCaption();
  caption.textContent = 'Imported question bank · all exam levels';
  const addCell = (row, tag, text, scope) => {
    const cell = document.createElement(tag); cell.textContent = text;
    if (scope) cell.scope = scope;
    row.append(cell);
  };
  const header = table.createTHead().insertRow();
  for (const title of ['Subject', ...difficulties, 'Total']) addCell(header, 'th', title, 'col');
  const body = table.createTBody();
  for (const [subject, counts] of [...subjects].sort(([a], [b]) => a.localeCompare(b))) {
    const row = body.insertRow();
    addCell(row, 'th', subject, 'row');
    for (const difficulty of difficulties) addCell(row, 'td', (counts.get(difficulty) || 0).toLocaleString());
    addCell(row, 'td', [...counts.values()].reduce((sum, count) => sum + count, 0).toLocaleString());
  }
  const total = table.createTFoot().insertRow();
  addCell(total, 'th', 'Total questions', 'row');
  for (const difficulty of difficulties) addCell(total, 'td', rows.filter(r => r.difficulty === difficulty).reduce((sum, r) => sum + r.count, 0).toLocaleString());
  addCell(total, 'td', rows.reduce((sum, r) => sum + r.count, 0).toLocaleString());
  const scroll = document.createElement('div');
  scroll.className = 'bank-table-scroll'; scroll.tabIndex = 0;
  scroll.setAttribute('role', 'region'); scroll.setAttribute('aria-label', 'Question counts by subject and difficulty');
  scroll.append(table); summary.append(scroll);
}
async function previewImport(file) {
  const version = ++importVersion;
  pendingImport = null; $('import-confirm').classList.add('hidden');
  if (!file) return;
  if (!/\.html?$/i.test(file.name) || file.size > 2000000) { $('import-preview').textContent='Choose an HTML file smaller than 2 MB.'; return; }
  $('import-preview').textContent='Reading and validating questions…';
  try {
    const input={fileName:file.name,html:await file.text()};
    const result=await api('/imports/preview',input);
    if(version!==importVersion)return;
    const preview=$('import-preview'); preview.replaceChildren();
    const summary=document.createElement('p');
    summary.textContent=`${result.title}: ${result.valid} valid unique questions. ${result.newQuestions} new to your bank; ${result.valid-result.newQuestions} already stored; ${result.duplicates} repeated within the file; ${result.issues.length} invalid and excluded.`;
    preview.append(summary);
    const subjects=document.createElement('p');subjects.textContent=result.categories.map(c=>`${c.category}: ${c.count}`).join(' · ');preview.append(subjects);
    if(result.issues.length) {
      const details=document.createElement('details'), heading=document.createElement('summary');heading.textContent='Review excluded questions';details.append(heading);
      for(const issue of result.issues){const p=document.createElement('p');p.textContent=issue;details.append(p);}preview.append(details);
    }
    if(result.valid) {pendingImport=input;$('import-confirm').classList.remove('hidden');}
  }catch(e){if(version===importVersion)$('import-preview').textContent=e.message;}
}
$('import-file').onchange=e=>previewImport(e.target.files[0]);
const zone=$('drop-zone');
zone.ondragover=e=>{e.preventDefault();zone.classList.add('bg-teal-50');};
zone.ondragleave=()=>zone.classList.remove('bg-teal-50');
zone.ondrop=e=>{e.preventDefault();zone.classList.remove('bg-teal-50');previewImport(e.dataTransfer.files[0]);};
$('import-confirm').onclick=async()=>{
  if(!pendingImport)return;
  $('import-confirm').disabled=true;$('import-file').disabled=true;zone.ondrop=e=>e.preventDefault();
  try {
    const result=await api('/imports',pendingImport);
    pendingImport=null;importVersion++;$('import-confirm').classList.add('hidden');
    $('bank-total').textContent=result.totalQuestions.toLocaleString();
    const importSummary=`${result.added.toLocaleString()} new questions saved. Total in your imported database bank: ${result.totalQuestions.toLocaleString()}. Skipped ${result.duplicates} duplicates and ${result.skipped} invalid entries. Created a practice set of ${result.count} questions.`;
    $('import-preview').textContent=importSummary;
    await refreshImportedBank();await library();await openSet(result.id);
    notice(importSummary);
  }catch(e){$('import-preview').textContent=e.message;}
  finally{$('import-confirm').disabled=false;$('import-file').disabled=false;zone.ondrop=e=>{e.preventDefault();previewImport(e.dataTransfer.files[0]);};}
};
document.addEventListener('study-ready',()=>!$('admin-import').hidden && refreshImportedBank().catch(e=>{$('bank-total').textContent='Unavailable';$('bank-summary').textContent=e.message;}));
