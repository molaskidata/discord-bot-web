// Smooth scrolling for navigation
function scrollToSection(sectionId) {
    const section = document.getElementById(sectionId);
    if (section) {
        section.scrollIntoView({
            behavior: 'smooth',
            block: 'start'
        });
    }
}

// Discord OAuth Login
document.getElementById('loginBtn')?.addEventListener('click', function () {
    // TODO: Replace with actual Discord OAuth URL
    const clientId = 'YOUR_DISCORD_CLIENT_ID';
    const redirectUri = encodeURIComponent(window.location.origin + '/auth/callback');
    const scope = 'identify guilds';

    const discordAuthUrl = `https://discord.com/api/oauth2/authorize?client_id=${clientId}&redirect_uri=${redirectUri}&response_type=code&scope=${scope}`;

    // For now, show alert - replace with actual OAuth flow
    alert('Discord OAuth wird bald implementiert! 🚀');
    // window.location.href = discordAuthUrl;
});

// Plan selection with Stripe
function selectPlan(planType) {
    console.log(`Selected plan: ${planType}`);

    // Check if user is logged in
    const isLoggedIn = checkUserLogin();

    if (!isLoggedIn) {
        alert('Bitte melde dich zuerst mit Discord an! 🔐');
        document.getElementById('loginBtn')?.click();
        return;
    }

    // TODO: Implement Stripe Checkout
    // For now, show confirmation
    const planDetails = {
        monthly: { price: '€5.99', period: 'monatlich' },
        yearly: { price: '€60', period: 'jährlich' }
    };

    const plan = planDetails[planType];
    if (plan) {
        alert(`Premium ${plan.period} ausgewählt (${plan.price})\n\nStripe Payment wird bald implementiert! 💳`);
        // Redirect to Stripe Checkout
        // redirectToStripeCheckout(planType);
    }
}

// Check if user is logged in (check localStorage or cookie)
function checkUserLogin() {
    // TODO: Implement actual login check
    return localStorage.getItem('discord_user') !== null;
}

// Redirect to Stripe Checkout
function redirectToStripeCheckout(planType) {
    // TODO: Call backend API to create Stripe Checkout session
    fetch('/api/create-checkout-session', {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({
            planType: planType,
            userId: getCurrentUserId()
        })
    })
        .then(response => response.json())
        .then(data => {
            if (data.url) {
                window.location.href = data.url;
            }
        })
        .catch(error => {
            console.error('Error:', error);
            alert('Ein Fehler ist aufgetreten. Bitte versuche es später erneut.');
        });
}

// Get current user ID from localStorage
function getCurrentUserId() {
    const user = localStorage.getItem('discord_user');
    return user ? JSON.parse(user).id : null;
}

// Handle OAuth callback
function handleOAuthCallback() {
    const urlParams = new URLSearchParams(window.location.search);
    const code = urlParams.get('code');

    if (code) {
        // Exchange code for access token
        fetch('/api/auth/discord', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
            },
            body: JSON.stringify({ code })
        })
            .then(response => response.json())
            .then(data => {
                if (data.user) {
                    localStorage.setItem('discord_user', JSON.stringify(data.user));
                    // Redirect to dashboard or home
                    window.location.href = '/';
                }
            })
            .catch(error => {
                console.error('OAuth error:', error);
            });
    }
}

// Navbar scroll effect
let lastScroll = 0;
window.addEventListener('scroll', () => {
    const navbar = document.querySelector('.navbar');
    const currentScroll = window.pageYOffset;

    if (currentScroll > 100) {
        navbar.style.boxShadow = '0 4px 20px rgba(0, 0, 0, 0.3)';
    } else {
        navbar.style.boxShadow = 'none';
    }

    lastScroll = currentScroll;
});

// Intersection Observer for animations
const observerOptions = {
    threshold: 0.1,
    rootMargin: '0px 0px -50px 0px'
};

const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
        if (entry.isIntersecting) {
            entry.target.style.opacity = '1';
            entry.target.style.transform = 'translateY(0)';
        }
    });
}, observerOptions);

// Observe all feature cards and pricing cards
document.addEventListener('DOMContentLoaded', () => {
    const cards = document.querySelectorAll('.feature-card, .pricing-card');
    cards.forEach(card => {
        card.style.opacity = '0';
        card.style.transform = 'translateY(30px)';
        card.style.transition = 'all 0.6s ease-out';
        observer.observe(card);
    });

    // Check if we're on the callback page
    if (window.location.pathname === '/auth/callback') {
        handleOAuthCallback();
    }
});

// Add ripple effect to buttons
document.querySelectorAll('button, .btn-primary, .btn-secondary').forEach(button => {
    button.addEventListener('click', function (e) {
        const ripple = document.createElement('span');
        const rect = this.getBoundingClientRect();
        const size = Math.max(rect.width, rect.height);
        const x = e.clientX - rect.left - size / 2;
        const y = e.clientY - rect.top - size / 2;

        ripple.style.width = ripple.style.height = size + 'px';
        ripple.style.left = x + 'px';
        ripple.style.top = y + 'px';
        ripple.classList.add('ripple');

        this.appendChild(ripple);

        setTimeout(() => ripple.remove(), 600);
    });
});

// Add ripple effect styles
const style = document.createElement('style');
style.textContent = `
    button, .btn-primary, .btn-secondary {
        position: relative;
        overflow: hidden;
    }
    
    .ripple {
        position: absolute;
        border-radius: 50%;
        background: rgba(255, 255, 255, 0.3);
        transform: scale(0);
        animation: ripple-animation 0.6s ease-out;
        pointer-events: none;
    }
    
    @keyframes ripple-animation {
        to {
            transform: scale(4);
            opacity: 0;
        }
    }
`;
document.head.appendChild(style);
