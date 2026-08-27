// =========================================================================
// Standalone Financial Goals Logic
// =========================================================================

document.addEventListener('DOMContentLoaded', async () => {
  requireAuth();
  updateUserUI();
  await loadGoals();

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
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }
});

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
              ${isAchieved ? '<i class="fas fa-check-circle"></i> 🎉 Goal Achieved' : '<i class="fas fa-spinner"></i> In Progress'}
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
