// =========================================================================
// WalletWise AI - Authentication Logic (Login & Registration)
// =========================================================================

document.addEventListener('DOMContentLoaded', () => {
  const loginForm = document.getElementById('loginForm');
  const registerForm = document.getElementById('registerForm');
  const loginTab = document.getElementById('loginTab');
  const registerTab = document.getElementById('registerTab');
  const authAlert = document.getElementById('authAlert');

  function showAlert(message, type = 'error') {
    if (authAlert) {
      authAlert.className = `auth-alert ${type}`;
      authAlert.textContent = message;
      authAlert.style.display = 'block';
    } else {
      showToast(message, type);
    }
  }

  function hideAlert() {
    if (authAlert) {
      authAlert.style.display = 'none';
      authAlert.textContent = '';
    }
  }

  function switchToLogin() {
    hideAlert();
    if (loginTab && registerTab && loginForm && registerForm) {
      loginTab.classList.add('active');
      registerTab.classList.remove('active');
      loginForm.style.display = 'block';
      registerForm.style.display = 'none';
    }
  }

  function switchToRegister() {
    hideAlert();
    if (loginTab && registerTab && loginForm && registerForm) {
      registerTab.classList.add('active');
      loginTab.classList.remove('active');
      registerForm.style.display = 'block';
      loginForm.style.display = 'none';
    }
  }

  if (loginTab) loginTab.addEventListener('click', switchToLogin);
  if (registerTab) registerTab.addEventListener('click', switchToRegister);

  // Check URL tab parameter (e.g. login.html?tab=register or login.html?tab=login)
  const urlParams = new URLSearchParams(window.location.search);
  const tabParam = urlParams.get('tab');
  if (tabParam === 'register' || tabParam === 'signup') {
    switchToRegister();
  } else {
    switchToLogin();
  }

  // Handle Login Submission
  if (loginForm) {
    loginForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      hideAlert();

      const emailInput = document.getElementById('loginEmail');
      const passwordInput = document.getElementById('loginPassword');
      const email = emailInput ? emailInput.value.trim() : '';
      const password = passwordInput ? passwordInput.value : '';
      const submitBtn = loginForm.querySelector('button[type="submit"]');

      if (!email || !password) {
        showAlert('Please enter both your email address and password.');
        return;
      }

      try {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Signing In...';

        const data = await api.post('/auth/login', { email, password });
        
        if (!data || !data.token) {
          throw new Error('Authentication failed: No token received.');
        }

        const userObj = data.user || {
          id: data.userId,
          name: data.fullName,
          email: data.email
        };

        api.setSession(data.token, {
          userId: userObj.id || data.userId,
          email: userObj.email || data.email,
          fullName: userObj.name || data.fullName
        });

        showAlert('Sign in successful! Entering dashboard...', 'success');
        setTimeout(() => {
          window.location.replace('dashboard.html');
        }, 400);
      } catch (err) {
        showAlert(err.message || 'Invalid email or password. Please try again.');
      } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = 'Sign In <i class="fas fa-arrow-right"></i>';
      }
    });
  }

  // Handle Register Submission
  if (registerForm) {
    registerForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      hideAlert();

      const nameInput = document.getElementById('regFullName');
      const emailInput = document.getElementById('regEmail');
      const passwordInput = document.getElementById('regPassword');

      const fullName = nameInput ? nameInput.value.trim() : '';
      const email = emailInput ? emailInput.value.trim() : '';
      const password = passwordInput ? passwordInput.value : '';
      const submitBtn = registerForm.querySelector('button[type="submit"]');

      if (!fullName || !email || !password) {
        showAlert('Please fill in all registration fields.');
        return;
      }
      if (password.length < 6) {
        showAlert('Password must be at least 6 characters.');
        return;
      }

      try {
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<i class="fas fa-spinner fa-spin"></i> Creating Account...';

        const data = await api.post('/auth/register', { fullName, email, password });
        
        if (!data || !data.token) {
          throw new Error('Registration failed: No token received.');
        }

        const userObj = data.user || {
          id: data.userId,
          name: data.fullName,
          email: data.email
        };

        api.setSession(data.token, {
          userId: userObj.id || data.userId,
          email: userObj.email || data.email,
          fullName: userObj.name || data.fullName
        });

        showAlert('Account created successfully! Entering dashboard...', 'success');
        setTimeout(() => {
          window.location.replace('dashboard.html');
        }, 400);
      } catch (err) {
        showAlert(err.message || 'Registration failed. Please check your information.');
      } finally {
        submitBtn.disabled = false;
        submitBtn.innerHTML = 'Create Account <i class="fas fa-arrow-right"></i>';
      }
    });
  }
});
