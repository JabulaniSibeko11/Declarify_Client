// ============================================================================
// TEMPLATE BUILDER - FIXED VERSION WITH RELIABLE SECTION ID TRACKING
// ============================================================================

let currentStep = 1;
let sections = [];
let sectionIdCounter = 0;
let fieldIdCounter = 0;
let currentEditingField = null;
let currentAddFieldSection = null;
let selectedFieldType = null;

// Advanced Table Builder State
const advancedTableBuilder = {
    currentFieldId: null,
    currentSectionId: null,
    step: 1,
    columns: [],
    rows: 4,
    gridColumns: 3,
    gridCells: [],
    selectedCells: [],
    fieldLabel: '',
    fieldHelpText: '',
    fieldRequired: false,

    init(fieldId, sectionId) {
        this.currentFieldId = fieldId;
        this.currentSectionId = sectionId;
        this.step = 1;
        this.columns = [];
        this.rows = 4;
        this.gridColumns = 3;
        this.gridCells = [];
        this.selectedCells = [];
        this.fieldLabel = '';
        this.fieldHelpText = '';
        this.fieldRequired = false;
    }
};

// ============================================================================
// WIZARD NAVIGATION
// ============================================================================

function openTemplateBuilder() {
    const modal = document.getElementById('templateBuilderModal');
    if (!modal) {
        console.error('Template builder modal not found');
        return;
    }
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';
    goToStep(1);
    resetBuilder();
}

function closeTemplateBuilder() {
    if (!confirm('Are you sure? All unsaved changes will be lost.')) return;
    const modal = document.getElementById('templateBuilderModal');
    if (modal) {
        modal.classList.remove('active');
        document.body.style.overflow = '';
    }
    resetBuilder();
}

function goToStep(stepNumber) {
    if (stepNumber > currentStep) {
        if (currentStep === 1 && !validateStep1()) return;
        if (currentStep === 2 && !validateStep2()) return;
    }

    document.querySelectorAll('.wizard-step').forEach(step => {
        step.style.display = 'none';
    });

    const stepElement = document.getElementById(`step${stepNumber}`);
    if (stepElement) {
        stepElement.style.display = 'block';
    }

    document.querySelectorAll('.progress-step').forEach(step => {
        step.classList.remove('active', 'completed');
        const stepNum = parseInt(step.dataset.step);
        if (stepNum === stepNumber) {
            step.classList.add('active');
        } else if (stepNum < stepNumber) {
            step.classList.add('completed');
        }
    });

    currentStep = stepNumber;

    if (stepNumber === 3) {
        generateFullPreview();
    }
}

function goToDashboard() {
    window.location.href = '/Home/Dashboard';
}

function resetBuilder() {
    currentStep = 1;
    sections = [];
    sectionIdCounter = 0;
    fieldIdCounter = 0;

    const templateName = document.getElementById('templateName');
    const templateDescription = document.getElementById('templateDescription');
    const defaultDueDays = document.getElementById('defaultDueDays');
    const reminder7days = document.getElementById('reminder7days');
    const reminderDueDate = document.getElementById('reminderDueDate');

    if (templateName) templateName.value = '';
    if (templateDescription) templateDescription.value = '';
    if (defaultDueDays) defaultDueDays.value = '30';
    if (reminder7days) reminder7days.checked = true;
    if (reminderDueDate) reminderDueDate.checked = true;

    const sectionsContainer = document.getElementById('sectionsContainer');
    if (sectionsContainer) {
        sectionsContainer.innerHTML = `
            <div class="empty-state">
                <div class="empty-icon">📋</div>
                <h3>Start Building Your Template</h3>
                <p>Add your first section to begin</p>
                <button class="btn btn-primary" onclick="addSection()">
                    <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                        <path d="M12 4v16m8-8H4" />
                    </svg>
                    Add First Section
                </button>
            </div>
        `;
    }
}

// Validation functions
function validateStep1() {
    const nameInput = document.getElementById('templateName');
    const dueDaysInput = document.getElementById('defaultDueDays');

    const name = nameInput ? nameInput.value.trim() : '';
    const dueDays = dueDaysInput ? parseInt(dueDaysInput.value) : 0;

    if (!name) {
        alert('⚠️ Please enter a template name');
        if (nameInput) nameInput.focus();
        return false;
    }

    if (!dueDays || dueDays < 1 || dueDays > 365) {
        alert('⚠️ Due date must be between 1 and 365 days');
        if (dueDaysInput) dueDaysInput.focus();
        return false;
    }

    return true;
}

function validateStep2() {
    if (sections.length === 0) {
        if (!confirm('⚠️ Your template has no sections. Continue anyway?')) {
            return false;
        }
    }

    for (const section of sections) {
        if (!section.sectionTitle.trim()) {
            alert('⚠️ Please provide a title for all sections');
            return false;
        }

        if (section.fields.length === 0) {
            if (!confirm(`⚠️ Section "${section.sectionTitle}" has no fields. Continue anyway?`)) {
                return false;
            }
        }
    }

    return true;
}

// Add CSS for spin animation
const style = document.createElement('style');
style.textContent = `
            @keyframes spin {
                from { transform: rotate(0deg); }
                to { transform: rotate(360deg); }
            }
        `;
document.head.appendChild(style);

// ============================================================================
// SECTION MANAGEMENT
// ============================================================================

function addSection() {
    const container = document.getElementById('sectionsContainer');
    if (!container) return;

    const emptyState = container.querySelector('.empty-state');
    if (emptyState) emptyState.remove();

    const sectionId = `section_${sectionIdCounter++}`;
    const section = {
        sectionId: sectionId,
        sectionTitle: '',
        sectionOrder: sections.length + 1,
        disclaimer: '',
        fields: []
    };

    sections.push(section);

    const sectionHtml = `
        <div class="section-card-builder" data-section-id="${sectionId}">
            <div class="section-header-builder">
                <div class="drag-handle" title="Drag to reorder">⋮⋮</div>
                <input type="text" class="section-title-input" placeholder="Section Title (e.g., Shares and Securities)" 
                       onblur="updateSectionTitle('${sectionId}', this.value)" value="">
                <button class="btn-icon-delete" onclick="deleteSection('${sectionId}')" title="Delete Section">
                    <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                        <path d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                </button>
            </div>
            
            <div class="section-disclaimer">
                <input type="text" class="disclaimer-input" placeholder="Optional: Instructions or disclaimer for this section..." 
                       onblur="updateSectionDisclaimer('${sectionId}', this.value)">
            </div>
            
            <div class="fields-container-builder" id="fields-${sectionId}">
               <div class="empty-fields">
                    <p class="clickable-add-field" onclick="showAddFieldModal('${sectionId}')">
                        <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                            <path d="M12 4v16m8-8H4" />
                        </svg>
                        No fields yet. Click here to add a field
                    </p>
                </div>
             </div>
            
             <div class="section-actions">
                <button class="btn btn-ghost btn-sm" style="background-color:#00C2CB; color:#081B38;" onclick="showAddFieldModal('${sectionId}')">
                    <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                        <path d="M12 4v16m8-8H4" />
                    </svg>
                    Add Field
                </button>
                 <span class="field-count" id="fieldCount-${sectionId}">0 fields</span>
            </div>
        </div>
    `;

    container.insertAdjacentHTML('beforeend', sectionHtml);
    updateAddSectionButton();
}

function updateAddSectionButton() {
    const container = document.getElementById('sectionsContainer');
    if (!container) return;

    let addBtn = container.querySelector('.add-section-btn');

    if (!addBtn) {
        container.insertAdjacentHTML('beforeend', `
            <button class="btn btn-ghost add-section-btn" style="background-color:#00C2CB; color:#081B38;" onclick="addSection()">
                <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path d="M12 4v16m8-8H4" />
                </svg>
                Add Another Section
            </button>
        `);
    }
}

function updateSectionTitle(sectionId, value) {
    const section = sections.find(s => s.sectionId === sectionId);
    if (section) section.sectionTitle = value;
}

function updateSectionDisclaimer(sectionId, value) {
    const section = sections.find(s => s.sectionId === sectionId);
    if (section) section.disclaimer = value;
}

function deleteSection(sectionId) {
    const section = sections.find(s => s.sectionId === sectionId);
    const fieldCount = section ? section.fields.length : 0;

    if (fieldCount > 0) {
        if (!confirm(`Delete this section and its ${fieldCount} field(s)?`)) return;
    }

    sections = sections.filter(s => s.sectionId !== sectionId);
    const sectionElement = document.querySelector(`[data-section-id="${sectionId}"]`);
    if (sectionElement) sectionElement.remove();

    const container = document.getElementById('sectionsContainer');
    if (sections.length === 0 && container) {
        container.innerHTML = `
            <div class="empty-state">
                <div class="empty-icon">📋</div>
                <h3>Start Building Your Template</h3>
                <p>Add your first section to begin</p>
                <button class="btn btn-primary" onclick="addSection()">
                    <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                        <path d="M12 4v16m8-8H4" />
                    </svg>
                    Add First Section
                </button>
            </div>
        `;
    }
}

// ============================================================================
// FIELD MANAGEMENT
// ============================================================================

function getFieldTypeInfo(fieldType) {
    const types = {
        text: { label: 'Text', icon: '📝', color: '#3B82F6' },
        textarea: { label: 'Long Text', icon: '📄', color: '#8B5CF6' },
        email: { label: 'Email', icon: '📧', color: '#10B981' },
        number: { label: 'Number', icon: '#️⃣', color: '#F59E0B' },
        currency: { label: 'Currency', icon: '💰', color: '#10B981' },
        date: { label: 'Date', icon: '📅', color: '#EC4899' },
        boolean: { label: 'Yes/No', icon: '✓', color: '#6366F1' },
        checkbox: { label: 'Checkbox', icon: '☑', color: '#14B8A6' },
        select: { label: 'Dropdown', icon: '▼', color: '#8B5CF6' },
        radio: { label: 'Radio', icon: '◉', color: '#3B82F6' },
        file: { label: 'File Upload', icon: '📎', color: '#F59E0B' },
        phone: { label: 'Phone', icon: '📞', color: '#10B981' },
        url: { label: 'URL', icon: '🔗', color: '#3B82F6' },
        table: { label: 'Simple Table', icon: '⊞', color: '#6366F1' },
        advancedTable: { label: 'Advanced Table', icon: '⊞⊞', color: '#8B5CF6' },
        signature: { label: 'Signature', icon: '✍', color: '#EC4899' },
        heading: { label: 'Heading', icon: 'H', color: '#64748B' },
        paragraph: { label: 'Paragraph', icon: '¶', color: '#64748B' },
        divider: { label: 'Divider', icon: '—', color: '#CBD5E1' }
    };
    return types[fieldType] || { label: 'Unknown', icon: '?', color: '#94A3B8' };
}

function generateFieldPreview(field) {
    switch (field.fieldType) {
        case 'text':
        case 'email':
        case 'url':
        case 'phone':
            return `<input type="text" disabled placeholder="${escapeHTML(field.placeholder || 'Enter ' + field.fieldType)}">`;
        case 'textarea':
            return `<textarea disabled rows="2" placeholder="${escapeHTML(field.placeholder || 'Enter long text')}"></textarea>`;
        case 'number':
        case 'currency':
            return `<input type="number" disabled placeholder="${escapeHTML(field.placeholder || '0')}">`;
        case 'date':
            return `<input type="date" disabled>`;
        case 'boolean':
            return `<div class="radio-group-preview"><label><input type="radio" disabled> Yes</label><label><input type="radio" disabled> No</label></div>`;
        case 'checkbox':
            return `<label class="checkbox-label"><input type="checkbox" disabled> ${escapeHTML(field.placeholder || 'Check this')}</label>`;
        case 'select':
            return `<select disabled><option>${escapeHTML(field.placeholder || 'Select option')}</option>${(field.options || []).map(opt => `<option>${escapeHTML(opt)}</option>`).join('')}</select>`;
        case 'radio':
            return `<div class="radio-group-preview">${(field.options || []).map(opt => `<label><input type="radio" name="prev-${field.fieldId}" disabled> ${escapeHTML(opt)}</label>`).join('')}</div>`;
        case 'file':
            return `<div class="file-upload-preview"><svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24"><path d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" /></svg><span>Click to upload file</span></div>`;
        case 'table': {
            const columns = getTableColumns(field);

            if (!columns.length) {
                return `<div class="table-preview warning">⚠ No columns defined</div>`;
            }

            return `
        <table class="preview-table">
            <thead>
                <tr>
                    ${columns.map(col => `<th>${escapeHTML(col.name || col)}</th>`).join('')}
                </tr>
            </thead>
            <tbody>
                <tr>
                    ${columns.map(() => `<td>—</td>`).join('')}
                </tr>
            </tbody>
        </table>
    `;
        }
        case 'advancedTable': {
            const columns = field.columns || [];

            if (!columns.length) {
                return `<div class="table-preview warning">⚠ No columns defined</div>`;
            }

            return `
        <table class="preview-table advanced">
            <thead>
                <tr>
                    ${columns.map(col => `<th>${escapeHTML(col.name || col)}</th>`).join('')}
                </tr>
            </thead>
            <tbody>
                <tr>
                    ${columns.map(() => `<td>—</td>`).join('')}
                </tr>
            </tbody>
        </table>
    `;
        }
        case 'signature':
            return `<div class="signature-preview"><div class="signature-pad">✍️ Signature area</div></div>`;
        case 'heading':
            return `<h3 class="heading-preview">${escapeHTML(field.fieldLabel || 'Heading')}</h3>`;
        case 'paragraph':
            return `<p class="paragraph-preview">${escapeHTML(field.helpText || 'Paragraph text...')}</p>`;
        case 'divider':
            return `<hr class="divider-preview">`;
        default:
            return `<input type="text" disabled placeholder="Field preview">`;
    }
}

function renderFieldInSection(sectionId, field) {
    const fieldsContainer = document.getElementById(`fields-${sectionId}`);
    if (!fieldsContainer) return;

    const emptyFields = fieldsContainer.querySelector('.empty-fields');
    if (emptyFields) emptyFields.remove();

    const fieldTypeInfo = getFieldTypeInfo(field.fieldType);

    const fieldHtml = `
        <div class="field-card-builder" data-field-id="${field.fieldId}">
            <div class="field-header-builder">
                <div class="drag-handle-small" title="Drag to reorder">⋮⋮</div>
                <span class="field-type-badge" style="background: ${fieldTypeInfo.color};">
                    ${fieldTypeInfo.icon} ${fieldTypeInfo.label}
                </span>
                <div class="field-actions-inline">
                    <button class="btn-icon-edit" onclick="editField('${sectionId}', '${field.fieldId}')" title="Edit">
                        <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                            <path d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                        </svg>
                    </button>
                    <button class="btn-icon-delete-small" onclick="deleteField('${sectionId}', '${field.fieldId}')" title="Delete">×</button>
                </div>
            </div>
            <div class="field-label-display">
                ${escapeHTML(field.fieldLabel || '<em>No label set</em>')}
                ${field.required ? '<span class="required-mark">*</span>' : ''}
            </div>
            <div class="field-preview-mini" id="preview-${field.fieldId}">
                ${generateFieldPreview(field)}
            </div>
        </div>
    `;

    fieldsContainer.insertAdjacentHTML('beforeend', fieldHtml);
    updateFieldCount(sectionId);
}

function updateFieldCount(sectionId) {
    const section = sections.find(s => s.sectionId === sectionId);
    if (!section) return;

    const countEl = document.getElementById(`fieldCount-${sectionId}`);
    if (countEl) {
        const count = section.fields.length;
        countEl.textContent = `${count} field${count !== 1 ? 's' : ''}`;
    }
}

function deleteField(sectionId, fieldId) {
    if (!confirm('Delete this field?')) return;

    const section = sections.find(s => s.sectionId === sectionId);
    if (!section) return;

    section.fields = section.fields.filter(f => f.fieldId !== fieldId);
    const fieldElement = document.querySelector(`[data-field-id="${fieldId}"]`);
    if (fieldElement) fieldElement.remove();

    updateFieldCount(sectionId);

    const fieldsContainer = document.getElementById(`fields-${sectionId}`);
    if (section.fields.length === 0 && fieldsContainer) {
        fieldsContainer.innerHTML = `
            <div class="empty-fields">
                <p class="clickable-add-field" onclick="showAddFieldModal('${sectionId}')">
                    <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                        <path d="M12 4v16m8-8H4" />
                    </svg>
                    No fields yet. Click here to add a field
                </p>
            </div>
        `;
    }
}

function editField(sectionId, fieldId) {
    const section = sections.find(s => s.sectionId === sectionId);
    if (!section) return;

    const field = section.fields.find(f => f.fieldId === fieldId);
    if (!field) return;

    currentEditingField = { sectionId, fieldId };
    currentAddFieldSection = sectionId;
    selectedFieldType = field.fieldType;

    if (field.fieldType === 'table' || field.fieldType === 'advancedTable') {
        alert('Editing tables is not yet implemented. Please delete and recreate.');
        return;
    }

    showAddFieldModal(sectionId);
    setTimeout(() => {
        populateEditForm(field);
    }, 100);
}

function populateEditForm(field) {
    const labelInput = document.getElementById('newFieldLabel');
    const helpTextInput = document.getElementById('newFieldHelpText');
    const requiredCheckbox = document.getElementById('newFieldRequired');
    const placeholderInput = document.getElementById('newFieldPlaceholder');

    if (labelInput) labelInput.value = field.fieldLabel || '';
    if (helpTextInput) helpTextInput.value = field.helpText || '';
    if (requiredCheckbox) requiredCheckbox.checked = field.required || false;
    if (placeholderInput) placeholderInput.value = field.placeholder || '';

    if (['number', 'currency'].includes(field.fieldType)) {
        const minInput = document.getElementById('newFieldMinValue');
        const maxInput = document.getElementById('newFieldMaxValue');
        if (minInput && field.validation?.min) minInput.value = field.validation.min;
        if (maxInput && field.validation?.max) maxInput.value = field.validation.max;
    }

    if (['select', 'radio'].includes(field.fieldType)) {
        const optionsTextarea = document.getElementById('newFieldOptions');
        if (optionsTextarea && field.options) {
            optionsTextarea.value = field.options.join('\n');
        }
    }
}


// ============================================================================
// ADD/EDIT FIELD MODAL
// ============================================================================

function showAddFieldModal(sectionId) {
    if (!sectionId) {
        console.error('❌ showAddFieldModal called without section ID');
        alert('⚠️ Error: No section specified. Please try again.');
        return;
    }

    console.log('✅ showAddFieldModal called for section:', sectionId);

    currentAddFieldSection = sectionId;
    if (!currentEditingField) {
        selectedFieldType = null;
    }

    const selectStep = document.getElementById('selectFieldTypeStep');
    const configStep = document.getElementById('configureFieldStep');

    if (selectStep) selectStep.style.display = 'block';
    if (configStep) configStep.style.display = 'none';

    const modal = document.getElementById('addFieldModal');
    if (modal) {
        modal.classList.add('active');
    }

    console.log('→ Section ID stored as:', currentAddFieldSection);
}

function selectFieldType(fieldType) {
    console.log('🎯 selectFieldType called with fieldType:', fieldType);

    selectedFieldType = fieldType;

    const storedSectionId = currentAddFieldSection;

    if (!storedSectionId) {
        console.error('❌ No section ID available in selectFieldType');
        alert('⚠️ Error: Section context lost. Please close this dialog and try adding the field again.');
        return;
    }

    // ✅ FIX: Advanced table should skip config and go straight to wizard
    if (fieldType === 'advancedTable') {
        console.log('✅ Advanced table selected - opening wizard directly');
        console.log('   Section ID:', storedSectionId);
        closeAddFieldModal();

        // Small delay to ensure modal is closed before opening wizard
        setTimeout(() => {
            // Generate field ID and open wizard
            const fieldId = `field_${fieldIdCounter++}`;
            console.log('   Generated field ID:', fieldId);

            advancedTableBuilder.init(fieldId, storedSectionId);
            advancedTableBuilder.fieldLabel = '';
            advancedTableBuilder.fieldHelpText = '';
            advancedTableBuilder.fieldRequired = false;

            console.log('   Opening advanced table wizard...');
            openAdvancedTableWizard();
        }, 100);

        return;
    }

    console.log('✅ Regular field type - showing config form');
    console.log('   Section ID:', storedSectionId);

    const selectStep = document.getElementById('selectFieldTypeStep');
    const configStep = document.getElementById('configureFieldStep');

    if (selectStep) selectStep.style.display = 'none';
    if (configStep) configStep.style.display = 'block';

    generateFieldConfigForm(fieldType);
}


function closeAddFieldModal() {
    const modal = document.getElementById('addFieldModal');
    if (modal) {
        modal.classList.remove('active');
    }

    selectedFieldType = null;
    currentEditingField = null;

    console.log('→ Field modal closed');
}

function backToFieldTypeSelection() {
    const selectStep = document.getElementById('selectFieldTypeStep');
    const configStep = document.getElementById('configureFieldStep');

    if (selectStep) selectStep.style.display = 'block';
    if (configStep) configStep.style.display = 'none';
}

function generateFieldConfigForm(fieldType) {
    const container = document.getElementById('configureFieldStep');
    if (!container) return;

    // ✅ SAFETY CHECK: Advanced table should never reach this function
    if (fieldType === 'advancedTable') {
        console.error('❌ ERROR: generateFieldConfigForm called for advancedTable - this should not happen!');
        console.log('→ Redirecting to advanced table wizard...');
        closeAddFieldModal();

        const fieldId = `field_${fieldIdCounter++}`;
        advancedTableBuilder.init(fieldId, currentAddFieldSection);
        openAdvancedTableWizard();
        return;
    }

    const fieldTypeInfo = getFieldTypeInfo(fieldType);

    let html = `
        <div class="step-intro">
            <div class="field-type-icon">${fieldTypeInfo.icon}</div>
            <h3>${currentEditingField ? 'Edit' : 'Configure'} ${fieldTypeInfo.label} Field</h3>
            <p>Set up the properties for this field</p>
        </div>
        
        <div class="form-group-wizard">
            <label>Field Label <span class="required-mark">*</span></label>
            <input type="text" id="newFieldLabel" placeholder="e.g., Company Name" autofocus>
            <small>The question or label shown to employees</small>
        </div>
        
        <div class="form-group-wizard">
            <label>Help Text (Optional)</label>
            <input type="text" id="newFieldHelpText" placeholder="Additional instructions...">
            <small>Extra information to help employees fill this field</small>
        </div>
        
        <div class="form-group-wizard">
            <label class="checkbox-label">
                <input type="checkbox" id="newFieldRequired">
                <span>This field is required</span>
            </label>
        </div>
    `;

    if (['text', 'textarea', 'email', 'url', 'phone'].includes(fieldType)) {
        html += `
            <div class="form-group-wizard">
                <label>Placeholder Text</label>
                <input type="text" id="newFieldPlaceholder" placeholder="e.g., Enter your answer here...">
            </div>
        `;
    }

    if (['number', 'currency'].includes(fieldType)) {
        html += `
            <div class="form-grid-2">
                <div class="form-group-wizard">
                    <label>Minimum Value</label>
                    <input type="number" id="newFieldMinValue" placeholder="Optional">
                </div>
                <div class="form-group-wizard">
                    <label>Maximum Value</label>
                    <input type="number" id="newFieldMaxValue" placeholder="Optional">
                </div>
            </div>
        `;
    }

    if (['select', 'radio'].includes(fieldType)) {
        html += `
            <div class="form-group-wizard">
                <label>Options <span class="required-mark">*</span></label>
                <textarea id="newFieldOptions" rows="4" placeholder="Enter each option on a new line&#10;Option 1&#10;Option 2&#10;Option 3"></textarea>
                <small>One option per line</small>
            </div>
        `;
    }

    if (fieldType === 'file') {
        html += `
            <div class="form-group-wizard">
                <label>Allowed File Types</label>
                <input type="text" id="newFieldFileTypes" placeholder=".pdf, .doc, .docx" value=".pdf, .doc, .docx">
                <small>Leave empty to allow all file types</small>
            </div>
            <div class="form-group-wizard">
                <label>Maximum File Size (MB)</label>
                <input type="number" id="newFieldMaxSize" value="5" min="1" max="50">
            </div>
        `;
    }

    if (fieldType === 'signature') {
        html += `
            <div class="form-group-wizard">
                <label class="checkbox-label">
                    <input type="checkbox" id="newFieldSignatureTyped">
                    <span>Allow typed signatures</span>
                </label>
            </div>
        `;
    }

    if (fieldType === 'paragraph') {
        html += `
            <div class="form-group-wizard">
                <label>Paragraph Text <span class="required-mark">*</span></label>
                <textarea id="newFieldParagraphText" rows="4" placeholder="Enter the text..."></textarea>
                <small>This text will be shown to employees</small>
            </div>
        `;
    }

    // ===================================================================
    // SIMPLE TABLE CONFIGURATION
    // ===================================================================
    if (fieldType === 'table') {
        html += `
            <div class="form-group-wizard">
                <label>Number of Columns <span class="required-mark">*</span></label>
                <input type="number" id="newFieldTableColumns" value="3" min="1" max="10" 
                       onchange="updateTableColumnsPreview()">
                <small>How many columns should this table have?</small>
            </div>
            
            <div class="form-group-wizard">
                <label>Column Headers <span class="required-mark">*</span></label>
                <div id="tableColumnHeadersContainer">
                </div>
            </div>
            
            <div class="form-group-wizard">
                <label>Minimum Rows</label>
                <input type="number" id="newFieldTableMinRows" value="1" min="1" max="20">
                <small>Minimum number of data rows required</small>
            </div>
        <div class="form-group-wizard">
            <label class="checkbox-label">
                <input type="checkbox" id="newFieldTableAllowAddRows" checked>
                <span>Allow users to add more rows</span>
            </label>
        </div>
    `;
    }

    html += `
    <div class="modal-actions">
        <button class="btn btn-ghost" onclick="backToFieldTypeSelection()">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                <path d="M15 19l-7-7 7-7" />
            </svg>
            Back
        </button>
        <button class="btn btn-primary" onclick="addConfiguredField()">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                <path d="M12 4v16m8-8H4" />
            </svg>
            ${currentEditingField ? 'Update Field' : 'Add Field'}
        </button>
    </div>
`;

    container.innerHTML = html;

    if (fieldType === 'table') {
        setTimeout(() => updateTableColumnsPreview(), 100);
    }

    setTimeout(() => {
        const firstInput = document.getElementById('newFieldLabel');
        if (firstInput) firstInput.focus();
    }, 100);
}

// ===================================================================
// SIMPLE TABLE - HELPER FUNCTIONS
// ===================================================================
function updateTableColumnsPreview() {
    const numColumns = parseInt(document.getElementById('newFieldTableColumns')?.value) || 3;
    const container = document.getElementById('tableColumnHeadersContainer');
    if (!container) return;
    let html = '';
    for (let i = 0; i < numColumns; i++) {
        html += `
        <div class="form-group-wizard" style="margin-bottom: 12px;">
            <label>Column ${i + 1}</label>
            <input type="text" id="tableColHeader${i}" placeholder="e.g., Company Name" 
                   class="form-control">
        </div>
    `;
    }
    container.innerHTML = html;
}


// ===================================================================
// ADVANCED TABLE - WIZARD FUNCTIONS
// ===================================================================
function openAdvancedTableWizard() {
    console.log('🎯 Opening advanced table wizard');

    // Generate unique field ID if not already set
    if (!advancedTableBuilder.currentFieldId) {
        advancedTableBuilder.currentFieldId = `field_${fieldIdCounter++}`;
    }

    const fieldId = advancedTableBuilder.currentFieldId;

    console.log('Field ID:', fieldId);
    console.log('Section ID:', advancedTableBuilder.currentSectionId);

    // Create and show advanced table wizard modal
    const modalHTML = createAdvancedTableWizardHTML(fieldId);

    // Remove existing wizard modal if any
    const existingWizard = document.querySelector('[id^="advTableWizard-"]');
    if (existingWizard) {
        existingWizard.remove();
    }

    document.body.insertAdjacentHTML('beforeend', modalHTML);

    // Initialize Step 1
    setTimeout(() => {
        renderAdvTableStep1(fieldId);
    }, 100);
}


function createAdvancedTableWizardHTML(fieldId) {
    return `
<div id="advTableWizard-${fieldId}" class="modal-overlay active">
<div class="modal-container" style="max-width: 1100px; width: 95%;">
<div class="modal-header">
<div>
<h2>🎯 Advanced Table Builder</h2>
<p style="margin: 4px 0 0 0; color: #6b7280; font-size: 14px;">
<span id="advTable-step-indicator-${fieldId}">Step 1 of 3: Define Columns</span>
</p>
</div>
<button class="modal-close" onclick="closeAdvancedTableWizard('${fieldId}')">×</button>
</div>
        <!-- Progress Steps -->
        <div style="padding: 20px 32px 0 32px;">
            <div class="wizard-progress" id="advTable-progress-${fieldId}">
                <div class="progress-step active" data-step="1">
                    <div class="progress-step-circle">1</div>
                    <div class="progress-step-label">Define Columns</div>
                </div>
                <div class="progress-line"></div>
                <div class="progress-step" data-step="2">
                    <div class="progress-step-circle">2</div>
                    <div class="progress-step-label">Build Table</div>
                </div>
                <div class="progress-line"></div>
                <div class="progress-step" data-step="3">
                    <div class="progress-step-circle">3</div>
                    <div class="progress-step-label">Preview</div>
                </div>
            </div>
        </div>

        <div class="modal-body" style="max-height: 65vh; overflow-y: auto; padding: 24px 32px;">
            <!-- Step 1: Define Columns -->
            <div id="advTable-${fieldId}-step-1" class="wizard-step active">
                <!-- Content will be generated by renderAdvTableStep1 -->
            </div>

            <!-- Step 2: Build Table Structure -->
            <div id="advTable-${fieldId}-step-2" class="wizard-step" style="display: none;">
                <!-- Content will be generated by renderAdvTableStep2 -->
            </div>

            <!-- Step 3: Preview -->
            <div id="advTable-${fieldId}-step-3" class="wizard-step" style="display: none;">
                <!-- Content will be generated by renderAdvTableStep3 -->
            </div>
        </div>

        <div class="modal-actions">
            <button type="button" 
                    id="advTable-${fieldId}-btn-back"
                    class="btn btn-ghost" 
                    onclick="advTablePrevStep('${fieldId}')"
                    style="display: none;">
                <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="15 18 9 12 15 6"></polyline>
                </svg>
                Back
            </button>
            <div style="flex: 1;"></div>
            <button type="button" class="btn btn-ghost" onclick="closeAdvancedTableWizard('${fieldId}')">
                Cancel
            </button>
            <button type="button" 
                    id="advTable-${fieldId}-btn-next"
                    class="btn btn-primary" 
                    onclick="advTableNextStep('${fieldId}')">
                Next
                <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="9 18 15 12 9 6"></polyline>
                </svg>
            </button>
            <button type="button" 
                    id="advTable-${fieldId}-btn-save"
                    class="btn btn-primary" 
                    onclick="saveAdvancedTableField('${fieldId}')"
                    style="display: none;">
                <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="20 6 9 17 4 12"></polyline>
                </svg>
                Add Table to Form
            </button>
        </div>
    </div>
</div>`;
}

function renderAdvTableStep1(fieldId) {
    const container = document.getElementById(`advTable-${fieldId}-step-1`);
    if (!container) return;

    container.innerHTML = `
    <!-- ── BASIC FIELD PROPERTIES ── first section ─────────────────────── -->
    <div style="margin-bottom: 32px; padding-bottom: 24px; border-bottom: 1px solid #e5e7eb;">
        <h3 style="margin: 0 0 16px 0; color: #1e40af;">1. Table Information</h3>
        
        <div class="form-group-wizard">
            <label>Table Label <span class="required-mark">*</span></label>
            <input type="text" id="advTable-${fieldId}-label" class="form-control" 
                   value="${escapeHTML(advancedTableBuilder.fieldLabel || '')}"
                   placeholder="e.g. Schedule of Shareholdings" required>
        </div>

        <div class="form-group-wizard">
            <label>Help Text / Instructions (optional)</label>
            <textarea id="advTable-${fieldId}-help" rows="2" class="form-control"
                      placeholder="Additional guidance for the employee...">${escapeHTML(advancedTableBuilder.fieldHelpText || '')}</textarea>
        </div>

        <div class="form-group-wizard">
            <label class="checkbox-label">
                <input type="checkbox" id="advTable-${fieldId}-required" 
                       ${advancedTableBuilder.fieldRequired ? 'checked' : ''}>
                This table is required
            </label>
        </div>
    </div>

    <!-- ── Existing columns / rows configuration ───────────────────────── -->
    <h3 style="margin: 0 0 16px 0; color: #1e40af;">2. Table Structure</h3>
    
    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px;">
        <div class="form-group-wizard">
            <label>Number of Columns</label>
            <input type="number" id="advTable-${fieldId}-columns-count" value="${advancedTableBuilder.gridColumns || 3}" min="1" max="10">
        </div>
        <div class="form-group-wizard">
            <label>Number of Data Rows</label>
            <input type="number" id="advTable-${fieldId}-rows-count" value="${advancedTableBuilder.rows || 4}" min="1" max="30">
        </div>
    </div>

    <div class="form-group-wizard">
        <label>Define Columns</label>
        <div id="advTable-${fieldId}-columns-list" class="columns-list"></div>
        <button type="button" class="btn btn-ghost btn-sm" onclick="addAdvTableColumn('${fieldId}')">
            + Add Column
        </button>
    </div>

    <div class="info-box" style="margin-top: 24px;">
        <strong>Next steps:</strong> After defining columns → arrange layout & merge cells
    </div>
    `;

    // Re-populate columns if any already exist
    advancedTableBuilder.columns.forEach(() => addAdvTableColumn(fieldId));
}

let advTableColumnCounter = 0;

function addAdvTableColumn(fieldId) {
    const container = document.getElementById(`advTable-${fieldId}-columns-list`);
    if (!container) return;
    const colId = `advTableCol-${advTableColumnCounter++}`;
    const columnNumber = container.children.length + 1;

    const colDiv = document.createElement('div');
    colDiv.className = 'adv-table-column-item';
    colDiv.dataset.columnId = colId;
    colDiv.style.cssText = 'display: flex; align-items: center; gap: 8px; padding: 8px; background: #f9fafb; border-radius: 6px;';

    colDiv.innerHTML = `
    <span style="min-width: 32px; height: 32px; background: #e0e7ff; color: #4f46e5; border-radius: 6px; display: flex; align-items: center; justify-content: center; font-weight: 600; font-size: 14px; flex-shrink: 0;">${columnNumber}</span>
    <input type="text" placeholder="Column name (e.g., Account Number)" class="form-control adv-table-col-name" 
           style="flex: 1;" data-column-id="${colId}">
    <select class="form-control adv-table-col-type" style="width: 140px; flex-shrink: 0;" data-column-id="${colId}">
        <option value="text">Text</option>
        <option value="number">Number</option>
        <option value="date">Date</option>
        <option value="email">Email</option>
    </select>
    <button type="button" 
            class="btn-icon-delete-small" 
            onclick="removeAdvTableColumn('${fieldId}', this)"
            style="flex-shrink: 0; padding: 4px 8px; background: #ef4444; color: white; border: none; border-radius: 4px; cursor: pointer;">
        ×
    </button>
`;

    container.appendChild(colDiv);
    updateAdvColumnNumbers(fieldId);
}

function removeAdvTableColumn(fieldId, button) {
    const container = document.getElementById(`advTable-${fieldId}-columns-list`);
    if (!container || container.children.length <= 1) {
        alert('⚠️ At least one column is required');
        return;
    }
    button.closest('.adv-table-column-item').remove();
    updateAdvColumnNumbers(fieldId);
}

function updateAdvColumnNumbers(fieldId) {
    const container = document.getElementById(`advTable-${fieldId}-columns-list`);
    if (!container) return;
    Array.from(container.children).forEach((item, index) => {
        const numberSpan = item.querySelector('span');
        if (numberSpan) numberSpan.textContent = index + 1;
    });
}

function advTableNextStep(fieldId) {
    const currentStep = advancedTableBuilder.step;
    if (currentStep === 1) {
        if (!validateAdvTableStep1(fieldId)) return;
        advancedTableBuilder.step = 2;
        renderAdvTableStep2(fieldId);
    } else if (currentStep === 2) {
        if (!validateAdvTableStep2(fieldId)) return;
        advancedTableBuilder.step = 3;
        renderAdvTableStep3(fieldId);
    }

    updateAdvTableStepUI(fieldId);
}

function advTablePrevStep(fieldId) {
    if (advancedTableBuilder.step > 1) {
        advancedTableBuilder.step--;
        updateAdvTableStepUI(fieldId);
    }
}

function validateAdvTableStep1(fieldId) {
    // ✅ FIX: Update field label from Step 1 input
    const labelInput = document.getElementById(`advTable-${fieldId}-label`);
    const label = labelInput?.value.trim() || '';

    if (!label) {
        alert('⚠️ Please enter a table label');
        labelInput?.focus();
        return false;
    }

    advancedTableBuilder.fieldLabel = label;

    // Update help text and required status
    const helpInput = document.getElementById(`advTable-${fieldId}-help`);
    const requiredInput = document.getElementById(`advTable-${fieldId}-required`);

    advancedTableBuilder.fieldHelpText = helpInput?.value.trim() || '';
    advancedTableBuilder.fieldRequired = requiredInput?.checked || false;

    const container = document.getElementById(`advTable-${fieldId}-columns-list`);
    if (!container || container.children.length === 0) {
        alert('⚠️ Please add at least one column');
        return false;
    }
    advancedTableBuilder.columns = [];
    const columnItems = container.querySelectorAll('.adv-table-column-item');

    for (let item of columnItems) {
        const nameInput = item.querySelector('.adv-table-col-name');
        const typeSelect = item.querySelector('.adv-table-col-type');
        const name = nameInput ? nameInput.value.trim() : '';

        if (!name) {
            alert('⚠️ All columns must have names');
            if (nameInput) nameInput.focus();
            return false;
        }

        advancedTableBuilder.columns.push({
            id: nameInput.dataset.columnId,
            name: name,
            type: typeSelect ? typeSelect.value : 'text'
        });
    }

    const rowsInput = document.getElementById(`advTable-${fieldId}-rows-count`);
    advancedTableBuilder.rows = rowsInput ? parseInt(rowsInput.value) || 4 : 4;

    const colsInput = document.getElementById(`advTable-${fieldId}-columns-count`);
    advancedTableBuilder.gridColumns = colsInput ? parseInt(colsInput.value) || 3 : advancedTableBuilder.columns.length;

    return true;
}

function validateAdvTableStep2(fieldId) {
    const hasAssignments = advancedTableBuilder.gridCells.some(cell => cell.columnId);
    if (!hasAssignments) {
        return confirm(
            'You haven\'t assigned any columns to cells. Continue anyway?\n\n' +
            'The table will have a basic structure without merged cells.'
        );
    }

    return true;
}

function renderAdvTableStep2(fieldId) {
    const container = document.getElementById(`advTable-${fieldId}-step-2`);
    if (!container) return;
    container.innerHTML = `
    <div class="info-box" style="margin-bottom: 20px; background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; padding: 16px; display: flex; gap: 12px;">
        <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="16" x2="12" y2="12"></line>
            <line x1="12" y1="8" x2="12.01" y2="8"></line>
        </svg>
        <div>
            <strong style="color: #1e40af;">Step 2: Build Your Table Structure</strong>
            <p style="margin: 4px 0 0 0; color: #1e40af;">Select cells to merge, then assign column names to merged regions.</p>
        </div>
    </div>

    <!-- Toolbar -->
    <div class="table-toolbar" style="display: flex; gap: 8px; margin-bottom: 20px; padding: 16px; background: #f9fafb; border-radius: 8px; flex-wrap: wrap;">
        <button type="button" onclick="mergeSelectedCells('${fieldId}')" style="display: flex; align-items: center; gap: 8px; padding: 10px 16px; background: white; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer;">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="3" width="18" height="18" rx="2"></rect>
                <line x1="12" y1="3" x2="12" y2="21"></line>
                <line x1="3" y1="12" x2="21" y2="12"></line>
            </svg>
            Merge Selected
        </button>
        <button type="button" onclick="assignColumnToMerge('${fieldId}')" style="display: flex; align-items: center; gap: 8px; padding: 10px 16px; background: white; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer;">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
            </svg>
            Assign Column
        </button>
        <button type="button" onclick="clearTableSelection('${fieldId}')" style="display: flex; align-items: center; gap: 8px; padding: 10px 16px; background: white; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer;">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="18" y1="6" x2="6" y2="18"></line>
                <line x1="6" y1="6" x2="18" y2="18"></line>
            </svg>
            Clear Selection
        </button>
        <button type="button" onclick="resetTableStructure('${fieldId}')" style="display: flex; align-items: center; gap: 8px; padding: 10px 16px; background: white; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer;">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"></path>
                <path d="M21 3v5h-5"></path>
            </svg>
            Reset
        </button>
        <div style="flex: 1;"></div>
        <span style="color: #6b7280; font-size: 13px; align-self: center;">
            Click cells to select • Shift+Click for range
        </span>
    </div>

    <!-- Table Grid -->
    <div id="advTable-${fieldId}-table-grid" class="advanced-table-grid" style="display: grid; gap: 2px; background: #e5e7eb; border: 2px solid #d1d5db; border-radius: 8px; padding: 2px; margin-bottom: 20px;">
        <!-- Grid will be rendered here -->
    </div>

    <!-- Column Assignment Panel -->
    <div id="advTable-${fieldId}-assignment-panel" class="assignment-panel" style="display: none; background: #f9fafb; border: 1px solid #d1d5db; border-radius: 8px; padding: 16px; margin-top: 16px;">
        <h4 style="margin: 0 0 12px 0;">Assign Column to Merged Cells</h4>
        <select id="advTable-${fieldId}-column-select" class="form-control" style="margin-bottom: 12px;">
            <option value="">Select a column...</option>
        </select>
        <div style="display: flex; gap: 8px;">
            <button type="button" class="btn btn-primary" onclick="confirmColumnAssignment('${fieldId}')">
                Assign
            </button>
            <button type="button" class="btn btn-ghost" onclick="cancelColumnAssignment('${fieldId}')">
                Cancel
            </button>
        </div>
    </div>
`;

    initializeTableGrid(fieldId);
    updateColumnSelector(fieldId);
}

function initializeTableGrid(fieldId) {
    const cols = advancedTableBuilder.gridColumns;
    const rows = advancedTableBuilder.rows + 1; // +1 for header row
    advancedTableBuilder.gridCells = [];

    for (let r = 0; r < rows; r++) {
        for (let c = 0; c < cols; c++) {
            advancedTableBuilder.gridCells.push({
                row: r,
                col: c,
                rowspan: 1,
                colspan: 1,
                columnId: null,
                columnName: '',
                isHeader: r === 0,
                isMerged: false,
                mergeRoot: null,
                hidden: false
            });
        }
    }

    renderTableGrid(fieldId);
}

function renderTableGrid(fieldId) {
    const grid = document.getElementById(`advTable-${fieldId}-table-grid`);
    if (!grid) return;
    grid.innerHTML = '';

    const cols = advancedTableBuilder.gridColumns;
    grid.style.gridTemplateColumns = `repeat(${cols}, 1fr)`;

    advancedTableBuilder.gridCells.forEach(cell => {
        if (cell.hidden) return;

        const cellDiv = document.createElement('div');
        cellDiv.className = 'table-grid-cell';
        cellDiv.dataset.row = cell.row;
        cellDiv.dataset.col = cell.col;

        cellDiv.style.cssText = `
        background: ${cell.isHeader ? '#f3f4f6' : 'white'};
        border: 1px solid #d1d5db;
        padding: 16px;
        text-align: center;
        font-size: 13px;
        color: #6b7280;
        cursor: pointer;
        transition: all 0.2s;
    `;

        if (cell.rowspan > 1) cellDiv.style.gridRowEnd = `span ${cell.rowspan}`;
        if (cell.colspan > 1) cellDiv.style.gridColumnEnd = `span ${cell.colspan}`;

        if (cell.columnName) {
            cellDiv.classList.add('assigned');
            cellDiv.style.background = '#dcfce7';
            cellDiv.style.borderColor = '#22c55e';
            cellDiv.innerHTML = `
            <div style="font-weight: 600; color: #059669;">${escapeHTML(cell.columnName)}</div>
            <div style="font-size: 11px; color: #6b7280; margin-top: 2px;">
                ${cell.rowspan > 1 || cell.colspan > 1 ?
                    `Spans ${cell.rowspan}×${cell.colspan}` :
                    'Single cell'}
            </div>
        `;
        } else {
            cellDiv.textContent = `R${cell.row + 1},C${cell.col + 1}`;
            if (cell.isHeader) {
                cellDiv.style.fontWeight = '600';
                cellDiv.style.color = '#374151';
            }
        }

        if (cell.isMerged) {
            cellDiv.classList.add('merged');
        }

        cellDiv.onclick = (e) => selectTableCell(e, cell, fieldId);
        grid.appendChild(cellDiv);
    });
}

function selectTableCell(event, cell, fieldId) {
    const cellDiv = event.currentTarget;
    const isSelected = advancedTableBuilder.selectedCells.some(
        c => c.row === cell.row && c.col === cell.col
    );
    if (!event.shiftKey) {
        document.querySelectorAll(`#advTable-${fieldId}-table-grid .table-grid-cell.selected`)
            .forEach(el => {
                el.classList.remove('selected');
                el.style.background = el.dataset.row === '0' ? '#f3f4f6' : 'white';
            });
        advancedTableBuilder.selectedCells = [];
    }

    if (isSelected) {
        advancedTableBuilder.selectedCells = advancedTableBuilder.selectedCells.filter(
            c => !(c.row === cell.row && c.col === cell.col)
        );
        cellDiv.classList.remove('selected');
        cellDiv.style.background = cell.isHeader ? '#f3f4f6' : 'white';
    } else {
        advancedTableBuilder.selectedCells.push({
            row: cell.row,
            col: cell.col
        });
        cellDiv.classList.add('selected');
        cellDiv.style.background = '#dbeafe';
        cellDiv.style.borderColor = '#3b82f6';
    }
}

function mergeSelectedCells(fieldId) {
    const selected = advancedTableBuilder.selectedCells;

    if (selected.length < 2) {
        showNotification('Select at least 2 cells to merge', 'error');
        return;
    }

    const alreadyMerged = selected.some(s => {
        const cell = advancedTableBuilder.gridCells.find(
            c => c.row === s.row && c.col === s.col
        );
        return cell && (cell.isMerged || cell.hidden);
    });

    if (alreadyMerged) {
        showNotification('Cannot merge: some cells are already merged', 'error');
        return;
    }

    const rows = selected.map(c => c.row);
    const cols = selected.map(c => c.col);
    const minRow = Math.min(...rows);
    const maxRow = Math.max(...rows);
    const minCol = Math.min(...cols);
    const maxCol = Math.max(...cols);

    const rowspan = maxRow - minRow + 1;
    const colspan = maxCol - minCol + 1;

    if (selected.length !== rowspan * colspan) {
        showNotification('Please select a rectangular region', 'error');
        return;
    }

    const rootCell = advancedTableBuilder.gridCells.find(
        c => c.row === minRow && c.col === minCol
    );

    if (!rootCell) return;

    rootCell.isMerged = true;
    rootCell.rowspan = rowspan;
    rootCell.colspan = colspan;

    advancedTableBuilder.gridCells.forEach(cell => {
        if (cell.row >= minRow && cell.row <= maxRow &&
            cell.col >= minCol && cell.col <= maxCol &&
            !(cell.row === minRow && cell.col === minCol)) {
            cell.hidden = true;
            cell.mergeRoot = rootCell;
        }
    });

    showNotification(`Merged ${selected.length} cells`, 'success');
    clearTableSelection(fieldId);
    renderTableGrid(fieldId);
}

function assignColumnToMerge(fieldId) {
    const selected = advancedTableBuilder.selectedCells;
    if (selected.length === 0) {
        alert('⚠️ Please select cell(s) first');
        return;
    }

    const panel = document.getElementById(`advTable-${fieldId}-assignment-panel`);
    if (panel) {
        panel.style.display = 'block';
        panel.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }
}

function updateColumnSelector(fieldId) {
    const select = document.getElementById(`advTable-${fieldId}-column-select`);
    if (!select) return;

    select.innerHTML = '<option value="">Select a column...</option>';
    advancedTableBuilder.columns.forEach(col => {
        const option = document.createElement('option');
        option.value = col.id;
        option.textContent = col.name;
        select.appendChild(option);
    });
}

function confirmColumnAssignment(fieldId) {
    const select = document.getElementById(`advTable-${fieldId}-column-select`);
    const columnId = select ? select.value : '';
    if (!columnId) {
        alert('⚠️ Please select a column');
        return;
    }

    const column = advancedTableBuilder.columns.find(c => c.id === columnId);
    if (!column) return;

    const selected = advancedTableBuilder.selectedCells;

    const rows = selected.map(c => c.row);
    const cols = selected.map(c => c.col);
    const minRow = Math.min(...rows);
    const minCol = Math.min(...cols);

    const rootCell = advancedTableBuilder.gridCells.find(
        c => c.row === minRow && c.col === minCol && !c.hidden
    );

    if (rootCell) {
        rootCell.columnId = column.id;
        rootCell.columnName = column.name;

        alert(`✅ Assigned "${column.name}"`);
    }

    cancelColumnAssignment(fieldId);
    clearTableSelection(fieldId);
    renderTableGrid(fieldId);
}

function cancelColumnAssignment(fieldId) {
    const panel = document.getElementById(`advTable-${fieldId}-assignment-panel`);
    if (panel) panel.style.display = 'none';
}

function clearTableSelection(fieldId) {
    document.querySelectorAll(`#advTable-${fieldId}-table-grid .table-grid-cell.selected`)
        .forEach(el => {
            el.classList.remove('selected');
            const row = el.dataset.row;
            if (!el.classList.contains('assigned')) {
                el.style.background = row === '0' ? '#f3f4f6' : 'white';
                el.style.borderColor = '#d1d5db';
            }
        });
    advancedTableBuilder.selectedCells = [];
}

function resetTableStructure(fieldId) {
    if (!confirm('Reset all merges and assignments? This cannot be undone.')) return;
    initializeTableGrid(fieldId);
    alert('✅ Table structure reset');
}

function renderAdvTableStep3(fieldId) {
    const container = document.getElementById(`advTable-${fieldId}-step-3`);
    if (!container) return;
    container.innerHTML = `
    <div class="info-box" style="margin-bottom: 20px; background: #eff6ff; border: 1px solid #bfdbfe; border-radius: 8px; padding: 16px; display: flex; gap: 12px;">
        <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"></circle>
            <polyline points="9 11 12 14 22 4"></polyline>
        </svg>
        <div>
            <strong style="color: #1e40af;">Step 3: Review Your Table</strong>
            <p style="margin: 4px 0 0 0; color: #1e40af;">This is how your table will appear in the form. Make sure everything looks correct.</p>
        </div>
    </div>

    <div id="advTable-${fieldId}-table-preview" class="table-preview-container">
        <!-- Preview will be rendered here -->
    </div>
`;

    generateTablePreview(fieldId);
}

function generateTablePreview(fieldId) {
    const container = document.getElementById(`advTable-${fieldId}-table-preview`);
    if (!container) return;
    const label = advancedTableBuilder.fieldLabel || 'Advanced Table';

    let html = `
    <div style="margin-bottom: 20px;">
        <label style="display: block; font-weight: 600; margin-bottom: 8px; font-size: 15px;">
            ${escapeHTML(label)}
        </label>
    </div>
    <div style="overflow-x: auto;">
        <table style="width: 100%; border-collapse: collapse; border: 2px solid #d1d5db;">
`;

    const cols = advancedTableBuilder.gridColumns;
    const rows = advancedTableBuilder.rows + 1;

    for (let r = 0; r < rows; r++) {
        html += '<tr>';

        for (let c = 0; c < cols; c++) {
            const cell = advancedTableBuilder.gridCells.find(
                cell => cell.row === r && cell.col === c && !cell.hidden
            );

            if (!cell) continue;

            const isHeader = cell.isHeader;
            const tag = isHeader ? 'th' : 'td';
            const bgColor = isHeader ? '#f3f4f6' : (cell.columnName ? '#f0fdf4' : 'white');
            const textColor = isHeader ? '#111827' : (cell.columnName ? '#065f46' : '#6b7280');

            html += `<${tag} 
            style="
                border: 1px solid #d1d5db;
                padding: 12px;
                text-align: left;
                background: ${bgColor};
                color: ${textColor};
                font-weight: ${isHeader ? '600' : '400'};
            "
            ${cell.rowspan > 1 ? `rowspan="${cell.rowspan}"` : ''}
            ${cell.colspan > 1 ? `colspan="${cell.colspan}"` : ''}
        >`;

            if (isHeader && !cell.columnName) {
                html += escapeHTML(advancedTableBuilder.columns[c]?.name || `Column ${c + 1}`);
            } else if (cell.columnName) {
                html += escapeHTML(cell.columnName);
            } else {
                html += `<span style="color: #9ca3af; font-style: italic;">Data will be entered here</span>`;
            }

            html += `</${tag}>`;
        }

        html += '</tr>';
    }

    html += '</table></div>';
    container.innerHTML = html;
}

function updateAdvTableStepUI(fieldId) {
    const step = advancedTableBuilder.step;
    const progressSteps = document.querySelectorAll(`#advTable-progress-${fieldId} .progress-step`);
    progressSteps.forEach((el, index) => {
        el.classList.toggle('active', index + 1 === step);
        el.classList.toggle('completed', index + 1 < step);
    });

    const stepTexts = [
        'Step 1 of 3: Define Columns',
        'Step 2 of 3: Build Table Structure',
        'Step 3 of 3: Preview & Confirm'
    ];
    const indicator = document.getElementById(`advTable-step-indicator-${fieldId}`);
    if (indicator) indicator.textContent = stepTexts[step - 1];

    document.getElementById(`advTable-${fieldId}-step-1`).style.display = step === 1 ? 'block' : 'none';
    document.getElementById(`advTable-${fieldId}-step-2`).style.display = step === 2 ? 'block' : 'none';
    document.getElementById(`advTable-${fieldId}-step-3`).style.display = step === 3 ? 'block' : 'none';

    const btnBack = document.getElementById(`advTable-${fieldId}-btn-back`);
    const btnNext = document.getElementById(`advTable-${fieldId}-btn-next`);
    const btnSave = document.getElementById(`advTable-${fieldId}-btn-save`);

    if (btnBack) btnBack.style.display = step > 1 ? 'flex' : 'none';
    if (btnNext) btnNext.style.display = step < 3 ? 'flex' : 'none';
    if (btnSave) btnSave.style.display = step === 3 ? 'flex' : 'none';
}

function saveAdvancedTableField(fieldId) {
    console.log('💾 Saving advanced table field:', fieldId);

    const section = sections.find(s => s.sectionId === advancedTableBuilder.currentSectionId);
    if (!section) {
        console.error('❌ Section not found:', advancedTableBuilder.currentSectionId);
        alert('⚠️ Error: Section not found');
        return;
    }

    // ✅ FIX: Create field with ALL advanced table properties
    const field = {
        fieldId: fieldId,
        fieldLabel: advancedTableBuilder.fieldLabel,
        fieldType: 'advancedTable',
        required: advancedTableBuilder.fieldRequired,
        order: section.fields.length + 1,
        helpText: advancedTableBuilder.fieldHelpText,

        // ✅ CRITICAL: Include all advanced table data
        columns: advancedTableBuilder.columns,
        rows: advancedTableBuilder.rows + 1,
        gridColumns: advancedTableBuilder.gridColumns,
        cells: advancedTableBuilder.gridCells.filter(c => !c.hidden)
    };

    console.log('✅ Advanced table field created:', field);

    section.fields.push(field);
    renderFieldInSection(advancedTableBuilder.currentSectionId, field);
    updateFieldCount(advancedTableBuilder.currentSectionId);

    closeAdvancedTableWizard(fieldId);
    alert('✅ Advanced table added successfully!');
}


function closeAdvancedTableWizard(fieldId) {
    const modal = document.getElementById(`advTableWizard-${fieldId}`);
    if (modal) modal.remove();
    // Reset builder
    advancedTableBuilder.currentFieldId = null;
    advancedTableBuilder.currentSectionId = null;
    advancedTableBuilder.step = 1;
    advancedTableBuilder.columns = [];
    advancedTableBuilder.gridCells = [];
    advancedTableBuilder.selectedCells = [];
    advancedTableBuilder.fieldLabel = '';
    advancedTableBuilder.fieldHelpText = '';
    advancedTableBuilder.fieldRequired = false;
}


// ===================================================================
// MAIN ADD CONFIGURED FIELD FUNCTION
// ===================================================================
function addConfiguredField() {
    if (!currentAddFieldSection || !selectedFieldType) return;
    const section = sections.find(s => s.sectionId === currentAddFieldSection);
    if (!section) return;

    const label = document.getElementById('newFieldLabel')?.value.trim();

    if (!['heading', 'paragraph', 'divider', 'table', 'advancedTable'].includes(selectedFieldType) && !label) {
        alert('⚠️ Field label is required');
        return;
    }

    if (['select', 'radio'].includes(selectedFieldType)) {
        const options = document.getElementById('newFieldOptions')?.value.trim();
        if (!options) {
            alert('⚠️ Please provide at least one option');
            return;
        }
    }

    if (selectedFieldType === 'paragraph') {
        const paragraphText = document.getElementById('newFieldParagraphText')?.value.trim();
        if (!paragraphText) {
            alert('⚠️ Paragraph text is required');
            return;
        }
    }

    let field;

    if (currentEditingField) {
        field = section.fields.find(f => f.fieldId === currentEditingField.fieldId);
        if (!field) return;

        const oldCard = document.querySelector(`[data-field-id="${field.fieldId}"]`);
        if (oldCard) oldCard.remove();
    } else {
        const fieldId = `field_${fieldIdCounter++}`;

        field = {
            fieldId: fieldId,
            fieldLabel: label || '',
            fieldType: selectedFieldType,
            required: false,
            order: section.fields.length + 1,
            placeholder: '',
            helpText: '',
            conditionalOn: null,
            options: [],
            validation: {}
        };

        section.fields.push(field);
    }

    field.fieldLabel = label || '';
    field.required = document.getElementById('newFieldRequired')?.checked || false;
    field.placeholder = document.getElementById('newFieldPlaceholder')?.value || '';
    field.helpText = document.getElementById('newFieldHelpText')?.value || '';

    if (['number', 'currency'].includes(selectedFieldType)) {
        field.validation = {
            min: document.getElementById('newFieldMinValue')?.value || null,
            max: document.getElementById('newFieldMaxValue')?.value || null
        };
    }

    if (['select', 'radio'].includes(selectedFieldType)) {
        const optionsText = document.getElementById('newFieldOptions')?.value || '';
        field.options = optionsText.split('\n').filter(o => o.trim()).map(o => o.trim());
    }

    if (selectedFieldType === 'file') {
        field.validation = {
            fileTypes: document.getElementById('newFieldFileTypes')?.value || '',
            maxSize: parseInt(document.getElementById('newFieldMaxSize')?.value) || 5
        };
    }

    if (selectedFieldType === 'signature') {
        field.validation = {
            allowTyped: document.getElementById('newFieldSignatureTyped')?.checked || false
        };
    }

    if (selectedFieldType === 'paragraph') {
        field.helpText = document.getElementById('newFieldParagraphText')?.value || '';
    }

    // ✅ FIX: SIMPLE TABLE - Collect and store columns properly
    if (selectedFieldType === 'table') {
        const numColumns = parseInt(document.getElementById('newFieldTableColumns')?.value) || 0;

        if (numColumns < 1) {
            alert('⚠️ Table must have at least 1 column');
            return;
        }

        const columns = [];
        for (let i = 0; i < numColumns; i++) {
            const headerInput = document.getElementById(`tableColHeader${i}`);
            const headerValue = headerInput?.value.trim() || `Column ${i + 1}`;
            columns.push(headerValue);
        }

        // ✅ CRITICAL: Store tableConfig with columns
        field.tableConfig = {
            columns: columns,
            minRows: parseInt(document.getElementById('newFieldTableMinRows')?.value) || 1,
            allowAddRows: document.getElementById('newFieldTableAllowAddRows')?.checked || false
        };

        console.log('✅ Simple table configured with columns:', field.tableConfig);
    }

    renderFieldInSection(currentAddFieldSection, field);

    closeAddFieldModal();
    currentEditingField = null;
}

// ============================================================================
// PREVIEW & SAVE FUNCTIONS
// ============================================================================

function generateFullPreview() {
    const templateName = document.getElementById('templateName')?.value || 'Untitled Template';
    const templateDesc = document.getElementById('templateDescription')?.value || 'No description';
    const dueDays = document.getElementById('defaultDueDays')?.value || '30';
    const previewNameEl = document.getElementById('previewTemplateName');
    const previewDescEl = document.getElementById('previewTemplateDescription');
    const previewDueEl = document.getElementById('previewDueDate');
    const previewSectionEl = document.getElementById('previewSectionCount');

    if (previewNameEl) previewNameEl.textContent = templateName;
    if (previewDescEl) previewDescEl.textContent = templateDesc;
    if (previewDueEl) previewDueEl.textContent = `${dueDays} days from issue`;
    if (previewSectionEl) previewSectionEl.textContent = `${sections.length} section${sections.length !== 1 ? 's' : ''}`;

    const container = document.getElementById('previewSectionsContainer');
    if (!container) return;

    container.innerHTML = '';

    sections.forEach((section, index) => {
        let sectionHtml = `
        <div class="preview-section-full">
            <div class="section-header-preview">
                <div class="section-number">${index + 1}</div>
                <h2>${escapeHTML(section.sectionTitle || 'Untitled Section')}</h2>
            </div>
    `;

        if (section.disclaimer) {
            sectionHtml += `<p class="section-disclaimer-preview">ℹ️ ${escapeHTML(section.disclaimer)}</p>`;
        }

        sectionHtml += '<div class="preview-fields-grid">';

        section.fields.forEach(field => {
            sectionHtml += `
    <div class="preview-field-full">
        <label>
            ${escapeHTML(field.fieldLabel)}
            ${field.required ? '<span class="required-mark">*</span>' : ''}
        </label>
        ${field.helpText ? `<small>${escapeHTML(field.helpText)}</small>` : ''}
        <div class="preview-field-render">
            ${generateFieldPreview(field)}
        </div>
    </div>
`;
        });

        sectionHtml += '</div></div>';

        container.insertAdjacentHTML('beforeend', sectionHtml);
    });
}

async function saveDraft() {
    if (!validateStep1()) {
        goToStep(1);
        return;
    }

    const templateData = {
        TemplateName: document.getElementById('templateName').value.trim(),
        Description: document.getElementById('templateDescription').value.trim(),
        IsPublished: false,
        Config: JSON.stringify({
            sections: sections.map(section => ({
                sectionId: section.sectionId,
                sectionTitle: section.sectionTitle,
                sectionOrder: section.sectionOrder,
                disclaimer: section.disclaimer,
                fields: section.fields.map(field => {
                    const baseField = {
                        fieldId: field.fieldId,
                        fieldLabel: field.fieldLabel,
                        fieldType: field.fieldType,
                        required: field.required,
                        order: field.order,
                        placeholder: field.placeholder || '',
                        helpText: field.helpText || '',
                        conditionalOn: field.conditionalOn,
                        options: field.options || [],
                        validation: field.validation || {}
                    };

                    // ✅ Add simple table data
                    if (field.fieldType === 'table' && field.tableConfig) {
                        baseField.tableConfig = field.tableConfig;
                    }

                    // ✅ Add advanced table data
                    if (field.fieldType === 'advancedTable') {
                        baseField.columns = field.columns || [];
                        baseField.rows = field.rows || 0;
                        baseField.gridColumns = field.gridColumns || 0;
                        baseField.cells = field.cells || [];
                    }

                    return baseField;
                })
            })),
            defaultDueDays: parseInt(document.getElementById('defaultDueDays').value) || 30,
            reminders: {
                sevenDays: document.getElementById('reminder7days').checked,
                dueDate: document.getElementById('reminderDueDate').checked
            },
            employeeDetailsIncluded: true
        })
    };

    console.log('📤 Sending template data:', JSON.stringify(templateData, null, 2));

    try {
        const response = await fetch('/Home/CreateTemplate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': document.querySelector('input[name="__RequestVerificationToken"]')?.value
            },
            body: JSON.stringify(templateData)
        });

        const result = await response.json();

        if (result.success) {
            alert('✅ Template saved as draft successfully!');
            window.location.href = '/Home/Templates';
        } else {
            alert('❌ Failed to save: ' + (result.message || 'Unknown error'));
        }
    } catch (error) {
        console.error('Error:', error);
        alert('❌ An error occurred while saving the template.');
    }
}

async function publishTemplate() {
    if (!validateStep1()) {
        goToStep(1);
        return;
    }
    if (!validateStep2()) {
        goToStep(2);
        return;
    }

    if (sections.length === 0) {
        alert('⚠️ Cannot publish a template with no sections.');
        goToStep(2);
        return;
    }

    if (!confirm('📢 Publishing this template will make it available for use. Continue?')) {
        return;
    }

    const templateData = {
        TemplateName: document.getElementById('templateName').value.trim(),
        Description: document.getElementById('templateDescription').value.trim(),
        IsPublished: true,
        Config: JSON.stringify({
            sections: sections.map(section => ({
                sectionId: section.sectionId,
                sectionTitle: section.sectionTitle,
                sectionOrder: section.sectionOrder,
                disclaimer: section.disclaimer,
                fields: section.fields.map(field => {
                    const baseField = {
                        fieldId: field.fieldId,
                        fieldLabel: field.fieldLabel,
                        fieldType: field.fieldType,
                        required: field.required,
                        order: field.order,
                        placeholder: field.placeholder || '',
                        helpText: field.helpText || '',
                        conditionalOn: field.conditionalOn,
                        options: field.options || [],
                        validation: field.validation || {}
                    };

                    // ✅ Add simple table data
                    if (field.fieldType === 'table' && field.tableConfig) {
                        baseField.tableConfig = field.tableConfig;
                    }

                    // ✅ Add advanced table data
                    if (field.fieldType === 'advancedTable') {
                        baseField.columns = field.columns || [];
                        baseField.rows = field.rows || 0;
                        baseField.gridColumns = field.gridColumns || 0;
                        baseField.cells = field.cells || [];
                    }

                    return baseField;
                })
            })),
            defaultDueDays: parseInt(document.getElementById('defaultDueDays').value) || 30,
            reminders: {
                sevenDays: document.getElementById('reminder7days').checked,
                dueDate: document.getElementById('reminderDueDate').checked
            },
            employeeDetailsIncluded: true,
            publishedDate: new Date().toISOString(),
            totalSections: sections.length,
            totalFields: sections.reduce((sum, s) => sum + s.fields.length, 0)
        })
    };

    console.log('📤 Publishing template:', JSON.stringify(templateData, null, 2));

    try {
        const response = await fetch('/Home/CreateTemplate', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(templateData)
        });

        const result = await response.json();

        if (result.success) {
            alert('✅ Template published successfully!');
            window.location.href = '/Home/Templates?published=true';
        } else {
            alert('❌ Failed to publish: ' + (result.message || 'Unknown error'));
        }
    } catch (error) {
        console.error('Error:', error);
        alert('❌ An error occurred while publishing the template.');
    }
}

function getTableColumns(field) {
    // Simple table
    if (field.fieldType === 'table' && field.tableConfig?.columns) {
        return field.tableConfig.columns.map(col => ({
            name: col,
            type: 'text'
        }));
    }
    // Advanced table
    if (field.fieldType === 'advancedTable' && Array.isArray(field.columns)) {
        return field.columns.map(col => ({
            name: col.name || col,
            type: col.type || 'text'
        }));
    }

    return [];
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

function escapeHTML(str) {
    if (!str) return '';
    const div = document.createElement('div');
    div.textContent = str;
    return div.innerHTML;
}

function showNotification(message, type = 'info') {
    alert((type === 'success' ? '✅ ' : type === 'error' ? '❌ ' : 'ℹ️ ') + message);
}