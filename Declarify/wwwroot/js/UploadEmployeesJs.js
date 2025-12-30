// Global cache for managers
let managersCache = null;
let employeeNumberValid = false;
let emailValid = false;

// Modal Functions
function openModal() {
    const modal = document.getElementById('addEmployeeModal');
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';

    // Load managers when opening modal
    loadInitialManagers();
}

function closeModal() {
    const modal = document.getElementById('addEmployeeModal');
    modal.classList.remove('active');
    document.body.style.overflow = '';

    // Reset forms
    document.getElementById('singleForm').reset();
    document.getElementById('bulkForm').reset();
    document.getElementById('filePreview').innerHTML = '';
    document.getElementById('uploadBtn').disabled = true;

    // Reset validation states
    employeeNumberValid = false;
    emailValid = false;
    document.getElementById('employeeNumberValidation').textContent = '';
    document.getElementById('emailValidation').textContent = '';

    // Reset file input
    const excelFileInput = document.getElementById('excelFileInput');
    if (excelFileInput) {
        excelFileInput.value = '';
    }
}

// Toggle between Single and Bulk mode
function switchMode(mode) {
    const singleBtn = document.getElementById('singleBtn');
    const bulkBtn = document.getElementById('bulkBtn');
    const singleForm = document.getElementById('singleForm');
    const bulkForm = document.getElementById('bulkForm');

    if (mode === 'single') {
        singleBtn.classList.add('active');
        bulkBtn.classList.remove('active');
        singleForm.style.display = 'block';
        bulkForm.style.display = 'none';

        // Reload managers when switching to single mode
        if (!managersCache) {
            loadInitialManagers();
        }
    } else {
        bulkBtn.classList.add('active');
        singleBtn.classList.remove('active');
        bulkForm.style.display = 'block';
        singleForm.style.display = 'none';
    }
}

// Load all managers when modal opens
async function loadInitialManagers() {
    const dropdown = document.getElementById('managerDropdown');

    // Show loading state
    dropdown.innerHTML = '<option value="">Loading managers...</option>';
    dropdown.disabled = true;

    try {
        const response = await fetch('/Home/GetAllManagers');
        const data = await response.json();

        if (data.success) {
            managersCache = data.managers;
            populateManagerDropdown(data.managers);
            dropdown.disabled = false;
        } else {
            dropdown.innerHTML = '<option value="">Failed to load managers</option>';
            showNotification(data.message || 'Failed to load managers', 'error');
        }
    } catch (error) {
        console.error('Error loading managers:', error);
        dropdown.innerHTML = '<option value="">Error loading managers</option>';
        showNotification('Failed to load managers', 'error');
    }
}

// Update manager dropdown based on position and department
async function updateManagerDropdown() {
    const position = document.getElementById('position').value.trim();
    const department = document.getElementById('department').value.trim();
    const dropdown = document.getElementById('managerDropdown');

    if (!position || !department) {
        // If no position/department, show all managers from cache
        if (managersCache) {
            populateManagerDropdown(managersCache);
        }
        return;
    }

    // Show loading state
    dropdown.disabled = true;
    const originalHTML = dropdown.innerHTML;
    dropdown.innerHTML = '<option value="">Filtering managers...</option>';

    try {
        const response = await fetch(
            `/Home/GetPotentialManagers?position=${encodeURIComponent(position)}&department=${encodeURIComponent(department)}`
        );
        const data = await response.json();

        if (data.success) {
            populateManagerDropdown(data.managers);
            dropdown.disabled = false;

            if (data.managers.length === 0) {
                showNotification('No suitable managers found for this position/department. Showing all managers.', 'info');
                // Fall back to all managers if no filtered results
                populateManagerDropdown(managersCache);
            }
        } else {
            dropdown.innerHTML = originalHTML;
            dropdown.disabled = false;
            showNotification(data.message || 'Failed to filter managers', 'warning');
        }
    } catch (error) {
        console.error('Error updating managers:', error);
        dropdown.innerHTML = originalHTML;
        dropdown.disabled = false;
        showNotification('Failed to filter managers', 'error');
    }
}

// Populate dropdown with manager options
function populateManagerDropdown(managers) {
    const dropdown = document.getElementById('managerDropdown');

    // Clear existing options
    dropdown.innerHTML = '<option value="">-- No Manager / CEO Level --</option>';

    if (!managers || managers.length === 0) {
        dropdown.innerHTML += '<option value="" disabled>No managers available</option>';
        return;
    }

    // Group managers by department
    const managersByDept = {};
    managers.forEach(manager => {
        const dept = manager.department || 'Other';
        if (!managersByDept[dept]) {
            managersByDept[dept] = [];
        }
        managersByDept[dept].push(manager);
    });

    // Add optgroups for each department
    Object.keys(managersByDept).sort().forEach(dept => {
        const optgroup = document.createElement('optgroup');
        optgroup.label = dept;

        managersByDept[dept]
            .sort((a, b) => a.name.localeCompare(b.name))
            .forEach(manager => {
                const option = document.createElement('option');
                option.value = manager.id;
                option.textContent = `${manager.name} - ${manager.position} (${manager.employeeNumber})`;
                optgroup.appendChild(option);
            });

        dropdown.appendChild(optgroup);
    });
}

// Validation Functions
async function checkEmployeeNumber(employeeNumber) {
    const validationSpan = document.getElementById('employeeNumberValidation');

    if (!employeeNumber) {
        validationSpan.textContent = '';
        employeeNumberValid = false;
        return;
    }

    try {
        const response = await fetch(`/Home/CheckEmployeeNumber?employeeNumber=${encodeURIComponent(employeeNumber)}`);
        const data = await response.json();

        if (data.isUnique) {
            validationSpan.textContent = '✓ ' + data.message;
            validationSpan.className = 'validation-message success';
            employeeNumberValid = true;
        } else {
            validationSpan.textContent = '✗ ' + data.message;
            validationSpan.className = 'validation-message error';
            employeeNumberValid = false;
        }
    } catch (error) {
        console.error('Error checking employee number:', error);
        validationSpan.textContent = '✗ Unable to validate employee number';
        validationSpan.className = 'validation-message error';
        employeeNumberValid = false;
    }
}

async function checkEmail(email) {
    const validationSpan = document.getElementById('emailValidation');

    if (!email) {
        validationSpan.textContent = '';
        emailValid = false;
        return;
    }

    // Basic email format validation
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(email)) {
        validationSpan.textContent = '✗ Invalid email format';
        validationSpan.className = 'validation-message error';
        emailValid = false;
        return;
    }

    try {
        const response = await fetch(`/Home/CheckEmail?email=${encodeURIComponent(email)}`);
        const data = await response.json();

        if (data.isUnique) {
            validationSpan.textContent = '✓ ' + data.message;
            validationSpan.className = 'validation-message success';
            emailValid = true;
        } else {
            validationSpan.textContent = '✗ ' + data.message;
            validationSpan.className = 'validation-message error';
            emailValid = false;
        }
    } catch (error) {
        console.error('Error checking email:', error);
        validationSpan.textContent = '✗ Unable to validate email';
        validationSpan.className = 'validation-message error';
        emailValid = false;
    }
}

function validateForm() {
    if (!employeeNumberValid) {
        showNotification('Please enter a unique employee number', 'error');
        document.getElementById('employeeNumber').focus();
        return false;
    }

    if (!emailValid) {
        showNotification('Please enter a unique email address', 'error');
        document.getElementById('email').focus();
        return false;
    }

    // Show loading state
    const submitBtn = document.getElementById('submitBtn');
    submitBtn.disabled = true;
    submitBtn.innerHTML = `
        <svg class="animate-spin" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <path d="M12 2v4m0 12v4M4.93 4.93l2.83 2.83m8.48 8.48l2.83 2.83M2 12h4m12 0h4M4.93 19.07l2.83-2.83m8.48-8.48l2.83-2.83"/>
        </svg>
        Creating Employee...
    `;

    return true;
}

// File upload handling for bulk import
function handleFileSelect(event) {
    const file = event.target.files[0];
    const filePreview = document.getElementById('filePreview');
    const uploadBtn = document.getElementById('uploadBtn');

    if (file) {
        const fileSize = (file.size / 1024).toFixed(2);
        const fileExtension = file.name.split('.').pop().toLowerCase();

        if (fileExtension !== 'xlsx' && fileExtension !== 'xls') {
            filePreview.innerHTML = `
                <div style="color: #e53e3e; padding: 10px; background: #fff5f5; border: 1px solid #feb2b2; border-radius: 6px; margin-top: 10px;">
                    <strong>⚠️ Invalid file type!</strong> Please select an Excel file (.xlsx or .xls)
                </div>
            `;
            uploadBtn.disabled = true;
            event.target.value = '';
            return;
        }

        displayFilePreview(file, fileSize);
        uploadBtn.disabled = false;
    } else {
        filePreview.innerHTML = '';
        uploadBtn.disabled = true;
    }
}

function displayFilePreview(file, fileSize) {
    const filePreview = document.getElementById('filePreview');

    filePreview.innerHTML = `
        <div style="display: flex; align-items: center; gap: 12px; padding: 14px; background: #f0fdf4; border: 1px solid #86efac; border-radius: 8px; margin-top: 12px; animation: slideIn 0.3s ease;">
            <svg width="32" height="32" fill="none" stroke="#16a34a" stroke-width="2" viewBox="0 0 24 24">
                <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
                <polyline points="14 2 14 8 20 8" />
                <line x1="12" y1="11" x2="12" y2="17" />
                <line x1="9" y1="14" x2="15" y2="14" />
            </svg>
            <div style="flex: 1;">
                <div style="font-weight: 600; color: #166534; margin-bottom: 2px;">${file.name}</div>
                <div style="font-size: 13px; color: #15803d;">${fileSize} KB • Excel File</div>
            </div>
            <button type="button" onclick="clearFile()" style="background: none; border: none; cursor: pointer; color: #dc2626; padding: 8px; border-radius: 4px; transition: background 0.2s;" onmouseover="this.style.background='#fee2e2'" onmouseout="this.style.background='none'">
                <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <line x1="18" y1="6" x2="6" y2="18" />
                    <line x1="6" y1="6" x2="18" y2="18" />
                </svg>
            </button>
        </div>
    `;
}

function clearFile() {
    const excelFileInput = document.getElementById('excelFileInput');
    const filePreview = document.getElementById('filePreview');
    const uploadBtn = document.getElementById('uploadBtn');

    excelFileInput.value = '';
    filePreview.innerHTML = '';
    uploadBtn.disabled = true;
}

function showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        padding: 16px 24px;
        background: ${type === 'error' ? '#fee2e2' : type === 'success' ? '#d1fae5' : type === 'warning' ? '#fef3c7' : '#dbeafe'};
        color: ${type === 'error' ? '#991b1b' : type === 'success' ? '#065f46' : type === 'warning' ? '#92400e' : '#1e40af'};
        border: 1px solid ${type === 'error' ? '#fecaca' : type === 'success' ? '#a7f3d0' : type === 'warning' ? '#fde68a' : '#bfdbfe'};
        border-radius: 8px;
        box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        z-index: 10000;
        max-width: 400px;
        animation: slideInRight 0.3s ease;
    `;

    notification.innerHTML = `
        <div style="display: flex; align-items: center; gap: 12px;">
            <span style="flex: 1;">${message}</span>
            <button onclick="this.parentElement.parentElement.remove()" style="background: none; border: none; cursor: pointer; font-size: 20px; color: inherit; padding: 0; line-height: 1;">✕</button>
        </div>
    `;

    document.body.appendChild(notification);

    setTimeout(() => {
        notification.remove();
    }, 5000);
}

// Initialize drag and drop functionality
document.addEventListener('DOMContentLoaded', function () {
    const uploadArea = document.querySelector('.upload-area');
    const excelFileInput = document.getElementById('excelFileInput');

    if (uploadArea && excelFileInput) {
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            uploadArea.addEventListener(eventName, preventDefaults, false);
            document.body.addEventListener(eventName, preventDefaults, false);
        });

        function preventDefaults(e) {
            e.preventDefault();
            e.stopPropagation();
        }

        ['dragenter', 'dragover'].forEach(eventName => {
            uploadArea.addEventListener(eventName, () => {
                uploadArea.style.borderColor = '#3b82f6';
                uploadArea.style.background = '#eff6ff';
                uploadArea.style.transform = 'scale(1.02)';
            }, false);
        });

        ['dragleave', 'drop'].forEach(eventName => {
            uploadArea.addEventListener(eventName, () => {
                uploadArea.style.borderColor = '#d1d5db';
                uploadArea.style.background = '#f9fafb';
                uploadArea.style.transform = 'scale(1)';
            }, false);
        });

        uploadArea.addEventListener('drop', (e) => {
            const dt = e.dataTransfer;
            const files = dt.files;

            if (files.length > 0) {
                const file = files[0];
                const fileExtension = file.name.split('.').pop().toLowerCase();

                if (fileExtension === 'xlsx' || fileExtension === 'xls') {
                    excelFileInput.files = files;
                    handleFileSelect({ target: excelFileInput });
                } else {
                    const filePreview = document.getElementById('filePreview');
                    filePreview.innerHTML = `
                        <div style="color: #e53e3e; padding: 10px; background: #fff5f5; border: 1px solid #feb2b2; border-radius: 6px; margin-top: 10px;">
                            <strong>⚠️ Invalid file type!</strong> Please drop an Excel file (.xlsx or .xls)
                        </div>
                    `;
                }
            }
        }, false);
    }

    if (excelFileInput) {
        excelFileInput.addEventListener('change', handleFileSelect);
    }
});

// Close modal on outside click
document.getElementById('addEmployeeModal')?.addEventListener('click', function (e) {
    if (e.target === this) {
        closeModal();
    }
});

// Close modal on Escape key
document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        const modal = document.getElementById('addEmployeeModal');
        if (modal && modal.classList.contains('active')) {
            closeModal();
        }
    }
});

// Add CSS animations
const style = document.createElement('style');
style.textContent = `
    @keyframes slideIn {
        from {
            opacity: 0;
            transform: translateY(-10px);
        }
        to {
            opacity: 1;
            transform: translateY(0);
        }
    }

    @keyframes slideInRight {
        from {
            opacity: 0;
            transform: translateX(100px);
        }
        to {
            opacity: 1;
            transform: translateX(0);
        }
    }

    .upload-area {
        transition: all 0.3s ease;
    }

    .upload-area:hover {
        border-color: #9ca3af;
    }

    .animate-spin {
        animation: spin 1s linear infinite;
    }

    @keyframes spin {
        from {
            transform: rotate(0deg);
        }
        to {
            transform: rotate(360deg);
        }
    }
`;
document.head.appendChild(style);