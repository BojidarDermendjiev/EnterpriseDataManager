// Enterprise Data Manager - Site JavaScript

(function () {
    'use strict';

    // Sidebar Toggle
    const sidebarToggle = document.getElementById('sidebarToggle');
    const sidebar = document.getElementById('sidebar');
    const mainContent = document.querySelector('.main-content');

    if (sidebarToggle && sidebar) {
        sidebarToggle.addEventListener('click', function () {
            sidebar.classList.toggle('open');

            // On desktop, also adjust the main content margin
            if (window.innerWidth > 1024) {
                if (sidebar.style.width === '70px') {
                    sidebar.style.width = '';
                    mainContent.style.marginLeft = '';
                } else {
                    sidebar.style.width = '70px';
                    mainContent.style.marginLeft = '70px';
                }
            }
        });
    }

    // Close sidebar on mobile when clicking outside
    document.addEventListener('click', function (e) {
        if (window.innerWidth <= 1024 && sidebar && sidebar.classList.contains('open')) {
            if (!sidebar.contains(e.target) && !sidebarToggle.contains(e.target)) {
                sidebar.classList.remove('open');
            }
        }
    });

    // Tab switching functionality
    const tabs = document.querySelectorAll('.tabs .tab');
    tabs.forEach(function (tab) {
        tab.addEventListener('click', function () {
            const parent = this.closest('.tabs');
            parent.querySelectorAll('.tab').forEach(function (t) {
                t.classList.remove('active');
            });
            this.classList.add('active');
        });
    });

    // Progress bar animation on page load
    const progressBars = document.querySelectorAll('.progress-fill');
    progressBars.forEach(function (bar) {
        const width = bar.style.width;
        bar.style.width = '0%';
        setTimeout(function () {
            bar.style.width = width;
        }, 100);
    });

    // Confirmation dialogs
    const dangerButtons = document.querySelectorAll('[data-confirm]');
    dangerButtons.forEach(function (button) {
        button.addEventListener('click', function (e) {
            const message = this.getAttribute('data-confirm') || 'Are you sure you want to proceed?';
            if (!confirm(message)) {
                e.preventDefault();
            }
        });
    });

    // Search functionality with debounce
    function debounce(func, wait) {
        let timeout;
        return function executedFunction(...args) {
            const later = function () {
                clearTimeout(timeout);
                func(...args);
            };
            clearTimeout(timeout);
            timeout = setTimeout(later, wait);
        };
    }

    const searchInputs = document.querySelectorAll('.header-search input, [type="search"]');
    searchInputs.forEach(function (input) {
        input.addEventListener('input', debounce(function (e) {
            const searchTerm = e.target.value.toLowerCase();
            const table = document.querySelector('.data-table tbody');
            if (table) {
                const rows = table.querySelectorAll('tr');
                rows.forEach(function (row) {
                    const text = row.textContent.toLowerCase();
                    row.style.display = text.includes(searchTerm) ? '' : 'none';
                });
            }
        }, 300));
    });

    // Tooltips initialization (if Bootstrap is loaded)
    if (typeof bootstrap !== 'undefined' && bootstrap.Tooltip) {
        const tooltipTriggerList = document.querySelectorAll('[title]');
        tooltipTriggerList.forEach(function (tooltipTriggerEl) {
            new bootstrap.Tooltip(tooltipTriggerEl, {
                trigger: 'hover',
                placement: 'top'
            });
        });
    }

    // Form validation visual feedback
    const forms = document.querySelectorAll('form');
    forms.forEach(function (form) {
        form.addEventListener('submit', function (e) {
            const requiredFields = form.querySelectorAll('[required]');
            let isValid = true;

            requiredFields.forEach(function (field) {
                if (!field.value.trim()) {
                    field.classList.add('is-invalid');
                    isValid = false;
                } else {
                    field.classList.remove('is-invalid');
                }
            });

            if (!isValid) {
                e.preventDefault();
                // Focus first invalid field
                form.querySelector('.is-invalid')?.focus();
            }
        });
    });

    // Real-time form validation
    const formControls = document.querySelectorAll('.form-control[required]');
    formControls.forEach(function (control) {
        control.addEventListener('blur', function () {
            if (!this.value.trim()) {
                this.classList.add('is-invalid');
            } else {
                this.classList.remove('is-invalid');
            }
        });

        control.addEventListener('input', function () {
            if (this.value.trim()) {
                this.classList.remove('is-invalid');
            }
        });
    });

    // Schedule type change handler (for create archive plan form)
    const scheduleTypeSelect = document.getElementById('scheduleType');
    if (scheduleTypeSelect) {
        scheduleTypeSelect.addEventListener('change', function () {
            const cronInput = document.querySelector('input[placeholder="0 2 * * *"]');
            const daySelect = document.querySelector('select option[value="0"]')?.closest('select');

            if (this.value === 'custom') {
                if (cronInput) cronInput.disabled = false;
            } else {
                if (cronInput) cronInput.disabled = true;
            }

            if (this.value === 'weekly') {
                if (daySelect) daySelect.disabled = false;
            } else {
                if (daySelect) daySelect.disabled = true;
            }
        });
    }

    // Auto-refresh for running jobs (every 30 seconds)
    const runningJobsSection = document.querySelector('.status-running');
    if (runningJobsSection) {
        setInterval(function () {
            // In production, this would make an AJAX call to update job progress
            console.log('Checking for job updates...');
        }, 30000);
    }

    // Notification badge update
    function updateNotificationBadge(count) {
        const badge = document.querySelector('.notification-badge');
        if (badge) {
            badge.style.display = count > 0 ? 'block' : 'none';
        }
    }

    // Stats card hover effect enhancement
    const statCards = document.querySelectorAll('.stat-card');
    statCards.forEach(function (card) {
        card.addEventListener('mouseenter', function () {
            this.style.cursor = 'pointer';
        });
    });

    // Copy to clipboard for job IDs and code elements
    const codeElements = document.querySelectorAll('code');
    codeElements.forEach(function (code) {
        code.style.cursor = 'pointer';
        code.title = 'Click to copy';
        code.addEventListener('click', function () {
            navigator.clipboard.writeText(this.textContent).then(function () {
                // Visual feedback
                const originalBg = code.style.backgroundColor;
                code.style.backgroundColor = 'rgba(16, 185, 129, 0.2)';
                setTimeout(function () {
                    code.style.backgroundColor = originalBg;
                }, 300);
            });
        });
    });

    // Keyboard shortcuts
    document.addEventListener('keydown', function (e) {
        // Ctrl/Cmd + K to focus search
        if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
            e.preventDefault();
            const searchInput = document.querySelector('.header-search input');
            if (searchInput) {
                searchInput.focus();
            }
        }

        // Escape to close modals
        if (e.key === 'Escape') {
            const openModal = document.querySelector('.modal-backdrop.show');
            if (openModal) {
                openModal.classList.remove('show');
            }
        }
    });

    // Dynamic date/time display
    function updateRelativeTimes() {
        const timeElements = document.querySelectorAll('[data-timestamp]');
        timeElements.forEach(function (el) {
            const timestamp = new Date(el.getAttribute('data-timestamp'));
            const now = new Date();
            const diff = now - timestamp;

            const seconds = Math.floor(diff / 1000);
            const minutes = Math.floor(seconds / 60);
            const hours = Math.floor(minutes / 60);
            const days = Math.floor(hours / 24);

            let relative;
            if (seconds < 60) {
                relative = 'Just now';
            } else if (minutes < 60) {
                relative = minutes + ' minute' + (minutes > 1 ? 's' : '') + ' ago';
            } else if (hours < 24) {
                relative = hours + ' hour' + (hours > 1 ? 's' : '') + ' ago';
            } else {
                relative = days + ' day' + (days > 1 ? 's' : '') + ' ago';
            }

            el.textContent = relative;
        });
    }

    // Update relative times every minute
    setInterval(updateRelativeTimes, 60000);

    // Initialize on DOM ready
    document.addEventListener('DOMContentLoaded', function () {
        updateRelativeTimes();
        updateNotificationBadge(3); // Example: 3 notifications
    });

})();
