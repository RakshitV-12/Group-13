// =========================================================================
// WalletWise AI - API Helper & Centralized Fetch Client
// =========================================================================

const API_BASE_URL = '/api';

const api = {
  getToken() {
    return localStorage.getItem('token');
  },

  getUser() {
    const userStr = localStorage.getItem('user');
    return userStr ? JSON.parse(userStr) : null;
  },

  setSession(token, user) {
    localStorage.setItem('token', token);
    if (user) {
      localStorage.setItem('user', JSON.stringify(user));
    }
  },

  clearSession() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    sessionStorage.clear();
  },

  isLoggedIn() {
    const token = this.getToken();
    return !!token && token !== 'null' && token !== 'undefined';
  },

  async request(endpoint, options = {}) {
    const token = this.getToken();

    const headers = {
      'Content-Type': 'application/json',
      ...(options.headers || {})
    };

    if (token) {
      headers['Authorization'] = `Bearer ${token}`;
    }

    try {
      const response = await fetch(`${API_BASE_URL}${endpoint}`, {
        ...options,
        headers
      });

      if (response.status === 401) {
        this.clearSession();
        const currentPath = window.location.pathname.toLowerCase();
        if (!currentPath.endsWith('login.html') && !currentPath.endsWith('index.html') && currentPath !== '/' && currentPath !== '') {
          window.location.replace('login.html');
        }
        throw new Error('Session expired or unauthorized. Please sign in again.');
      }

      if (response.status === 204) {
        return null;
      }

      const data = await response.json().catch(() => null);

      if (!response.ok) {
        const errorMsg = data?.message || (data?.errors ? Object.values(data.errors).flat().join(', ') : `HTTP Error ${response.status}`);
        throw new Error(errorMsg);
      }

      return data;
    } catch (err) {
      console.error(`API Error [${endpoint}]:`, err);
      throw err;
    }
  },

  get(endpoint) {
    return this.request(endpoint, { method: 'GET' });
  },

  post(endpoint, body) {
    return this.request(endpoint, {
      method: 'POST',
      body: JSON.stringify(body)
    });
  },

  upload(endpoint, formData) {
    const token = this.getToken();
    const headers = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;

    return fetch(`${API_BASE_URL}${endpoint}`, {
      method: 'POST',
      headers,
      body: formData
    }).then(async (response) => {
      if (response.status === 401) {
        this.clearSession();
        window.location.replace('login.html');
        throw new Error('Unauthorized.');
      }
      const data = await response.json().catch(() => null);
      if (!response.ok) {
        throw new Error(data?.message || 'Upload failed');
      }
      return data;
    });
  },

  put(endpoint, body) {
    return this.request(endpoint, {
      method: 'PUT',
      body: JSON.stringify(body)
    });
  },

  delete(endpoint) {
    return this.request(endpoint, { method: 'DELETE' });
  }
};

// UI Toast Notification helper
function showToast(message, type = 'info') {
  let container = document.getElementById('toastContainer');
  if (!container) {
    container = document.createElement('div');
    container.id = 'toastContainer';
    container.className = 'toast-container';
    container.style.cssText = 'position:fixed;bottom:24px;left:24px;z-index:99999;display:flex;flex-direction:column;gap:8px;pointer-events:none;';
    document.body.appendChild(container);
  }

  const toast = document.createElement('div');
  toast.className = `toast toast-${type}`;
  const bg = type === 'success' ? '#10b981' : type === 'error' ? '#ef4444' : '#1e293b';
  toast.style.cssText = `padding:12px 20px;border-radius:10px;background:${bg};color:#ffffff;font-weight:600;font-size:13px;box-shadow:0 10px 25px rgba(0,0,0,0.2);`;
  toast.textContent = message;
  container.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateY(10px)';
    toast.style.transition = 'all 0.3s ease';
    setTimeout(() => toast.remove(), 300);
  }, 3500);
}

function requireAuth() {
  if (!api.isLoggedIn()) {
    const currentPath = window.location.pathname.toLowerCase();
    if (!currentPath.endsWith('login.html') && !currentPath.endsWith('index.html') && currentPath !== '/' && currentPath !== '') {
      window.location.replace('login.html');
    }
  }
}

function logout() {
  api.clearSession();
  window.location.replace('index.html');
}
