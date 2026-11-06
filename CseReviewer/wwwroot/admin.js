const el = id => document.getElementById(id);
async function request(path, data) {
  const r = await fetch('/api' + path, data === undefined ? {} : { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const body = await r.json().catch(() => ({}));
  if (r.status === 401) location.replace('/login.html');
  if (body.code === 'password_change_required') location.replace('/login.html?changePassword=1');
  if (!r.ok) throw new Error(body.error || 'You do not have permission to perform this action.');
  return body;
}
async function users() {
  const profiles = await request('/admin/users'); el('users').replaceChildren();
  for (const p of profiles) {
    const row = document.createElement('div'); row.className = 'flex flex-wrap items-center gap-3';
    const label = document.createElement('label'); label.textContent = p.username + (p.email ? ' · ' + p.email : ''); row.append(label);
    if (p.role === 'SuperAdmin') { const role = document.createElement('span'); role.textContent = 'Super Admin'; row.append(role); }
    else {
      const select = document.createElement('select'); select.setAttribute('aria-label', 'Role for ' + p.username);
      for (const role of ['User', 'Admin']) { const o = document.createElement('option'); o.value = role; o.textContent = role; select.append(o); }
      select.value = p.role; row.append(select);
      const password = document.createElement('input'); password.type = 'password'; password.placeholder = 'Temporary password (12+ characters)'; password.autocomplete = 'new-password'; password.setAttribute('aria-label', 'Temporary password for ' + p.username); row.append(password);
      const reset = document.createElement('button'); reset.className = 'secondary'; reset.textContent = 'Reset password'; row.append(reset);
      reset.onclick = async () => { reset.disabled = true; try { await request('/admin/users/' + encodeURIComponent(p.id) + '/password', { password: password.value }); password.value = ''; el('message').textContent = 'Password reset for ' + p.username + '. They must change it at next sign-in.'; } catch(e) { el('message').textContent = e.message; } finally { reset.disabled = false; } };

      const save = document.createElement('button'); save.className = 'secondary'; save.textContent = 'Save role'; row.append(save);
      save.onclick = async () => { save.disabled = true; try { await request('/admin/users/' + encodeURIComponent(p.id) + '/role', { role: select.value }); el('message').textContent = 'Role updated for ' + p.username + '.'; } catch(e) { el('message').textContent = e.message; } finally { save.disabled = false; } };
    }
    el('users').append(row);
  }
}
el('question-form').onsubmit = async e => {
  e.preventDefault(); const button = e.target.querySelector('button'); button.disabled = true;
  try {
    const q = { category: el('category').value, level: el('level').value, difficulty: el('difficulty').value, prompt: el('prompt').value, options: [0,1,2,3].map(i => el('option-' + i).value), answer: Number(el('answer').value), explanation: el('explanation').value, steps: el('steps').value.split('\n').map(s => s.trim()).filter(Boolean) };
    const json = JSON.stringify({ version: 1, title: 'Admin question', questions: [q] }).replace(/</g, '\\u003c');
    const html = '<script type="application/json" id="cse-question-bank">' + json + '</script>';
    const result = await request('/imports', { fileName: 'admin-question.html', html });
    el('message').textContent = result.added ? 'Question added to the shared bank.' : 'This question is already in your bank.';
    if (result.added) e.target.reset();
  } catch(e) { el('message').textContent = e.message; } finally { button.disabled = false; }
};
request('/session').then(async session => {
  if (!session.isAdmin) { el('message').textContent = 'Administrator access is required.'; return; }
  el('admin-content').hidden = false;
  if (session.isSuperAdmin) { el('users-section').hidden = false; await users(); }
}).catch(e => el('message').textContent = e.message);
