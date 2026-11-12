// ========================================================
// AppointX - Client-side UI & Navigation Scripts
// ========================================================

document.addEventListener('DOMContentLoaded', function () {
    const sidebarToggleBtn = document.getElementById('sidebarToggleBtn');
    const sidebarCloseBtn = document.getElementById('sidebarCloseBtn');
    const sidebarBackdrop = document.getElementById('sidebarBackdrop');
    const body = document.body;

    // Restore desktop collapsed preference
    if (window.innerWidth >= 992) {
        const isCollapsed = localStorage.getItem('appointx-sidebar-collapsed') === 'true';
        if (isCollapsed) {
            body.classList.add('sidebar-collapsed');
        }
    }

    // Toggle sidebar handler
    if (sidebarToggleBtn) {
        sidebarToggleBtn.addEventListener('click', function () {
            if (window.innerWidth < 992) {
                // Mobile: toggle mobile open drawer
                body.classList.toggle('sidebar-mobile-open');
            } else {
                // Desktop: toggle compact/collapsed mode
                body.classList.toggle('sidebar-collapsed');
                localStorage.setItem('appointx-sidebar-collapsed', body.classList.contains('sidebar-collapsed'));
            }
        });
    }

    function closeMobileSidebar() {
        body.classList.remove('sidebar-mobile-open');
    }

    if (sidebarCloseBtn) {
        sidebarCloseBtn.addEventListener('click', closeMobileSidebar);
    }

    if (sidebarBackdrop) {
        sidebarBackdrop.addEventListener('click', closeMobileSidebar);
    }

    // Auto-close sidebar on mobile when navigating via a link
    const navLinks = document.querySelectorAll('.app-sidebar .sidebar-nav-link');
    navLinks.forEach(function (link) {
        link.addEventListener('click', function () {
            if (window.innerWidth < 992) {
                closeMobileSidebar();
            }
        });
    });

    // Resize handler: if resizing to desktop, clean mobile open class
    window.addEventListener('resize', function () {
        if (window.innerWidth >= 992) {
            body.classList.remove('sidebar-mobile-open');
        }
    });
});
