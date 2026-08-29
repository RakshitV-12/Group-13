document.addEventListener('DOMContentLoaded', async () => {
  requireAuth();
  updateUserUI();
  await loadDashboard();

  // Quick Expense Form handler
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
        showToast(err.message || 'Quick entry failed.', 'danger');
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

let categoryChartInstance = null;

function formatCurrency(amount) {
  const value = Number(amount || 0);
  return new Intl.NumberFormat('en-IN', { style: 'currency', currency: 'INR', minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value);
}

function buildDistinctColors(count) {
  const colors = [];
  for (let i = 0; i < count; i++) {
    const hue = (i * 360) / (count || 1);
    colors.push(`hsl(${hue} 65% 58%)`);
  }
  return colors;
}

async function loadDashboard() {
  try {
    const [summary, activeRule, health, notifications] = await Promise.all([
      api.get('/dashboard/summary'),
      api.get('/rules/active').catch(() => null),
      api.get('/insights/health-score').catch(() => null),
      api.get('/notifications?unreadOnly=true').catch(() => [])
    ]);

    renderKPIs(summary, health);
    renderNotificationsBanner(notifications);
    renderActiveRuleWidget(activeRule);
    renderCategoryChart(summary.categoryExpenses);
    renderRecentTransactions(summary.recentTransactions);
  } catch (err) {
    console.error('Failed to load dashboard:', err);
    showToast('Failed to load dashboard data.', 'danger');
  }
}

function renderNotificationsBanner(notifications) {
  const container = document.getElementById('dashNotificationBanner');
  if (!container) return;

  if (!notifications || notifications.length === 0) {
    container.innerHTML = '';
    return;
  }

  const notif = notifications[0]; // Top unread notification
  let bg = '#fff8e6';
  let border = 'var(--warning)';
  let icon = 'fa-exclamation-triangle text-warning';

  if (notif.type === 'BudgetExceeded') {
    bg = '#ffeef0';
    border = 'var(--danger)';
    icon = 'fa-exclamation-circle text-danger';
  } else if (notif.type === 'GoalAchieved') {
    bg = '#e8f8f0';
    border = 'var(--success)';
    icon = 'fa-award text-success';
  }

  container.innerHTML = `
    <div style="background: ${bg}; border-left: 4px solid ${border}; border-radius: 8px; padding: 12px 16px; display: flex; justify-content: space-between; align-items: center;">
      <div style="display: flex; align-items: center; gap: 10px;">
        <i class="fas ${icon}" style="font-size: 1.1rem;"></i>
        <div>
          <strong style="font-size: 0.9rem; color: #1e293b;">${notif.title}</strong>
          <span style="font-size: 0.85rem; color: #475569; margin-left: 8px;">${notif.message}</span>
        </div>
      </div>
      <a href="budget.html" class="btn btn-sm btn-outline-primary" style="font-size:0.78rem;">View Details</a>
    </div>
  `;
}

function renderKPIs(summary, health) {
  const inc = document.getElementById('totalIncome');
  const exp = document.getElementById('totalExpenses');
  const sav = document.getElementById('totalSavings');
  const scoreEl = document.getElementById('savingsPercentage');

  if (inc) inc.textContent = formatCurrency(summary.totalIncome);
  if (exp) exp.textContent = formatCurrency(summary.totalExpenses);
  if (sav) sav.textContent = formatCurrency(summary.totalSavings);

  const savingsRate = summary.totalIncome > 0 ? ((summary.totalIncome - summary.totalExpenses) / summary.totalIncome) * 100 : 0;
  if (scoreEl) scoreEl.textContent = `${Number(savingsRate).toFixed(1)}%`;
}

function renderActiveRuleWidget(status) {
  const titleEl = document.getElementById('dashRuleTitle');
  const bucketsContainer = document.getElementById('dashRuleBuckets');
  if (!bucketsContainer) return;

  if (!status) {
    if (titleEl) titleEl.textContent = 'AI / Financial Insight: No active strategy';
    bucketsContainer.innerHTML = '<div class="text-muted p-2">Add some transactions to generate a useful AI financial summary.</div>';
    return;
  }

  if (titleEl) titleEl.textContent = `AI / Financial Insight: ${status.ruleName}`;

  bucketsContainer.innerHTML = status.buckets.map(b => {
    const isOver = b.status.includes('Over') || b.status.includes('Below');
    const badgeClass = isOver ? 'badge-danger' : 'badge-success';

    return `
      <div class="card p-3 bg-light">
        <div class="d-flex justify-content-between align-items-center mb-1">
          <strong class="font-sm">${b.bucketName} (${b.targetPercentage}%)</strong>
          <span class="badge ${badgeClass} text-xs">${b.status}</span>
        </div>
        <div class="text-xs text-muted mb-1">Target: ${formatCurrency(b.targetAmount)} | Actual: ${formatCurrency(b.actualSpent)}</div>
        <div class="progress-bar-bg" style="height: 6px;">
          <div class="progress-bar-fill ${isOver ? 'bg-danger' : 'bg-success'}" style="width: ${Math.min(100, Math.round((b.actualSpent / (b.targetAmount || 1)) * 100))}%"></div>
        </div>
      </div>
    `;
  }).join('');
}

function renderCategoryChart(categories) {
  const ctx = document.getElementById('categoryChart')?.getContext('2d');
  if (!ctx) return;

  if (!categories || categories.length === 0) {
    ctx.clearRect(0, 0, ctx.canvas.width, ctx.canvas.height);
    return;
  }

  const labels = categories.map(c => c.categoryName);
  const data = categories.map(c => Number(c.amount || 0));
  const palette = buildDistinctColors(categories.length);
  const colors = palette;

  if (categoryChartInstance) {
    categoryChartInstance.destroy();
  }

  categoryChartInstance = new Chart(ctx, {
    type: 'doughnut',
    data: {
      labels,
      datasets: [{
        data,
        backgroundColor: colors,
        borderWidth: 2,
        borderColor: '#ffffff'
      }]
    },
    options: {
      responsive: true,
      maintainAspectRatio: false,
      plugins: {
        legend: { position: 'right', labels: { boxWidth: 12, font: { family: 'Plus Jakarta Sans', size: 12 } } },
        tooltip: {
          callbacks: {
            label: (context) => `${context.label}: ${formatCurrency(context.parsed)}`
          }
        }
      }
    },
    plugins: [{
      id: 'centerText',
      afterDraw(chart) {
        const { ctx, chartArea } = chart;
        if (!chartArea) return;
        const total = chart.data.datasets[0].data.reduce((sum, value) => sum + Number(value || 0), 0);
        const x = (chartArea.left + chartArea.right) / 2;
        const y = (chartArea.top + chartArea.bottom) / 2;
        ctx.save();
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = '#1f2937';
        ctx.font = '600 14px sans-serif';
        ctx.fillText('Total spend', x, y - 10);
        ctx.font = '700 20px sans-serif';
        ctx.fillText(formatCurrency(total), x, y + 18);
        ctx.restore();
      }
    }]
  });
}

function renderRecentTransactions(transactions) {
  const container = document.getElementById('recentTxnsList');
  if (!container) return;

  if (!transactions || transactions.length === 0) {
    container.innerHTML = '<div class="text-muted text-center p-3">No transactions recorded yet.</div>';
    return;
  }

  container.innerHTML = transactions.map(t => {
    const date = new Date(t.transactionDate).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' });
    const amount = Number(t.amount || 0);
    const sign = t.type === 'Income' ? '+' : '-';
    const amountCss = t.type === 'Income' ? 'text-success' : 'text-danger';
    return `
      <div class="d-flex justify-content-between align-items-center py-2 border-bottom">
        <div>
          <strong class="font-sm d-block">${t.description || 'Expense'}</strong>
          <span class="text-xs text-muted">${date} • ${t.categoryName || 'Uncategorized'} • ${t.paymentMethod || 'UPI'}</span>
        </div>
        <div class="text-end">
          <strong class="font-sm ${amountCss}">${sign}${formatCurrency(amount)}</strong>
        </div>
      </div>
    `;
  }).join('');
}
