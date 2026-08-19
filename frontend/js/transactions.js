// =========================================================================
// Transactions Management (Manual & Quick Entry, Filtering & CRUD)
// =========================================================================

let currentPage = 1;
let categoriesList = [];

document.addEventListener('DOMContentLoaded', async () => {
  requireAuth();
  updateUserUI();
  await loadCategories();
  await loadTransactions();

  // Quick Entry Bar
  const quickForm = document.getElementById('quickExpenseForm');
  if (quickForm) {
    quickForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const input = document.getElementById('quickInput').value.trim();
      const pMethod = document.getElementById('quickPaymentMethod')?.value || 'UPI';

      if (!input) return;

      try {
        const res = await api.post('/transactions/quick', { input, paymentMethod: pMethod });
        showToast(`Auto-Categorized: "${res.description}" -> ${res.categoryName} (₹${res.amount})`, 'success');
        document.getElementById('quickInput').value = '';
        await loadTransactions();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }

  // Filter Listeners
  const searchInput = document.getElementById('searchInput');
  const catFilter = document.getElementById('categoryFilter');
  const typeFilter = document.getElementById('typeFilter');

  if (searchInput) searchInput.addEventListener('input', debounce(() => { currentPage = 1; loadTransactions(); }, 300));
  if (catFilter) catFilter.addEventListener('change', () => { currentPage = 1; loadTransactions(); });
  if (typeFilter) typeFilter.addEventListener('change', () => { currentPage = 1; loadTransactions(); });

  // Manual Add / Edit Form Submit
  const manualForm = document.getElementById('manualTransactionForm');
  if (manualForm) {
    manualForm.addEventListener('submit', async (e) => {
      e.preventDefault();
      const editId = document.getElementById('editTransactionId').value;
      const amount = parseFloat(document.getElementById('manualAmount').value);
      const categoryId = parseInt(document.getElementById('manualCategory').value);
      const type = document.getElementById('manualType').value;
      const paymentMethod = document.getElementById('manualPaymentMethod').value;
      const transactionDate = document.getElementById('manualDate').value || new Date().toISOString();
      const description = document.getElementById('manualDescription').value.trim();

      const payload = { amount, categoryId, type, paymentMethod, transactionDate, description };

      try {
        if (editId) {
          await api.put(`/transactions/${editId}`, payload);
          showToast('Transaction updated successfully!', 'success');
        } else {
          await api.post('/transactions', payload);
          showToast('Transaction created successfully!', 'success');
        }
        closeModal();
        await loadTransactions();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }
});

async function loadCategories() {
  try {
    categoriesList = await api.get('/categories');
    
    // Populate filter dropdown
    const filterSelect = document.getElementById('categoryFilter');
    const modalSelect = document.getElementById('manualCategory');

    if (filterSelect) {
      filterSelect.innerHTML = '<option value="">All Categories</option>' +
        categoriesList.map(c => `<option value="${c.categoryId}">${c.name}</option>`).join('');
    }

    if (modalSelect) {
      modalSelect.innerHTML = categoriesList.map(c => `<option value="${c.categoryId}">${c.name}</option>`).join('');
    }
  } catch (err) {
    console.error('Failed to load categories:', err);
  }
}

async function loadTransactions() {
  const search = document.getElementById('searchInput')?.value.trim() || '';
  const categoryId = document.getElementById('categoryFilter')?.value || '';
  const type = document.getElementById('typeFilter')?.value || '';

  let endpoint = `/transactions?page=${currentPage}&pageSize=15`;
  if (search) endpoint += `&search=${encodeURIComponent(search)}`;
  if (categoryId) endpoint += `&categoryId=${categoryId}`;
  if (type) endpoint += `&type=${type}`;

  try {
    const data = await api.get(endpoint);
    renderTable(data.items);
    renderPagination(data);
  } catch (err) {
    console.error('Failed to load transactions:', err);
  }
}

function renderTable(items) {
  const tbody = document.getElementById('transactionsTableBody');
  if (!tbody) return;

  if (!items || items.length === 0) {
    tbody.innerHTML = '<tr><td colspan="7" style="text-align: center; color: var(--gray-400); padding: 32px;">No transactions found.</td></tr>';
    return;
  }

  tbody.innerHTML = items.map(t => `
    <tr>
      <td>${new Date(t.transactionDate).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' })}</td>
      <td><strong>${t.description || '—'}</strong></td>
      <td>
        <span class="badge" style="background: ${t.categoryColor || '#6c757d'}15; color: ${t.categoryColor || '#6c757d'};">
          <i class="fas fa-${t.categoryIcon || 'tag'}"></i> ${t.categoryName}
        </span>
      </td>
      <td><span class="badge badge-${t.type.toLowerCase()}">${t.type}</span></td>
      <td><span class="badge badge-${t.paymentMethod.toLowerCase()}">${t.paymentMethod}</span></td>
      <td style="font-weight: 700; color: ${t.type === 'Income' ? 'var(--success)' : 'var(--danger)'};">
        ${t.type === 'Income' ? '+' : '-'}₹${t.amount.toLocaleString('en-IN', { minimumFractionDigits: 2 })}
      </td>
      <td>
        <div style="display: flex; gap: 8px;">
          <button class="btn btn-secondary btn-sm" onclick="editTransaction(${t.transactionId})"><i class="fas fa-edit"></i></button>
          <button class="btn btn-danger btn-sm" onclick="deleteTransaction(${t.transactionId})"><i class="fas fa-trash"></i></button>
        </div>
      </td>
    </tr>
  `).join('');
}

function renderPagination(data) {
  const container = document.getElementById('paginationControls');
  if (!container) return;

  container.innerHTML = `
    <span style="font-size: 0.85rem; color: var(--gray-500);">Page ${data.pageNumber} of ${data.totalPages || 1} (${data.totalCount} total)</span>
    <div style="display: flex; gap: 8px;">
      <button class="btn btn-secondary btn-sm" ${data.pageNumber <= 1 ? 'disabled' : ''} onclick="changePage(${data.pageNumber - 1})">Previous</button>
      <button class="btn btn-secondary btn-sm" ${data.pageNumber >= data.totalPages ? 'disabled' : ''} onclick="changePage(${data.pageNumber + 1})">Next</button>
    </div>
  `;
}

function changePage(page) {
  currentPage = page;
  loadTransactions();
}

function openAddModal() {
  document.getElementById('modalTitle').textContent = 'Add Transaction';
  document.getElementById('editTransactionId').value = '';
  document.getElementById('manualAmount').value = '';
  document.getElementById('manualDescription').value = '';
  document.getElementById('manualDate').value = new Date().toISOString().split('T')[0];
  document.getElementById('transactionModal').classList.add('open');
}

async function editTransaction(id) {
  try {
    const t = await api.get(`/transactions/${id}`);
    document.getElementById('modalTitle').textContent = 'Edit Transaction';
    document.getElementById('editTransactionId').value = t.transactionId;
    document.getElementById('manualAmount').value = t.amount;
    document.getElementById('manualType').value = t.type;
    document.getElementById('manualCategory').value = t.categoryId;
    document.getElementById('manualPaymentMethod').value = t.paymentMethod;
    document.getElementById('manualDescription').value = t.description || '';
    document.getElementById('manualDate').value = new Date(t.transactionDate).toISOString().split('T')[0];
    document.getElementById('transactionModal').classList.add('open');
  } catch (err) {
    showToast('Failed to load transaction details.', 'error');
  }
}

function closeModal() {
  document.getElementById('transactionModal').classList.remove('open');
}

async function deleteTransaction(id) {
  if (!confirm('Are you sure you want to delete this transaction?')) return;
  try {
    await api.delete(`/transactions/${id}`);
    showToast('Transaction deleted.', 'info');
    await loadTransactions();
  } catch (err) {
    showToast(err.message, 'error');
  }
}

function debounce(func, delay) {
  let timeout;
  return function(...args) {
    clearTimeout(timeout);
    timeout = setTimeout(() => func.apply(this, args), delay);
  };
}
