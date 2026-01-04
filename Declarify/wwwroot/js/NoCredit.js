function closeModal() {
    const overlay = document.getElementById('modalOverlay');
    overlay.style.animation = 'fadeOut 0.2s ease-out forwards';
    setTimeout(() => {
        // In a real application, this would close the modal
        // For demo purposes, we'll just hide it
        overlay.style.display = 'none';
    }, 200);
}

function contactAdmin() {
    // In a real application, this would trigger an email or contact form
    alert('Contact administrator functionality would be triggered here.\n\nThis could open an email client, internal messaging system, or help desk ticket.');
}

// Add fadeOut animation
const style = document.createElement('style');
style.textContent = `
            @keyframes fadeOut {
                from {
                    opacity: 1;
                }
                to {
                    opacity: 0;
                }
            }
        `;
document.head.appendChild(style);

// Close on escape key
document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') {
        closeModal();
    }
});

// Close on overlay click (but not on modal click)
document.getElementById('modalOverlay').addEventListener('click', (e) => {
    if (e.target.id === 'modalOverlay') {
        closeModal();
    }
});