// ============================================================================
// BULK IMPORT - JavaScript with Backend Integration
// ============================================================================

// Global variables
let managersCache = null;
let employeeNumberValid = false;
let emailValid = false;
let parsedEmployeeData = null;
let currentFile = null;

// ============================================================================
// MODAL FUNCTIONS
// ============================================================================

function openModal() {
    const modal = document.getElementById('addEmployeeModal');
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';
    loadInitialManagers();
}

function closeModal() {
    const modal = document.getElementById('addEmployeeModal');
    modal.classList.remove('active');
    document.body.style.overflow = '';

    // Reset all forms and states
    resetBulkImportModal();
    document.getElementById('singleForm').reset();

    // Reset validation states
    employeeNumberValid = false;
    emailValid = false;
    document.getElementById('employeeNumberValidation').textContent = '';
    document.getElementById('emailValidation').textContent = '';
}

function resetBulkImportModal() {
    // Reset to upload step
    showImportStep('uploadStep');

    // Clear file input
    const fileInput = document.getElementById('excelFileInput');
    if (fileInput) fileInput.value = '';

    // Reset data
    parsedEmployeeData = null;
    currentFile = null;

    // Reset button
    const importBtn = document.getElementById('importBtn');
    importBtn.disabled = true;
    importBtn.innerHTML = `
        <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <path d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
        </svg>
        <span>Import Employees</span>
    `;

    // Show footer
    document.getElementById('modalActionsFooter').style.display = 'flex';
}

function showImportStep(stepId) {
    // Hide all steps
    document.querySelectorAll('.import-step').forEach(step => {
        step.style.display = 'none';
    });

    // Show selected step
    const step = document.getElementById(stepId);
    if (step) {
        step.style.display = 'block';
        step.classList.add('active');
    }
}

// ============================================================================
// TOGGLE BETWEEN SINGLE AND BULK MODE
// ============================================================================

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

        if (!managersCache) {
            loadInitialManagers();
        }
    } else {
        bulkBtn.classList.add('active');
        singleBtn.classList.remove('active');
        bulkForm.style.display = 'block';
        singleForm.style.display = 'none';

        // Reset bulk import when switching
        resetBulkImportModal();
    }
}

// ============================================================================
// FILE UPLOAD AND VALIDATION
// ============================================================================

// Initialize file input listener
document.addEventListener('DOMContentLoaded', function () {
    const fileInput = document.getElementById('excelFileInput');
    if (fileInput) {
        fileInput.addEventListener('change', handleFileSelection);
    }

    // Drag and drop
    const uploadArea = document.getElementById('uploadArea');
    if (uploadArea) {
        setupDragAndDrop(uploadArea, fileInput);
    }
});

function handleFileSelection(event) {
    const file = event.target.files[0];
    if (!file) return;

    // Validate file type
    const fileExtension = file.name.split('.').pop().toLowerCase();
    if (fileExtension !== 'xlsx' && fileExtension !== 'xls') {
        showNotification('Invalid file type! Please select an Excel file (.xlsx or .xls)', 'error');
        event.target.value = '';
        return;
    }

    // Store file and start validation
    currentFile = file;
    validateExcelFile(file);
}

function changeFile() {
    document.getElementById('excelFileInput').click();
}

async function validateExcelFile(file) {
    // Show preview step
    showImportStep('previewStep');

    // Show upload progress
    document.getElementById('fileUploadProgress').style.display = 'block';
    document.getElementById('fileInfoCard').style.display = 'none';
    document.getElementById('employeePreviewSummary').style.display = 'none';

    // Update file upload progress
    updateUploadProgress(0);
    document.getElementById('uploadProgressTitle').textContent = 'Uploading File...';
    document.getElementById('uploadProgressSubtitle').textContent = 'Please wait';

    try {
        // Create form data
        const formData = new FormData();
        formData.append('excelFile', file);

        // Get anti-forgery token
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        // Simulate upload progress
        const progressInterval = setInterval(() => {
            const currentProgress = parseFloat(document.getElementById('uploadProgressFill').style.width) || 0;
            if (currentProgress < 90) {
                updateUploadProgress(currentProgress + 10);
            }
        }, 100);

        // Send validation request to backend
        const response = await fetch('/Home/ValidateExcelFile', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        clearInterval(progressInterval);
        updateUploadProgress(100);

        const data = await response.json();

        if (!data.success) {
            throw new Error(data.message || 'Validation failed');
        }

        // Mark upload as complete
        document.getElementById('uploadProgressTitle').textContent = 'File Uploaded Successfully';
        document.getElementById('uploadProgressSubtitle').textContent = 'Analyzing data...';
        document.querySelector('.icon-uploading').style.display = 'none';
        document.querySelector('.icon-success').style.display = 'block';

        // Wait a moment
        await new Promise(resolve => setTimeout(resolve, 500));

        // Hide upload progress, show file info
        document.getElementById('fileUploadProgress').style.display = 'none';
        document.getElementById('fileInfoCard').style.display = 'flex';

        // Display file info
        displayFileInfo(file);

        // Store validation data
        parsedEmployeeData = data;

        // Display preview
        displayEmployeePreview(data);

        // Enable/disable import button
        const importBtn = document.getElementById('importBtn');
        if (data.hasErrors) {
            importBtn.disabled = true;
            importBtn.classList.add('btn-disabled');
            showNotification('Please fix validation errors before importing', 'error');
        } else {
            importBtn.disabled = false;
            importBtn.classList.remove('btn-disabled');
        }

    } catch (error) {
        console.error('Error validating file:', error);
        showNotification(error.message || 'Failed to validate Excel file. Please try again.', 'error');
        resetBulkImportModal();
    }
}

function updateUploadProgress(percentage) {
    const fill = document.getElementById('uploadProgressFill');
    const text = document.getElementById('uploadProgressPercentage');

    fill.style.width = percentage + '%';
    text.textContent = Math.round(percentage) + '%';
}

function displayFileInfo(file) {
    const fileName = document.getElementById('fileName');
    const fileSize = document.getElementById('fileSize');

    fileName.textContent = file.name;
    fileSize.textContent = formatFileSize(file.size);
}

function formatFileSize(bytes) {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
}

function displayEmployeePreview(data) {
    // Show the preview summary
    document.getElementById('employeePreviewSummary').style.display = 'block';

    // Update statistics
    document.getElementById('totalEmployeesCount').textContent = data.totalEmployees;
    document.getElementById('validRowsCount').textContent = data.validRows;
    document.getElementById('totalDepartmentsCount').textContent = Object.keys(data.departments).length;

    // Display errors if any
    if (data.errors && data.errors.length > 0) {
        document.getElementById('errorStatCard').style.display = 'flex';
        document.getElementById('errorRowsCount').textContent = data.errors.length;
        displayValidationErrors(data.errors);
    } else {
        document.getElementById('errorStatCard').style.display = 'none';
        document.getElementById('validationErrors').style.display = 'none';
    }

    // Display department breakdown
    displayDepartmentBreakdown(data.departments);
}

function displayValidationErrors(errors) {
    const errorContainer = document.getElementById('validationErrors');
    const errorList = document.getElementById('errorList');

    errorContainer.style.display = 'block';
    errorList.innerHTML = '';

    errors.forEach(error => {
        const errorItem = document.createElement('div');
        errorItem.className = 'error-item';
        errorItem.innerHTML = `
            <div class="error-item-header">
                <span class="error-row-badge">Row ${error.row}</span>
                <span class="error-field-badge">${error.field}</span>
            </div>
            <div class="error-message">${error.message}</div>
        `;
        errorList.appendChild(errorItem);
    });
}

function displayDepartmentBreakdown(departments) {
    const container = document.getElementById('departmentListPreview');
    container.innerHTML = '';

    Object.entries(departments)
        .sort((a, b) => b[1] - a[1])
        .forEach(([dept, count]) => {
            const deptItem = document.createElement('div');
            deptItem.className = 'department-item-preview';
            deptItem.innerHTML = `
                <div class="dept-icon">
                    <svg width="20" height="20" fill="none" stroke="#00C2CB" stroke-width="2" viewBox="0 0 24 24">
                        <path d="M19 21V5a2 2 0 00-2-2H7a2 2 0 00-2 2v16m14 0h2m-2 0h-5m-9 0H3m2 0h5M9 7h1m-1 4h1m4-4h1m-1 4h1m-5 10v-5a1 1 0 011-1h2a1 1 0 011 1v5m-4 0h4" />
                    </svg>
                </div>
                <div class="dept-info">
                    <div class="dept-name">${dept}</div>
                    <div class="dept-count">${count} ${count === 1 ? 'employee' : 'employees'}</div>
                </div>
            `;
            container.appendChild(deptItem);
        });
}

// ============================================================================
// IMPORT PROCESS WITH 3 STAGES
// ============================================================================

async function startImport() {
    if (!currentFile || !parsedEmployeeData) {
        showNotification('No data to import', 'error');
        return;
    }

    if (parsedEmployeeData.hasErrors) {
        showNotification('Please fix validation errors before importing', 'error');
        return;
    }

    // Show import progress step
    showImportStep('importProgressStep');
    document.getElementById('modalActionsFooter').style.display = 'none';

    // Set total count
    document.getElementById('totalImportCount').textContent = parsedEmployeeData.totalEmployees;

    try {
        // Stage 1: Upload (0-33%)
        await runStage1();

        // Stage 2: Validation (33-66%)
        await runStage2();

        // Stage 3: Import (66-100%)
        const result = await runStage3();

        // Show success
        showSuccessStep(result);

    } catch (error) {
        console.error('Import failed:', error);
        showNotification(error.message || 'Import failed. Please try again.', 'error');

        // Wait a bit then reset
        setTimeout(() => {
            resetBulkImportModal();
        }, 3000);
    }
}

async function runStage1() {
    activateStage('stage1');
    addStatusMessage('Preparing file for upload...', 'info');

    await simulateProgress((percentage) => {
        updateStageProgress('stage1', percentage);
        updateOverallProgress(percentage * 0.33);
    }, 0, 100, 1000);

    completeStage('stage1');
    addStatusMessage('File prepared successfully', 'success');
}

async function runStage2() {
    activateStage('stage2');
    addStatusMessage('Validating employee data...', 'info');

    await simulateProgress((percentage) => {
        updateStageProgress('stage2', percentage);
        updateOverallProgress(33 + (percentage * 0.33));
    }, 0, 100, 800);

    completeStage('stage2');
    addStatusMessage('Validation complete', 'success');
}

async function runStage3() {
    activateStage('stage3');
    addStatusMessage('Importing employees to system...', 'info');

    // Create form data
    const formData = new FormData();
    formData.append('excelFile', currentFile);

    // Get anti-forgery token
    const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

    // Start progress simulation
    let currentProgress = 0;
    const progressInterval = setInterval(() => {
        if (currentProgress < 90) {
            currentProgress += 5;
            const percentage = Math.min(currentProgress, 90);
            updateStageProgress('stage3', percentage);
            updateOverallProgress(66 + (percentage * 0.34));

            const imported = Math.floor((percentage / 100) * parsedEmployeeData.totalEmployees);
            document.getElementById('importedCount').textContent = imported;

            if (imported % 3 === 0 && imported > 0) {
                addStatusMessage(`Processing employee ${imported}/${parsedEmployeeData.totalEmployees}...`, 'info');
            }
        }
    }, 150);

    try {
        // Send import request
        const response = await fetch('/Home/ImportEmployees', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData
        });

        const result = await response.json();

        clearInterval(progressInterval);

        if (!result.success) {
            throw new Error(result.message || 'Import failed');
        }

        // Complete progress
        updateStageProgress('stage3', 100);
        updateOverallProgress(100);
        document.getElementById('importedCount').textContent = parsedEmployeeData.totalEmployees;

        completeStage('stage3');
        addStatusMessage('Import completed successfully!', 'success');

        return result;

    } catch (error) {
        clearInterval(progressInterval);
        throw error;
    }
}

function activateStage(stageId) {
    const stage = document.getElementById(stageId);
    stage.classList.add('active');
    stage.querySelector('.stage-icon-svg').style.display = 'block';
    stage.querySelector('.stage-check').style.display = 'none';
}

function completeStage(stageId) {
    const stage = document.getElementById(stageId);
    stage.classList.remove('active');
    stage.classList.add('completed');
    stage.querySelector('.stage-icon-svg').style.display = 'none';
    stage.querySelector('.stage-check').style.display = 'block';
}

function updateStageProgress(stageId, percentage) {
    const stagePercentage = document.getElementById(stageId + 'Percentage');
    if (stagePercentage) {
        stagePercentage.textContent = Math.round(percentage) + '%';
    }
}

function updateOverallProgress(percentage) {
    const fill = document.getElementById('overallProgressFill');
    const text = document.getElementById('overallPercentage');

    fill.style.width = percentage + '%';
    text.textContent = Math.round(percentage) + '%';
}

function addStatusMessage(message, type = 'info') {
    const container = document.getElementById('statusMessagesList');

    const messageElement = document.createElement('div');
    messageElement.className = `status-message-item ${type}`;

    const icon = type === 'success'
        ? '<svg width="16" height="16" fill="none" stroke="#10B981" stroke-width="2" viewBox="0 0 24 24"><path d="M5 13l4 4L19 7"/></svg>'
        : type === 'error'
            ? '<svg width="16" height="16" fill="none" stroke="#EF4444" stroke-width="2" viewBox="0 0 24 24"><path d="M6 18L18 6M6 6l12 12"/></svg>'
            : '<svg width="16" height="16" fill="none" stroke="#00C2CB" stroke-width="2" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10"/><path d="M12 16v-4m0-4h.01"/></svg>';

    messageElement.innerHTML = `
        ${icon}
        <span>${message}</span>
        <span class="message-time">${new Date().toLocaleTimeString()}</span>
    `;

    container.appendChild(messageElement);
    container.scrollTop = container.scrollHeight;

    while (container.children.length > 8) {
        container.removeChild(container.firstChild);
    }
}

function showSuccessStep(result) {
    showImportStep('successStep');

    // Update success stats
    document.getElementById('finalSuccessCount').textContent = result.successCount || result.created + result.updated || parsedEmployeeData.totalEmployees;

    if (result.errorCount > 0) {
        document.getElementById('errorStatCardFinal').style.display = 'flex';
        document.getElementById('finalErrorCount').textContent = result.errorCount;
    }

    document.getElementById('successMessageText').textContent = result.message || `Successfully imported ${result.successCount} employees to the system.`;

    // Trigger confetti
    setTimeout(() => {
        createConfetti();
    }, 300);
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

async function simulateProgress(callback, start, end, duration) {
    const steps = 50;
    const stepDuration = duration / steps;
    const increment = (end - start) / steps;

    for (let i = 0; i <= steps; i++) {
        callback(start + (increment * i));
        await new Promise(resolve => setTimeout(resolve, stepDuration));
    }
}

function setupDragAndDrop(uploadArea, fileInput) {
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        uploadArea.addEventListener(eventName, preventDefaults, false);
    });

    function preventDefaults(e) {
        e.preventDefault();
        e.stopPropagation();
    }

    ['dragenter', 'dragover'].forEach(eventName => {
        uploadArea.addEventListener(eventName, () => {
            uploadArea.classList.add('dragging');
        });
    });

    ['dragleave', 'drop'].forEach(eventName => {
        uploadArea.addEventListener(eventName, () => {
            uploadArea.classList.remove('dragging');
        });
    });

    uploadArea.addEventListener('drop', (e) => {
        const files = e.dataTransfer.files;
        if (files.length > 0) {
            fileInput.files = files;
            handleFileSelection({ target: fileInput });
        }
    });
}

function viewEmployees() {
    window.location.reload();
}

function createConfetti() {
    const duration = 3 * 1000;
    const animationEnd = Date.now() + duration;
    const colors = ['#00C2CB', '#081B38', '#10B981', '#F59E0B'];

    (function frame() {
        const timeLeft = animationEnd - Date.now();
        if (timeLeft <= 0) return;

        const particleCount = 3;
        for (let i = 0; i < particleCount; i++) {
            const particle = document.createElement('div');
            particle.style.cssText = `
                position: fixed;
                width: 10px;
                height: 10px;
                background-color: ${colors[Math.floor(Math.random() * colors.length)]};
                left: ${Math.random() * window.innerWidth}px;
                top: -10px;
                border-radius: 50%;
                pointer-events: none;
                z-index: 10000;
                transition: all 3s ease-out;
            `;

            document.body.appendChild(particle);

            setTimeout(() => {
                particle.style.top = window.innerHeight + 'px';
                particle.style.opacity = '0';
            }, 10);

            setTimeout(() => particle.remove(), 3000);
        }

        requestAnimationFrame(frame);
    }());
}

function showNotification(message, type = 'info') {
    const notification = document.createElement('div');
    notification.className = `notification-toast ${type}`;
    notification.style.cssText = `
        position: fixed;
        top: 20px;
        right: 20px;
        padding: 16px 24px;
        background: ${type === 'error' ? '#FEE2E2' : type === 'success' ? '#D1FAE5' : type === 'warning' ? '#FEF3C7' : '#DBEAFE'};
        color: ${type === 'error' ? '#991B1B' : type === 'success' ? '#065F46' : type === 'warning' ? '#92400E' : '#1E40AF'};
        border: 1px solid ${type === 'error' ? '#FECACA' : type === 'success' ? '#A7F3D0' : type === 'warning' ? '#FDE68A' : '#BFDBFE'};
        border-radius: 12px;
        box-shadow: 0 10px 40px rgba(0, 0, 0, 0.1);
        z-index: 10000;
        max-width: 400px;
        animation: slideInRight 0.3s ease;
        display: flex;
        align-items: center;
        gap: 12px;
    `;

    notification.innerHTML = `
        <span style="flex: 1;">${message}</span>
        <button onclick="this.parentElement.remove()" style="background: none; border: none; cursor: pointer; font-size: 20px; color: inherit; padding: 0; line-height: 1;">✕</button>
    `;

    document.body.appendChild(notification);
    setTimeout(() => notification.remove(), 5000);
}

// ============================================================================
// SINGLE EMPLOYEE FORM FUNCTIONS
// ============================================================================

async function loadInitialManagers() {
    const dropdown = document.getElementById('managerDropdown');
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

async function updateManagerDropdown() {
    const position = document.getElementById('position').value.trim();
    const department = document.getElementById('department').value.trim();
    const dropdown = document.getElementById('managerDropdown');

    if (!position || !department) {
        if (managersCache) {
            populateManagerDropdown(managersCache);
        }
        return;
    }

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
                showNotification('No suitable managers found. Showing all managers.', 'info');
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

function populateManagerDropdown(managers) {
    const dropdown = document.getElementById('managerDropdown');
    dropdown.innerHTML = '<option value="">-- No Manager / CEO Level --</option>';

    if (!managers || managers.length === 0) {
        dropdown.innerHTML += '<option value="" disabled>No managers available</option>';
        return;
    }

    const managersByDept = {};
    managers.forEach(manager => {
        const dept = manager.department || 'Other';
        if (!managersByDept[dept]) {
            managersByDept[dept] = [];
        }
        managersByDept[dept].push(manager);
    });

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

    const submitBtn = document.getElementById('submitBtn');
    submitBtn.disabled = true;
    submitBtn.innerHTML = `
        <svg class="animate-spin" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <circle cx="12" cy="12" r="10" stroke="#00C2CB" stroke-opacity="0.25"/>
            <path d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z" stroke="#00C2CB"/>
        </svg>
        Creating Employee...
    `;

    return true;
}

// Event listeners
document.getElementById('addEmployeeModal')?.addEventListener('click', function (e) {
    if (e.target === this) {
        closeModal();
    }
});

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') {
        const modal = document.getElementById('addEmployeeModal');
        if (modal && modal.classList.contains('active')) {
            closeModal();
        }
    }
});