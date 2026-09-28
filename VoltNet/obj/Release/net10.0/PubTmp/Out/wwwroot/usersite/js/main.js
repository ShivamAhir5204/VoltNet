// ── VoltNet User-Site JavaScript ──────────────────────
document.addEventListener('DOMContentLoaded', () => {

  // ── Navbar scroll effect ──
  const navbar = document.querySelector('.vn-navbar');
  if (navbar) {
    window.addEventListener('scroll', () => {
      navbar.classList.toggle('scrolled', window.scrollY > 50);
    });
  }

  // ── Scroll-triggered animations ──
  const animElements = document.querySelectorAll('.animate-on-scroll, .vn-reveal, .vn-card, .vn-scale-reveal');
  if (animElements.length) {
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry, index) => {
        if (entry.isIntersecting) {
          // Stagger effect if there are multiple elements entering at once
          setTimeout(() => {
            entry.target.classList.add('visible');
          }, index * 80);
          observer.unobserve(entry.target);
        }
      });
    }, { threshold: 0.1 });

    animElements.forEach(el => observer.observe(el));
  }

  // ── Smooth scroll for anchor links ──
  document.querySelectorAll('a[href^="#"]').forEach(link => {
    link.addEventListener('click', function (e) {
      const target = document.querySelector(this.getAttribute('href'));
      if (target) {
        e.preventDefault();
        const offset = 80;
        const top = target.getBoundingClientRect().top + window.scrollY - offset;
        window.scrollTo({ top, behavior: 'smooth' });
        // Close mobile menu
        const navCollapse = document.querySelector('.navbar-collapse.show');
        if (navCollapse) {
          const bsCollapse = bootstrap.Collapse.getInstance(navCollapse);
          if (bsCollapse) bsCollapse.hide();
        }
      }
    });
  });

  // ── Counter animation for stats ──
  const counters = document.querySelectorAll('[data-count]');
  if (counters.length) {
    const counterObserver = new IntersectionObserver((entries) => {
      entries.forEach(entry => {
        if (entry.isIntersecting) {
          const el = entry.target;
          const target = parseInt(el.getAttribute('data-count'), 10);
          const suffix = el.getAttribute('data-suffix') || '';
          const duration = 2000;
          const start = 0;
          const startTime = performance.now();

          function updateCounter(currentTime) {
            const elapsed = currentTime - startTime;
            const progress = Math.min(elapsed / duration, 1);
            const eased = 1 - Math.pow(1 - progress, 3);
            const current = Math.floor(eased * (target - start) + start);
            el.textContent = current.toLocaleString() + suffix;
            if (progress < 1) requestAnimationFrame(updateCounter);
          }

          requestAnimationFrame(updateCounter);
          counterObserver.unobserve(el);
        }
      });
    }, { threshold: 0.5 });

    counters.forEach(el => counterObserver.observe(el));
  }

  // ── Scroll To Top Button ──
  const scrollTopBtn = document.querySelector('.vn-scroll-top');
  if (scrollTopBtn) {
    window.addEventListener('scroll', () => {
      if (window.scrollY > 400) {
        scrollTopBtn.classList.add('visible');
      } else {
        scrollTopBtn.classList.remove('visible');
      }
    });

    scrollTopBtn.addEventListener('click', (e) => {
      e.preventDefault();
      window.scrollTo({ top: 0, behavior: 'smooth' });
    });
  }

  // ── Hero Subtle Mouse Parallax (Desktop Only) ──
  const heroSection = document.querySelector('.vn-hero');
  const chargerImg = document.querySelector('.charger-img');
  
  if (heroSection && chargerImg && window.matchMedia("(hover: hover) and (pointer: fine)").matches) {
    heroSection.addEventListener('mousemove', (e) => {
      // Calculate mouse position relative to center of screen, scaled down for subtlety
      const x = (window.innerWidth / 2 - e.pageX) / 90;
      const y = (window.innerHeight / 2 - e.pageY) / 90;
      
      chargerImg.style.transform = `translate(${x}px, ${y}px)`;
    });

    heroSection.addEventListener('mouseleave', () => {
      chargerImg.style.transform = `translate(0px, 0px)`;
      chargerImg.style.transition = `transform 0.5s ease-out`;
    });
    
    heroSection.addEventListener('mouseenter', () => {
      chargerImg.style.transition = `none`;
    });
  }

});
