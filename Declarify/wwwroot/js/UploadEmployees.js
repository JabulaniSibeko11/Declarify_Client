// ============================================================================
// SIGNUP PAGE - BULK UPLOAD WITH ANALYSIS
// ============================================================================

let analyzedSignupData = null;

// Handle file selection on signup page
const signupFileInput = document.getElementById('employee-file');
if (signupFileInput) {
    signupFileInput.addEventListener('change', handleSignupFileSelect);
}

function handleSignupFileSelect(event) {
    const file = event.target.files[0];
    const filePreview = document.getElementById('filePreviewSignup');
    const uploadActions = document.getElementById('uploadActions');

    if (file) {
        const fileSize = (file.size / 1024).toFixed(2);
        const fileExtension = file.name.split('.').pop().toLowerCase();

        if (fileExtension !== 'xlsx' && fileExtension !== 'xls') {
            filePreview.innerHTML = `
                <div style="color: #e53e3e; padding: 10px; background: #fff5f5; border: 1px solid #feb2b2; border-radius: 6px; margin-top: 10px;">
                    <strong>⚠️ Invalid file type!</strong> Please select an Excel file (.xlsx or .xls)
                </div>
            `;
            uploadActions.style.display = 'none';
            event.target.value = '';
            return;
        }

        filePreview.innerHTML = `
            <div style="display: flex; align-items: center; gap: 12px; padding: 14px; background: #f0fdf4; border: 1px solid #86efac; border-radius: 8px; margin-top: 12px;">
                <svg width="32" height="32" fill="none" stroke="#16a34a" stroke-width="2" viewBox="0 0 24 24">
                    <path d="M14 2H6a2 2 0 00-2 2v16a2 2 0 002 2h12a2 2 0 002-2V8z" />
                    <polyline points="14 2 14 8 20 8" />
                </svg>
                <div style="flex: 1;">
                    <div style="font-weight: 600; color: #166534;">${file.name}</div>
                    <div style="font-size: 13px; color: #15803d;">${fileSize} KB • Excel File</div>
                </div>
            </div>
        `;
        uploadActions.style.display = 'flex';
    } else {
        filePreview.innerHTML = '';
        uploadActions.style.display = 'none';
    }
}

function clearSignupFile() {
    const fileInput = document.getElementById('employee-file');
    const filePreview = document.getElementById('filePreviewSignup');
    const uploadActions = document.getElementById('uploadActions');

    fileInput.value = '';
    filePreview.innerHTML = '';
    uploadActions.style.display = 'none';
    analyzedSignupData = null;
}

async function analyzeSignupFile() {
    const fileInput = document.getElementById('employee-file');
    const file = fileInput.files[0];

    if (!file) {
        showToast('Please select a file first', 'error');
        return;
    }

    const analyzeBtn = document.getElementById('analyzeSignupBtn');
    const originalBtnHTML = analyzeBtn.innerHTML;
    analyzeBtn.disabled = true;
    analyzeBtn.innerHTML = `
        <svg class="animate-spin" width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
            <circle cx="12" cy="12" r="10" stroke-opacity="0.25"/>
            <path d="M12 2a10 10 0 0110 10" stroke-linecap="round"/>
        </svg>
        Analyzing File...
    `;

    try {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('selectedPlan', document.getElementById('selected-plan')?.value || 'Free');

        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

        const response = await fetch('/Home/AnalyzeSignupEmployeeFile', {
            method: 'POST',
            headers: {
                'RequestVerificationToken': token
            },
            body: formData,
            credentials: 'same-origin'
        });

        const data = await response.json();

        if (data.success) {
            analyzedSignupData = data;
            showSignupAnalysisResults(data);
        } else {
            showToast(data.message || 'Failed to analyze file', 'error');
            analyzeBtn.disabled = false;
            analyzeBtn.innerHTML = originalBtnHTML;
        }
    } catch (error) {
        console.error('Analysis error:', error);
        showToast('Failed to analyze file. Please try again.', 'error');
        analyzeBtn.disabled = false;
        analyzeBtn.innerHTML = originalBtnHTML;
    }
}

function showSignupAnalysisResults(data) {
    document.getElementById('uploadStep').style.display = 'none';
    document.getElementById('analysisSignupStep').style.display = 'block';

    document.getElementById('totalSignupEmployeesCount').textContent = data.totalCount || 0;
    document.getElementById('validSignupEmployeesCount').textContent = data.validCount || 0;
    document.getElementById('warningsSignupCount').textContent = data.warningCount || 0;
    document.getElementById('errorsSignupCount').textContent = data.errorCount || 0;

    const analysisMessage = document.getElementById('analysisSignupMessage');
    if (data.errorCount > 0) {
        analysisMessage.textContent = `Found ${data.errorCount} error(s) that need attention`;
        analysisMessage.style.color = '#DC2626';
    } else if (data.warningCount > 0) {
        analysisMessage.textContent = `Found ${data.warningCount} warning(s) - review before importing`;
        analysisMessage.style.color = '#D97706';
    } else {
        analysisMessage.textContent = 'All records look good! Ready to import';
        analysisMessage.style.color = '#059669';
    }

    const previewCount = Math.min(5, data.totalCount);
    document.getElementById('previewSignupBadge').textContent = `Showing ${previewCount} of ${data.totalCount}`;

    populateSignupPreviewTable(data.preview || []);

    if (data.validationMessages && data.validationMessages.length > 0) {
        showSignupValidationMessages(data.validationMessages);
    }

    const importBtn = document.getElementById('importSignupBtn');
    if (data.errorCount > 0) {
        importBtn.disabled = true;
        importBtn.innerHTML = `
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                <path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z" />
            </svg>
            Fix Errors to Import
        `;
    } else {
        importBtn.disabled = false;
        importBtn.innerHTML = `
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                <path d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-8l-4-4m0 0L8 8m4-4v12" />
            </svg>
            Import ${data.validCount} Employees
        `;
    }
}

function populateSignupPreviewTable(previewData) {
    const tbody = document.getElementById('previewSignupTableBody');
    tbody.innerHTML = '';

    if (!previewData || previewData.length === 0) {
        tbody.innerHTML = `
            <tr>
                <td colspan="7" style="text-align: center; padding: 2rem; color: var(--color-text-muted);">
                    No preview data available
                </td>
            </tr>
        `;
        return;
    }

    previewData.forEach((employee, index) => {
        const statusClass = employee.hasError ? 'error' : employee.hasWarning ? 'warning' : 'valid';
        const statusIcon = employee.hasError ?
            '<svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>' :
            employee.hasWarning ?
                '<svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>' :
                '<svg width="14" height="14" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>';

        const statusText = employee.hasError ? 'Error' : employee.hasWarning ? 'Warning' : 'Valid';

        const row = document.createElement('tr');
        row.innerHTML = `
            <td style="font-weight: 600; color: var(--color-text-muted);">${index + 1}</td>
            <td><strong>${employee.employeeNumber || employee.Employee_Number || 'N/A'}</strong></td>
            <td>${employee.fullName || employee.Full_Name || ''} ${employee.surname || employee.Surname || ''}</td>
            <td>${employee.email || employee.Email || 'N/A'}</td>
            <td>${employee.position || employee.Position || 'N/A'}</td>
            <td>${employee.department || employee.Department || 'N/A'}</td>
            <td>
                <span class="status-badge-preview ${statusClass}">
                    ${statusIcon}
                    ${statusText}
                </span>
            </td>
        `;
        tbody.appendChild(row);
    });
}

function showSignupValidationMessages(messages) {
    const container = document.getElementById('validationSignupMessages');
    container.innerHTML = '';
    container.style.display = 'block';

    messages.forEach(msg => {
        const messageDiv = document.createElement('div');
        messageDiv.className = `validation-message-item ${msg.type}`;

        const icon = msg.type === 'error' ?
            '<svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>' :
            '<svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"/></svg>';

        messageDiv.innerHTML = `
            ${icon}
            <div class="validation-message-text">${msg.message}</div>
        `;
        container.appendChild(messageDiv);
    });
}

function backToSignupUpload() {
    document.getElementById('analysisSignupStep').style.display = 'none';
    document.getElementById('uploadStep').style.display = 'block';
    analyzedSignupData = null;
}

async function startSignupImport() {
    if (!analyzedSignupData) {
        showToast('Please analyze the file first', 'error');
        return;
    }

    const fileInput = document.getElementById('employee-file');
    const file = fileInput.files[0];

    if (!file) {
        showToast('File not found. Please upload again.', 'error');
        return;
    }

    document.getElementById('analysisSignupStep').style.display = 'none';
    document.getElementById('uploadSignupProgressContainer').style.display = 'block';

    document.getElementById('totalSignupCount').textContent = analyzedSignupData.validCount || 0;

    const formData = new FormData();
    formData.append('file', file);
    formData.append('selectedPlan', document.getElementById('selected-plan')?.value || 'Free');

    const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;

    try {
        simulateSignupProcessing(analyzedSignupData.validCount || 0);

        const response = await fetch('/Home/UploadEmployees', {
            method: 'POST',
            body: formData,
            headers: {
                'RequestVerificationToken': token
            },
            credentials: 'same-origin'
        });

        const data = await response.json();

        if (data.success) {
            uploadedEmployees = data.employees || [];
            uploadedLineManagerMappings = data.lineManagerMappings || {};
            uploadedFileInfo = data.fileInfo;

            setTimeout(() => {
                showSignupUploadSuccess(data);
            }, 1000);
        } else {
            showSignupUploadError(data.message || 'Import failed');
        }
    } catch (error) {
        console.error('Import error:', error);
        showSignupUploadError(error.message);
    }
}

function simulateSignupProcessing(totalCount) {
    let processedCount = 0;
    const interval = setInterval(() => {
        if (processedCount >= totalCount) {
            clearInterval(interval);
            return;
        }

        processedCount++;
        const percentage = Math.round((processedCount / totalCount) * 100);

        document.getElementById('uploadedSignupCount').textContent = processedCount;
        document.getElementById('successSignupRate').textContent = percentage + '%';
        updateSignupProgressBar(percentage);

        addSignupStatusMessage(
            `Employee ${processedCount} of ${totalCount} imported successfully`,
            'success'
        );

    }, 150);
}

function updateSignupProgressBar(percentage) {
    const progressFill = document.getElementById('progressSignupBarFill');
    const progressPercentage = document.getElementById('progressSignupPercentage');

    if (progressFill) progressFill.style.width = percentage + '%';
    if (progressPercentage) progressPercentage.textContent = percentage + '%';
}
function addSignupStatusMessage(message, type = 'success') {
    const statusMessages = document.getElementById('statusSignupMessages');
    if (!statusMessages) return;
    const messageDiv = document.createElement('div');
    messageDiv.className = `status-message ${type}`;

    const icon = type === 'success' ?
        '<svg class="status-message-icon" fill="none" stroke="#10B981" stroke-width="2" viewBox="0 0 24 24"><path d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>' :
        '<svg class="status-message-icon" fill="none" stroke="#EF4444" stroke-width="2" viewBox="0 0 24 24"><path d="M10 14l2-2m0 0l2-2m-2 2l-2-2m2 2l2 2m7-2a9 9 0 11-18 0 9 9 0 0118 0z"/></svg>';

    messageDiv.innerHTML = `${icon}<span>${message}</span>`;
    statusMessages.appendChild(messageDiv);

    statusMessages.scrollTop = statusMessages.scrollHeight;

    while (statusMessages.children.length > 10) {
        statusMessages.removeChild(statusMessages.firstChild);
    }
}
function showSignupUploadSuccess(response) {
    document.getElementById('uploadSignupProgressContainer').style.display = 'none';
    document.getElementById('uploadSignupSuccessContainer').style.display = 'block';
    document.getElementById('successSignupMessage').textContent =
        response.message || `${response.employees.length} employees imported successfully!`;
    document.getElementById('finalSignupSuccessCount').textContent = response.employees?.length || 0;
    document.getElementById('finalSignupErrorCount').textContent = response.errorCount || 0;

    updateEmployeeStats();
    updateAddEmployeeButton();

    createConfetti();
}
function showSignupUploadError(errorMessage) {
    document.getElementById('progressSignupTitle').textContent = 'Import Failed';
    document.getElementById('progressSignupSubtitle').textContent = errorMessage;
    document.getElementById('progressSignupTitle').style.color = '#EF4444';
    showToast(errorMessage, 'error');

    setTimeout(() => {
        backToSignupUpload();
        document.getElementById('uploadSignupProgressContainer').style.display = 'none';
    }, 3000);
}
function resetSignupUpload() {
    document.getElementById('uploadSignupSuccessContainer').style.display = 'none';
    document.getElementById('uploadStep').style.display = 'block';
    const fileInput = document.getElementById('employee-file');
    if (fileInput) fileInput.value = '';

    const filePreview = document.getElementById('filePreviewSignup');
    if (filePreview) filePreview.innerHTML = '';

    const uploadActions = document.getElementById('uploadActions');
    if (uploadActions) uploadActions.style.display = 'none';

    analyzedSignupData = null;
}