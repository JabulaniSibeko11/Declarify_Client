// Bulk Request Modal - Step Navigation & Employee Selection
// PRD: FR 4.3.1 - Admin bulk request functionality

// ============================================
// MODAL OPEN/CLOSE
// ============================================

console.log('Testing modal function...');
function openBulkRequestModal() {
    console.log('openBulkRequestModal called!');
    document.getElementById('bulkRequestModal').style.display = 'flex';
    resetBulkRequestModal();
}

function closeBulkRequestModal() {
    document.getElementById('bulkRequestModal').style.display = 'none';
}

// ============================================
// STEP NAVIGATION
// ============================================
let currentStep = 1;

document.addEventListener('DOMContentLoaded', function () {
    const nextBtn = document.getElementById('bulkNextBtn');
    const prevBtn = document.getElementById('bulkPrevBtn');
    const submitBtn = document.getElementById('bulkSubmitBtn');

    // Next button
    if (nextBtn) {
        nextBtn.addEventListener('click', function () {
            if (validateCurrentStep()) {
                if (currentStep < 3) {
                    currentStep++;
                    updateStepDisplay();
                }
            }
        });
    }

    // Previous button
    if (prevBtn) {
        prevBtn.addEventListener('click', function () {
            if (currentStep > 1) {
                currentStep--;
                updateStepDisplay();
            }
        });
    }

    // Tab switching for employee selection
    const tabButtons = document.querySelectorAll('.tab-btn');
    tabButtons.forEach(btn => {
        btn.addEventListener('click', function () {
            const tab = this.getAttribute('data-tab');
            switchTab(tab);
        });
    });

    // Select All checkbox
    const selectAllCheck = document.getElementById('selectAllEmployees');
    if (selectAllCheck) {
        selectAllCheck.addEventListener('change', function () {
            handleSelectAll(this.checked);
        });
    }

    // Department checkboxes
    const deptCheckboxes = document.querySelectorAll('.dept-checkbox');
    deptCheckboxes.forEach(checkbox => {
        checkbox.addEventListener('change', function () {
            handleDepartmentSelection();
        });
    });

    // Template selection
    const templateSelect = document.getElementById('templateSelect');
    if (templateSelect) {
        templateSelect.addEventListener('change', updatePreview);
    }

    // Due date
    const dueDate = document.getElementById('dueDate');
    if (dueDate) {
        dueDate.addEventListener('change', updatePreview);
    }

    // Department search
    const deptSearch = document.getElementById('deptSearch');
    if (deptSearch) {
        deptSearch.addEventListener('input', function () {
            filterDepartments(this.value);
        });
    }
});

// ============================================
// STEP DISPLAY UPDATE
// ============================================
function updateStepDisplay() {
    // Update step indicators
    const steps = document.querySelectorAll('.bulk-step');
    steps.forEach((step, index) => {
        if (index + 1 <= currentStep) {
            step.classList.add('active');
        } else {
            step.classList.remove('active');
        }
    });

    // Update step content
    const contents = document.querySelectorAll('.bulk-step-content');
    contents.forEach(content => {
        const stepNum = parseInt(content.getAttribute('data-step'));
        if (stepNum === currentStep) {
            content.classList.add('active');
        } else {
            content.classList.remove('active');
        }
    });

    // Update buttons
    const prevBtn = document.getElementById('bulkPrevBtn');
    const nextBtn = document.getElementById('bulkNextBtn');
    const submitBtn = document.getElementById('bulkSubmitBtn');

    if (currentStep === 1) {
        prevBtn.style.display = 'none';
    } else {
        prevBtn.style.display = 'inline-block';
    }

    if (currentStep === 3) {
        nextBtn.style.display = 'none';
        submitBtn.style.display = 'inline-block';
        updatePreview();
    } else {
        nextBtn.style.display = 'inline-block';
        submitBtn.style.display = 'none';
    }
}

// ============================================
// VALIDATION
// ============================================
function validateCurrentStep() {
    if (currentStep === 1) {
        // Validate template selection
        const templateSelect = document.getElementById('templateSelect');
        if (!templateSelect.value) {
            alert('Please select a DOI template.');
            return false;
        }
    }

    if (currentStep === 2) {
        // Validate employee selection
        const selectedCount = getSelectedEmployeeCount();
        if (selectedCount === 0) {
            alert('Please select at least one employee.');
            return false;
        }
    }

    return true;
}

// ============================================
// EMPLOYEE SELECTION
// ============================================
let selectedEmployeeIds = [];

function handleSelectAll(isChecked) {
    if (isChecked) {
        // Get all employee IDs from Model.BulkData.Employees
        // This will be populated by the server
        const allEmployees = window.allEmployeeIds || [];
        selectedEmployeeIds = [...allEmployees];
    } else {
        selectedEmployeeIds = [];
    }

    // Uncheck all department checkboxes
    const deptCheckboxes = document.querySelectorAll('.dept-checkbox');
    deptCheckboxes.forEach(cb => cb.checked = false);

    updateEmployeeCounter();
    updateHiddenField();
}

function handleDepartmentSelection() {
    // Uncheck "Select All"
    const selectAllCheck = document.getElementById('selectAllEmployees');
    if (selectAllCheck) {
        selectAllCheck.checked = false;
    }

    // Calculate selected employees from departments
    selectedEmployeeIds = [];
    const deptCheckboxes = document.querySelectorAll('.dept-checkbox:checked');

    deptCheckboxes.forEach(checkbox => {
        const dept = checkbox.getAttribute('data-dept');
        const deptEmployees = window.departmentEmployeeIds[dept] || [];
        selectedEmployeeIds.push(...deptEmployees);
    });

    // Remove duplicates
    selectedEmployeeIds = [...new Set(selectedEmployeeIds)];

    updateEmployeeCounter();
    updateHiddenField();
}

function getSelectedEmployeeCount() {
    return selectedEmployeeIds.length;
}

function updateEmployeeCounter() {
    const counter = document.getElementById('selectedCount');
    if (counter) {
        counter.textContent = selectedEmployeeIds.length;
    }
}

function updateHiddenField() {
    const hiddenField = document.getElementById('employeeIdsHidden');
    if (hiddenField) {
        hiddenField.value = JSON.stringify(selectedEmployeeIds);
    }
}

// ============================================
// TAB SWITCHING
// ============================================
function switchTab(tabName) {
    // Update tab buttons
    const tabButtons = document.querySelectorAll('.tab-btn');
    tabButtons.forEach(btn => {
        if (btn.getAttribute('data-tab') === tabName) {
            btn.classList.add('active');
        } else {
            btn.classList.remove('active');
        }
    });

    // Update tab content
    const allTab = document.getElementById('allTab');
    const deptTab = document.getElementById('deptTab');

    if (tabName === 'all') {
        allTab.classList.add('active');
        deptTab.classList.remove('active');
    } else if (tabName === 'dept') {
        allTab.classList.remove('active');
        deptTab.classList.add('active');
    }
}

// ============================================
// DEPARTMENT SEARCH
// ============================================
function filterDepartments(searchTerm) {
    const cards = document.querySelectorAll('.department-card');
    const term = searchTerm.toLowerCase();

    cards.forEach(card => {
        const deptName = card.querySelector('.department-name').textContent.toLowerCase();
        if (deptName.includes(term)) {
            card.style.display = 'flex';
        } else {
            card.style.display = 'none';
        }
    });
}

// ============================================
// PREVIEW UPDATE
// ============================================
function updatePreview() {
    // Template
    const templateSelect = document.getElementById('templateSelect');
    const previewTemplate = document.getElementById('previewTemplate');
    if (templateSelect && previewTemplate) {
        const selectedOption = templateSelect.options[templateSelect.selectedIndex];
        previewTemplate.textContent = selectedOption.text || 'Not selected';
    }

    // Employees
    const previewEmployees = document.getElementById('previewEmployees');
    if (previewEmployees) {
        previewEmployees.textContent = selectedEmployeeIds.length + ' employees';
    }

    // Due Date
    const dueDate = document.getElementById('dueDate');
    const previewDue = document.getElementById('previewDue');
    if (dueDate && previewDue) {
        if (dueDate.value) {
            const date = new Date(dueDate.value);
            previewDue.textContent = date.toLocaleString('en-ZA', {
                year: 'numeric',
                month: 'long',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit'
            });
        } else {
            previewDue.textContent = 'Not set';
        }
    }

    // Update submit button text
    const submitBtn = document.getElementById('bulkSubmitBtn');
    if (submitBtn) {
        submitBtn.innerHTML = `📤 Send Requests (${selectedEmployeeIds.length})`;
    }
}

// ============================================
// RESET MODAL
// ============================================
function resetBulkRequestModal() {
    currentStep = 1;
    selectedEmployeeIds = [];

    // Reset form
    const form = document.getElementById('bulkRequestForm');
    if (form) {
        form.reset();
    }

    // Reset checkboxes
    const selectAll = document.getElementById('selectAllEmployees');
    if (selectAll) selectAll.checked = false;

    const deptCheckboxes = document.querySelectorAll('.dept-checkbox');
    deptCheckboxes.forEach(cb => cb.checked = false);

    // Reset tabs
    switchTab('all');

    // Reset counter
    updateEmployeeCounter();
    updateHiddenField();

    // Reset step display
    updateStepDisplay();
}

// ============================================
// HELPER: Initialize employee data from server
// ============================================
// This function should be called after the modal is rendered
// to populate employee IDs from the Razor Model
function initializeEmployeeData(allEmployeeIds, departmentEmployeeMap) {
    window.allEmployeeIds = allEmployeeIds;
    window.departmentEmployeeIds = departmentEmployeeMap;
}