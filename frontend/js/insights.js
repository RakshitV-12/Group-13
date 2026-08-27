document.addEventListener('DOMContentLoaded', () => {
  requireAuth();
  loadInsightsData();
});

async function loadInsightsData() {
  try {
    const data = await api.get('/insights/overview');
    renderHealthScore(data.healthScore);
    renderPrediction(data.prediction);
    renderAnomalies(data.anomalies);
    renderRecurringExpenses(data.recurringExpenses, data.totalAnnualRecurring);
  } catch (err) {
    showToast(err.message || 'Failed to load insights & analytics.', 'danger');
  }
}

function renderHealthScore(health) {
  if (!health) return;

  const scoreEl = document.getElementById('healthScoreValue');
  const gradeEl = document.getElementById('healthGradeBadge');
  const gridEl = document.getElementById('componentScoresGrid');
  const recsEl = document.getElementById('recommendationsList');

  if (scoreEl) scoreEl.textContent = health.healthScore;
  if (gradeEl) {
    gradeEl.textContent = health.ratingGrade;
    if (health.healthScore >= 80) gradeEl.className = 'badge badge-success';
    else if (health.healthScore >= 60) gradeEl.className = 'badge badge-warning';
    else gradeEl.className = 'badge badge-danger';
  }

  if (gridEl && health.components) {
    gridEl.innerHTML = health.components.map(c => `
      <div class="card p-2 text-center bg-light">
        <span class="text-xs text-muted d-block">${c.name}</span>
        <strong class="font-lg text-dark">${c.earnedScore} / ${c.maxPoints}</strong>
        <span class="text-xs text-muted d-block mt-1">${c.description}</span>
      </div>
    `).join('');
  }

  if (recsEl && health.actionableRecommendations) {
    recsEl.innerHTML = health.actionableRecommendations.map(r => `
      <li class="mb-1"><i class="fa-solid fa-angle-right text-primary me-1"></i> ${r}</li>
    `).join('');
  }
}

function renderPrediction(p) {
  if (!p) return;

  const banner = document.getElementById('predictionBanner');
  const burn = document.getElementById('dailyBurnRate');
  const proj = document.getElementById('projectedExpense');
  const limit = document.getElementById('budgetLimit');
  const varEl = document.getElementById('projectedVariance');

  if (banner) {
    banner.className = `alert ${p.isExceedingBudgetRisk ? 'alert-danger' : 'alert-success'} p-3 mb-3`;
    banner.innerHTML = `<i class="fa-solid ${p.isExceedingBudgetRisk ? 'fa-triangle-exclamation' : 'fa-circle-check'} me-2"></i> ${p.message}`;
  }

  if (burn) burn.textContent = `₹${p.dailyBurnRate.toLocaleString()} / day`;
  if (proj) proj.textContent = `₹${p.projectedMonthEndExpense.toLocaleString()}`;
  if (limit) limit.textContent = `₹${p.monthlyBudgetLimit.toLocaleString()}`;
  if (varEl) {
    varEl.textContent = `₹${Math.abs(p.projectedVariance).toLocaleString()} ${p.projectedVariance > 0 ? 'Over' : 'Under'}`;
    varEl.className = p.projectedVariance > 0 ? 'text-danger font-bold' : 'text-success font-bold';
  }
}

function renderAnomalies(anomalies) {
  const container = document.getElementById('anomaliesList');
  if (!container) return;

  if (!anomalies || !anomalies.length) {
    container.innerHTML = `
      <div class="alert alert-success p-3 text-sm">
        <i class="fa-solid fa-circle-check me-2"></i> No spending anomalies detected this month. Category spending remains stable.
      </div>
    `;
    return;
  }

  container.innerHTML = anomalies.map(a => `
    <div class="alert alert-warning p-3 mb-2 text-sm d-flex justify-content-between align-items-center">
      <div>
        <strong>${a.categoryName} Spike</strong>: Spent ₹${a.currentSpend.toLocaleString()} (Historical Avg: ₹${a.historicalAverage.toLocaleString()})
        <div class="text-xs text-muted mt-1">${a.message}</div>
      </div>
      <span class="badge badge-danger">+${a.percentageIncrease}%</span>
    </div>
  `).join('');
}

function renderRecurringExpenses(recurring, annualTotal) {
  const tbody = document.getElementById('recurringTableBody');
  const annualEl = document.getElementById('totalAnnualRecurring');

  if (annualEl) annualEl.textContent = `₹${(annualTotal || 0).toLocaleString()} / yr`;
  if (!tbody) return;

  if (!recurring || !recurring.length) {
    tbody.innerHTML = `<tr><td colspan="5" class="text-center text-muted p-3">No recurring expense patterns detected yet. Add transactions over multiple months to identify recurring commitments.</td></tr>`;
    return;
  }

  tbody.innerHTML = recurring.map(r => `
    <tr>
      <td><strong>${r.title}</strong></td>
      <td><span class="badge badge-light">${r.categoryName}</span></td>
      <td class="font-bold text-dark">₹${r.monthlyAmount.toLocaleString()}</td>
      <td class="text-primary font-bold">₹${r.annualCost.toLocaleString()}</td>
      <td><span class="badge badge-secondary">${r.frequency}</span></td>
    </tr>
  `).join('');
}
