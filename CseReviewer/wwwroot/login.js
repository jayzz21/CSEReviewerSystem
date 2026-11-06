const el = id => document.getElementById(id);
let submitting = false;
function showChange() {
  el('login-form').classList.add('hidden'); el('register-form').classList.add('hidden'); el('auth-options').classList.add('hidden');
  el('change-form').classList.remove('hidden'); el('auth-title').textContent = 'Set your new password';
}
async function request(path, data) {
  const response = await fetch('/api/auth/' + path, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(data) });
  const result = await response.json().catch(() => ({}));
  if (!response.ok) throw new Error(result.error || (response.status === 429 ? 'Too many attempts. Wait one minute and try again.' : 'Request failed. Please retry.'));
  return result;
}
function form(id, action) {
  el(id).onsubmit = async e => {
    e.preventDefault(); if (submitting) return; submitting = true;
    const button = e.target.querySelector('button'); button.disabled = true; el('auth-message').textContent = '';
    try { await action(); } catch (e) { el('auth-message').textContent = e.message; }
    finally { submitting = false; button.disabled = false; }
  };
}
form('login-form', async () => {
  const result = await request('login', { login: el('login-name').value, password: el('login-password').value });
  el('login-password').value = '';
  if (result.mustChangePassword) showChange(); else location.replace('/');
});
form('register-form', async () => {
  const result = await request('register', { username: el('register-name').value, password: el('register-password').value });
  el('register-password').value = ''; el('auth-message').textContent = result.message;
});
form('change-form', async () => {
  await request('change-password', { currentPassword: el('current-password').value, newPassword: el('new-password').value }); location.replace('/');
});
el('toggle-register').onclick = () => {
  const registering = el('register-form').classList.contains('hidden');
  el('register-form').classList.toggle('hidden', !registering); el('login-form').classList.toggle('hidden', registering);
  el('auth-title').textContent = registering ? 'Create your study account' : 'Welcome back';
  el('toggle-register').textContent = registering ? 'Already registered? Sign in' : 'New here? Create an account';
  el('auth-message').textContent = '';
};
const params = new URLSearchParams(location.search);
if (params.has('changePassword')) showChange();
if (location.hash) history.replaceState(null, '', '/login.html');
