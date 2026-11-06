// Uploaded content is handled as text, never as active HTML.
let pendingImport = null, importVersion = 0;
async function refreshImportedBank() {
  const rows = await api('/bank');
  $('bank-total').textContent = rows.reduce((total, row) => total + row.count, 0).toLocaleString();
  $('bank-summary').textContent = rows.length ? 'Your imported bank: ' + rows.map(r => `${r.category} / ${r.difficulty}: ${r.count}`).join(' · ') : 'Your imported bank is empty. Choose a file to preview it.';
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
