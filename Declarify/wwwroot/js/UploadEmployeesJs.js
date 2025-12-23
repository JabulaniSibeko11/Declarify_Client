// Modal Functions
function openModal() {
    const modal = document.getElementById('addEmployeeModal');
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';
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
    } else {
        bulkBtn.classList.add('active');
        singleBtn.classList.remove('active');
        bulkForm.style.display = 'block';
        singleForm.style.display = 'none';
    }
}

// File upload handling
function handleFileSelect(event) {
    const file = event.target.files[0];
    const filePreview = document.getElementById('filePreview');
    const uploadBtn = document.getElementById('uploadBtn');

    if (file) {
        const fileSize = (file.size / 1024).toFixed(2); // Convert to KB
        const fileExtension = file.name.split('.').pop().toLowerCase();

        // Validate file type
        if (fileExtension !== 'xlsx' && fileExtension !== 'xls') {
            filePreview.innerHTML = `
                <div style="color: #e53e3e; padding: 10px; background: #fff5f5; border: 1px solid #feb2b2; border-radius: 6px; margin-top: 10px;">
                    <strong>⚠️ Invalid file type!</strong> Please select an Excel file (.xlsx or .xls)
                </div>
            `;
            uploadBtn.disabled = true;
            event.target.value = ''; // Clear the input
            return;
        }

        // Display file info
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

// Initialize drag and drop functionality when DOM is loaded
document.addEventListener('DOMContentLoaded', function () {
    const uploadArea = document.querySelector('.upload-area');
    const excelFileInput = document.getElementById('excelFileInput');

    if (uploadArea && excelFileInput) {
        // Prevent default drag behaviors
        ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
            uploadArea.addEventListener(eventName, preventDefaults, false);
            document.body.addEventListener(eventName, preventDefaults, false);
        });

        function preventDefaults(e) {
            e.preventDefault();
            e.stopPropagation();
        }

        // Highlight drop area when item is dragged over it
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

        // Handle dropped files
        uploadArea.addEventListener('drop', (e) => {
            const dt = e.dataTransfer;
            const files = dt.files;

            if (files.length > 0) {
                const file = files[0];
                const fileExtension = file.name.split('.').pop().toLowerCase();

                // Validate file type
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

    // File input change handler
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

// Add CSS animation for file preview
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

    .upload-area {
        transition: all 0.3s ease;
    }

    .upload-area:hover {
        border-color: #9ca3af;
    }
`;
document.head.appendChild(style);