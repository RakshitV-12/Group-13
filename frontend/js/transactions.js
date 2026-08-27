// =========================================================================
// Transactions Management (Manual & Quick Entry, CSV Import, Filtering & CRUD)
// =========================================================================

let currentPage = 1;
let categoriesList = [];
let selectedTransactionId = null;
let currentCsvReviewRows = [];

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
      const categorySelect = document.getElementById('manualCategory');
      const categoryId = parseInt(categorySelect?.value || '0');
      const type = document.getElementById('manualType').value;
      const paymentMethod = document.getElementById('manualPaymentMethod').value;
      const transactionDate = document.getElementById('manualDate').value || new Date().toISOString();
      const description = document.getElementById('manualDescription').value.trim();

      try {
        const payload = { amount, categoryId, type, paymentMethod, transactionDate, description };

        if (editId) {
          const result = await api.put(`/transactions/${editId}`, payload);
          selectedTransactionId = result.transactionId;
          showToast('Transaction updated successfully!', 'success');
        } else {
          const result = await api.post('/transactions', payload);
          selectedTransactionId = result.transactionId;
          showToast('Transaction created successfully!', 'success');
        }
        closeModal();
        await loadTransactions();
      } catch (err) {
        showToast(err.message, 'error');
      }
    });
  }

  const tbody = document.getElementById('transactionsTableBody');
  if (tbody) {
    tbody.addEventListener('click', async (event) => {
      const row = event.target.closest('tr[data-transaction-id]');
      if (!row) return;
      const id = Number(row.dataset.transactionId);
      if (!Number.isNaN(id)) {
        selectedTransactionId = id;
        await loadSelectedTransactionBalance();
      }
    });
  }
});

async function loadCategories() {
  try {
    categoriesList = await api.get('/categories');
    const catFilter = document.getElementById('categoryFilter');
    const manualCategory = document.getElementById('manualCategory');

    const optionsHtml = categoriesList.map(c => `<option value="${c.categoryId}">${c.name}</option>`).join('');

    if (catFilter) {
      catFilter.innerHTML = '<option value="">All Categories</option>' + optionsHtml;
    }
    if (manualCategory) {
      manualCategory.innerHTML = optionsHtml;
    }
  } catch (err) {
    console.error('Failed to load categories:', err);
  }
}

async function loadTransactions() {
  const searchInput = document.getElementById('searchInput')?.value || '';
  const categoryId = document.getElementById('categoryFilter')?.value || '';
  const type = document.getElementById('typeFilter')?.value || '';

  try {
    let query = `?page=${currentPage}&pageSize=20`;
    if (searchInput) query += `&search=${encodeURIComponent(searchInput)}`;
    if (categoryId) query += `&categoryId=${categoryId}`;
    if (type) query += `&type=${type}`;

    const data = await api.get(`/transactions${query}`);
    renderTable(data.items);
    renderPagination(data);

    if (selectedTransactionId && data.items.some(t => t.transactionId === selectedTransactionId)) {
      await loadSelectedTransactionBalance();
    } else if (data.items.length > 0 && !selectedTransactionId) {
      selectedTransactionId = data.items[0].transactionId;
      await loadSelectedTransactionBalance();
    }
  } catch (err) {
    console.error('Failed to load transactions:', err);
  }
}

async function loadSelectedTransactionBalance() {
  if (!selectedTransactionId) return;

  const actionCenterEmpty = document.getElementById('actionCenterEmpty');
  const actionCenterDetails = document.getElementById('actionCenterDetails');
  const balanceBeforeValue = document.getElementById('balanceBeforeValue');
  const transactionDeltaValue = document.getElementById('transactionDeltaValue');
  const balanceAfterValue = document.getElementById('balanceAfterValue');
  const selectedTransactionMeta = document.getElementById('selectedTransactionMeta');

  if (!actionCenterEmpty || !actionCenterDetails) return;

  try {
    const context = await api.get(`/transactions/${selectedTransactionId}/balance-context`);
    actionCenterEmpty.style.display = 'none';
    actionCenterDetails.style.display = 'block';

    const formatCurrency = (val) => {
      const formatted = Math.abs(val).toLocaleString('en-IN', { minimumFractionDigits: 2 });
      return val < 0 ? `-₹${formatted}` : `₹${formatted}`;
    };

    balanceBeforeValue.textContent = formatCurrency(context.balanceBefore);
    transactionDeltaValue.textContent = formatCurrency(context.transactionAmount);
    transactionDeltaValue.style.color = context.type === 'Income' ? 'var(--success)' : 'var(--danger)';
    balanceAfterValue.textContent = formatCurrency(context.balanceAfter);

    selectedTransactionMeta.innerHTML = `
      <strong>${context.description}</strong><br>
      ${new Date(context.transactionDate).toLocaleDateString('en-IN', { day: 'numeric', month: 'short', year: 'numeric' })} • ${context.paymentMethod || 'UPI'} • ${context.categoryName || 'General'}
    `;
  } catch (err) {
    console.warn('Balance context unavailable:', err);
    actionCenterEmpty.textContent = 'Select a transaction to view balance details.';
    actionCenterEmpty.style.display = 'block';
    actionCenterDetails.style.display = 'none';
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
    <tr data-transaction-id="${t.transactionId}" style="cursor: pointer; ${selectedTransactionId === t.transactionId ? 'background: #f5f3ff;' : ''}">
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
          <button class="btn btn-secondary btn-sm" onclick="event.stopPropagation(); editTransaction(${t.transactionId})"><i class="fas fa-edit"></i></button>
          <button class="btn btn-danger btn-sm" onclick="event.stopPropagation(); deleteTransaction(${t.transactionId})"><i class="fas fa-trash"></i></button>
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

// CSV IMPORT & INTERACTIVE REVIEW SCREEN
async function handleCsvFileSelected(event) {
  const file = event.target.files[0];
  if (!file) return;

  const formData = new FormData();
  formData.append('file', file);

  try {
    showToast('Parsing CSV and running AI RAG classification...', 'info');
    const preview = await api.upload('/import/csv/preview', formData);
    currentCsvReviewRows = preview.rows || [];
    renderCsvReviewScreen();
    document.getElementById('csvReviewModal').classList.add('open');
  } catch (err) {
    showToast(err.message || 'Failed to parse CSV.', 'error');
  } finally {
    event.target.value = '';
  }
}

function renderCsvReviewScreen() {
  const tbody = document.getElementById('csvReviewTableBody');
  const countEl = document.getElementById('csvReviewTotalCount');
  const amountEl = document.getElementById('csvReviewTotalAmount');

  if (!tbody) return;

  let totalAmt = 0;
  currentCsvReviewRows.forEach(r => {
    totalAmt += (parseFloat(r.amount) || 0);
  });

  if (countEl) countEl.textContent = currentCsvReviewRows.length;
  if (amountEl) amountEl.textContent = `₹${totalAmt.toLocaleString('en-IN', { minimumFractionDigits: 2 })}`;

  if (currentCsvReviewRows.length === 0) {
    tbody.innerHTML = `<tr><td colspan="7" style="text-align:center; color:var(--gray-400); padding:24px;">No transactions in review list. Click "+ Add Transaction" to add manually.</td></tr>`;
    return;
  }

  tbody.innerHTML = currentCsvReviewRows.map((r, idx) => {
    let dateVal = r.parsedDate ? new Date(r.parsedDate).toISOString().split('T')[0] : (r.dateRaw || '');
    let badgeClass = 'badge-valid';
    let badgeText = '✓ Valid';

    if (r.statusBadge === 'Needs Review' || r.confidence < 0.70) {
      badgeClass = 'badge-review';
      badgeText = '⚠ Needs Review';
    } else if (r.statusBadge === 'Possible Duplicate' || r.isDuplicate) {
      badgeClass = 'badge-duplicate';
      badgeText = '⚠ Possible Duplicate';
    } else if (r.statusBadge === 'AI Suggested') {
      const confPct = Math.round((r.confidence || 0.95) * 100);
      badgeClass = 'badge-ai';
      badgeText = `✓ AI Suggested (${confPct}%)`;
    } else if (r.statusBadge === 'Invalid' || !r.isValid) {
      badgeClass = 'badge-invalid';
      badgeText = '⚠ Invalid';
    }

    const catOptions = categoriesList.map(c => `
      <option value="${c.categoryId}" ${(c.categoryId === r.categoryId || c.name.toLowerCase() === (r.categoryName || '').toLowerCase()) ? 'selected' : ''}>${c.name}</option>
    `).join('');

    const pMethods = ['UPI', 'Credit Card', 'Debit Card', 'Cash', 'Bank Transfer', 'Other'];
    const pMethodOptions = pMethods.map(m => `
      <option value="${m}" ${(r.paymentMethod || 'UPI').toLowerCase() === m.toLowerCase() ? 'selected' : ''}>${m}</option>
    `).join('');

    let duplicateActions = '';
    if (r.isDuplicate || r.statusBadge === 'Possible Duplicate') {
      duplicateActions = `
        <div style="margin-top:4px; display:flex; gap:4px;">
          <button type="button" class="btn btn-secondary btn-sm" style="padding:2px 6px; font-size:0.7rem;" onclick="deleteCsvReviewRow(${idx})" title="Skip duplicate row">Skip</button>
          <button type="button" class="btn btn-primary btn-sm" style="padding:2px 6px; font-size:0.7rem;" onclick="ignoreDuplicateWarning(${idx})" title="Import row anyway">Import Anyway</button>
        </div>
      `;
    }

    return `
      <tr>
        <td>
          <input type="date" class="form-control" style="padding:4px 8px; font-size:0.85rem;" value="${dateVal}" onchange="updateCsvReviewRow(${idx}, 'parsedDate', this.value)">
        </td>
        <td>
          <input type="text" class="form-control" style="padding:4px 8px; font-size:0.85rem;" value="${r.name || ''}" onchange="updateCsvReviewRow(${idx}, 'name', this.value)">
        </td>
        <td>
          <input type="number" class="form-control" style="padding:4px 8px; font-size:0.85rem; width:90px;" step="0.01" value="${r.amount}" onchange="updateCsvReviewRow(${idx}, 'amount', this.value)">
        </td>
        <td>
          <select class="form-control" style="padding:4px 8px; font-size:0.85rem;" onchange="updateCsvReviewRow(${idx}, 'categoryId', this.value)">
            <option value="">[ Select Category ▼ ]</option>
            ${catOptions}
          </select>
        </td>
        <td>
          <select class="form-control" style="padding:4px 8px; font-size:0.85rem;" onchange="updateCsvReviewRow(${idx}, 'paymentMethod', this.value)">
            ${pMethodOptions}
          </select>
        </td>
        <td>
          <span class="badge ${badgeClass}" title="${r.statusReason || ''}">${badgeText}</span>
          ${duplicateActions}
        </td>
        <td>
          <button type="button" class="btn btn-secondary btn-sm" onclick="deleteCsvReviewRow(${idx})" title="Delete row"><i class="fas fa-trash text-danger"></i></button>
        </td>
      </tr>
    `;
  }).join('');
}

function updateCsvReviewRow(index, field, value) {
  if (index < 0 || index >= currentCsvReviewRows.length) return;
  const row = currentCsvReviewRows[index];

  if (field === 'parsedDate') {
    row.parsedDate = value;
    row.dateRaw = value;
  } else if (field === 'name') {
    row.name = value;
  } else if (field === 'amount') {
    row.amount = parseFloat(value) || 0;
  } else if (field === 'paymentMethod') {
    row.paymentMethod = value;
  } else if (field === 'categoryId') {
    row.categoryId = parseInt(value) || null;
    const cat = categoriesList.find(c => c.categoryId === row.categoryId);
    if (cat) {
      row.categoryName = cat.name;
      row.statusBadge = 'Valid';
      row.isValid = true;
      if (cat.name.toLowerCase() === 'income' || cat.name.toLowerCase() === 'salary') {
        row.type = 'Income';
      } else {
        row.type = 'Expense';
      }
    }
  }

  // Re-check validity
  if (row.parsedDate && row.amount > 0 && row.name && row.categoryId) {
    row.isValid = true;
    if (row.statusBadge === 'Invalid' || row.statusBadge === 'Needs Review') {
      row.statusBadge = 'Valid';
    }
  }

  renderCsvReviewScreen();
}

function ignoreDuplicateWarning(index) {
  if (index >= 0 && index < currentCsvReviewRows.length) {
    currentCsvReviewRows[index].isDuplicate = false;
    currentCsvReviewRows[index].statusBadge = 'Valid';
    currentCsvReviewRows[index].statusReason = 'User chose to import anyway';
    renderCsvReviewScreen();
  }
}

function addNewCsvRow() {
  const defaultCat = categoriesList.find(c => c.name === 'Food') || categoriesList[0];
  const todayStr = new Date().toISOString().split('T')[0];

  currentCsvReviewRows.push({
    rowId: Guid(),
    dateRaw: todayStr,
    parsedDate: todayStr,
    name: 'New Expense',
    amount: 100,
    categoryId: defaultCat ? defaultCat.categoryId : null,
    categoryName: defaultCat ? defaultCat.name : '',
    statusBadge: 'Valid',
    isValid: true,
    confidence: 1.0,
    paymentMethod: 'UPI',
    type: 'Expense'
  });

  renderCsvReviewScreen();
}

function deleteCsvReviewRow(index) {
  if (index >= 0 && index < currentCsvReviewRows.length) {
    currentCsvReviewRows.splice(index, 1);
    renderCsvReviewScreen();
  }
}

function closeCsvReviewModal() {
  document.getElementById('csvReviewModal').classList.remove('open');
  currentCsvReviewRows = [];
}

async function confirmCsvImport() {
  if (currentCsvReviewRows.length === 0) {
    showToast('No transactions to import.', 'warning');
    return;
  }

  // Validate every row
  for (let i = 0; i < currentCsvReviewRows.length; i++) {
    const r = currentCsvReviewRows[i];
    if (!r.parsedDate) {
      showToast(`Row ${i + 1} ("${r.name}") has an invalid date. Please correct it.`, 'error');
      return;
    }
    if (!r.amount || r.amount <= 0) {
      showToast(`Row ${i + 1} ("${r.name}") amount must be greater than 0.`, 'error');
      return;
    }
    if (!r.categoryId) {
      showToast(`Row ${i + 1} ("${r.name}") requires a valid category.`, 'error');
      return;
    }
  }

  const payload = {
    transactions: currentCsvReviewRows.map(r => ({
      transactionDate: new Date(r.parsedDate).toISOString(),
      name: r.name,
      amount: parseFloat(r.amount),
      categoryId: parseInt(r.categoryId),
      paymentMethod: r.paymentMethod || 'UPI',
      type: r.type || 'Expense'
    }))
  };

  try {
    const res = await api.post('/import/csv/confirm', payload);
    showToast(res.message || `${res.importedCount} transactions imported successfully.`, 'success');
    closeCsvReviewModal();
    await loadTransactions();
  } catch (err) {
    showToast(err.message || 'Failed to confirm CSV import.', 'error');
  }
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
    document.getElementById('manualPaymentMethod').value = t.paymentMethod;
    document.getElementById('manualDescription').value = t.description || '';
    document.getElementById('manualDate').value = new Date(t.transactionDate).toISOString().split('T')[0];
    const categorySelect = document.getElementById('manualCategory');
    if (categorySelect) categorySelect.value = t.categoryId;

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

function Guid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function (c) {
    var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
    return v.toString(16);
  });
}

function debounce(func, delay) {
  let timeout;
  return function (...args) {
    clearTimeout(timeout);
    timeout = setTimeout(() => func.apply(this, args), delay);
  };
}
