document.addEventListener('click', function (e) {
  var toggle = e.target.closest('.password-toggle');
  if (!toggle) return;
  var input = document.getElementById(toggle.dataset.target);
  if (!input) return;
  var showing = input.type === 'password';
  input.type = showing ? 'text' : 'password';
  toggle.querySelector('.eye-open').classList.toggle('hidden', showing);
  toggle.querySelector('.eye-closed').classList.toggle('hidden', !showing);
  toggle.setAttribute('aria-label', showing ? 'Hide password' : 'Show password');
  toggle.setAttribute('aria-pressed', String(showing));
});
