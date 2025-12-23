// ============================================================================
// TEMPLATE BUILDER - 3 STEP WIZARD WITH LIVE PREVIEW
// ============================================================================

let currentStep = 1;
let sections = [];
let sectionIdCounter = 0;
let fieldIdCounter = 0;
let currentEditingField = null;
let currentTableConfig = null;
let currentAddFieldSection = null;
let selectedFieldType = null;

// ============================================================================
// WIZARD NAVIGATION
// ============================================================================

function openTemplateBuilder() {
    const modal = document.getElementById('templateBuilderModal');
    modal.classList.add('active');
    document.body.style.overflow = 'hidden';

    // Reset to step 1
    goToStep(1);
    resetBuilder();
}

function closeTemplateBuilder() {
    if (!confirm('Are you sure? All unsaved changes will be lost.')) return;

    const modal = document.getElementById('templateBuilderModal');
    modal.classList.remove('active');
    document.body.style.overflow = '';
    resetBuilder();
}

function goToStep(stepNumber) {
    // Validate current step before moving
    if (stepNumber > currentStep) {
        if (currentStep === 1 && !validateStep1()) return;
        if (currentStep === 2 && !validateStep2()) return;
    }

    // Hide all steps
    document.querySelectorAll('.wizard-step').forEach(step => {
        step.style.display = 'none';
    });

    // Show target step
    document.getElementById(`step${stepNumber}`).style.display = 'block';

    // Update progress indicator
    document.querySelectorAll('.progress-step').forEach(step => {
        step.classList.remove('active', 'completed');
        const stepNum = parseInt(step.dataset.step);
        if (stepNum === stepNumber) {
            step.classList.add('active');
        } else if (stepNum < stepNumber) {
            step.classList.add('completed');
        }
    });

    // Update title
    const titles = {
        1: '✨ Create Template - Step 1: Template Info',
        2: '🏗️ Create Template - Step 2: Build Template',
        3: '👁️ Create Template - Step 3: Preview & Publish'
    };
    document.getElementById('wizardTitle').textContent = titles[stepNumber];

    currentStep = stepNumber;

    // If going to step 3, generate full preview
    if (stepNumber === 3) {
        generateFullPreview();
    }
}

function resetBuilder() {
    currentStep = 1;
    sections = [];
    sectionIdCounter = 0;
    fieldIdCounter = 0;

    // Reset form fields
    document.getElementById('templateName').value = '';
    document.getElementById('templateDescription').value = '';
    document.getElementById('defaultDueDays').value = '30';
    document.getElementById('reminder7days').checked = true;
    document.getElementById('reminderDueDate').checked = true;

    // Reset sections
    document.getElementById('sectionsContainer').innerHTML = `
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

// ============================================================================
// STEP 1 VALIDATION
// ============================================================================

function validateStep1() {
    const name = document.getElementById('templateName').value.trim();
    const dueDays = parseInt(document.getElementById('defaultDueDays').value);

    if (!name) {
        alert('⚠️ Please enter a template name');
        return false;
    }

    if (!dueDays || dueDays < 1 || dueDays > 365) {
        alert('⚠️ Due date must be between 1 and 365 days');
        return false;
    }

    return true;
}

// ============================================================================
// STEP 2: SECTION MANAGEMENT
// ============================================================================

function addSection() {
    const container = document.getElementById('sectionsContainer');

    // Remove empty state
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
                <button class="btn btn-ghost btn-sm" style="
        background-color:#00C2CB;
        color:#081B38;" onclick="showAddFieldModal('${sectionId}')">
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

    // Add "Add Section" button if doesn't exist
    updateAddSectionButton();
}

function updateAddSectionButton() {
    const container = document.getElementById('sectionsContainer');
    let addBtn = container.querySelector('.add-section-btn');

    if (!addBtn) {
        container.insertAdjacentHTML('beforeend', `
            <button class="btn btn-ghost add-section-btn" style="
        background-color:#00C2CB;
        color:#081B38;" onclick="addSection()">
                <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path d="M12 4v16m8-8H4" />
                </svg>
                Add Another Section
            </button>
        `);
    }
}

function showFieldPicker(sectionId) {
    // Find the section card
    const sectionCard = document.querySelector(`[data-section-id="${sectionId}"]`);
    if (!sectionCard) return;

    // Highlight the field palette
    const palette = document.querySelector('.field-palette');
    palette.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    palette.style.animation = 'pulse 0.5s';

    // Store current section for field addition
    window.currentActiveSection = sectionId;

    // Add click handlers to palette items
    document.querySelectorAll('.palette-item').forEach(item => {
        item.onclick = () => addFieldToSection(sectionId, item.dataset.type);
    });

    showAddFieldModal(sectionId);
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
    document.querySelector(`[data-section-id="${sectionId}"]`).remove();

    // Show empty state if no sections
    if (sections.length === 0) {
        document.getElementById('sectionsContainer').innerHTML = `
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

function addFieldToSection(sectionId, fieldType) {
    const section = sections.find(s => s.sectionId === sectionId);
    if (!section) return;

    // For table type, open table builder
    if (fieldType === 'table') {
        openTableBuilder(sectionId);
        return;
    }

    const fieldId = `field_${fieldIdCounter++}`;
    const field = {
        fieldId: fieldId,
        fieldLabel: '',
        fieldType: fieldType,
        required: false,
        order: section.fields.length + 1,
        placeholder: '',
        helpText: '',
        conditionalOn: null,
        options: [], // For select, radio, checkbox groups
        validation: {} // For number, currency, etc.
    };

    section.fields.push(field);

    renderFieldInSection(sectionId, field);
    updateFieldCount(sectionId);

    // Auto-open field config for customization
    setTimeout(() => openFieldConfig(sectionId, fieldId), 100);
}

function renderFieldInSection(sectionId, field) {
    const fieldsContainer = document.getElementById(`fields-${sectionId}`);
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
                    <button class="btn-icon-edit" onclick="openFieldConfig('${sectionId}', '${field.fieldId}')" title="Configure">
                        <svg width="16" height="16" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                            <path d="M11 5H6a2 2 0 00-2 2v11a2 2 0 002 2h11a2 2 0 002-2v-5m-1.414-9.414a2 2 0 112.828 2.828L11.828 15H9v-2.828l8.586-8.586z" />
                        </svg>
                    </button>
                    <button class="btn-icon-delete-small" onclick="deleteField('${sectionId}', '${field.fieldId}')" title="Delete">×</button>
                </div>
            </div>
            <div class="field-label-display">
                ${field.fieldLabel || '<em>Click configure to set label</em>'}
                ${field.required ? '<span class="required-mark">*</span>' : ''}
            </div>
            <div class="field-preview-mini" id="preview-${field.fieldId}">
                ${generateFieldPreview(field)}
            </div>
        </div>
    `;

    fieldsContainer.insertAdjacentHTML('beforeend', fieldHtml);
}

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
        table: { label: 'Table', icon: '⊞', color: '#6366F1' },
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
            return `<input type="text" disabled placeholder="${field.placeholder || 'Enter ' + field.fieldType}">`;

        case 'textarea':
            return `<textarea disabled rows="2" placeholder="${field.placeholder || 'Enter long text'}"></textarea>`;

        case 'number':
        case 'currency':
            return `<input type="number" disabled placeholder="${field.placeholder || '0'}">`;

        case 'date':
            return `<input type="date" disabled>`;

        case 'boolean':
            return `<div class="radio-group-preview">
                <label><input type="radio" disabled> Yes</label>
                <label><input type="radio" disabled> No</label>
            </div>`;

        case 'checkbox':
            return `<label class="checkbox-label"><input type="checkbox" disabled> ${field.placeholder || 'Check this'}</label>`;

        case 'select':
            return `<select disabled>
                <option>${field.placeholder || 'Select option'}</option>
                ${field.options.map(opt => `<option>${opt}</option>`).join('')}
            </select>`;

        case 'radio':
            return `<div class="radio-group-preview">
                ${field.options.map(opt => `<label><input type="radio" name="prev-${field.fieldId}" disabled> ${opt}</label>`).join('')}
            </div>`;

        case 'file':
            return `<div class="file-upload-preview">
                <svg width="20" height="20" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                    <path d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                </svg>
                <span>Click to upload file</span>
            </div>`;

        case 'table':
            return `<div class="table-preview">📊 Table: ${field.tableConfig?.columns?.length || 0} columns</div>`;

        case 'signature':
            return `<div class="signature-preview">
                <div class="signature-pad">✍️ Signature area</div>
            </div>`;

        case 'heading':
            return `<h3 class="heading-preview">${field.fieldLabel || 'Heading'}</h3>`;

        case 'paragraph':
            return `<p class="paragraph-preview">${field.helpText || 'Paragraph text...'}</p>`;

        case 'divider':
            return `<hr class="divider-preview">`;

        default:
            return `<input type="text" disabled placeholder="Field preview">`;
    }
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
    const section = sections.find(s => s.sectionId === sectionId);
    if (!section) return;

    section.fields = section.fields.filter(f => f.fieldId !== fieldId);
    document.querySelector(`[data-field-id="${fieldId}"]`).remove();

    updateFieldCount(sectionId);

    // Show empty state if no fields
    const fieldsContainer = document.getElementById(`fields-${sectionId}`);
    if (section.fields.length === 0) {
        fieldsContainer.innerHTML = '<div class="empty-fields"><p>No fields yet. Click any field type above to add it here.</p></div>';
    }
}

// ============================================================================
// FIELD CONFIGURATION MODAL
// ============================================================================

function openFieldConfig(sectionId, fieldId) {
    const section = sections.find(s => s.sectionId === sectionId);
    if (!section) return;

    const field = section.fields.find(f => f.fieldId === fieldId);
    if (!field) return;

    currentEditingField = { sectionId, fieldId };

    const modal = document.getElementById('fieldConfigModal');
    const form = document.getElementById('fieldConfigForm');

    document.getElementById('fieldConfigTitle').textContent = `Configure ${getFieldTypeInfo(field.fieldType).label} Field`;

    let configHtml = `
        <div class="form-group-wizard">
            <label>Field Label <span class="required-mark">*</span></label>
            <input type="text" id="configFieldLabel" value="${field.fieldLabel || ''}" placeholder="e.g., Company Name">
            <small>The question or label shown to employees</small>
        </div>
        
        <div class="form-group-wizard">
            <label>Help Text</label>
            <input type="text" id="configHelpText" value="${field.helpText || ''}" placeholder="Additional instructions...">
            <small>Optional guidance for filling this field</small>
        </div>
        
        <div class="form-group-wizard">
            <label class="checkbox-label">
                <input type="checkbox" id="configRequired" ${field.required ? 'checked' : ''}>
                <span>Required field</span>
            </label>
        </div>
    `;

    // Type-specific configuration
    if (['text', 'textarea', 'email', 'url', 'phone'].includes(field.fieldType)) {
        configHtml += `
            <div class="form-group-wizard">
                <label>Placeholder Text</label>
                <input type="text" id="configPlaceholder" value="${field.placeholder || ''}" placeholder="e.g., Enter your answer here...">
            </div>
        `;
    }

    if (['number', 'currency'].includes(field.fieldType)) {
        configHtml += `
            <div class="form-grid-2">
                <div class="form-group-wizard">
                    <label>Minimum Value</label>
                    <input type="number" id="configMinValue" value="${field.validation?.min || ''}">
                </div>
                <div class="form-group-wizard">
                    <label>Maximum Value</label>
                    <input type="number" id="configMaxValue" value="${field.validation?.max || ''}">
                </div>
            </div>
        `;
    }

    if (['select', 'radio'].includes(field.fieldType)) {
        configHtml += `
            <div class="form-group-wizard">
                <label>Options <span class="required-mark">*</span></label>
                <textarea id="configOptions" rows="4" placeholder="Enter each option on a new line">${field.options.join('\n')}</textarea>
                <small>One option per line</small>
            </div>
        `;
    }

    if (field.fieldType === 'file') {
        configHtml += `
            <div class="form-group-wizard">
                <label>Allowed File Types</label>
                <input type="text" id="configFileTypes" value="${field.validation?.fileTypes || ''}" placeholder=".pdf, .doc, .jpg">
                <small>Leave empty to allow all types</small>
            </div>
            <div class="form-group-wizard">
                <label>Max File Size (MB)</label>
                <input type="number" id="configMaxSize" value="${field.validation?.maxSize || 5}" min="1" max="50">
            </div>
        `;
    }

    if (field.fieldType === 'signature') {
        configHtml += `
            <div class="form-group-wizard">
                <label class="checkbox-label">
                    <input type="checkbox" id="configSignatureTyped" ${field.validation?.allowTyped ? 'checked' : ''}>
                    <span>Allow typed signatures</span>
                </label>
                <small>If unchecked, only drawn signatures are allowed</small>
            </div>
        `;
    }

    configHtml += `
        <div class="modal-actions">
            <button class="btn btn-ghost" onclick="closeFieldConfig()">Cancel</button>
            <button class="btn btn-primary" onclick="saveFieldConfig()">Save Configuration</button>
        </div>
    `;

    form.innerHTML = configHtml;
    modal.classList.add('active');
}

function closeFieldConfig() {
    document.getElementById('fieldConfigModal').classList.remove('active');
    currentEditingField = null;
}

function saveFieldConfig() {
    if (!currentEditingField) return;

    const section = sections.find(s => s.sectionId === currentEditingField.sectionId);
    if (!section) return;

    const field = section.fields.find(f => f.fieldId === currentEditingField.fieldId);
    if (!field) return;

    // Update field properties
    field.fieldLabel = document.getElementById('configFieldLabel').value;
    field.helpText = document.getElementById('configHelpText')?.value || '';
    field.required = document.getElementById('configRequired').checked;
    field.placeholder = document.getElementById('configPlaceholder')?.value || '';

    // Type-specific updates
    if (['number', 'currency'].includes(field.fieldType)) {
        field.validation = {
            min: document.getElementById('configMinValue')?.value || null,
            max: document.getElementById('configMaxValue')?.value || null
        };
    }

    if (['select', 'radio'].includes(field.fieldType)) {
        const optionsText = document.getElementById('configOptions').value;
        field.options = optionsText.split('\n').filter(o => o.trim()).map(o => o.trim());
    }

    if (field.fieldType === 'file') {
        field.validation = {
            fileTypes: document.getElementById('configFileTypes')?.value || '',
            maxSize: parseInt(document.getElementById('configMaxSize')?.value) || 5
        };
    }

    if (field.fieldType === 'signature') {
        field.validation = {
            allowTyped: document.getElementById('configSignatureTyped')?.checked || false
        };
    }

    // Update the visual display
    const fieldCard = document.querySelector(`[data-field-id="${field.fieldId}"]`);
    if (fieldCard) {
        fieldCard.querySelector('.field-label-display').innerHTML = `
            ${field.fieldLabel || '<em>Click configure to set label</em>'}
            ${field.required ? '<span class="required-mark">*</span>' : ''}
        `;

        // Update mini preview
        const previewEl = document.getElementById(`preview-${field.fieldId}`);
        if (previewEl) {
            previewEl.innerHTML = generateFieldPreview(field);
        }
    }

    closeFieldConfig();
}

// ============================================================================
// TABLE BUILDER
// ============================================================================

function openTableBuilder(sectionId) {
    currentTableConfig = { sectionId };
    const modal = document.getElementById('tableBuilderModal');
    modal.classList.add('active');

    document.getElementById('tableLabel').value = '';
    document.getElementById('tableColumns').value = '3';
    generateTableColumns();
}

function closeTableBuilder() {
    document.getElementById('tableBuilderModal').classList.remove('active');
    currentTableConfig = null;
}

function generateTableColumns() {
    const numColumns = parseInt(document.getElementById('tableColumns').value);
    const container = document.getElementById('tableColumnsConfig');

    let html = '<div class="table-columns-list">';
    for (let i = 0; i < numColumns; i++) {
        html += `
            <div class="form-group-wizard">
                <label>Column ${i + 1} Header <span class="required-mark">*</span></label>
                <input type="text" id="colHeader${i}" placeholder="e.g., Company Name, Registration Number">
            </div>
        `;
    }
    html += '</div>';

    container.innerHTML = html;
}

function saveTableConfig() {
    if (!currentTableConfig) return;

    const label = document.getElementById('tableLabel').value.trim();
    if (!label) {
        alert('⚠️ Please enter a table label');
        return;
    }

    const numColumns = parseInt(document.getElementById('tableColumns').value);
    const columns = [];

    for (let i = 0; i < numColumns; i++) {
        const header = document.getElementById(`colHeader${i}`).value.trim();
        if (!header) {
            alert(`⚠️ Please enter a header for column ${i + 1}`);
            return;
        }
        columns.push(header);
    }

    const section = sections.find(s => s.sectionId === currentTableConfig.sectionId);
    if (!section) return;

    const fieldId = `field_${fieldIdCounter++}`;
    const field = {
        fieldId: fieldId,
        fieldLabel: label,
        fieldType: 'table',
        required: false,
        order: section.fields.length + 1,
        tableConfig: {
            columns: columns,
            minRows: parseInt(document.getElementById('tableMinRows').value) || 1,
            allowAddRows: document.getElementById('tableAllowAddRows').checked
        }
    };

    section.fields.push(field);
    renderFieldInSection(currentTableConfig.sectionId, field);
    updateFieldCount(currentTableConfig.sectionId);

    closeTableBuilder();
}

// ============================================================================
// STEP 2 VALIDATION
// ============================================================================

function validateStep2() {
    if (sections.length === 0) {
        if (!confirm('⚠️ Your template has no sections. Continue anyway?')) {
            return false;
        }
    }

    for (const section of sections) {
        if (!section.sectionTitle.trim()) {
            alert(`⚠️ Please provide a title for all sections`);
            return false;
        }
    }

    return true;
}

// ============================================================================
// STEP 3: FULL PREVIEW GENERATION
// ============================================================================

function generateFullPreview() {
    // Update header info
    const templateName = document.getElementById('templateName').value || 'Untitled Template';
    const templateDesc = document.getElementById('templateDescription').value || 'No description provided';
    const dueDays = document.getElementById('defaultDueDays').value;

    document.getElementById('previewTemplateName').textContent = templateName;
    document.getElementById('previewTemplateDescription').textContent = templateDesc;
    document.getElementById('previewDueDate').textContent = `${dueDays} days from issue`;
    document.getElementById('previewSectionCount').textContent = `${sections.length} section${sections.length !== 1 ? 's' : ''}`;

    // Generate sections preview
    const container = document.getElementById('previewSectionsContainer');
    container.innerHTML = '';

    sections.forEach((section, index) => {
        let sectionHtml = `
            <div class="preview-section-full">
                <div class="section-header-preview">
                    <div class="section-number">${index + 1}</div>
                    <h2>${section.sectionTitle || 'Untitled Section'}</h2>
                </div>
        `;

        if (section.disclaimer) {
            sectionHtml += `<p class="section-disclaimer-preview">ℹ️ ${section.disclaimer}</p>`;
        }

        sectionHtml += '<div class="preview-fields-grid">';

        section.fields.forEach(field => {
            sectionHtml += generateFullFieldPreview(field);
        });

        sectionHtml += '</div></div>';

        container.insertAdjacentHTML('beforeend', sectionHtml);
    });
}

function generateFullFieldPreview(field) {
    const requiredMark = field.required ? '<span class="required-mark">*</span>' : '';

    if (field.fieldType === 'heading') {
        return `<div class="preview-field-full full-width"><h3>${field.fieldLabel}</h3></div>`;
    }

    if (field.fieldType === 'paragraph') {
        return `<div class="preview-field-full full-width"><p class="info-text">${field.helpText}</p></div>`;
    }

    if (field.fieldType === 'divider') {
        return `<div class="preview-field-full full-width"><hr></div>`;
    }

    let html = `
        <div class="preview-field-full ${field.fieldType === 'table' ? 'full-width' : ''}">
            <label>${field.fieldLabel || 'Unlabeled Field'}${requiredMark}</label>
    `;

    if (field.helpText) {
        html += `<small class="field-help">${field.helpText}</small>`;
    }

    html += '<div class="field-input-preview">';

    switch (field.fieldType) {
        case 'text':
        case 'email':
        case 'url':
        case 'phone':
            html += `<input type="text" disabled placeholder="${field.placeholder || 'Enter ' + field.fieldType}">`;
            break;

        case 'textarea':
            html += `<textarea disabled rows="3" placeholder="${field.placeholder || 'Enter detailed information'}"></textarea>`;
            break;

        case 'number':
            html += `<input type="number" disabled placeholder="${field.placeholder || '0'}" ${field.validation?.min ? `min="${field.validation.min}"` : ''} ${field.validation?.max ? `max="${field.validation.max}"` : ''}>`;
            break;

        case 'currency':
            html += `<div class="currency-input"><span class="currency-symbol">R</span><input type="number" disabled placeholder="0.00" step="0.01"></div>`;
            break;

        case 'date':
            html += `<input type="date" disabled>`;
            break;

        case 'boolean':
            html += `
                <div class="radio-group-full">
                    <label class="radio-label"><input type="radio" disabled name="bool-${field.fieldId}"> Yes</label>
                    <label class="radio-label"><input type="radio" disabled name="bool-${field.fieldId}"> No</label>
                </div>
            `;
            break;

        case 'checkbox':
            html += `<label class="checkbox-label-full"><input type="checkbox" disabled> ${field.placeholder || 'I agree'}</label>`;
            break;

        case 'select':
            html += `<select disabled><option>${field.placeholder || 'Select an option'}</option>`;
            field.options.forEach(opt => html += `<option>${opt}</option>`);
            html += `</select>`;
            break;

        case 'radio':
            html += `<div class="radio-group-full">`;
            field.options.forEach(opt => {
                html += `<label class="radio-label"><input type="radio" disabled name="radio-${field.fieldId}"> ${opt}</label>`;
            });
            html += `</div>`;
            break;

        case 'file':
            html += `
                <div class="file-upload-full">
                    <svg width="24" height="24" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                        <path d="M7 16a4 4 0 01-.88-7.903A5 5 0 1115.9 6L16 6a5 5 0 011 9.9M15 13l-3-3m0 0l-3 3m3-3v12" />
                    </svg>
                    <span>Click to upload or drag file here</span>
                    ${field.validation?.fileTypes ? `<small>Allowed: ${field.validation.fileTypes}</small>` : ''}
                </div>
            `;
            break;

        case 'table':
            html += `<div class="table-full"><table><thead><tr>`;
            field.tableConfig.columns.forEach(col => html += `<th>${col}</th>`);
            html += `<th width="50"></th></tr></thead><tbody>`;
            for (let i = 0; i < field.tableConfig.minRows; i++) {
                html += `<tr>`;
                field.tableConfig.columns.forEach(() => html += `<td><input type="text" disabled></td>`);
                html += `<td><button disabled>×</button></td></tr>`;
            }
            html += `</tbody></table>`;
            if (field.tableConfig.allowAddRows) {
                html += `<button class="btn-add-row" disabled>+ Add Row</button>`;
            }
            html += `</div>`;
            break;

        case 'signature':
            html += `
                <div class="signature-full">
                    <div class="signature-pad-full">
                        <svg width="24" height="24" fill="none" stroke="currentColor" stroke-width="2" viewBox="0 0 24 24">
                            <path d="M15.232 5.232l3.536 3.536m-2.036-5.036a2.5 2.5 0 113.536 3.536L6.5 21.036H3v-3.572L16.732 3.732z" />
                        </svg>
                        <span>Sign here</span>
                    </div>
                    ${field.validation?.allowTyped ? '<button class="btn-ghost btn-sm" disabled>Type Signature</button>' : ''}
                </div>
            `;
            break;
    }

    html += '</div></div>';

    return html;
}

// ============================================================================
// SAVE & PUBLISH
// ============================================================================

async function saveDraft() {
    const templateData = buildTemplateData('Draft');
    await saveTemplateToServer(templateData);
}

async function publishTemplate() {
    if (!validateForPublish()) return;

    const templateData = buildTemplateData('Active');
    await saveTemplateToServer(templateData);
}

function validateForPublish() {
    const name = document.getElementById('templateName').value.trim();
    if (!name) {
        alert('⚠️ Template name is required');
        return false;
    }

    if (sections.length === 0) {
        alert('⚠️ Template must have at least one section');
        return false;
    }

    for (const section of sections) {
        if (!section.sectionTitle.trim()) {
            alert('⚠️ All sections must have titles');
            return false;
        }

        if (section.fields.length === 0) {
            alert(`⚠️ Section "${section.sectionTitle}" has no fields`);
            return false;
        }

        for (const field of section.fields) {
            if (!['heading', 'paragraph', 'divider'].includes(field.fieldType) && !field.fieldLabel.trim()) {
                alert(`⚠️ All fields must have labels in section "${section.sectionTitle}"`);
                return false;
            }
        }
    }

    return true;
}

function buildTemplateData() {
    return {
        templateName: document.getElementById('templateName').value.trim(),
        description: document.getElementById('templateDescription').value.trim(),
        config: {
            defaultDueDays: parseInt(document.getElementById('defaultDueDays').value),
            reminders: {
                sevenDaysBefore: document.getElementById('reminder7days').checked,
                onDueDate: document.getElementById('reminderDueDate').checked
            },
            sections: sections
        }
    };
}

async function saveTemplateToServer(templateData) {

    try {
        const token = document.querySelector('input[name="__RequestVerificationToken"]').value;

        const response = await fetch(window.createTemplateUrl, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': token
               

            },
            body: JSON.stringify(templateData)
        });

        if (!response.ok) {
            const text = await response.text();
            console.error('Server response:', text);
            throw new Error(`HTTP ${response.status}`);
        }

        const result = await response.json();

        if (result.success) {
            alert(`✅ ${result.message}`);
            closeTemplateBuilder();
            location.reload();
        } else {
            alert(`❌ ${result.message}`);
        }
    } catch (error) {
        console.error('Error saving template:', error);
        alert('❌ Error saving template. Please try again.');
    }
}

function showAddFieldModal(sectionId) {
    currentAddFieldSection = sectionId;
    selectedFieldType = null;

    // Reset to step 1
    document.getElementById('selectFieldTypeStep').style.display = 'block';
    document.getElementById('configureFieldStep').style.display = 'none';

    const modal = document.getElementById('addFieldModal');
    modal.classList.add('active');
}

function closeAddFieldModal() {
    document.getElementById('addFieldModal').classList.remove('active');
    currentAddFieldSection = null;
    selectedFieldType = null;
}

function selectFieldType(fieldType) {
    selectedFieldType = fieldType;

    // For table type, open table builder instead
    if (fieldType === 'table') {
        closeAddFieldModal();
        openTableBuilder(currentAddFieldSection);
        return;
    }

    // Switch to configuration step
    document.getElementById('selectFieldTypeStep').style.display = 'none';
    document.getElementById('configureFieldStep').style.display = 'block';

    // Generate configuration form
    generateFieldConfigForm(fieldType);
}

function generateFieldConfigForm(fieldType) {
    const container = document.getElementById('configureFieldStep');
    const fieldTypeInfo = getFieldTypeInfo(fieldType);

    let html = `
        <div class="step-intro">
            <div class="field-type-icon">${fieldTypeInfo.icon}</div>
            <h3>Configure ${fieldTypeInfo.label} Field</h3>
            <p>Set up the properties for this field</p>
        </div>
        
        <div class="form-group-wizard">
            <label>Field Label <span class="required-mark">*</span></label>
            <input type="text" id="newFieldLabel" placeholder="e.g., Company Name, Do you own shares?" autofocus>
            <small>The question or label shown to employees</small>
        </div>
        
        <div class="form-group-wizard">
            <label>Help Text (Optional)</label>
            <input type="text" id="newFieldHelpText" placeholder="Additional instructions or guidance...">
            <small>Extra information to help employees fill this field</small>
        </div>
        
        <div class="form-group-wizard">
            <label class="checkbox-label">
                <input type="checkbox" id="newFieldRequired">
                <span>This field is required</span>
            </label>
        </div>
    `;

    // Type-specific configuration
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
                <small>One option per line - employees will choose from these</small>
            </div>
        `;
    }

    if (fieldType === 'file') {
        html += `
            <div class="form-group-wizard">
                <label>Allowed File Types</label>
                <input type="text" id="newFieldFileTypes" placeholder=".pdf, .doc, .docx, .jpg, .png" value=".pdf, .doc, .docx">
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
                    <span>Allow typed signatures (in addition to drawn)</span>
                </label>
            </div>
        `;
    }

    if (fieldType === 'paragraph') {
        html += `
            <div class="form-group-wizard">
                <label>Paragraph Text <span class="required-mark">*</span></label>
                <textarea id="newFieldParagraphText" rows="4" placeholder="Enter the text that will be displayed..."></textarea>
                <small>This text will be shown to employees (not editable by them)</small>
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
                Add Field
            </button>
        </div>
    `;

    container.innerHTML = html;

    // Focus on label input
    setTimeout(() => document.getElementById('newFieldLabel')?.focus(), 100);
}

function backToFieldTypeSelection() {
    document.getElementById('selectFieldTypeStep').style.display = 'block';
    document.getElementById('configureFieldStep').style.display = 'none';
}

function addConfiguredField() {
    if (!currentAddFieldSection || !selectedFieldType) return;

    const section = sections.find(s => s.sectionId === currentAddFieldSection);
    if (!section) return;

    // Validate required fields
    const label = document.getElementById('newFieldLabel')?.value.trim();

    if (!['heading', 'paragraph', 'divider'].includes(selectedFieldType) && !label) {
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

    // Create field object
    const fieldId = `field_${fieldIdCounter++}`;
    const field = {
        fieldId: fieldId,
        fieldLabel: label || '',
        fieldType: selectedFieldType,
        required: document.getElementById('newFieldRequired')?.checked || false,
        order: section.fields.length + 1,
        placeholder: document.getElementById('newFieldPlaceholder')?.value || '',
        helpText: document.getElementById('newFieldHelpText')?.value || '',
        conditionalOn: null,
        options: [],
        validation: {}
    };

    // Type-specific properties
    if (['number', 'currency'].includes(selectedFieldType)) {
        field.validation = {
            min: document.getElementById('newFieldMinValue')?.value || null,
            max: document.getElementById('newFieldMaxValue')?.value || null
        };
    }

    if (['select', 'radio'].includes(selectedFieldType)) {
        const optionsText = document.getElementById('newFieldOptions').value;
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

    // Add to section
    section.fields.push(field);
    renderFieldInSection(currentAddFieldSection, field);
    updateFieldCount(currentAddFieldSection);

    // Close modal
    closeAddFieldModal();
}
