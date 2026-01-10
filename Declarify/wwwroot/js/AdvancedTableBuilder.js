// ============================================================================
// ADVANCED TABLE BUILDER - STANDALONE MODULE
// ============================================================================

/**
 * Advanced Table Builder - Standalone module for creating complex tables
 * with merged cells and column assignments
 */
const AdvancedTableBuilder = (function () {
    'use strict';

    // ========================================================================
    // PRIVATE STATE
    // ========================================================================

    const state = {
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
        columnCounter: 0,
        onSaveCallback: null
    };

    // ========================================================================
    // PRIVATE UTILITY FUNCTIONS
    // ========================================================================

    function escapeHTML(str) {
        if (!str) return '';
        const div = document.createElement('div');
        div.textContent = str;
        return div.innerHTML;
    }

    function showNotification(message, type = 'info') {
        const prefix = type === 'success' ? '✅ ' : type === 'error' ? '❌ ' : 'ℹ️ ';
        alert(prefix + message);
    }

    // ========================================================================
    // INITIALIZATION
    // ========================================================================

    function init(fieldId, sectionId, options = {}) {
        state.currentFieldId = fieldId;
        state.currentSectionId = sectionId;
        state.step = 1;
        state.columns = [];
        state.rows = options.rows || 4;
        state.gridColumns = options.gridColumns || 3;
        state.gridCells = [];
        state.selectedCells = [];
        state.fieldLabel = options.fieldLabel || '';
        state.fieldHelpText = options.fieldHelpText || '';
        state.fieldRequired = options.fieldRequired || false;
        state.onSaveCallback = options.onSave || null;
        state.columnCounter = 0;

        console.log('✅ Advanced Table Builder initialized:', {
            fieldId,
            sectionId,
            options
        });
    }

    // ========================================================================
    // MODAL CREATION AND MANAGEMENT
    // ========================================================================

    function createWizardModal(fieldId) {
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
            <button class="modal-close" onclick="AdvancedTableBuilder.close('${fieldId}')">×</button>
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
                <!-- Content rendered dynamically -->
            </div>

            <!-- Step 2: Build Table Structure -->
            <div id="advTable-${fieldId}-step-2" class="wizard-step" style="display: none;">
                <!-- Content rendered dynamically -->
            </div>

            <!-- Step 3: Preview -->
            <div id="advTable-${fieldId}-step-3" class="wizard-step" style="display: none;">
                <!-- Content rendered dynamically -->
            </div>
        </div>

        <div class="modal-actions">
            <button type="button" 
                    id="advTable-${fieldId}-btn-back"
                    class="btn btn-ghost" 
                    onclick="AdvancedTableBuilder.previousStep('${fieldId}')"
                    style="display: none;">
                <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="15 18 9 12 15 6"></polyline>
                </svg>
                Back
            </button>
            <div style="flex: 1;"></div>
            <button type="button" class="btn btn-ghost" onclick="AdvancedTableBuilder.close('${fieldId}')">
                Cancel
            </button>
            <button type="button" 
                    id="advTable-${fieldId}-btn-next"
                    class="btn btn-primary" 
                    onclick="AdvancedTableBuilder.nextStep('${fieldId}')">
                Next
                <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="9 18 15 12 9 6"></polyline>
                </svg>
            </button>
            <button type="button" 
                    id="advTable-${fieldId}-btn-save"
                    class="btn btn-primary" 
                    onclick="AdvancedTableBuilder.save('${fieldId}')"
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

    function open() {
        const fieldId = state.currentFieldId;

        if (!fieldId) {
            console.error('❌ No field ID set');
            return;
        }

        console.log('🎯 Opening advanced table wizard for field:', fieldId);

        // Remove existing wizard if any
        const existingWizard = document.querySelector(`[id^="advTableWizard-"]`);
        if (existingWizard) {
            existingWizard.remove();
        }

        // Create and insert modal
        const modalHTML = createWizardModal(fieldId);
        document.body.insertAdjacentHTML('beforeend', modalHTML);

        // Initialize Step 1
        setTimeout(() => renderStep1(fieldId), 100);
    }

    function close(fieldId) {
        if (!confirm('Close the advanced table builder? Any unsaved changes will be lost.')) {
            return;
        }

        const modal = document.getElementById(`advTableWizard-${fieldId}`);
        if (modal) {
            modal.remove();
        }

        // Reset state
        Object.assign(state, {
            currentFieldId: null,
            currentSectionId: null,
            step: 1,
            columns: [],
            gridCells: [],
            selectedCells: [],
            fieldLabel: '',
            fieldHelpText: '',
            fieldRequired: false,
            columnCounter: 0
        });

        console.log('✅ Advanced table wizard closed');
    }

    // ========================================================================
    // STEP 1: DEFINE COLUMNS
    // ========================================================================

    function renderStep1(fieldId) {
        const container = document.getElementById(`advTable-${fieldId}-step-1`);
        if (!container) return;

        container.innerHTML = `
    <!-- BASIC FIELD PROPERTIES -->
    <div style="margin-bottom: 32px; padding-bottom: 24px; border-bottom: 1px solid #e5e7eb;">
        <h3 style="margin: 0 0 16px 0; color: #1e40af;">1. Table Information</h3>
        
        <div class="form-group-wizard">
            <label>Table Label <span class="required-mark">*</span></label>
            <input type="text" id="advTable-${fieldId}-label" class="form-control" 
                   value="${escapeHTML(state.fieldLabel || '')}"
                   placeholder="e.g. Schedule of Shareholdings" required>
        </div>

        <div class="form-group-wizard">
            <label>Help Text / Instructions (optional)</label>
            <textarea id="advTable-${fieldId}-help" rows="2" class="form-control"
                      placeholder="Additional guidance for the employee...">${escapeHTML(state.fieldHelpText || '')}</textarea>
        </div>

        <div class="form-group-wizard">
            <label class="checkbox-label">
                <input type="checkbox" id="advTable-${fieldId}-required" 
                       ${state.fieldRequired ? 'checked' : ''}>
                This table is required
            </label>
        </div>
    </div>

    <!-- TABLE STRUCTURE -->
    <h3 style="margin: 0 0 16px 0; color: #1e40af;">2. Table Structure</h3>
    
    <div style="display: grid; grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px;">
        <div class="form-group-wizard">
            <label>Number of Columns</label>
            <input type="number" id="advTable-${fieldId}-columns-count" 
                   value="${state.gridColumns || 3}" min="1" max="10">
        </div>
        <div class="form-group-wizard">
            <label>Number of Data Rows</label>
            <input type="number" id="advTable-${fieldId}-rows-count" 
                   value="${state.rows || 4}" min="1" max="30">
        </div>
    </div>

    <div class="form-group-wizard">
        <label>Define Columns</label>
        <div id="advTable-${fieldId}-columns-list" class="columns-list"></div>
        <button type="button" class="btn btn-ghost btn-sm" 
                onclick="AdvancedTableBuilder.addColumn('${fieldId}')">
            + Add Column
        </button>
    </div>

    <div class="info-box" style="margin-top: 24px;">
        <strong>Next steps:</strong> After defining columns → arrange layout & merge cells
    </div>
    `;

        // Re-populate columns if any exist
        state.columns.forEach(() => addColumn(fieldId));
    }

    function addColumn(fieldId) {
        const container = document.getElementById(`advTable-${fieldId}-columns-list`);
        if (!container) return;

        const colId = `advTableCol-${state.columnCounter++}`;
        const columnNumber = container.children.length + 1;

        const colDiv = document.createElement('div');
        colDiv.className = 'adv-table-column-item';
        colDiv.dataset.columnId = colId;
        colDiv.style.cssText = 'display: flex; align-items: center; gap: 8px; padding: 8px; background: #f9fafb; border-radius: 6px; margin-bottom: 8px;';

        colDiv.innerHTML = `
    <span style="min-width: 32px; height: 32px; background: #e0e7ff; color: #4f46e5; border-radius: 6px; display: flex; align-items: center; justify-content: center; font-weight: 600; font-size: 14px; flex-shrink: 0;">
        ${columnNumber}
    </span>
    <input type="text" placeholder="Column name (e.g., Account Number)" 
           class="form-control adv-table-col-name" 
           style="flex: 1;" data-column-id="${colId}">
    <select class="form-control adv-table-col-type" 
            style="width: 140px; flex-shrink: 0;" data-column-id="${colId}">
        <option value="text">Text</option>
        <option value="number">Number</option>
        <option value="date">Date</option>
        <option value="email">Email</option>
    </select>
    <button type="button" 
            class="btn-icon-delete-small" 
            onclick="AdvancedTableBuilder.removeColumn('${fieldId}', this)"
            style="flex-shrink: 0; padding: 4px 8px; background: #ef4444; color: white; border: none; border-radius: 4px; cursor: pointer;">
        ×
    </button>
`;

        container.appendChild(colDiv);
        updateColumnNumbers(fieldId);
    }

    function removeColumn(fieldId, button) {
        const container = document.getElementById(`advTable-${fieldId}-columns-list`);
        if (!container || container.children.length <= 1) {
            showNotification('At least one column is required', 'error');
            return;
        }

        button.closest('.adv-table-column-item').remove();
        updateColumnNumbers(fieldId);
    }

    function updateColumnNumbers(fieldId) {
        const container = document.getElementById(`advTable-${fieldId}-columns-list`);
        if (!container) return;

        Array.from(container.children).forEach((item, index) => {
            const numberSpan = item.querySelector('span');
            if (numberSpan) numberSpan.textContent = index + 1;
        });
    }

    function validateStep1(fieldId) {
        // Validate and save field label
        const labelInput = document.getElementById(`advTable-${fieldId}-label`);
        const label = labelInput?.value.trim() || '';

        if (!label) {
            showNotification('Please enter a table label', 'error');
            labelInput?.focus();
            return false;
        }

        state.fieldLabel = label;

        // Save help text and required status
        const helpInput = document.getElementById(`advTable-${fieldId}-help`);
        const requiredInput = document.getElementById(`advTable-${fieldId}-required`);

        state.fieldHelpText = helpInput?.value.trim() || '';
        state.fieldRequired = requiredInput?.checked || false;

        // Validate columns
        const container = document.getElementById(`advTable-${fieldId}-columns-list`);
        if (!container || container.children.length === 0) {
            showNotification('Please add at least one column', 'error');
            return false;
        }

        state.columns = [];
        const columnItems = container.querySelectorAll('.adv-table-column-item');

        for (let item of columnItems) {
            const nameInput = item.querySelector('.adv-table-col-name');
            const typeSelect = item.querySelector('.adv-table-col-type');
            const name = nameInput ? nameInput.value.trim() : '';

            if (!name) {
                showNotification('All columns must have names', 'error');
                nameInput?.focus();
                return false;
            }

            state.columns.push({
                id: nameInput.dataset.columnId,
                name: name,
                type: typeSelect ? typeSelect.value : 'text'
            });
        }

        // Save row and column counts
        const rowsInput = document.getElementById(`advTable-${fieldId}-rows-count`);
        const colsInput = document.getElementById(`advTable-${fieldId}-columns-count`);

        state.rows = rowsInput ? parseInt(rowsInput.value) || 4 : 4;
        state.gridColumns = colsInput ? parseInt(colsInput.value) || 3 : state.columns.length;

        return true;
    }

    // ========================================================================
    // STEP 2: BUILD TABLE STRUCTURE
    // ========================================================================

    function renderStep2(fieldId) {
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
            <p style="margin: 4px 0 0 0; color: #1e40af;">
                Select cells to merge, then assign column names to merged regions.
            </p>
        </div>
    </div>

    <!-- Toolbar -->
    <div class="table-toolbar" style="display: flex; gap: 8px; margin-bottom: 20px; padding: 16px; background: #f9fafb; border-radius: 8px; flex-wrap: wrap;">
        <button type="button" onclick="AdvancedTableBuilder.mergeCells('${fieldId}')" 
                style="display: flex; align-items: center; gap: 8px; padding: 10px 16px; background: white; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer;">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                <rect x="3" y="3" width="18" height="18" rx="2"></rect>
                <line x1="12" y1="3" x2="12" y2="21"></line>
                <line x1="3" y1="12" x2="21" y2="12"></line>
            </svg>
            Merge Selected
        </button>
        <button type="button" onclick="AdvancedTableBuilder.showColumnAssignment('${fieldId}')" 
                style="display: flex; align-items: center; gap: 8px; padding: 10px 16px; background: white; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer;">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                <path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"></path>
                <path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z"></path>
            </svg>
            Assign Column
        </button>
        <button type="button" onclick="AdvancedTableBuilder.clearSelection('${fieldId}')" 
                style="display: flex; align-items: center; gap: 8px; padding: 10px 16px; background: white; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer;">
            <svg width="18" height="18" fill="none" stroke="currentColor" stroke-width="2">
                <line x1="18" y1="6" x2="6" y2="18"></line>
                <line x1="6" y1="6" x2="18" y2="18"></line>
            </svg>
            Clear Selection
        </button>
        <button type="button" onclick="AdvancedTableBuilder.resetGrid('${fieldId}')" 
                style="display: flex; align-items: center; gap: 8px; padding: 10px 16px; background: white; border: 1px solid #d1d5db; border-radius: 6px; font-size: 14px; font-weight: 500; cursor: pointer;">
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
    <div id="advTable-${fieldId}-table-grid" class="advanced-table-grid" 
         style="display: grid; gap: 2px; background: #e5e7eb; border: 2px solid #d1d5db; border-radius: 8px; padding: 2px; margin-bottom: 20px;">
    </div>

    <!-- Column Assignment Panel -->
    <div id="advTable-${fieldId}-assignment-panel" class="assignment-panel" 
         style="display: none; background: #f9fafb; border: 1px solid #d1d5db; border-radius: 8px; padding: 16px; margin-top: 16px;">
        <h4 style="margin: 0 0 12px 0;">Assign Column to Merged Cells</h4>
        <select id="advTable-${fieldId}-column-select" class="form-control" style="margin-bottom: 12px;">
            <option value="">Select a column...</option>
        </select>
        <div style="display: flex; gap: 8px;">
            <button type="button" class="btn btn-primary" 
                    onclick="AdvancedTableBuilder.confirmAssignment('${fieldId}')">
                Assign
            </button>
            <button type="button" class="btn btn-ghost" 
                    onclick="AdvancedTableBuilder.cancelAssignment('${fieldId}')">
                Cancel
            </button>
        </div>
    </div>
`;

        initializeGrid(fieldId);
        updateColumnSelector(fieldId);
    }

    function initializeGrid(fieldId) {
        const cols = state.gridColumns;
        const rows = state.rows + 1; // +1 for header row
        state.gridCells = [];

        for (let r = 0; r < rows; r++) {
            for (let c = 0; c < cols; c++) {
                state.gridCells.push({
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

        renderGrid(fieldId);
    }

    function renderGrid(fieldId) {
        const grid = document.getElementById(`advTable-${fieldId}-table-grid`);
        if (!grid) return;

        grid.innerHTML = '';
        grid.style.gridTemplateColumns = `repeat(${state.gridColumns}, 1fr)`;

        state.gridCells.forEach(cell => {
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

            cellDiv.onclick = (e) => selectCell(e, cell, fieldId);
            grid.appendChild(cellDiv);
        });
    }

    function selectCell(event, cell, fieldId) {
        const cellDiv = event.currentTarget;
        const isSelected = state.selectedCells.some(
            c => c.row === cell.row && c.col === cell.col
        );

        if (!event.shiftKey) {
            // Clear all selections
            document.querySelectorAll(`#advTable-${fieldId}-table-grid .table-grid-cell.selected`)
                .forEach(el => {
                    el.classList.remove('selected');
                    el.style.background = el.dataset.row === '0' ? '#f3f4f6' : 'white';
                });
            state.selectedCells = [];
        }

        if (isSelected) {
            // Deselect
            state.selectedCells = state.selectedCells.filter(
                c => !(c.row === cell.row && c.col === cell.col)
            );
            cellDiv.classList.remove('selected');
            cellDiv.style.background = cell.isHeader ? '#f3f4f6' : 'white';
        } else {
            // Select
            state.selectedCells.push({ row: cell.row, col: cell.col });
            cellDiv.classList.add('selected');
            cellDiv.style.background = '#dbeafe';
            cellDiv.style.borderColor = '#3b82f6';
        }
    }

    function mergeCells(fieldId) {
        const selected = state.selectedCells;

        if (selected.length < 2) {
            showNotification('Select at least 2 cells to merge', 'error');
            return;
        }

        // Check if any selected cells are already merged
        const alreadyMerged = selected.some(s => {
            const cell = state.gridCells.find(c => c.row === s.row && c.col === s.col);
            return cell && (cell.isMerged || cell.hidden);
        });

        if (alreadyMerged) {
            showNotification('Cannot merge: some cells are already merged', 'error');
            return;
        }

        // Calculate bounds
        const rows = selected.map(c => c.row);
        const cols = selected.map(c => c.col);
        const minRow = Math.min(...rows);
        const maxRow = Math.max(...rows);
        const minCol = Math.min(...cols);
        const maxCol = Math.max(...cols);

        const rowspan = maxRow - minRow + 1;
        const colspan = maxCol - minCol + 1;

        // Verify rectangular selection
        if (selected.length !== rowspan * colspan) {
            showNotification('Please select a rectangular region', 'error');
            return;
        }

        // Perform merge
        const rootCell = state.gridCells.find(c => c.row === minRow && c.col === minCol);
        if (!rootCell) return;

        rootCell.isMerged = true;
        rootCell.rowspan = rowspan;
        rootCell.colspan = colspan;

        // Hide merged cells
        state.gridCells.forEach(cell => {
            if (cell.row >= minRow && cell.row <= maxRow &&
                cell.col >= minCol && cell.col <= maxCol &&
                !(cell.row === minRow && cell.col === minCol)) {
                cell.hidden = true;
                cell.mergeRoot = rootCell;
            }
        });

        showNotification(`Merged ${selected.length} cells`, 'success');
        clearSelection(fieldId);
        renderGrid(fieldId);
    }

    function showColumnAssignment(fieldId) {
        if (state.selectedCells.length === 0) {
            showNotification('Please select cell(s) first', 'error');
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
        state.columns.forEach(col => {
            const option = document.createElement('option');
            option.value = col.id;
            option.textContent = col.name;
            select.appendChild(option);
        });
    }

    function confirmAssignment(fieldId) {
        const select = document.getElementById(`advTable-${fieldId}-column-select`);
        const columnId = select?.value;

        if (!columnId) {
            showNotification('Please select a column', 'error');
            return;
        }

        const column = state.columns.find(c => c.id === columnId);
        if (!column) return;

        // Find root cell of selection
        const selected = state.selectedCells;
        const rows = selected.map(c => c.row);
        const cols = selected.map(c => c.col);
        const minRow = Math.min(...rows);
        const minCol = Math.min(...cols);

        const rootCell = state.gridCells.find(
            c => c.row === minRow && c.col === minCol && !c.hidden
        );

        if (rootCell) {
            rootCell.columnId = column.id;
            rootCell.columnName = column.name;
            showNotification(`Assigned "${column.name}"`, 'success');
        }

        cancelAssignment(fieldId);
        clearSelection(fieldId);
        renderGrid(fieldId);
    }

    function cancelAssignment(fieldId) {
        const panel = document.getElementById(`advTable-${fieldId}-assignment-panel`);
        if (panel) panel.style.display = 'none';
    }

    function clearSelection(fieldId) {
        document.querySelectorAll(`#advTable-${fieldId}-table-grid .table-grid-cell.selected`)
            .forEach(el => {
                el.classList.remove('selected');
                if (!el.classList.contains('assigned')) {
                    el.style.background = el.dataset.row === '0' ? '#f3f4f6' : 'white';
                    el.style.borderColor = '#d1d5db';
                }
            });
        state.selectedCells = [];
    }

    function resetGrid(fieldId) {
        if (!confirm('Reset all merges and assignments? This cannot be undone.')) return;
        initializeGrid(fieldId);
        showNotification('Table structure reset', 'success');
    }

    function validateStep2() {
        const hasAssignments = state.gridCells.some(cell => cell.columnId);

        if (!hasAssignments) {
            return confirm(
                'You haven\'t assigned any columns to cells. Continue anyway?\n\n' +
                'The table will have a basic structure without merged cells.'
            );
        }

        return true;
    }

    // ========================================================================
    // STEP 3: PREVIEW
    // ========================================================================

    function renderStep3(fieldId) {
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
            <p style="margin: 4px 0 0 0; color: #1e40af;">
                This is how your table will appear in the form. Make sure everything looks correct.
            </p>
        </div>
    </div>

    <div id="advTable-${fieldId}-table-preview" class="table-preview-container">
        <!-- Preview rendered here -->
    </div>
`;

        generatePreview(fieldId);
    }

    function generatePreview(fieldId) {
        const container = document.getElementById(`advTable-${fieldId}-table-preview`);
        if (!container) return;

        const label = state.fieldLabel || 'Advanced Table';
        const cols = state.gridColumns;
        const rows = state.rows + 1;

        let html = `
    <div style="margin-bottom: 20px;">
        <label style="display: block; font-weight: 600; margin-bottom: 8px; font-size: 15px;">
            ${escapeHTML(label)}
        </label>
    </div>
    <div style="overflow-x: auto;">
        <table style="width: 100%; border-collapse: collapse; border: 2px solid #d1d5db;">
`;

        for (let r = 0; r < rows; r++) {
            html += '<tr>';

            for (let c = 0; c < cols; c++) {
                const cell = state.gridCells.find(
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
                    html += escapeHTML(state.columns[c]?.name || `Column ${c + 1}`);
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

    // ========================================================================
    // NAVIGATION
    // ========================================================================

    function nextStep(fieldId) {
        const currentStep = state.step;

        if (currentStep === 1) {
            if (!validateStep1(fieldId)) return;
            state.step = 2;
            renderStep2(fieldId);
        } else if (currentStep === 2) {
            if (!validateStep2(fieldId)) return;
            state.step = 3;
            renderStep3(fieldId);
        }

        updateStepUI(fieldId);
    }

    function previousStep(fieldId) {
        if (state.step > 1) {
            state.step--;
            updateStepUI(fieldId);
        }
    }

    function updateStepUI(fieldId) {
        const step = state.step;

        // Update progress indicators
        const progressSteps = document.querySelectorAll(`#advTable-progress-${fieldId} .progress-step`);
        progressSteps.forEach((el, index) => {
            el.classList.toggle('active', index + 1 === step);
            el.classList.toggle('completed', index + 1 < step);
        });

        // Update step indicator text
        const stepTexts = [
            'Step 1 of 3: Define Columns',
            'Step 2 of 3: Build Table Structure',
            'Step 3 of 3: Preview & Confirm'
        ];
        const indicator = document.getElementById(`advTable-step-indicator-${fieldId}`);
        if (indicator) indicator.textContent = stepTexts[step - 1];

        // Show/hide step content
        for (let i = 1; i <= 3; i++) {
            const stepEl = document.getElementById(`advTable-${fieldId}-step-${i}`);
            if (stepEl) {
                stepEl.style.display = i === step ? 'block' : 'none';
            }
        }

        // Update buttons
        const btnBack = document.getElementById(`advTable-${fieldId}-btn-back`);
        const btnNext = document.getElementById(`advTable-${fieldId}-btn-next`);
        const btnSave = document.getElementById(`advTable-${fieldId}-btn-save`);

        if (btnBack) btnBack.style.display = step > 1 ? 'flex' : 'none';
        if (btnNext) btnNext.style.display = step < 3 ? 'flex' : 'none';
        if (btnSave) btnSave.style.display = step === 3 ? 'flex' : 'none';
    }

    // ========================================================================
    // SAVE
    // ========================================================================

    function save(fieldId) {
        console.log('💾 Saving advanced table field:', fieldId);

        const fieldData = {
            fieldId: state.currentFieldId,
            fieldLabel: state.fieldLabel,
            fieldType: 'advancedTable',
            required: state.fieldRequired,
            helpText: state.fieldHelpText,
            columns: state.columns,
            rows: state.rows + 1,
            gridColumns: state.gridColumns,
            cells: state.gridCells.filter(c => !c.hidden)
        };

        console.log('✅ Advanced table field data:', fieldData);

        // Call the callback if provided
        if (state.onSaveCallback && typeof state.onSaveCallback === 'function') {
            state.onSaveCallback(fieldData, state.currentSectionId);
        }

        close(fieldId);
        showNotification('Advanced table added successfully!', 'success');
    }

    // ========================================================================
    // PUBLIC API
    // ========================================================================

    return {
        init,
        open,
        close,
        nextStep,
        previousStep,
        save,
        addColumn,
        removeColumn,
        mergeCells,
        showColumnAssignment,
        confirmAssignment,
        cancelAssignment,
        clearSelection,
        resetGrid,
        getState: () => ({ ...state }),
        setState: (newState) => Object.assign(state, newState)
    };

})();

// Make it globally accessible
window.AdvancedTableBuilder = AdvancedTableBuilder;