// ============================================================================
// TEMPLATE BUILDER - MAIN MODULE (Advanced Table Separated)
// ============================================================================

let currentStep = 1;
let sections = [];
let sectionIdCounter = 0;
let fieldIdCounter = 0;
let currentEditingField = null;
let currentAddFieldSection = null;
let selectedFieldType = null;

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
    window.location.href = '/Home/Index';
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

    // ✅ Advanced table - use separated module
    if (fieldType === 'advancedTable') {
        console.log('✅ Advanced table selected - initializing advanced table builder');
        closeAddFieldModal();

        setTimeout(() => {
            const fieldId = `field_${fieldIdCounter++}`;

            // Initialize advanced table builder with callback
            AdvancedTableBuilder.init(fieldId, storedSectionId, {
                onSave: handleAdvancedTableSave
            });

            AdvancedTableBuilder.open();
        }, 100);

        return;
    }

    console.log('✅ Regular field type - showing config form');

    const selectStep = document.getElementById('selectFieldTypeStep');
    const configStep = document.getElementById('configureFieldStep');

    if (selectStep) selectStep.style.display = 'none';
    if (configStep) configStep.style.display = 'block';

    generateFieldConfigForm(fieldType);
}

// Callback for advanced table save
function handleAdvancedTableSave(fieldData, sectionId) {
    console.log('📥 Received advanced table field data:', fieldData);

    const section = sections.find(s => s.sectionId === sectionId);
    if (!section) {
        console.error('❌ Section not found:', sectionId);
        alert('⚠️ Error: Section not found');
        return;
    }

    // Add order property
    fieldData.order = section.fields.length + 1;

    // Add field to section
    section.fields.push(fieldData);

    // Render field in UI
    renderFieldInSection(sectionId, fieldData);
    updateFieldCount(sectionId);

    console.log('✅ Advanced table field added to section');
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

    // Simple table configuration
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
                <div id="tableColumnHeadersContainer"></div>
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

// Simple table helper functions
function updateTableColumnsPreview() {
    const numColumns = parseInt(document.getElementById('newFieldTableColumns')?.value) || 3;
    const container = document.getElementById('tableColumnHeadersContainer');
    if (!container) return;

    let html = '';
    for (let i = 0; i < numColumns; i++) {
        html += `
            <div class="form-group-wizard" style="margin-bottom: 12px;">
                <label>Column ${i + 1}</label>
                <input type="text" id="tableColHeader${i}" placeholder="e.g., Company Name" class="form-control">
            </div>
        `;
    }
    container.innerHTML = html;
}

// Main add configured field function
function addConfiguredField() {
    if (!currentAddFieldSection || !selectedFieldType) return;

    const section = sections.find(s => s.sectionId === currentAddFieldSection);
    if (!section) return;

    const label = document.getElementById('newFieldLabel')?.value.trim();

    if (!['heading', 'paragraph', 'divider', 'table'].includes(selectedFieldType) && !label) {
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

    // Simple table configuration
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

    const templateData = buildTemplateData(false);

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

    const templateData = buildTemplateData(true);

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

function buildTemplateData(isPublished) {
    return {
        TemplateName: document.getElementById('templateName').value.trim(),
        Description: document.getElementById('templateDescription').value.trim(),
        IsPublished: isPublished,
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

                    // Add simple table data
                    if (field.fieldType === 'table' && field.tableConfig) {
                        baseField.tableConfig = field.tableConfig;
                    }

                    // Add advanced table data
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
            ...(isPublished && {
                publishedDate: new Date().toISOString(),
                totalSections: sections.length,
                totalFields: sections.reduce((sum, s) => sum + s.fields.length, 0)
            })
        })
    };
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