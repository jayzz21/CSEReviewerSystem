window.husayAuth = {
  post: async function (path, data) {
    let response;
    try {
      response = await fetch('/api/auth/' + path, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
      });
    } catch (e) {
      return { status: 0, error: 'Cannot reach the server. Check your connection and try again.', mustChangePassword: false, message: '' };
    }
    const body = await response.json().catch(function () { return {}; });
    return {
      status: response.status,
      error: body.error || '',
      mustChangePassword: !!body.mustChangePassword,
      message: body.message || ''
    };
  }
};
