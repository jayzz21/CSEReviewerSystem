(() => {
const el = id => document.getElementById('adm-' + id);
async function request(path, data) {
  const r = await fetch('/api' + path, data === undefined ? {} : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const body = await r.json().catch(() => ({}));
  if (r.status === 401) location.replace('/login.html');
  if (body.code === 'password_change_required') location.replace('/login.html?changePassword=1');
  if (!r.ok) throw new Error(body.error || 'You do not have permission to perform this action.');
  return body;
}
let profiles = [], selectedUser = null, savingUser = false;
const search = document.createElement('input'); search.type = 'search'; search.placeholder = 'Search by username…'; search.setAttribute('aria-label', 'Search users by username');
const results = document.createElement('div'); results.className = 'space-y-3 mt-4'; results.setAttribute('aria-live', 'polite');
const editor = document.createElement('form'); editor.hidden = true; editor.className = 'panel p-4 space-y-4 mt-4';
editor.innerHTML = '<h3 class="text-lg font-bold" id="adm-edit-title">Update user</h3><label class="block">Username<input name="username" required minlength="3" maxlength="30" pattern="[a-zA-Z0-9_]{3,30}" autocomplete="off"></label><label class="block">Role<select name="role"><option>User</option><option>Admin</option></select></label><label class="block">New temporary password (optional)<input name="password" type="password" minlength="12" maxlength="128" autocomplete="new-password" placeholder="Leave blank to keep current password"></label><p class="text-sm text-slate-500">A new password requires the user to change it at their next sign-in.</p><div class="flex flex-wrap gap-3"><button type="submit" class="primary">Save changes</button><button type="button" class="secondary" id="adm-edit-cancel">Cancel</button></div><p id="adm-edit-status" role="status"></p>';
el('users').replaceChildren(search, results, editor);
function closeEditor() { selectedUser = null; editor.reset(); editor.hidden = true; el('edit-status').textContent = ''; }
function renderUsers() {
  results.replaceChildren();
  const query = search.value.trim().toLowerCase();
  if (!query) { results.textContent = 'Search a username to find an account.'; return; }
  const filtered = profiles.filter(p => p.username.toLowerCase().includes(query));
  if (!filtered.length) { results.textContent = 'No users match your search.'; return; }
  for (const p of filtered) {
    const row = document.createElement('div'); row.className = 'flex flex-wrap items-center justify-between gap-3 border border-slate-200 rounded-xl p-4';
    const name = document.createElement('strong'); name.textContent = p.username;
    const role = document.createElement('span'); role.className = 'text-sm text-slate-500'; role.textContent = p.role === 'SuperAdmin' ? 'Super Admin' : p.role;
    row.append(name, role);
    if (p.role !== 'SuperAdmin') {
      const update = document.createElement('button'); update.type = 'button'; update.className = 'secondary'; update.textContent = 'Update'; update.disabled = savingUser;
      update.setAttribute('aria-label', 'Update ' + p.username);
      update.onclick = () => {
        closeEditor(); selectedUser = p; editor.hidden = false;
        editor.elements.username.value = p.username; editor.elements.role.value = p.role;
        el('edit-title').textContent = 'Update ' + p.username;
        editor.elements.username.focus();
      };
      row.append(update);
    }
    results.append(row);
  }
}
search.oninput = () => { if (!savingUser) closeEditor(); renderUsers(); };
el('edit-cancel').onclick = () => { closeEditor(); search.focus(); };
editor.onsubmit = async e => {
  e.preventDefault(); if (!selectedUser || savingUser || !editor.reportValidity()) return;
  savingUser = true; const target = selectedUser;
  const data = { username: editor.elements.username.value, role: editor.elements.role.value, password: editor.elements.password.value };
  Array.from(editor.elements).forEach(control => control.disabled = true); search.disabled = true; renderUsers();
  el('edit-status').textContent = 'Saving…';
  try {
    const saved = await request('/admin/users/' + encodeURIComponent(target.id) + '/update', data);
    Object.assign(target, saved); target.username = saved.username; target.role = saved.role;
    search.value = saved.username; closeEditor();
    el('message').textContent = 'Saved changes for ' + saved.username + '.' + (data.password ? ' They must change their temporary password at next sign-in.' : ' Existing password kept.');
  } catch(e) { el('edit-status').textContent = e.message; }
  finally { savingUser = false; search.disabled = false; Array.from(editor.elements).forEach(control => control.disabled = false); renderUsers(); }
};
async function users() { search.value = ''; closeEditor(); results.textContent = 'Search a username to find an account.'; profiles = await request('/admin/users'); renderUsers(); }
el('question-form').onsubmit = async e => {
  e.preventDefault(); const button = e.target.querySelector('button'); button.disabled = true;
  try {
    const q = { category: el('category').value, level: el('level').value, difficulty: el('difficulty').value, prompt: el('prompt').value, options: [0,1,2,3].map(i => el('option-' + i).value), answer: Number(el('answer').value), explanation: el('explanation').value, steps: el('steps').value.split('\n').map(s => s.trim()).filter(Boolean) };
    const json = JSON.stringify({ version: 1, title: 'Admin question', questions: [q] }).replace(/</g, '\\u003c');
    const html = '<script type="application/json" id="cse-question-bank">' + json + '</script>';
    const result = await request('/imports', { fileName: 'admin-question.html', html });
    el('message').textContent = result.added ? 'Question added to the shared bank.' : 'This question is already in your bank.';
    if (result.added) { e.target.reset(); document.dispatchEvent(new Event('bank-updated')); }
  } catch(e) { el('message').textContent = e.message; } finally { button.disabled = false; }
};
window.HusayAdmin = { enter: () => request('/session').then(async session => {
  if (!session.isAdmin) { el('message').textContent = 'Administrator access is required.'; return; }
  el('admin-content').hidden = false;
  if (session.isSuperAdmin) { el('users-section').hidden = false; await users(); }
}).catch(e => el('message').textContent = e.message) };

})();
