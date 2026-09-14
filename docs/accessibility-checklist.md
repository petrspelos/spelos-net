# Accessibility verification

The Blazor migration targets applicable WCAG 2.2 Level AA criteria. Automated Playwright and axe checks cover the Home page, Tools page, and not-found view.

Manual verification completed on 2026-09-14:

- Keyboard focus is visible and follows the page's link order.
- External links identify their new-tab behavior to assistive technology.
- Content remains usable at 200% text size and at a 375 × 667 mobile viewport without page-level horizontal overflow.
- The fixed dark palette retains readable contrast, including focused links and status messages.
