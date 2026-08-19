// =========================================================================
// Authentication Logic (Login & Registration)
// =========================================================================

document.addEventListener('DOMContentLoaded', () => {
  const loginForm = document.getElementById('loginForm');
  const registerForm = document.getElementById('registerForm');
  const loginTab = document.getElementById('loginTab');
  const registerTab = document.getElementById('registerTab');

  // Tab switching
  if (loginTab && registerTab) {
    loginTab.addEventListener('click', () => {
      loginTab.classList.add('active');
      registerTab.classList.remove('active');
      loginForm.style.display = 'block';
      registerForm.style.display = 'none';
    });

    registerTab.addEventListener('click', () => {
      registerTab.classList.add('active');
      loginTab.classList.remove('active');
      registerForm.style.display = 'block';
      loginForm.style.display = 'none';
    });
  }

  // Handle Login
  if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const email = document.getElementById('loginEmail').value.trim();
      const password = document.getElementById('loginPassword').value;
      const submitBtn = loginForm.querySelector('button[type="submit"]');

      try {
        submitBtn.disabled = true;
        submitBtn.textContent = 'Logging in...';

        const data = await api.post('/auth/login', { email, password });
        api.setSession(data.token, {
          userId: data.userId,
          email: data.email,
          fullName: data.fullName
        });

        showToast('Login successful! Redirecting...', 'success');
        setTimeout(() => {
          window.location.href = 'dashboard.html';
        }, 800);
      } catch (err) {
        showToast(err.message || 'Invalid credentials.', 'error');
      } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Sign In';
      }
    });
  }

  // Handle Register
  if (registerForm) {
    registerForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const fullName = document.getElementById('regFullName').value.trim();
      const email = document.getElementById('regEmail').value.trim();
      const password = document.getElementById('regPassword').value;
      const submitBtn = registerForm.querySelector('button[type="submit"]');

      try {
        submitBtn.disabled = true;
        submitBtn.textContent = 'Creating Account...';

        const data = await api.post('/auth/register', { fullName, email, password });
        api.setSession(data.token, {
          userId: data.userId,
          email: data.email,
          fullName: data.fullName
        });

        showToast('Registration successful! Welcome.', 'success');
        setTimeout(() => {
          window.location.href = 'dashboard.html';
        }, 800);
      } catch (err) {
        showToast(err.message || 'Registration failed.', 'error');
      } finally {
        submitBtn.disabled = false;
        submitBtn.textContent = 'Create Account';
      }
    });
  }
});

function logout() {
  api.clearSession();
  window.location.href = 'login.html';
}
