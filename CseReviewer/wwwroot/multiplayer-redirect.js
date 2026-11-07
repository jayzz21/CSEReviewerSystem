const lobbyCode = new URLSearchParams(location.search).get('code') || '';
location.replace('/?view=multiplayer' + (lobbyCode ? '&code=' + encodeURIComponent(lobbyCode) : ''));
