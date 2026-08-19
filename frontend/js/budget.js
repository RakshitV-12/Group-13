// =========================================================================
// Budget Management Logic
// =========================================================================

document.addEventListener('DOMContentLoaded', async () => {
  requireAuth();
  updateUserUI();
  await loadCategoryOptions();
  await loadBudgets();

  const budgetForm = document.getElementById('setBudgetForm');
  if (budgetForm) {
    budgetForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const catVal = document.getElementById('budgetCategory').value;
      const categoryId = catVal ? parseInt(catVal) : null;
      const amount = parseFloat(document.getElementById('budgetAmount').value);
      const thresholdPercent = parseFloat(document.getElementById('budgetThreshold').value) || 80;

      const now = new Date();
      const periodMonth = now.getMonth() + 1;
      const periodYear = now.getFullYear();

      try {
        await api.post('/budgets', { categoryId, amount, periodMonth, periodYear, thresholdPercent });
        showToast('Budget saved successfully!', 'success');
        document.getElementById('budgetAmount').value = '';
        closeBudgetModal();
        await loadBudgets();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }
});

async function loadCategoryOptions() {
  try {
    const categories = await api.get('/categories');
    const select = document.getElementById('budgetCategory');
    if (select) {
      select.innerHTML = '<option value="">Overall Monthly Budget (All Categories)</option>' +
        categories.filter(c => c.type === 'Expense').map(c => `<option value="${c.categoryId}">${c.name}</option>`).join('');
    }
  } catch (err) {
    console.error('Failed to load categories:', err);
  }
}

async function loadBudgets() {
  try {
    const statuses = await api.get('/budgets/status');
    renderBudgetCards(statuses);
  } catch (err) {
    console.error('Failed to load budgets:', err);
  }
}

function renderBudgetCards(budgets) {
  const container = document.getElementById('budgetCardsContainer');
  if (!container) return;

  if (!budgets || budgets.length === 0) {
    container.innerHTML = `
      <div style="grid-column: 1 / -1; background: var(--white); padding: 48px; text-align: center; border-radius: var(--radius-lg); border: 1px dashed var(--gray-300);">
        <i class="fas fa-wallet" style="font-size: 40px; color: var(--primary); margin-bottom: 12px;"></i>
        <h3 style="font-size: 1.2rem; font-weight: 700; margin-bottom: 6px;">No Budgets Configured</h3>
        <p style="color: var(--gray-500); margin-bottom: 20px;">Set a monthly spending cap or category budgets to stay on track.</p>
        <button class="btn btn-primary" onclick="openBudgetModal()"><i class="fas fa-plus"></i> Set First Budget</button>
      </div>
    `;
    return;
  }

  container.innerHTML = budgets.map(b => {
    let statusClass = 'var(--success)';
    let statusText = 'Normal';
    let badgeBg = 'var(--success-light)';

    if (b.isExceeded) {
      statusClass = 'var(--danger)';
      statusText = 'OVER BUDGET';
      badgeBg = 'var(--danger-light)';
    } else if (b.isWarning) {
      statusClass = 'var(--warning)';
      statusText = `WARNING (${b.utilizationPercentage}% >= ${b.thresholdPercent}%)`;
      badgeBg = 'var(--warning-light)';
    }

    return `
      <div class="kpi-card" style="border-top: 4px solid ${statusClass};">
        <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 16px;">
          <div>
            <h3 style="font-size: 1.15rem; font-weight: 700;">${b.categoryName} Budget</h3>
            <span class="badge" style="background: ${badgeBg}; color: ${statusClass}; margin-top: 4px;">${statusText}</span>
          </div>
          <button class="btn btn-secondary btn-sm" onclick="deleteBudget(${b.budgetId})"><i class="fas fa-trash"></i></button>
        </div>

        <div style="margin-bottom: 12px;">
          <div style="display: flex; justify-content: space-between; font-size: 0.9rem; font-weight: 600; margin-bottom: 6px;">
            <span>Spent: ₹${b.totalSpent.toLocaleString('en-IN')}</span>
            <span>Limit: ₹${b.budgetAmount.toLocaleString('en-IN')}</span>
          </div>
          <div class="category-meter" style="height: 10px;">
            <div class="category-meter-bar" style="width: ${Math.min(100, b.utilizationPercentage)}%; background: ${statusClass};"></div>
          </div>
        </div>

        <div style="display: flex; justify-content: space-between; font-size: 0.8rem; color: var(--gray-500); margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--gray-100);">
          <span>Remaining: <strong style="color: ${b.remainingBudget > 0 ? 'var(--success)' : 'var(--danger)'};">₹${b.remainingBudget.toLocaleString('en-IN')}</strong></span>
          <span>Alert Threshold: ${b.thresholdPercent}%</span>
        </div>
      </div>
    `;
  }).join('');
}

function openBudgetModal() {
  document.getElementById('budgetModal').classList.add('open');
}

function closeBudgetModal() {
  document.getElementById('budgetModal').classList.remove('open');
}

async function deleteBudget(id) {
  if (!confirm('Are you sure you want to remove this budget?')) return;
  try {
    await api.delete(`/budgets/${id}`);
    showToast('Budget deleted.', 'info');
    await loadBudgets();
  } catch (err) {
    showToast(err.message, 'error');
  }
}
