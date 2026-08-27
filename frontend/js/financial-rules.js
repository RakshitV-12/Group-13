document.addEventListener('DOMContentLoaded', () => {
  requireAuth();
  loadFinancialRulesData();

  document.getElementById('updateIncomeBtn')?.addEventListener('click', updateIncome);
  document.getElementById('customRuleForm')?.addEventListener('submit', handleSaveCustomRule);
});

let currentActiveRule = null;
let editingRuleId = null;

async function loadFinancialRulesData() {
  try {
    const [allRules, activeStatus] = await Promise.all([
      api.get('/rules'),
      api.get('/rules/active').catch(() => null)
    ]);

    currentActiveRule = activeStatus;
    renderActiveRuleBanner(activeStatus);
    renderStrategyCards(allRules, activeStatus?.ruleId);
  } catch (err) {
    showToast(err.message || 'Failed to load financial rules.', 'danger');
  }
}

function renderActiveRuleBanner(status) {
  const titleEl = document.getElementById('activeRuleTitle');
  const descEl = document.getElementById('activeRuleDesc');
  const incomeInput = document.getElementById('monthlyIncomeInput');
  const gridEl = document.getElementById('bucketStatusGrid');

  if (!status) {
    titleEl.textContent = 'No Strategy Selected';
    descEl.textContent = 'Select one of the strategies below to start evaluating your spending against your financial goals.';
    gridEl.innerHTML = '<div class="text-muted p-3">Select a strategy below to calculate your target allocations.</div>';
    return;
  }

  titleEl.textContent = `${status.ruleName} Strategy`;
  descEl.textContent = status.ruleDescription || 'Active personal financial allocation framework.';
  if (incomeInput) incomeInput.value = status.monthlyIncome;

  gridEl.innerHTML = status.buckets.map(b => {
    const isOver = b.status.includes('Over') || b.status.includes('Below');
    const badgeClass = isOver ? 'badge-danger' : 'badge-success';
    const pctSpent = b.targetAmount > 0 ? Math.min(100, Math.round((b.actualSpent / b.targetAmount) * 100)) : 0;

    return `
      <div class="card bucket-card p-3">
        <div class="d-flex justify-content-between align-items-center mb-2">
          <span class="font-bold font-lg">${b.bucketName} (${b.targetPercentage}%)</span>
          <span class="badge ${badgeClass}">${b.status}</span>
        </div>
        <div class="metrics-row d-flex justify-content-between text-muted small mb-2">
          <span>Target: <strong>₹${b.targetAmount.toLocaleString()}</strong></span>
          <span>Actual: <strong>₹${b.actualSpent.toLocaleString()}</strong></span>
        </div>
        <div class="progress-bar-bg mb-2">
          <div class="progress-bar-fill ${isOver ? 'bg-danger' : 'bg-success'}" style="width: ${pctSpent}%"></div>
        </div>
        <div class="text-muted text-xs">Categories: ${b.categoriesCsv || 'All Expenses'}</div>
      </div>
    `;
  }).join('');
}

function renderStrategyCards(rules, activeRuleId) {
  const container = document.getElementById('strategyCardsGrid');
  if (!container) return;

  container.innerHTML = rules.map(r => {
    const isActive = r.ruleId === activeRuleId;
    const isCustom = !r.isSystemDefault;

    const allocationsHtml = r.allocations.map(a =>
      `<span class="badge badge-light text-dark me-1 mb-1">${a.bucketName}: ${a.percentage}%</span>`
    ).join('');

    return `
      <div class="card strategy-card ${isActive ? 'active-border' : ''} p-4">
        <div class="d-flex justify-content-between align-items-start mb-2">
          <div>
            <h3 class="card-title mb-1">${r.name}</h3>
            <p class="text-muted text-sm">${r.description}</p>
          </div>
          ${isActive ? '<span class="badge badge-primary"><i class="fa-solid fa-check"></i> Active</span>' : ''}
        </div>
        <div class="allocations-preview my-3">
          ${allocationsHtml}
        </div>
        <div class="d-flex justify-content-end gap-2 mt-3">
          ${isCustom ? `
            <button class="btn btn-outline-secondary btn-sm" onclick="editCustomRule(${r.ruleId})">
              <i class="fa-solid fa-pen"></i> Edit
            </button>
          ` : ''}
          ${!isActive ? `
            <button class="btn btn-primary btn-sm" onclick="activateStrategy(${r.ruleId})">
              Select Strategy
            </button>
          ` : '<button class="btn btn-secondary btn-sm" disabled>Selected</button>'}
        </div>
      </div>
    `;
  }).join('');
}

async function activateStrategy(ruleId) {
  try {
    const incomeInput = document.getElementById('monthlyIncomeInput');
    const income = incomeInput ? parseFloat(incomeInput.value) || 50000 : 50000;

    const res = await api.post('/rules/activate', { ruleId, monthlyIncome: income });
    showToast('Financial strategy activated successfully!', 'success');
    loadFinancialRulesData();
  } catch (err) {
    showToast(err.message || 'Failed to activate strategy.', 'danger');
  }
}

async function updateIncome() {
  if (!currentActiveRule) {
    showToast('Please select an active strategy first.', 'warning');
    return;
  }

  const incomeInput = document.getElementById('monthlyIncomeInput');
  const income = parseFloat(incomeInput.value);
  if (!income || income <= 0) {
    showToast('Please enter a valid monthly income.', 'warning');
    return;
  }

  try {
    await api.post('/rules/activate', { ruleId: currentActiveRule.ruleId, monthlyIncome: income });
    showToast('Monthly income updated and targets recalculated!', 'success');
    loadFinancialRulesData();
  } catch (err) {
    showToast(err.message || 'Failed to update income.', 'danger');
  }
}

// Modal handling for Custom Rules
function openCustomRuleModal(ruleToEdit = null) {
  editingRuleId = ruleToEdit ? ruleToEdit.ruleId : null;
  const modal = document.getElementById('customRuleModal');
  const title = document.getElementById('customRuleModalTitle');
  const nameInput = document.getElementById('ruleName');
  const descInput = document.getElementById('ruleDesc');
  const container = document.getElementById('allocationsContainer');

  if (title) title.textContent = editingRuleId ? 'Edit Custom Strategy' : 'Create Custom Financial Strategy';
  if (nameInput) nameInput.value = ruleToEdit ? ruleToEdit.name : '';
  if (descInput) descInput.value = ruleToEdit ? ruleToEdit.description : '';

  container.innerHTML = '';
  if (ruleToEdit && ruleToEdit.allocations?.length) {
    ruleToEdit.allocations.forEach(a => addAllocationRow(a.bucketName, a.percentage, a.categoryNamesCsv));
  } else {
    // Default initial rows (e.g., Needs 50%, Wants 30%, Savings 20%)
    addAllocationRow('Needs', 50, 'Food,Bills,Transport,Healthcare,Rent');
    addAllocationRow('Wants', 30, 'Shopping,Entertainment,Travel');
    addAllocationRow('Savings', 20, 'Investment');
  }

  updateAllocationTotal();
  if (modal) modal.style.display = 'flex';
}

function closeCustomRuleModal() {
  const modal = document.getElementById('customRuleModal');
  if (modal) modal.style.display = 'none';
  editingRuleId = null;
}

function addAllocationRow(bucketName = '', percentage = 0, categoriesCsv = '') {
  const container = document.getElementById('allocationsContainer');
  if (!container) return;

  const div = document.createElement('div');
  div.className = 'allocation-row d-flex gap-2 align-items-center mb-2';
  div.innerHTML = `
    <input type="text" class="form-control form-control-sm bucket-name" placeholder="Bucket Name (e.g. Needs)" value="${bucketName}" required>
    <div class="input-group input-group-sm" style="width: 140px;">
      <input type="number" class="form-control bucket-pct" placeholder="%" value="${percentage}" step="0.5" min="1" max="100" required onchange="updateAllocationTotal()" oninput="updateAllocationTotal()">
      <span class="input-group-text">%</span>
    </div>
    <input type="text" class="form-control form-control-sm bucket-cats" placeholder="Categories (e.g. Food,Rent)" value="${categoriesCsv}">
    <button type="button" class="btn btn-sm btn-outline-danger" onclick="this.parentElement.remove(); updateAllocationTotal();">&times;</button>
  `;
  container.appendChild(div);
  updateAllocationTotal();
}

function updateAllocationTotal() {
  const pctInputs = document.querySelectorAll('.bucket-pct');
  let total = 0;
  pctInputs.forEach(i => total += (parseFloat(i.value) || 0));

  const badge = document.getElementById('allocationTotalBadge');
  if (badge) {
    badge.textContent = `Total: ${total.toFixed(1)}%`;
    if (Math.abs(total - 100) < 0.01) {
      badge.className = 'badge badge-success';
    } else {
      badge.className = 'badge badge-danger';
    }
  }
  return total;
}

async function handleSaveCustomRule(e) {
  e.preventDefault();
  const name = document.getElementById('ruleName')?.value.trim();
  const description = document.getElementById('ruleDesc')?.value.trim();

  const total = updateAllocationTotal();
  if (Math.abs(total - 100) >= 0.01) {
    showToast(`Allocation percentages must total exactly 100%. Current sum: ${total.toFixed(1)}%`, 'danger');
    return;
  }

  const rows = document.querySelectorAll('.allocation-row');
  const allocations = [];
  rows.forEach(r => {
    const bName = r.querySelector('.bucket-name')?.value.trim();
    const bPct = parseFloat(r.querySelector('.bucket-pct')?.value) || 0;
    const bCats = r.querySelector('.bucket-cats')?.value.trim();
    if (bName && bPct > 0) {
      allocations.push({ bucketName: bName, percentage: bPct, categoryNamesCsv: bCats });
    }
  });

  try {
    const payload = { name, description, allocations };
    let saved;
    if (editingRuleId) {
      saved = await api.put(`/rules/${editingRuleId}`, payload);
      showToast('Custom strategy updated successfully!', 'success');
    } else {
      saved = await api.post('/rules/custom', payload);
      showToast('Custom strategy created!', 'success');
    }

    closeCustomRuleModal();
    if (saved && saved.ruleId) {
      await activateStrategy(saved.ruleId);
    }
  } catch (err) {
    showToast(err.message || 'Failed to save custom strategy.', 'danger');
  }
}

async function editCustomRule(ruleId) {
  try {
    const allRules = await api.get('/rules');
    const rule = allRules.find(r => r.ruleId === ruleId);
    if (rule) {
      openCustomRuleModal(rule);
    }
  } catch (err) {
    showToast('Failed to fetch rule details.', 'danger');
  }
}
