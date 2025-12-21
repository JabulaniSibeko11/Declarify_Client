// Password strength checker
const passwordInput = document.getElementById('passwordInput');
const strengthFill = document.getElementById('strengthFill');
const strengthText = document.getElementById('strengthText');

passwordInput.addEventListener('input', function () {
    const password = this.value;
    let strength = 0;

    if (password.length >= 8) strength++;
    if (/[a-z]/.test(password)) strength++;
    if (/[A-Z]/.test(password)) strength++;
    if (/[0-9]/.test(password)) strength++;
    if (/[@$!%*?&]/.test(password)) strength++;

    strengthFill.className = 'strength-fill';
    if (strength <= 2) {
        strengthFill.classList.add('strength-weak');
        strengthText.textContent = 'Weak password';
        strengthText.style.color = 'var(--color-error)';
    } else if (strength <= 4) {
        strengthFill.classList.add('strength-medium');
        strengthText.textContent = 'Medium strength';
        strengthText.style.color = 'var(--color-warning)';
    } else {
        strengthFill.classList.add('strength-strong');
        strengthText.textContent = 'Strong password';
        strengthText.style.color = 'var(--color-success)';
    }
});

// Step navigation
const passwordStep = document.getElementById('passwordStep');
const signatureStep = document.getElementById('signatureStep');
const step1 = document.getElementById('step1');
const step2 = document.getElementById('step2');
const nextToSignature = document.getElementById('nextToSignature');
const backToPassword = document.getElementById('backToPassword');

nextToSignature.addEventListener('click', function () {
    const password = document.getElementById('passwordInput').value;
    const confirmPassword = document.querySelector('input[name="ConfirmPassword"]').value;

    if (!password || password.length < 8) {
        alert('Please enter a valid password (minimum 8 characters)');
        return;
    }

    if (password !== confirmPassword) {
        alert('Passwords do not match');
        return;
    }

    passwordStep.style.display = 'none';
    signatureStep.style.display = 'block';
    step1.classList.remove('active');
    step2.classList.add('active');

    initCanvas();
});

backToPassword.addEventListener('click', function () {
    signatureStep.style.display = 'none';
    passwordStep.style.display = 'block';
    step2.classList.remove('active');
    step1.classList.add('active');
});

// Signature Canvas
let canvas, ctx, isDrawing = false, hasSignature = false;

function initCanvas() {
    canvas = document.getElementById('signatureCanvas');
    const wrapper = document.getElementById('canvasWrapper');

    canvas.width = wrapper.offsetWidth - 32;
    canvas.height = 180;

    ctx = canvas.getContext('2d');
    ctx.strokeStyle = '#081B38';
    ctx.lineWidth = 2.5;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';

    canvas.addEventListener('mousedown', startDrawing);
    canvas.addEventListener('mousemove', draw);
    canvas.addEventListener('mouseup', stopDrawing);
    canvas.addEventListener('mouseout', stopDrawing);
    canvas.addEventListener('touchstart', handleTouchStart);
    canvas.addEventListener('touchmove', handleTouchMove);
    canvas.addEventListener('touchend', stopDrawing);
}

function startDrawing(e) {
    isDrawing = true;
    const rect = canvas.getBoundingClientRect();
    ctx.beginPath();
    ctx.moveTo(e.clientX - rect.left, e.clientY - rect.top);
}

function draw(e) {
    if (!isDrawing) return;

    const rect = canvas.getBoundingClientRect();
    ctx.lineTo(e.clientX - rect.left, e.clientY - rect.top);
    ctx.stroke();

    if (!hasSignature) {
        hasSignature = true;
        document.getElementById('signaturePlaceholder').style.display = 'none';
        document.getElementById('canvasWrapper').classList.add('has-signature');
        document.getElementById('submitButton').disabled = false;
    }
}

