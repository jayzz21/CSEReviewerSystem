(function () {
  function isDark() { return document.documentElement.classList.contains('dark'); }
  function showToggleState() {
    var button = document.getElementById('theme-toggle');
    if (!button) return;
    button.textContent = isDark() ? '☀️' : '🌙';
    button.setAttribute('aria-label', isDark() ? 'Switch to light mode' : 'Switch to dark mode');
    button.setAttribute('title', isDark() ? 'Switch to light mode' : 'Switch to dark mode');
  }
  function setDark(dark) {
    document.documentElement.classList.toggle('dark', dark);
    try { localStorage.setItem('dark_mode', dark ? 'true' : 'false'); } catch (_) {}
    showToggleState();
  }
  document.addEventListener('click', function (e) {
    if (e.target.closest('#theme-toggle')) setDark(!isDark());
  });
  showToggleState();
})();
