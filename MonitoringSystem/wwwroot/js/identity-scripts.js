document.addEventListener('DOMContentLoaded', function () {
    // Toggle password visibility
    const passwordFields = document.querySelectorAll('input[type="password"]');

    passwordFields.forEach(field => {
        const toggleContainer = document.createElement('div');
        toggleContainer.className = 'password-toggle';
        toggleContainer.style.position = 'absolute';
        toggleContainer.style.right = '10px';
        toggleContainer.style.top = '50%';
        toggleContainer.style.transform = 'translateY(-50%)';
        toggleContainer.style.cursor = 'pointer';
        toggleContainer.style.zIndex = '5';

        const eyeIcon = document.createElement('i');
        eyeIcon.className = 'bi bi-eye';
        eyeIcon.style.fontSize = '1.2rem';
        toggleContainer.appendChild(eyeIcon);

        field.parentElement.style.position = 'relative';
        field.parentElement.appendChild(toggleContainer);

        toggleContainer.addEventListener('click', function () {
            if (field.type === 'password') {
                field.type = 'text';
                eyeIcon.className = 'bi bi-eye-slash';
            } else {
                field.type = 'password';
                eyeIcon.className = 'bi bi-eye';
            }
        });
    });

    // Form submission handling
    const forms = document.querySelectorAll('form');

    forms.forEach(form => {
        form.addEventListener('submit', function (event) {
            const submitButton = form.querySelector('button[type="submit"]');

            if (submitButton && form.checkValidity()) {
                submitButton.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Processing...';
                submitButton.disabled = true;
                // Allow form to submit normally
            }
        });
    });
}); 