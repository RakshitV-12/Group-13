// =========================================================================
// API Helper & Centralized Fetch Client
// =========================================================================

const API_BASE_URL = 'http://localhost:5077/api';

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
    localStorage.setItem('user', JSON.stringify(user));
  },

  clearSession() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
  },

  async request(endpoint, options = {}) {
    const token = this.getToken();
    const headers = {
      'Content-Type': 'application/json',
      ...options.headers
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
        if (!window.location.pathname.includes('login.html') && !window.location.pathname.includes('index.html')) {
          window.location.href = 'login.html';
        }
        throw new Error('Session expired. Please log in again.');
      }

      if (response.status === 204) {
        return null;
      }

      const data = await response.json().catch(() => null);

      if (!response.ok) {
        const errorMsg = data?.message || (data?.errors ? Object.values(data.errors).flat().join(', ') : 'An error occurred');
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
    document.body.appendChild(container);
  }

  const toast = document.createElement('div');
  toast.className = `toast toast-${type}`;
  toast.textContent = message;
  container.appendChild(toast);

  setTimeout(() => {
    toast.remove();
  }, 4000);
}

// Authentication Check Guard
function requireAuth() {
  if (!api.getToken()) {
    window.location.href = 'login.html';
  }
}
