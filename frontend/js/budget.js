// =========================================================================
// Budget & Goal Management Logic
// =========================================================================

document.addEventListener('DOMContentLoaded', async () => {
  requireAuth();
  updateUserUI();
  await loadCategoryOptions();
  await loadBudgets();
  await loadGoals();
  await loadNotifications();

  // Preset month & year in budget form
  const now = new Date();
  const monthSelect = document.getElementById('budgetMonth');
  const yearInput = document.getElementById('budgetYear');
  if (monthSelect) monthSelect.value = (now.getMonth() + 1).toString();
  if (yearInput) yearInput.value = now.getFullYear().toString();

  // Budget Form Submit
  const budgetForm = document.getElementById('setBudgetForm');
  if (budgetForm) {
    budgetForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const editId = document.getElementById('editBudgetId')?.value;
      const name = document.getElementById('budgetName').value.trim();
      const catVal = document.getElementById('budgetCategory').value;
      const categoryId = catVal ? parseInt(catVal) : null;
      const amount = parseFloat(document.getElementById('budgetAmount').value);
      const periodMonth = parseInt(document.getElementById('budgetMonth').value);
      const periodYear = parseInt(document.getElementById('budgetYear').value);
      const thresholdPercent = parseFloat(document.getElementById('budgetThreshold').value) || 80;

      try {
        if (editId) {
          await api.put(`/budgets/${editId}`, { name, categoryId, amount, periodMonth, periodYear, thresholdPercent });
          showToast('Budget updated successfully!', 'success');
        } else {
          await api.post('/budgets', { name, categoryId, amount, periodMonth, periodYear, thresholdPercent });
          showToast('Budget saved successfully!', 'success');
        }
        closeBudgetModal();
        await loadBudgets();
        await loadNotifications();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }

  // Goal Form Submit
  const goalForm = document.getElementById('goalForm');
  if (goalForm) {
    goalForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const editId = document.getElementById('editGoalId').value;
      const name = document.getElementById('goalName').value.trim();
      const targetAmount = parseFloat(document.getElementById('goalTargetAmount').value);
      const currentAmount = parseFloat(document.getElementById('goalCurrentAmount').value) || 0;
      const dueDate = document.getElementById('goalDueDate').value;
      const notes = document.getElementById('goalNotes').value.trim();

      try {
        if (editId) {
          await api.put(`/goals/${editId}`, { name, targetAmount, currentAmount, dueDate, notes });
          showToast('Goal updated successfully!', 'success');
        } else {
          await api.post('/goals', { name, targetAmount, currentAmount, dueDate, notes });
          showToast('Goal created successfully!', 'success');
        }
        closeGoalModal();
        await loadGoals();
        await loadNotifications();
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

let cachedBudgetsList = [];

async function loadBudgets() {
  try {
    const statuses = await api.get('/budgets/status');
    cachedBudgetsList = statuses;
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
        <button class="btn btn-primary" onclick="openBudgetModal()"><i class="fas fa-plus"></i> Create First Budget</button>
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
            <h3 style="font-size: 1.15rem; font-weight: 700; margin-bottom:4px;">${b.name || (b.categoryName + ' Budget')}</h3>
            <span class="badge" style="background: ${badgeBg}; color: ${statusClass};">${statusText}</span>
          </div>
          <div style="display:flex; gap:6px;">
            <button class="btn btn-secondary btn-sm" onclick="openEditBudgetModal(${b.budgetId})" title="Edit Budget"><i class="fas fa-pen"></i></button>
            <button class="btn btn-secondary btn-sm" onclick="deleteBudget(${b.budgetId})" title="Delete Budget"><i class="fas fa-trash"></i></button>
          </div>
        </div>

        <div style="margin-bottom: 12px;">
          <div style="display: flex; justify-content: space-between; font-size: 0.9rem; font-weight: 600; margin-bottom: 6px;">
            <span>Budget: ₹${b.budgetAmount.toLocaleString('en-IN')}</span>
            <span>Spent: ₹${b.totalSpent.toLocaleString('en-IN')}</span>
          </div>
          <div class="category-meter" style="height: 10px; background: #e2e8f0; border-radius: 6px; overflow: hidden;">
            <div class="category-meter-bar" style="width: ${Math.min(100, b.utilizationPercentage)}%; height: 100%; background: ${statusClass};"></div>
          </div>
        </div>

        <div style="display: flex; justify-content: space-between; font-size: 0.82rem; color: var(--gray-500); margin-top: 12px; padding-top: 12px; border-top: 1px solid var(--gray-100);">
          <span>Remaining: <strong style="color: ${b.remainingBudget > 0 ? 'var(--success)' : 'var(--danger)'};">₹${b.remainingBudget.toLocaleString('en-IN')}</strong></span>
          <span>Used: <strong>${b.utilizationPercentage}%</strong></span>
        </div>
      </div>
    `;
  }).join('');
}

async function loadGoals() {
  try {
    const goals = await api.get('/goals');
    renderGoalCards(goals);
  } catch (err) {
    console.error('Failed to load goals:', err);
  }
}

function renderGoalCards(goals) {
  const container = document.getElementById('goalsCardsContainer');
  if (!container) return;

  if (!goals || goals.length === 0) {
    container.innerHTML = `
      <div style="grid-column: 1 / -1; background: var(--white); padding: 40px; text-align: center; border-radius: var(--radius-lg); border: 1px dashed var(--gray-300);">
        <i class="fas fa-bullseye" style="font-size: 36px; color: var(--primary); margin-bottom: 12px;"></i>
        <h3 style="font-size: 1.1rem; font-weight: 700; margin-bottom: 6px;">No Goals Set</h3>
        <p style="color: var(--gray-500); margin-bottom: 18px;">Set savings targets (e.g., Buy New Laptop) to track your progress.</p>
        <button class="btn btn-primary" onclick="openGoalModal()"><i class="fas fa-plus"></i> Create Goal</button>
      </div>
    `;
    return;
  }

  container.innerHTML = goals.map(g => {
    const isAchieved = g.status === 'Achieved' || g.currentAmount >= g.targetAmount;
    const progress = Math.min(100, g.progressPercentage);
    const dueDateStr = new Date(g.dueDate).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });

    return `
      <div class="goal-card ${isAchieved ? 'achieved-state' : ''}">
        <div style="display: flex; justify-content: space-between; align-items: flex-start; margin-bottom: 12px;">
          <div>
            <h3 style="font-size: 1.1rem; font-weight: 700; margin: 0 0 6px 0;">${g.name}</h3>
            <span class="goal-badge ${isAchieved ? 'badge-achieved' : 'badge-in-progress'}">
              ${isAchieved ? '<i class="fas fa-check-circle"></i> ✓ Goal Achieved' : '<i class="fas fa-spinner"></i> In Progress'}
            </span>
          </div>
          <div style="display:flex; gap:6px;">
            <button class="btn btn-secondary btn-sm" onclick="openEditGoalModal(${g.goalId})" title="Edit Goal"><i class="fas fa-pen"></i></button>
            <button class="btn btn-secondary btn-sm" onclick="deleteGoal(${g.goalId})" title="Delete Goal"><i class="fas fa-trash"></i></button>
          </div>
        </div>

        <div style="margin-bottom: 12px;">
          <div style="font-size: 1.25rem; font-weight: 800; color: var(--gray-900); margin-bottom: 4px;">
            ₹${g.currentAmount.toLocaleString('en-IN')} <span style="font-size: 0.9rem; color: var(--gray-500); font-weight: 500;">/ ₹${g.targetAmount.toLocaleString('en-IN')}</span>
          </div>
          <div style="display: flex; justify-content: space-between; font-size: 0.82rem; color: var(--gray-600); margin-bottom: 6px;">
            <span><strong>${progress}%</strong> completed</span>
            <span>Remaining: <strong>₹${g.remainingAmount.toLocaleString('en-IN')}</strong></span>
          </div>
          <div style="height: 10px; background: #e2e8f0; border-radius: 6px; overflow: hidden;">
            <div style="width: ${progress}%; height: 100%; background: ${isAchieved ? 'var(--success)' : 'var(--primary)'}; border-radius: 6px;"></div>
          </div>
        </div>

        <div style="display: flex; justify-content: space-between; font-size: 0.8rem; color: var(--gray-500); padding-top: 10px; border-top: 1px solid var(--gray-100);">
          <span>Due: <strong>${dueDateStr}</strong></span>
          <span>${g.notes ? g.notes : ''}</span>
        </div>
      </div>
    `;
  }).join('');
}

async function loadNotifications() {
  try {
    const notifs = await api.get('/notifications');
    renderNotifications(notifs);
  } catch (err) {
    console.error('Failed to load notifications:', err);
  }
}

function renderNotifications(notifs) {
  const container = document.getElementById('activeNotifContainer');
  const badge = document.getElementById('notifBadge');
  const list = document.getElementById('notificationsList');

  const unread = notifs.filter(n => !n.IsRead && !n.isRead);
  if (badge) {
    if (unread.length > 0) {
      badge.textContent = unread.length;
      badge.style.display = 'inline-block';
    } else {
      badge.style.display = 'none';
    }
  }

  // Active top banners
  if (container) {
    const importantNotifs = notifs.slice(0, 3);
    container.innerHTML = importantNotifs.map(n => {
      let bannerClass = 'notif-banner';
      let icon = 'fa-exclamation-triangle';
      if (n.type === 'BudgetExceeded') {
        bannerClass = 'notif-banner exceeded';
        icon = 'fa-exclamation-circle text-danger';
      } else if (n.type === 'GoalAchieved') {
        bannerClass = 'notif-banner achieved';
        icon = 'fa-award text-success';
      }

      return `
        <div class="${bannerClass}">
          <div style="display:flex; align-items:center; gap:12px;">
            <i class="fas ${icon}" style="font-size:1.2rem;"></i>
            <div>
              <strong style="font-size:0.95rem; color:var(--gray-900);">${n.title}</strong>
              <div style="font-size:0.85rem; color:var(--gray-700); margin-top:2px;">${n.message}</div>
            </div>
          </div>
          <button class="btn btn-secondary btn-sm" onclick="markNotificationRead(${n.notificationId})"><i class="fas fa-times"></i> Dismiss</button>
        </div>
      `;
    }).join('');
  }

  // Notifications Modal list
  if (list) {
    if (notifs.length === 0) {
      list.innerHTML = `<div style="text-align:center; color:var(--gray-500); padding:20px;">No notifications.</div>`;
    } else {
      list.innerHTML = notifs.map(n => `
        <div style="background:#f8fafc; border:1px solid #e2e8f0; padding:12px; border-radius:8px; display:flex; justify-content:space-between; align-items:center;">
          <div>
            <div style="font-weight:700; font-size:0.9rem; color:var(--gray-900);">${n.title}</div>
            <div style="font-size:0.82rem; color:var(--gray-600); margin-top:2px;">${n.message}</div>
          </div>
          ${!n.isRead ? `<button class="btn btn-secondary btn-sm" onclick="markNotificationRead(${n.notificationId})">Read</button>` : '<span style="font-size:0.75rem; color:var(--gray-400);">Read</span>'}
        </div>
      `).join('');
    }
  }
}

function openBudgetModal() {
  const editIdEl = document.getElementById('editBudgetId');
  if (editIdEl) editIdEl.value = '';
  const titleEl = document.getElementById('budgetModalTitle');
  if (titleEl) titleEl.textContent = 'Create Budget';
  document.getElementById('budgetName').value = '';
  document.getElementById('budgetAmount').value = '';
  document.getElementById('budgetModal').classList.add('open');
}

function openEditBudgetModal(id) {
  const b = cachedBudgetsList.find(item => item.budgetId === id);
  if (!b) return;
  const editIdEl = document.getElementById('editBudgetId');
  if (editIdEl) editIdEl.value = b.budgetId;
  const titleEl = document.getElementById('budgetModalTitle');
  if (titleEl) titleEl.textContent = 'Edit Budget';
  document.getElementById('budgetName').value = b.name || '';
  document.getElementById('budgetAmount').value = b.budgetAmount || '';
  if (b.categoryId) {
    document.getElementById('budgetCategory').value = b.categoryId;
  } else {
    document.getElementById('budgetCategory').value = '';
  }
  document.getElementById('budgetMonth').value = b.periodMonth || (new Date().getMonth() + 1);
  document.getElementById('budgetYear').value = b.periodYear || new Date().getFullYear();
  document.getElementById('budgetThreshold').value = b.thresholdPercent || 80;
  document.getElementById('budgetModal').classList.add('open');
}

function closeBudgetModal() {
  document.getElementById('budgetModal').classList.remove('open');
}

function openGoalModal() {
  document.getElementById('editGoalId').value = '';
  document.getElementById('goalModalTitle').textContent = 'Create Goal';
  document.getElementById('goalName').value = '';
  document.getElementById('goalTargetAmount').value = '';
  document.getElementById('goalCurrentAmount').value = '0';
  document.getElementById('goalDueDate').value = '';
  document.getElementById('goalNotes').value = '';
  document.getElementById('goalModal').classList.add('open');
}

async function openEditGoalModal(id) {
  try {
    const goal = await api.get(`/goals/${id}`);
    document.getElementById('editGoalId').value = goal.goalId;
    document.getElementById('goalModalTitle').textContent = 'Edit Goal';
    document.getElementById('goalName').value = goal.name;
    document.getElementById('goalTargetAmount').value = goal.targetAmount;
    document.getElementById('goalCurrentAmount').value = goal.currentAmount;
    document.getElementById('goalDueDate').value = goal.dueDate.split('T')[0];
    document.getElementById('goalNotes').value = goal.notes || '';
    document.getElementById('goalModal').classList.add('open');
  } catch (err) {
    showToast(err.message, 'error');
  }
}

function closeGoalModal() {
  document.getElementById('goalModal').classList.remove('open');
}

function openNotificationsModal() {
  document.getElementById('notificationsModal').classList.add('open');
}

function closeNotificationsModal() {
  document.getElementById('notificationsModal').classList.remove('open');
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

async function deleteGoal(id) {
  if (!confirm('Are you sure you want to delete this financial goal?')) return;
  try {
    await api.delete(`/goals/${id}`);
    showToast('Goal deleted.', 'info');
    await loadGoals();
  } catch (err) {
    showToast(err.message, 'error');
  }
}

async function markNotificationRead(id) {
  try {
    await api.put(`/notifications/${id}/read`);
    await loadNotifications();
  } catch (err) {
    console.error('Error marking notification read:', err);
  }
}

async function markAllNotificationsRead() {
  try {
    await api.put('/notifications/read-all');
    await loadNotifications();
  } catch (err) {
    console.error('Error marking all notifications read:', err);
  }
}
