// =========================================================================
// Dashboard Logic & Visual Metrics
// =========================================================================

document.addEventListener('DOMContentLoaded', async () => {
  requireAuth();
  updateUserUI();
  await loadDashboard();

  // Setup Quick Expense form on dashboard if present
  const quickForm = document.getElementById('dashQuickForm');
  if (quickForm) {
    quickForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const input = document.getElementById('dashQuickInput').value.trim();
      if (!input) return;

      try {
        const res = await api.post('/transactions/quick', { input });
        showToast(`Added: ₹${res.amount} for "${res.description}" in ${res.categoryName}`, 'success');
        document.getElementById('dashQuickInput').value = '';
        await loadDashboard();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }
});

function updateUserUI() {
  const user = api.getUser();
  if (user) {
    const nameEl = document.getElementById('navUserName');
    const emailEl = document.getElementById('navUserEmail');
    const avatarEl = document.getElementById('navUserAvatar');
    if (nameEl) nameEl.textContent = user.fullName || 'User';
    if (emailEl) emailEl.textContent = user.email || '';
    if (avatarEl && user.fullName) {
      avatarEl.textContent = user.fullName.charAt(0).toUpperCase();
    }
  }
}

async function loadDashboard() {
  try {
    const data = await api.get('/dashboard/summary');
    renderKPIs(data);
    render50_30_20Rule(data.financialRule);
    renderCategoryBreakdown(data.categoryExpenses);
    renderRecentTransactions(data.recentTransactions);
    renderMonthlyTrends(data.monthlySpending);
  } catch (err) {
    console.error('Failed to load dashboard:', err);
    showToast('Failed to load dashboard data.', 'error');
  }
}

function renderKPIs(data) {
  document.getElementById('totalIncome').textContent = `₹${data.totalIncome.toLocaleString('en-IN', { minimumFractionDigits: 2 })}`;
  document.getElementById('totalExpenses').textContent = `₹${data.totalExpenses.toLocaleString('en-IN', { minimumFractionDigits: 2 })}`;
  document.getElementById('totalSavings').textContent = `₹${data.totalSavings.toLocaleString('en-IN', { minimumFractionDigits: 2 })}`;
  document.getElementById('savingsPercentage').textContent = `${data.savingsPercentage}%`;
}

function render50_30_20Rule(rule) {
  if (!rule) return;
  const statusBadge = document.getElementById('ruleStatusBadge');
  const recommendationEl = document.getElementById('ruleRecommendation');
  
  if (statusBadge) statusBadge.textContent = rule.status;
  if (recommendationEl) recommendationEl.textContent = rule.recommendation;

  const barNeeds = document.getElementById('barNeeds');
  const barWants = document.getElementById('barWants');
  const barSavings = document.getElementById('barSavings');

  if (barNeeds) {
    barNeeds.style.width = `${Math.min(100, rule.needsPercentage)}%`;
    document.getElementById('pctNeeds').textContent = `${rule.needsPercentage}% (₹${rule.needsAmount.toLocaleString('en-IN')})`;
  }
  if (barWants) {
    barWants.style.width = `${Math.min(100, rule.wantsPercentage)}%`;
    document.getElementById('pctWants').textContent = `${rule.wantsPercentage}% (₹${rule.wantsAmount.toLocaleString('en-IN')})`;
  }
  if (barSavings) {
    barSavings.style.width = `${Math.min(100, rule.savingsPercentage)}%`;
    document.getElementById('pctSavings').textContent = `${rule.savingsPercentage}% (₹${rule.savingsAmount.toLocaleString('en-IN')})`;
  }
}

function renderCategoryBreakdown(categories) {
  const container = document.getElementById('categoryBreakdownList');
  if (!container) return;

  if (!categories || categories.length === 0) {
    container.innerHTML = '<p style="color: var(--gray-400); text-align: center; padding: 20px;">No expenses recorded this month.</p>';
    return;
  }

  container.innerHTML = categories.map(cat => `
    <div class="category-item">
      <div class="category-icon-box" style="background: ${cat.colorCode}15; color: ${cat.colorCode};">
        <i class="fas fa-${cat.icon || 'tag'}"></i>
      </div>
      <div class="category-info">
        <div class="category-name-row">
          <span>${cat.categoryName}</span>
          <span>₹${cat.amount.toLocaleString('en-IN', { minimumFractionDigits: 2 })} (${cat.percentage}%)</span>
        </div>
        <div class="category-meter">
          <div class="category-meter-bar" style="width: ${cat.percentage}%; background: ${cat.colorCode || '#4f46e5'};"></div>
        </div>
      </div>
    </div>
  `).join('');
}

function renderRecentTransactions(transactions) {
  const tbody = document.getElementById('recentTransactionsBody');
  if (!tbody) return;

  if (!transactions || transactions.length === 0) {
    tbody.innerHTML = '<tr><td colspan="5" style="text-align: center; color: var(--gray-400); padding: 24px;">No transactions yet. Try quick entry above!</td></tr>';
    return;
  }

  tbody.innerHTML = transactions.map(t => `
    <tr>
      <td><strong>${t.description || 'Expense'}</strong></td>
      <td>
        <span class="badge" style="background: ${t.categoryColor || '#6c757d'}15; color: ${t.categoryColor || '#6c757d'};">
          <i class="fas fa-${t.categoryIcon || 'tag'}"></i> ${t.categoryName}
        </span>
      </td>
      <td><span class="badge badge-${t.paymentMethod.toLowerCase()}">${t.paymentMethod}</span></td>
      <td>${new Date(t.transactionDate).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' })}</td>
      <td style="font-weight: 700; color: ${t.type === 'Income' ? 'var(--success)' : 'var(--danger)'};">
        ${t.type === 'Income' ? '+' : '-'}₹${t.amount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
      </td>
    </tr>
  `).join('');
}

function renderMonthlyTrends(trends) {
  const container = document.getElementById('monthlyTrendsContainer');
  if (!container || !trends) return;

  container.innerHTML = `
    <div style="display: flex; justify-content: space-between; align-items: flex-end; height: 180px; gap: 12px; padding-top: 20px;">
      ${trends.map(t => {
        const max = Math.max(...trends.map(x => Math.max(x.income, x.expenses)), 1000);
        const expH = Math.min(100, Math.round((t.expenses / max) * 100));
        const incH = Math.min(100, Math.round((t.income / max) * 100));
        return `
          <div style="flex: 1; display: flex; flex-direction: column; align-items: center; gap: 8px;">
            <div style="display: flex; gap: 4px; align-items: flex-end; height: 120px; width: 100%; justify-content: center;">
              <div title="Income: ₹${t.income}" style="width: 14px; height: ${incH}%; background: var(--success); border-radius: 4px;"></div>
              <div title="Expenses: ₹${t.expenses}" style="width: 14px; height: ${expH}%; background: var(--danger); border-radius: 4px;"></div>
            </div>
            <span style="font-size: 0.75rem; color: var(--gray-500);">${t.monthName.split(' ')[0]}</span>
          </div>
        `;
      }).join('')}
    </div>
    <div style="display: flex; justify-content: center; gap: 20px; margin-top: 16px; font-size: 0.8rem; color: var(--gray-600);">
      <span style="display: flex; align-items: center; gap: 6px;"><div style="width: 10px; height: 10px; background: var(--success); border-radius: 2px;"></div> Income</span>
      <span style="display: flex; align-items: center; gap: 6px;"><div style="width: 10px; height: 10px; background: var(--danger); border-radius: 2px;"></div> Expenses</span>
    </div>
  `;
}
