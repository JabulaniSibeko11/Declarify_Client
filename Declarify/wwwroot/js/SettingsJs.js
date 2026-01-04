// User Role Management JavaScript with Manager Assignment
let changedUsers = new Map();
let allUsers = [];
let currentEmployeeForManager = null;

// Initialize on page load
// Call it in the DOMContentLoaded event
document.addEventListener('DOMContentLoaded', function () {
    initializeUserData();
    setupEventListeners();
    setupBulkReassignListeners();
    initializeRoleSelectors(); // ADD THIS LINE
});
// Initialize user data from DOM
function initializeUserData() {
    const userCards = document.querySelectorAll('.user-card');
    allUsers = Array.from(userCards).map(card => ({
        element: card,
        userId: card.dataset.userId,
        employeeId: parseInt(card.dataset.employeeId),
        fullName: card.dataset.fullName,
        email: card.dataset.email,
        department: card.dataset.department,
        position: card.dataset.position,
        role: card.dataset.role,
        currentManagerId: parseInt(card.dataset.currentManagerId) || null,
        currentManagerName: card.dataset.currentManagerName
    }));
}

// Setup event listeners
function setupEventListeners() {
    // Search functionality
    const searchInput = document.getElementById('userSearch');
    if (searchInput) {
        searchInput.addEventListener('input', debounce(filterUsers, 300));
    }

    // Filter functionality
    const roleFilter = document.getElementById('roleFilter');
    const departmentFilter = document.getElementById('departmentFilter');
    const managerFilter = document.getElementById('managerFilter');

    if (roleFilter) roleFilter.addEventListener('change', filterUsers);
    if (departmentFilter) departmentFilter.addEventListener('change', filterUsers);
    if (managerFilter) managerFilter.addEventListener('change', filterUsers);

    // Keyboard shortcuts
    document.addEventListener('keydown', function (e) {
        if ((e.ctrlKey || e.metaKey) && e.key === 's') {
            e.preventDefault();
            if (changedUsers.size > 0) {
                saveAllChanges();
            }
        }

        if (e.key === 'Escape') {
            closeManagerAssignModal();
            closeBulkReassignModal();
            searchInput.value = '';
            filterUsers();
        }
    });
}

// Setup bulk reassign listeners
function setupBulkReassignListeners() {
    const fromManager = document.getElementById('fromManager');
    const toManager = document.getElementById('toManager');

    if (fromManager) {
        fromManager.addEventListener('change', updateAffectedEmployeesPreview);
    }

    if (toManager) {
        toManager.addEventListener('change', updateAffectedEmployeesPreview);
    }
}

// Handle role change
function handleRoleChange(selectElement) {
    console.log('Role change triggered');

    const userId = selectElement.dataset.userId;
    const originalRole = selectElement.dataset.originalRole;
    const newRole = selectElement.value;
    const userCard = selectElement.closest('.user-card');
    const changeIndicator = userCard.querySelector('.change-indicator');

    console.log('User ID:', userId);
    console.log('Original Role:', originalRole);
    console.log('New Role:', newRole);

    if (newRole !== originalRole) {
        changedUsers.set(userId, {
            userId: userId,
            newRole: newRole,
            originalRole: originalRole
        });

        userCard.classList.add('changed');
        changeIndicator.style.display = 'flex';

        const roleBadge = userCard.querySelector('.role-badge');
        roleBadge.className = `role-badge ${newRole.toLowerCase()}`;
        roleBadge.textContent = newRole;

        console.log('User marked as changed. Total changes:', changedUsers.size);

    } else {
        changedUsers.delete(userId);
        userCard.classList.remove('changed');
        changeIndicator.style.display = 'none';

        const roleBadge = userCard.querySelector('.role-badge');
        roleBadge.className = `role-badge ${originalRole.toLowerCase()}`;
        roleBadge.textContent = originalRole;

        console.log('Change reverted. Total changes:', changedUsers.size);
    }

    updateSaveButtons();
}

// Update save button visibility and text
function updateSaveButtons() {
    const saveBtn = document.getElementById('saveChangesBtn');
    const floatingSaveBtn = document.getElementById('floatingSaveBtn');
    const floatingSaveText = document.getElementById('floatingSaveText');

    if (changedUsers.size > 0) {
        const changeCount = changedUsers.size;
        const changeText = `Save ${changeCount} Change${changeCount > 1 ? 's' : ''}`;

        if (saveBtn) {
            saveBtn.style.display = 'flex';
            saveBtn.querySelector('span').textContent = changeText;
        }

        if (floatingSaveBtn) {
            floatingSaveBtn.style.display = 'block';
            floatingSaveText.textContent = changeText;
        }
    } else {
        if (saveBtn) saveBtn.style.display = 'none';
        if (floatingSaveBtn) floatingSaveBtn.style.display = 'none';
    }
}

// Save all changes
async function saveAllChanges() {
    console.log('Save all changes called');
    console.log('Changed users count:', changedUsers.size);

    if (changedUsers.size === 0) {
        showNotification('No changes to save', 'info');
        return;
    }

    const changeCount = changedUsers.size;
    if (!confirm(`Are you sure you want to save ${changeCount} role change${changeCount > 1 ? 's' : ''}?`)) {
        console.log('User cancelled save');
        return;
    }

    const saveBtn = document.getElementById('saveChangesBtn');
    const floatingSaveBtn = document.getElementById('floatingSaveBtn');
    const originalBtnContent = saveBtn ? saveBtn.innerHTML : '';
    const originalFloatingContent = floatingSaveBtn ? floatingSaveBtn.querySelector('button').innerHTML : '';

    const loadingHTML = `
        <div style="width: 20px; height: 20px; border: 3px solid rgba(255,255,255,0.3); border-top-color: white; border-radius: 50%; animation: spin 1s linear infinite;"></div>
        <span>Saving...</span>
    `;

    if (saveBtn) saveBtn.innerHTML = loadingHTML;
    if (floatingSaveBtn) floatingSaveBtn.querySelector('button').innerHTML = loadingHTML;

    try {
        const changes = Array.from(changedUsers.values()).map(change => ({
            userId: change.userId,
            newRole: change.newRole
        }));

        console.log('Changes to save:', changes);

        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        console.log('Anti-forgery token found:', !!token);

        const url = window.updateUserRolesUrl || '/Home/UpdateUserRoles';
        console.log('Posting to URL:', url);

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({ changes })
        });

        console.log('Response status:', response.status);
        console.log('Response ok:', response.ok);

        const result = await response.json();
        console.log('Response data:', result);

        if (result.success) {
            showNotification(result.message, 'success');

            changedUsers.forEach((change) => {
                const selector = document.querySelector(`.role-selector[data-user-id="${change.userId}"]`);
                if (selector) {
                    selector.dataset.originalRole = change.newRole;
                }

                const userCard = document.querySelector(`.user-card[data-user-id="${change.userId}"]`);
                if (userCard) {
                    userCard.classList.remove('changed');
                    userCard.dataset.role = change.newRole;
                    const changeIndicator = userCard.querySelector('.change-indicator');
                    if (changeIndicator) {
                        changeIndicator.style.display = 'none';
                    }
                }
            });

            changedUsers.clear();
            updateSaveButtons();

            if (result.errors && result.errors.length > 0) {
                setTimeout(() => {
                    showNotification(`Errors: ${result.errors.join(', ')}`, 'warning');
                }, 2000);
            }

        } else {
            console.error('Save failed:', result.message);
            showNotification(result.message || 'Failed to save changes', 'error');

            if (saveBtn) saveBtn.innerHTML = originalBtnContent;
            if (floatingSaveBtn) floatingSaveBtn.querySelector('button').innerHTML = originalFloatingContent;
        }

    } catch (error) {
        console.error('Error saving changes:', error);
        showNotification('An error occurred while saving changes: ' + error.message, 'error');

        if (saveBtn) saveBtn.innerHTML = originalBtnContent;
        if (floatingSaveBtn) floatingSaveBtn.querySelector('button').innerHTML = originalFloatingContent;
    }
}

// Open Manager Assignment Modal
function openManagerAssignModal(userId, employeeId, employeeName) {
    console.log('Opening modal for:', userId, employeeId, employeeName); // Debug log

    const modal = document.getElementById('managerAssignModal');
    if (!modal) {
        console.error('Modal element not found!');
        return;
    }

    currentEmployeeForManager = {
        userId: userId,
        employeeId: parseInt(employeeId),
        name: employeeName
    };

    const subtitle = document.getElementById('modalSubtitle');
    const managerList = document.getElementById('managerList');
    const managerSearch = document.getElementById('managerSearch');

    if (!subtitle || !managerList || !managerSearch) {
        console.error('Modal elements not found:', { subtitle, managerList, managerSearch });
        return;
    }

    subtitle.textContent = `Select a new manager for ${employeeName}`;
    managerSearch.value = '';

    // Check if window.allUsersData exists
    if (!window.allUsersData || !Array.isArray(window.allUsersData)) {
        console.error('allUsersData not found or not an array');
        showNotification('Unable to load user data', 'error');
        return;
    }

    // Filter out the current employee and populate manager list
    const availableManagers = window.allUsersData.filter(u => u.employeeId !== currentEmployeeForManager.employeeId);

    renderManagerList(availableManagers);

    modal.classList.add('active');
    document.body.style.overflow = 'hidden';

    // Remove any existing search listeners before adding new one
    const newSearchInput = managerSearch.cloneNode(true);
    managerSearch.parentNode.replaceChild(newSearchInput, managerSearch);

    // Setup search for managers
    newSearchInput.addEventListener('input', function () {
        const searchTerm = this.value.toLowerCase();
        const filtered = availableManagers.filter(u =>
            u.fullName.toLowerCase().includes(searchTerm) ||
            u.department.toLowerCase().includes(searchTerm) ||
            u.position.toLowerCase().includes(searchTerm)
        );
        renderManagerList(filtered);
    });
}
// Render Manager List
function renderManagerList(managers) {
    const managerList = document.getElementById('managerList');

    if (managers.length === 0) {
        managerList.innerHTML = `
            <div class="empty-state-modal">
                <svg width="48" height="48" fill="none" stroke="currentColor" stroke-width="1.5" viewBox="0 0 24 24">
                    <path stroke-linecap="round" stroke-linejoin="round" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                </svg>
                <p>No managers found</p>
            </div>
        `;
        return;
    }

    managerList.innerHTML = managers.map(manager => `
        <div class="manager-item" onclick="selectManager(${manager.employeeId}, '${manager.fullName.replace(/'/g, "\\'")}')">
            <div class="manager-avatar">
                ${getInitials(manager.fullName)}
            </div>
            <div class="manager-info">
                <div class="manager-name">${manager.fullName}</div>
                <div class="manager-details">
                    <span>${manager.position}</span>
                    <span class="separator">•</span>
                    <span>${manager.department}</span>
                </div>
            </div>
            <svg class="manager-select-icon" width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                <path stroke-linecap="round" stroke-linejoin="round" d="M9 5l7 7-7 7" />
            </svg>
        </div>
    `).join('');
}

// Select Manager
async function selectManager(managerId, managerName) {
    if (!currentEmployeeForManager) {
        console.error('No employee selected for manager assignment');
        return;
    }

    // Escape single quotes in manager name for confirm dialog
    const safeManagerName = managerName.replace(/'/g, "\\'");
    const confirmed = confirm(`Assign ${safeManagerName} as the new manager for ${currentEmployeeForManager.name}?`);
    if (!confirmed) return;

    try {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        if (!tokenInput) {
            throw new Error('Anti-forgery token not found');
        }

        const token = tokenInput.value;
        const url = window.assignManagerUrl || '/Home/AssignManager';

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({
                employeeId: currentEmployeeForManager.employeeId,
                newManagerId: managerId
            })
        });

        if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
        }

        const result = await response.json();

        if (result.success) {
            showNotification('Manager assigned successfully', 'success');

            // Update the UI
            const userCard = document.querySelector(`.user-card[data-employee-id="${currentEmployeeForManager.employeeId}"]`);
            if (userCard) {
                userCard.dataset.currentManagerId = managerId;
                userCard.dataset.currentManagerName = managerName;

                const managerDisplay = userCard.querySelector('.manager-display');
                if (managerDisplay) {
                    managerDisplay.innerHTML = `<span class="manager-name">${managerName}</span>`;
                }
            }

            // Update the allUsersData
            if (window.allUsersData) {
                const userIndex = window.allUsersData.findIndex(u => u.employeeId === currentEmployeeForManager.employeeId);
                if (userIndex !== -1) {
                    window.allUsersData[userIndex].currentManagerId = managerId;
                    window.allUsersData[userIndex].currentManagerName = managerName;
                }
            }

            closeManagerAssignModal();
        } else {
            showNotification(result.message || 'Failed to assign manager', 'error');
        }

    } catch (error) {
        console.error('Error assigning manager:', error);
        showNotification('An error occurred while assigning manager: ' + error.message, 'error');
    }
}

// DEBUGGING: Add console logs to verify modal structure on load
document.addEventListener('DOMContentLoaded', function () {
    console.log('Checking modal elements...');
    console.log('managerAssignModal:', document.getElementById('managerAssignModal'));
    console.log('bulkReassignModal:', document.getElementById('bulkReassignModal'));
    console.log('allUsersData:', window.allUsersData);
});
// Close Manager Assignment Modal
function closeManagerAssignModal() {
    const modal = document.getElementById('managerAssignModal');
    modal.classList.remove('active');
    document.body.style.overflow = 'auto';
    currentEmployeeForManager = null;
}

// Open Bulk Reassign Modal
function openBulkReassignModal() {
    const modal = document.getElementById('bulkReassignModal');
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';

    // Reset form
    document.getElementById('fromManager').value = '';
    document.getElementById('toManager').value = '';
    document.getElementById('affectedEmployeesPreview').style.display = 'none';
    document.getElementById('bulkReassignBtn').disabled = true;
}

// Close Bulk Reassign Modal
function closeBulkReassignModal() {
    const modal = document.getElementById('bulkReassignModal');
    modal.classList.remove('active');
    document.body.style.overflow = 'auto';
}

// Update Affected Employees Preview
function updateAffectedEmployeesPreview() {
    const fromManagerId = parseInt(document.getElementById('fromManager').value);
    const toManagerId = parseInt(document.getElementById('toManager').value);
    const previewSection = document.getElementById('affectedEmployeesPreview');
    const affectedCount = document.getElementById('affectedCount');
    const affectedList = document.getElementById('affectedEmployeesList');
    const reassignBtn = document.getElementById('bulkReassignBtn');

    if (!fromManagerId) {
        previewSection.style.display = 'none';
        reassignBtn.disabled = true;
        return;
    }

    // Find all employees reporting to the "from" manager
    const affectedEmployees = window.allUsersData.filter(u => u.currentManagerId === fromManagerId);

    if (affectedEmployees.length === 0) {
        previewSection.style.display = 'none';
        reassignBtn.disabled = true;
        showNotification('No employees found reporting to this manager', 'info');
        return;
    }

    // Show preview
    previewSection.style.display = 'block';
    affectedCount.textContent = `${affectedEmployees.length} employee${affectedEmployees.length !== 1 ? 's' : ''}`;

    affectedList.innerHTML = affectedEmployees.map(emp => `
        <div class="affected-employee-item">
            <span>${emp.fullName}</span>
            <span class="emp-detail">${emp.position} - ${emp.department}</span>
        </div>
    `).join('');

    // Enable button only if both managers are selected
    reassignBtn.disabled = !(fromManagerId && toManagerId && fromManagerId !== toManagerId);
}

// Confirm Bulk Reassignment
async function confirmBulkReassignment() {
    const fromManagerId = parseInt(document.getElementById('fromManager').value);
    const toManagerId = parseInt(document.getElementById('toManager').value);

    const fromManagerName = document.getElementById('fromManager').selectedOptions[0].dataset.name;
    const toManagerName = document.getElementById('toManager').selectedOptions[0].textContent.split(' - ')[0];

    const affectedEmployees = window.allUsersData.filter(u => u.currentManagerId === fromManagerId);

    if (!confirm(`Are you sure you want to reassign ${affectedEmployees.length} employee${affectedEmployees.length !== 1 ? 's' : ''} from ${fromManagerName} to ${toManagerName}?`)) {
        return;
    }

    const reassignBtn = document.getElementById('bulkReassignBtn');
    const originalContent = reassignBtn.innerHTML;
    reassignBtn.innerHTML = '<div class="loading-spinner" style="width: 18px; height: 18px; border-width: 2px;"></div><span>Processing...</span>';
    reassignBtn.disabled = true;

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;
        const url = window.bulkReassignManagerUrl || '/Home/BulkReassignManager';

        const response = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
            },
            body: JSON.stringify({
                fromManagerId: fromManagerId,
                toManagerId: toManagerId
            })
        });

        const result = await response.json();

        if (result.success) {
            showNotification(result.message, 'success');

            // Update all affected employee cards
            affectedEmployees.forEach(emp => {
                const userCard = document.querySelector(`.user-card[data-employee-id="${emp.employeeId}"]`);
                if (userCard) {
                    userCard.dataset.currentManagerId = toManagerId;
                    userCard.dataset.currentManagerName = toManagerName;

                    const managerDisplay = userCard.querySelector('.manager-display');
                    if (managerDisplay) {
                        managerDisplay.innerHTML = `<span class="manager-name">${toManagerName}</span>`;
                    }
                }
            });

            closeBulkReassignModal();
        } else {
            showNotification(result.message || 'Failed to reassign employees', 'error');
            reassignBtn.innerHTML = originalContent;
            reassignBtn.disabled = false;
        }

    } catch (error) {
        console.error('Error during bulk reassignment:', error);
        showNotification('An error occurred during bulk reassignment', 'error');
        reassignBtn.innerHTML = originalContent;
        reassignBtn.disabled = false;
    }
}

// Filter users
function filterUsers() {
    const searchTerm = document.getElementById('userSearch').value.toLowerCase();
    const roleFilter = document.getElementById('roleFilter').value;
    const departmentFilter = document.getElementById('departmentFilter').value.toLowerCase();
    const managerFilter = document.getElementById('managerFilter').value;

    let visibleCount = 0;

    allUsers.forEach(user => {
        const matchesSearch = !searchTerm ||
            user.fullName.includes(searchTerm) ||
            user.email.includes(searchTerm) ||
            user.department.includes(searchTerm) ||
            user.position.includes(searchTerm);

        const matchesRole = !roleFilter || user.role === roleFilter;
        const matchesDepartment = !departmentFilter || user.department === departmentFilter;

        let matchesManager = true;
        if (managerFilter === 'has-manager') {
            matchesManager = user.currentManagerId !== null && user.currentManagerId > 0;
        } else if (managerFilter === 'no-manager') {
            matchesManager = !user.currentManagerId || user.currentManagerId === 0;
        }

        if (matchesSearch && matchesRole && matchesDepartment && matchesManager) {
            user.element.style.display = '';
            visibleCount++;
        } else {
            user.element.style.display = 'none';
        }
    });

    const noResults = document.getElementById('noResults');
    const usersGrid = document.getElementById('usersGrid');

    if (visibleCount === 0) {
        noResults.style.display = 'flex';
        usersGrid.style.display = 'none';
    } else {
        noResults.style.display = 'none';
        usersGrid.style.display = 'grid';
    }
}

// Utility Functions
function getInitials(name) {
    if (!name) return 'NA';
    const parts = name.split(' ');
    if (parts.length >= 2) {
        return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return name.substring(0, 2).toUpperCase();
}

function showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.className = `notification notification-${type}`;

    const icons = {
        success: `<svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>`,
        error: `<svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>`,
        warning: `<svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
        </svg>`,
        info: `<svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <path stroke-linecap="round" stroke-linejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
        </svg>`
    };

    notification.innerHTML = `${icons[type]}<span>${message}</span>`;
    document.body.appendChild(notification);

    setTimeout(() => notification.classList.add('show'), 10);

    setTimeout(() => {
        notification.classList.remove('show');
        setTimeout(() => notification.remove(), 300);
    }, 5000);
}

function debounce(func, wait) {
    let timeout;
    return function executedFunction(...args) {
        const later = () => {
            clearTimeout(timeout);
            func(...args);
        };
        clearTimeout(timeout);
        timeout = setTimeout(later, wait);
    };
}

// Add notification styles dynamically
const style = document.createElement('style');
style.textContent = `
    .notification {
        position: fixed;
        top: 2rem;
        right: 2rem;
        padding: 1rem 1.5rem;
        border-radius: 12px;
        display: flex;
        align-items: center;
        gap: 0.75rem;
        font-family: 'Inter', sans-serif;
        font-size: 0.9375rem;
        font-weight: 600;
        box-shadow: 0 10px 40px rgba(0, 0, 0, 0.15);
        z-index: 10000;
        opacity: 0;
        transform: translateX(400px);
        transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }
    
    .notification.show {
        opacity: 1;
        transform: translateX(0);
    }
    
    .notification-success {
        background: linear-gradient(135deg, #10B981 0%, #059669 100%);
        color: white;
    }
    
    .notification-error {
        background: linear-gradient(135deg, #EF4444 0%, #DC2626 100%);
        color: white;
    }
    
    .notification-warning {
        background: linear-gradient(135deg, #F59E0B 0%, #D97706 100%);
        color: white;
    }
    
    .notification-info {
        background: linear-gradient(135deg, #00C2CB 0%, #00E5FF 100%);
        color: #081B38;
    }
    
    @keyframes spin {
        to { transform: rotate(360deg); }
    }
    
    .loading-spinner {
        width: 18px;
        height: 18px;
        border: 3px solid rgba(255,255,255,0.3);
        border-top-color: white;
        border-radius: 50%;
        animation: spin 1s linear infinite;
    }
`;
document.head.appendChild(style);

function initializeRoleSelectors() {
    document.querySelectorAll('.role-selector').forEach(selector => {
        const originalRole = selector.dataset.originalRole;
        selector.value = originalRole;
    });
}
// Warn before leaving with unsaved changes
window.addEventListener('beforeunload', function (e) {
    if (changedUsers.size > 0) {
        e.preventDefault();
        e.returnValue = '';
        return '';
    }
});