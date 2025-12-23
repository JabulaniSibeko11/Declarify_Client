// ============================================================================
// DECLARIFY - INITIAL ACCOUNT SETUP JAVASCRIPT
// Handles password validation, strength checking, and signature capture
// ============================================================================

// ============================================================================
// PASSWORD STRENGTH CHECKER
// ============================================================================
const passwordInput = document.getElementById('passwordInput');
const strengthFill = document.getElementById('strengthFill');
const strengthText = document.getElementById('strengthText');

if (passwordInput && strengthFill && strengthText) {
    passwordInput.addEventListener('input', function () {
        const password = this.value;
        let strength = 0;
        let width = 0;

        // Check password criteria
        if (password.length >= 8) strength++;
        if (/[a-z]/.test(password)) strength++;
        if (/[A-Z]/.test(password)) strength++;
        if (/[0-9]/.test(password)) strength++;
        if (/[@$!%*?&]/.test(password)) strength++;

        // Calculate width percentage
        width = (strength / 5) * 100;
        strengthFill.style.width = width + '%';

        // Reset classes
        strengthFill.className = 'strength-fill';

        // Apply strength-based styling
        if (strength === 0) {
            strengthText.textContent = '';
            strengthFill.style.width = '0%';
        } else if (strength <= 2) {
            strengthFill.classList.add('strength-weak');
            strengthText.textContent = 'Weak password';
            strengthText.style.color = '#DC2626';
        } else if (strength <= 4) {
            strengthFill.classList.add('strength-medium');
            strengthText.textContent = 'Medium strength';
            strengthText.style.color = '#F59E0B';
        } else {
            strengthFill.classList.add('strength-strong');
            strengthText.textContent = 'Strong password';
            strengthText.style.color = '#10B981';
        }
    });
}

// ============================================================================
// STEP NAVIGATION
// ============================================================================
const passwordStep = document.getElementById('passwordStep');
const signatureStep = document.getElementById('signatureStep');
const step1 = document.getElementById('step1');
const step2 = document.getElementById('step2');
const nextToSignature = document.getElementById('nextToSignature');

// Navigate to signature step
if (nextToSignature) {
    nextToSignature.addEventListener('click', function () {
        const password = document.getElementById('passwordInput').value;
        const confirmPassword = document.querySelector('input[name="ConfirmPassword"]').value;

        // Validate password
        if (!password || password.length < 8) {
            showError('Please enter a valid password (minimum 8 characters)');
            return;
        }

        // Check password strength requirements
        if (!/[a-z]/.test(password)) {
            showError('Password must contain at least one lowercase letter');
            return;
        }

        if (!/[A-Z]/.test(password)) {
            showError('Password must contain at least one uppercase letter');
            return;
        }

        if (!/[0-9]/.test(password)) {
            showError('Password must contain at least one number');
            return;
        }

        if (!/[@$!%*?&]/.test(password)) {
            showError('Password must contain at least one special character (@$!%*?&)');
            return;
        }

        // Validate confirmation
        if (password !== confirmPassword) {
            showError('Passwords do not match');
            return;
        }

        // Move to signature step
        passwordStep.style.display = 'none';
        signatureStep.style.display = 'block';
        step1.classList.remove('active');
        step2.classList.add('active');

        // Initialize canvas after display
        setTimeout(initCanvas, 50);
    });
}

// ============================================================================
// SIGNATURE CANVAS
// ============================================================================
let canvas, ctx, isDrawing = false, hasSignature = false;
let lastX = 0, lastY = 0;

function initCanvas() {
    canvas = document.getElementById('signatureCanvas');
    const wrapper = document.getElementById('canvasWrapper');

    if (!canvas || !wrapper) {
        console.error('Canvas or wrapper not found');
        return;
    }

    // Set canvas size
    canvas.width = wrapper.offsetWidth - 32;
    canvas.height = 180;

    // Get 2D context
    ctx = canvas.getContext('2d');
    ctx.strokeStyle = '#081B38'; // Declarify primary navy
    ctx.lineWidth = 2.5;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    // Mouse events
    canvas.addEventListener('mousedown', startDrawing);
    canvas.addEventListener('mousemove', draw);
    canvas.addEventListener('mouseup', stopDrawing);
    canvas.addEventListener('mouseout', stopDrawing);

    // Touch events for mobile
    canvas.addEventListener('touchstart', handleTouchStart, { passive: false });
    canvas.addEventListener('touchmove', handleTouchMove, { passive: false });
    canvas.addEventListener('touchend', stopDrawing);
}

function startDrawing(e) {
    isDrawing = true;
    const rect = canvas.getBoundingClientRect();
    lastX = e.clientX - rect.left;
    lastY = e.clientY - rect.top;
    ctx.beginPath();
    ctx.moveTo(lastX, lastY);
}

function draw(e) {
    if (!isDrawing) return;

    const rect = canvas.getBoundingClientRect();
    const x = e.clientX - rect.left;
    const y = e.clientY - rect.top;

    ctx.lineTo(x, y);
    ctx.stroke();

    lastX = x;
    lastY = y;

    // Mark that signature has been drawn
    if (!hasSignature) {
        hasSignature = true;
        const placeholder = document.getElementById('signaturePlaceholder');
        if (placeholder) {
            placeholder.style.display = 'none';
        }
        document.getElementById('canvasWrapper').classList.add('has-signature');
    }
}

function stopDrawing() {
    if (isDrawing) {
        isDrawing = false;
        ctx.closePath();
    }
}

// Touch event handlers
function handleTouchStart(e) {
    e.preventDefault();
    const touch = e.touches[0];
    const rect = canvas.getBoundingClientRect();
    isDrawing = true;
    lastX = touch.clientX - rect.left;
    lastY = touch.clientY - rect.top;
    ctx.beginPath();
    ctx.moveTo(lastX, lastY);
}

function handleTouchMove(e) {
    if (!isDrawing) return;
    e.preventDefault();

    const touch = e.touches[0];
    const rect = canvas.getBoundingClientRect();
    const x = touch.clientX - rect.left;
    const y = touch.clientY - rect.top;

    ctx.lineTo(x, y);
    ctx.stroke();

    lastX = x;
    lastY = y;

    // Mark that signature has been drawn
    if (!hasSignature) {
        hasSignature = true;
        const placeholder = document.getElementById('signaturePlaceholder');
        if (placeholder) {
            placeholder.style.display = 'none';
        }
        document.getElementById('canvasWrapper').classList.add('has-signature');
    }
}

// Clear signature
const clearSignature = document.getElementById('clearSignature');
if (clearSignature) {
    clearSignature.addEventListener('click', function () {
        if (!canvas || !ctx) return;

        ctx.clearRect(0, 0, canvas.width, canvas.height);
        hasSignature = false;

        const placeholder = document.getElementById('signaturePlaceholder');
        if (placeholder) {
            placeholder.style.display = 'flex';
        }

        document.getElementById('canvasWrapper').classList.remove('has-signature');
        document.getElementById('submitButton').disabled = true;

        // Clear hidden input
        const signatureDataInput = document.getElementById('signatureDataInput');
        if (signatureDataInput) {
            signatureDataInput.value = '';
        }
    });
}

// Save signature
const saveSignatureBtn = document.getElementById('saveSignatureBtn');
if (saveSignatureBtn) {
    saveSignatureBtn.addEventListener('click', function () {
        if (!hasSignature) {
            showError('Please draw your signature first');
            return;
        }

        // Convert canvas to base64 PNG
        const signatureData = canvas.toDataURL('image/png');

        // Store in hidden input
        const signatureDataInput = document.getElementById('signatureDataInput');
        if (signatureDataInput) {
            signatureDataInput.value = signatureData;
            document.getElementById('submitButton').disabled = false;
            showSuccess('Signature saved successfully!');
        } else {
            console.error('signatureDataInput not found');
            showError('Error saving signature. Please try again.');
        }
    });
}

// ============================================================================
// FORM SUBMISSION
// ============================================================================
const setupForm = document.getElementById('setupForm');
if (setupForm) {
    setupForm.addEventListener('submit', function (e) {
        // Ensure signature is saved
        const signatureDataInput = document.getElementById('signatureDataInput');

        if (!signatureDataInput || !signatureDataInput.value) {
            e.preventDefault();
            showError('Please save your signature before submitting');
            return false;
        }

        if (!hasSignature) {
            e.preventDefault();
            showError('Please draw and save your signature before submitting');
            return false;
        }

        // Show loading state
        const submitButton = document.getElementById('submitButton');
        if (submitButton) {
            submitButton.disabled = true;
            submitButton.textContent = 'Setting up your account...';
        }

        return true;
    });
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================
function showError(message) {
    // Create temporary error notification
    const notification = document.createElement('div');
    notification.className = 'notification error-notification';
    notification.innerHTML = `
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
        </svg>
        <span>${message}</span>
    `;

    document.body.appendChild(notification);

    // Animate in
    setTimeout(() => notification.classList.add('show'), 10);

    // Remove after 4 seconds
    setTimeout(() => {
        notification.classList.remove('show');
        setTimeout(() => notification.remove(), 300);
    }, 4000);
}

function showSuccess(message) {
    // Create temporary success notification
    const notification = document.createElement('div');
    notification.className = 'notification success-notification';
    notification.innerHTML = `
        <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M22 11.08V12a10 10 0 1 1-5.93-9.14"></path>
            <polyline points="22 4 12 14.01 9 11.01"></polyline>
        </svg>
        <span>${message}</span>
    `;

    document.body.appendChild(notification);

    // Animate in
    setTimeout(() => notification.classList.add('show'), 10);

    // Remove after 3 seconds
    setTimeout(() => {
        notification.classList.remove('show');
        setTimeout(() => notification.remove(), 300);
    }, 3000);
}

// ============================================================================
// INITIALIZE ON LOAD
// ============================================================================
document.addEventListener('DOMContentLoaded', function () {
    console.log('Declarify Initial Setup - Loaded');

    // Add CSS for notifications if not already present
    if (!document.getElementById('notification-styles')) {
        const style = document.createElement('style');
        style.id = 'notification-styles';
        style.textContent = `
            .notification {
                position: fixed;
                top: 20px;
                right: 20px;
                background: white;
                padding: 16px 20px;
                border-radius: 12px;
                box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
                display: flex;
                align-items: center;
                gap: 12px;
                z-index: 10000;
                transform: translateX(400px);
                transition: transform 0.3s ease;
                max-width: 400px;
                font-family: 'Inter', sans-serif;
                font-size: 0.875rem;
            }

            .notification.show {
                transform: translateX(0);
            }

            .error-notification {
                border-left: 4px solid #DC2626;
                color: #1E293B;
            }

            .error-notification svg {
                color: #DC2626;
                flex-shrink: 0;
            }

            .success-notification {
                border-left: 4px solid #10B981;
                color: #1E293B;
            }

            .success-notification svg {
                color: #10B981;
                flex-shrink: 0;
            }

            @media (max-width: 640px) {
                .notification {
                    left: 20px;
                    right: 20px;
                    max-width: none;
                }
            }
        `;
        document.head.appendChild(style);
    }
});