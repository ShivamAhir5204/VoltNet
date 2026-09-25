# VoltNet — UI/UX Design Specification

> **Project:** VoltNet  
> **Product:** EV charging discovery, station booking, and station-management platform for India  
> **Design Direction:** Minimalism + iOS-inspired UI + Spotify-like confidence + Notion-like clarity + subtle Glass/Liquid Glass  
> **Frontend:** HTML5 + CSS3 + Bootstrap 5.3.x + Vanilla JavaScript  
> **Primary Goal:** Premium, fast, responsive, robust, accessible, and production-ready UI without breaking existing application functionality.

---

## 1. Design Vision

VoltNet should feel like a modern mobility product rather than a generic EV website.

The interface must communicate:

- Trust
- Simplicity
- Speed
- Clean technology
- Modern EV mobility
- Easy discovery
- Effortless booking
- Professional station management

### Visual references

Use the **design principles**, not copied layouts, from:

- **Apple / iOS:** spacing, hierarchy, refined surfaces, restrained controls
- **Spotify:** strong visual identity, confident CTAs, clear content hierarchy
- **Notion:** minimalism, whitespace, typography, simple interaction patterns
- **Glass / Liquid Glass:** subtle translucent surfaces, blur, depth, and layered backgrounds

**Important:** Do not copy proprietary layouts, illustrations, icons, or visual assets from these products.

---

# 2. Brand Identity

## 2.1 Logo

Use the approved VoltNet logo and approved standalone **V icon** exactly as supplied.

### Rules

- Do not redraw the logo.
- Do not alter the approved V shape.
- Do not distort or stretch the logo.
- Do not add unnecessary effects.
- Do not place the logo on visually noisy backgrounds.
- Use the standalone V icon for favicon, compact navigation, mobile branding, app-like UI, and profile/brand surfaces where appropriate.

---

# 3. Color System

VoltNet's primary visual identity is **Charcoal + Volt Green + White**.

## 3.1 Core Colors

| Token | Hex | Usage |
|---|---|---|
| Brand Dark | `#111A1A` | Main text, dark surfaces, primary branding |
| Brand Green | `#2E9D54` | Primary brand color, CTA, active states |
| Dark Green | `#218442` | Hover/pressed states |
| Light Green | `#4FB96B` | Highlights, secondary accents |
| Pale Green | `#E8F7EC` | Soft backgrounds, selected states |
| White | `#FFFFFF` | Main surface |
| Light Grey | `#F6F8F7` | Page background |
| Medium Grey | `#D9E1DD` | Borders/dividers |
| Text Grey | `#6B7C75` | Secondary text |
| Success | `#22C55E` | Available/success states |
| Warning | `#F59E0B` | Warning states |
| Error | `#EF4444` | Errors/unavailable/danger |

## 3.2 CSS Variables

```css
:root {
    --vn-dark: #111A1A;
    --vn-green: #2E9D54;
    --vn-green-dark: #218442;
    --vn-green-light: #4FB96B;
    --vn-green-pale: #E8F7EC;

    --vn-white: #FFFFFF;
    --vn-bg: #F6F8F7;
    --vn-border: #D9E1DD;
    --vn-text-muted: #6B7C75;

    --vn-success: #22C55E;
    --vn-warning: #F59E0B;
    --vn-error: #EF4444;

    --vn-radius-sm: 10px;
    --vn-radius-md: 16px;
    --vn-radius-lg: 24px;
    --vn-radius-xl: 32px;

    --vn-shadow-sm: 0 4px 16px rgba(17, 26, 26, 0.06);
    --vn-shadow-md: 0 12px 32px rgba(17, 26, 26, 0.09);
    --vn-shadow-lg: 0 20px 60px rgba(17, 26, 26, 0.12);
}
```

### Color restrictions

Do **not** use:

- Purple as a primary brand color
- Navy blue as a primary brand color
- Cyan as a primary brand color
- Neon rainbow gradients
- AI-style multicolor gradients

Gradients may only be used extremely subtly when they improve depth without changing the brand identity.

---

# 4. Typography

Use a clean system-first font stack.

```css
font-family:
    Inter,
    -apple-system,
    BlinkMacSystemFont,
    "SF Pro Display",
    "SF Pro Text",
    "Segoe UI",
    sans-serif;
```

## Typography hierarchy

### Hero heading

- Large
- Bold/semi-bold
- Tight line height
- Maximum 2–3 lines
- Strong contrast

### Section heading

- Bold
- Clear hierarchy
- Generous spacing

### Body

- Comfortable line height
- Medium contrast
- Avoid excessively small text

### UI text

- Compact
- Clear
- Highly readable

Avoid:

- Excessive uppercase
- Decorative fonts
- Heavy text shadows
- Long paragraphs inside cards

---

# 5. Layout System

Use Bootstrap 5.3.x's responsive grid and utilities as the structural foundation. Bootstrap 5.3 provides responsive layout utilities, CSS variables, components, forms, navbar/offcanvas patterns, and mobile-first behavior that fit the project requirements. citeturn1view0

Recommended container:

```html
<div class="container">
```

Use:

```html
<div class="container-fluid">
```

only for intentionally full-width sections.

## Spacing philosophy

Use generous whitespace.

Preferred spacing scale:

- 4px
- 8px
- 12px
- 16px
- 24px
- 32px
- 48px
- 64px
- 80px
- 96px

Avoid cramped layouts.

---

# 6. Responsive Design

The entire application must work from approximately:

- 320px mobile
- 375px mobile
- 414px mobile
- 576px
- 768px tablet
- 992px laptop
- 1200px desktop
- 1440px large desktop
- 1920px wide desktop

## Mobile-first requirements

Every page must be designed mobile-first.

### Mobile

- Compact navbar
- Offcanvas navigation
- Full-width CTAs
- Stacked cards
- Bottom-friendly controls
- Touch targets of approximately 44px or larger
- No horizontal scrolling
- Tables become cards or horizontally scrollable containers where necessary

### Tablet

- Two-column layouts where appropriate
- Larger cards
- Expanded navigation when space permits

### Desktop

- Multi-column layouts
- Dashboard sidebars
- Large map/detail layouts
- Maximum-width content containers

---

# 7. Global UI Style

## 7.1 Cards

Cards should feel light and premium.

Preferred:

```css
background: #FFFFFF;
border: 1px solid var(--vn-border);
border-radius: 20px;
box-shadow: var(--vn-shadow-sm);
```

Avoid:

- Thick borders
- Huge shadows
- Excessive gradients
- Over-rounded every element
- Excessive glass effects

---

# 8. Glass / Liquid Glass

Glassmorphism is a **supporting visual treatment**, not the entire design language.

Use it selectively for:

- Floating search panels
- Navbar when overlaying hero sections
- Map controls
- Floating action panels
- Booking summaries
- Selected station overlays
- Modal-like surfaces

Example:

```css
.vn-glass {
    background: rgba(255, 255, 255, 0.72);
    backdrop-filter: blur(18px);
    -webkit-backdrop-filter: blur(18px);
    border: 1px solid rgba(255, 255, 255, 0.65);
}
```

Always provide a solid fallback for browsers without backdrop-filter support.

Do not turn every card into glass.

---

# 9. Navigation

## Desktop

Navbar should contain:

- VoltNet logo
- Home
- About
- Services
- Stations
- Map
- Pricing
- Contact
- Become Partner
- Profile / authentication control

### Navbar style

- White or translucent white
- Thin bottom border
- Minimal shadow
- Sticky where useful
- Smooth scroll behavior
- Active navigation state using Volt Green

## Mobile

Use Bootstrap Offcanvas.

Structure:

```text
Logo
Menu button
      ↓
Offcanvas
 ├── Home
 ├── About
 ├── Services
 ├── Stations
 ├── Map
 ├── Pricing
 ├── Contact
 ├── Become Partner
 └── Account
```

Bootstrap's responsive Navbar and Offcanvas components are suitable for this pattern. citeturn1view0

---

# 10. Homepage

## Hero

Hero should immediately communicate the product.

Suggested direction:

### Headline

**Find. Book. Charge.**

Supporting message:

> Discover nearby EV charging stations, reserve a slot, and get back on the road with VoltNet.

Primary CTA:

**Find Charging Stations**

Secondary CTA:

**Become a Partner**

### Hero composition

Use:

- Strong typography
- Minimal EV/charging visual
- Search/discovery component
- Subtle green visual accent
- Large whitespace
- No visual clutter

---

# 11. Station Search

Station discovery is one of the most important interactions.

Create a premium search component containing:

- Location
- Search radius
- Charger type
- Connector
- Availability
- Date
- Time
- Search button

Example:

```text
[ Location              ]
[ Charger Type ] [ Date ]
[ Time         ] [ Search ]
```

On mobile, fields may stack.

Use clear focus states and keyboard navigation.

---

# 12. Station Cards

Each station card should communicate important information quickly.

### Required information

- Station name
- Distance
- Location
- Availability
- Charger type
- Charging speed
- Price
- Rating/reviews if available
- Open/closed state
- Book button
- View details button

Example hierarchy:

```text
Station Name
Location · 2.4 km

● Available
CCS2 · 60 kW

₹ X / kWh

[ View Details ] [ Book Now ]
```

Availability should use both color and text, not color alone.

---

# 13. Map Experience

Map UI should be clean and functional.

### Desktop

Recommended layout:

```text
------------------------------------------------
| Search                                        |
------------------------------------------------
| Station List        |                         |
|                     |          MAP            |
|                     |                         |
|                     |                         |
------------------------------------------------
```

### Mobile

Use:

```text
Search
Filters
Map
Bottom station sheet
```

Map controls should be compact floating controls.

Do not overload the map with unnecessary UI.

---

# 14. Station Detail Page

Include:

- Station name
- Hero image if available
- Address
- Distance
- Opening hours
- Available chargers
- Connector types
- Charging speeds
- Pricing
- Amenities
- Station status
- Reviews if supported
- Location/map
- Booking CTA

Use a sticky booking CTA on mobile when appropriate.

---

# 15. Booking Flow

Booking should feel like a simple guided process.

### Step 1

Select station.

### Step 2

Select charger.

### Step 3

Select date.

### Step 4

Select time slot.

### Step 5

Review booking.

### Step 6

Payment.

### Step 7

Confirmation.

Use a lightweight progress indicator.

Example:

```text
Station → Charger → Time → Review → Payment → Done
```

Do not overload users with unnecessary fields.

---

# 16. Booking Confirmation

Confirmation page should clearly show:

- Booking ID
- Station
- Date
- Time
- Charger
- Amount
- Payment status
- QR/reference code if supported
- Directions
- Cancellation option if allowed

Primary CTA:

**View Booking**

Secondary CTA:

**Get Directions**

---

# 17. Customer Dashboard

Dashboard should provide:

### Overview

- Upcoming booking
- Active booking
- Recent bookings
- Saved stations
- Spending summary

### Sections

- Dashboard
- Find Stations
- My Bookings
- Favorites
- Payments
- Profile
- Notifications
- Support

Use cards and simple data visualization.

---

# 18. Admin / Super Admin Dashboard

Dashboards should prioritize information density without becoming visually heavy.

## Metrics

Examples:

- Total customers
- Total stations
- Active stations
- Pending partner requests
- Total bookings
- Revenue
- Active chargers
- Failed/cancelled bookings

## Visual hierarchy

```text
Page Header
↓
KPI Cards
↓
Charts / Trends
↓
Recent Activity
↓
Tables
```

Use charts only where they communicate useful information.

---

# 19. Station Owner Dashboard

Provide:

- Station overview
- Station status
- Charger management
- Booking management
- Revenue
- Availability
- Pricing
- Analytics
- Staff management
- Profile/settings

The dashboard should make operational tasks obvious.

---

# 20. Station Manager Dashboard

Focus on daily operations:

- Today's bookings
- Active sessions
- Charger availability
- Upcoming bookings
- Customer details
- Booking status
- Maintenance/status reporting

Prioritize quick actions over analytics.

---

# 21. Partner / Become Partner Page

Explain the process clearly.

Suggested flow:

```text
Become a Partner
       ↓
Submit application
       ↓
Admin verification
       ↓
Station approval
       ↓
Station setup
       ↓
Start accepting bookings
```

Form should be divided into logical sections.

Avoid giant forms presented as one uninterrupted block.

---

# 22. Authentication

Pages:

- Login
- Register
- Forgot Password
- Reset Password
- Verification
- Account/Profile

Visual direction:

- Centered card
- Minimal background
- VoltNet icon
- Clear form hierarchy
- Strong focus states
- Password visibility toggle
- Validation feedback

---

# 23. Buttons

## Primary

```css
background: var(--vn-green);
color: #fff;
```

Hover:

```css
background: var(--vn-green-dark);
```

## Secondary

White background with subtle border.

## Ghost

Transparent with clear hover state.

### Button rules

- Avoid too many button styles.
- Use one clear primary CTA per section.
- Maintain consistent height.
- Use icons only when they improve understanding.
- Never use decorative icons without purpose.

---

# 24. Forms

Forms must be:

- Clean
- Spacious
- Accessible
- Easy to scan
- Properly validated

Use:

- Clear labels
- Helpful placeholders
- Inline validation
- Error messages
- Success states
- Required indicators

Do not rely on placeholder text as the only label.

---

# 25. Tables

Desktop:

- Clean header
- Light borders
- Hover state
- Status badges
- Pagination
- Search/filter

Mobile:

- Convert to cards when practical
- Otherwise use controlled horizontal scrolling

Avoid extremely dense tables.

---

# 26. Status System

Use consistent status badges.

### Available

`#22C55E`

### Pending

`#F59E0B`

### Unavailable / Error

`#EF4444`

### Neutral

Use muted grey.

Always include text labels.

Example:

```text
● Available
● Pending
● Offline
```

---

# 27. Modals / Toasts / Alerts

Use lightweight UI feedback.

### Toast examples

- Booking confirmed
- Profile updated
- Station added
- Payment successful
- Request submitted

### Error

Use clear language:

> Something went wrong. Please try again.

Avoid technical error messages in the primary UI.

---

# 28. Loading States

Never leave the interface visually frozen.

Use:

- Skeleton loaders
- Button loading states
- Inline spinners
- Map loading placeholders
- Table skeletons

Avoid full-screen loading overlays unless absolutely necessary.

---

# 29. Empty States

Every major data page needs an intentional empty state.

Example:

```text
No bookings yet

Find a nearby charging station and make your first booking.

[ Find Stations ]
```

Empty states should guide users toward the next action.

---

# 30. Microinteractions

Use subtle animations.

Recommended:

- 150–250ms hover transitions
- Button press feedback
- Card elevation changes
- Modal fade/slide
- Smooth dropdowns
- Skeleton shimmer
- Success confirmation animation

Avoid:

- Excessive bouncing
- Large parallax effects
- Constant motion
- Heavy animated backgrounds
- Long transitions

Animations must respect:

```css
@media (prefers-reduced-motion: reduce)
```

---

# 31. Icons

Preferred icon system:

- Bootstrap Icons or another existing lightweight icon system
- Consistent stroke/visual weight
- No random icon mixing

Icons should communicate actions, not decorate every text element.

---

# 32. Images

Images should support the product.

Use:

- EV charging stations
- EV vehicles
- Clean infrastructure
- India-relevant charging environments

Avoid:

- Generic futuristic AI imagery
- Excessive neon
- Unrealistic charging scenes
- Overly saturated visuals

Use responsive images and lazy loading where appropriate.

---

# 33. Performance Requirements

Performance is a first-class design requirement.

## Rules

- Keep JavaScript lightweight.
- Avoid unnecessary libraries.
- Do not introduce React/Vue/Angular unless already required by the existing application.
- Use Bootstrap 5.3.x and vanilla JavaScript.
- Load scripts efficiently.
- Use `defer` where appropriate.
- Lazy-load non-critical images.
- Debounce search/filter requests.
- Avoid unnecessary API calls.
- Avoid repeated DOM manipulation.
- Cache stable UI/data where appropriate.
- Minimize large assets.
- Avoid autoplay video backgrounds.
- Avoid heavy animation libraries.
- Avoid oversized images.

Bootstrap can be loaded through its production-ready CSS/JS distribution, and its documentation recommends placing the responsive viewport meta tag in the page head. citeturn1view0

---

# 34. Accessibility

Follow accessible UI practices.

Required:

- Semantic HTML
- Keyboard navigation
- Visible focus states
- Proper labels
- ARIA only when necessary
- Sufficient color contrast
- Alt text
- Accessible modals
- Accessible dropdowns
- Accessible forms
- Screen-reader-friendly status messages

Do not communicate important information through color alone.

---

# 35. Responsive Component Rules

Every reusable component should have defined states.

For example:

### Station Card

- Default
- Hover
- Focus
- Loading
- Available
- Unavailable
- Selected
- Mobile

### Button

- Default
- Hover
- Active
- Focus
- Disabled
- Loading

### Input

- Default
- Focus
- Filled
- Error
- Success
- Disabled

---

# 36. Design Tokens

Centralize all visual values.

Use:

```text
/assets/css/
    variables.css
    global.css
    components.css
    responsive.css
```

Suggested structure:

```css
/* variables.css */
:root {
    ...
}
```

```css
/* global.css */
body {
    ...
}
```

```css
/* components.css */
.vn-card { ... }
.vn-button { ... }
.vn-glass { ... }
.vn-station-card { ... }
```

```css
/* responsive.css */
@media (...) {
    ...
}
```

Avoid repeating arbitrary values throughout the project.

---

# 37. JavaScript Architecture

Use modular vanilla JavaScript.

Suggested:

```text
/assets/js/
    app.js
    navbar.js
    search.js
    stations.js
    booking.js
    dashboard.js
    notifications.js
    validation.js
```

Use event delegation where appropriate.

Avoid global variables.

Keep UI logic separate from API/data logic where practical.

---

# 38. Existing Backend Protection

The redesign must **not break existing functionality**.

Before changing UI:

1. Inspect the complete project.
2. Understand the current architecture.
3. Identify routes.
4. Identify authentication.
5. Identify roles.
6. Identify APIs.
7. Identify database interactions.
8. Identify server-side validation.
9. Identify existing business rules.
10. Identify reusable components.

Then redesign.

Never replace working backend logic merely for visual changes.

---

# 39. Security Requirements

Preserve and improve:

- Authentication
- Authorization
- Role-based access
- CSRF protection
- Server-side validation
- Input sanitization
- Secure session handling
- Access control
- API authorization
- File upload validation
- Payment security

Never rely only on frontend validation.

---

# 40. India-Specific UX

VoltNet is designed for India.

Use:

- Indian Rupee: `₹`
- IST
- Indian phone format: `+91`
- Indian address structure
- Indian city/state naming
- Appropriate date/time formatting
- Indian EV charging terminology where relevant

Do not invent station data, prices, charger availability, or statistics.

---

# 41. SEO

For public pages include:

- Unique `<title>`
- Meta description
- Semantic headings
- Proper canonical URLs where applicable
- Open Graph metadata
- Descriptive image alt text
- Structured data where appropriate
- Crawl-friendly navigation

Priority public pages:

- Home
- About
- Services
- Stations
- Map
- Pricing
- Contact
- Become Partner

---

# 42. Page Design Inventory

The design system should support at least:

## Public

- Home
- About
- Services
- Stations
- Station Details
- Map
- Pricing
- Contact
- Become Partner
- Login
- Register
- Forgot Password

## Customer

- Dashboard
- Find Stations
- Station Details
- Booking
- My Bookings
- Booking Details
- Favorites
- Payments
- Notifications
- Profile
- Settings

## Station Owner

- Dashboard
- Station
- Chargers
- Bookings
- Revenue
- Analytics
- Staff
- Pricing
- Settings

## Station Manager

- Dashboard
- Today's Operations
- Bookings
- Chargers
- Sessions
- Customers
- Station Status

## Admin

- Dashboard
- Users
- Stations
- Partner Requests
- Bookings
- Payments
- Reports
- Analytics
- Settings

## Super Admin

- System Dashboard
- Admin Management
- Users
- Stations
- Partner Requests
- Platform Analytics
- System Settings
- Audit/Activity Logs

---

# 43. Design Quality Checklist

Before considering a page complete:

### Visual

- [ ] Correct VoltNet colors
- [ ] Correct logo/icon
- [ ] Consistent typography
- [ ] Consistent spacing
- [ ] No unnecessary gradients
- [ ] No excessive glass effects
- [ ] No visual clutter

### Responsive

- [ ] 320px tested
- [ ] 375px tested
- [ ] 414px tested
- [ ] Tablet tested
- [ ] Desktop tested
- [ ] 1920px tested
- [ ] No horizontal overflow

### Interaction

- [ ] Buttons work
- [ ] Forms validate
- [ ] Dropdowns work
- [ ] Modals work
- [ ] Mobile navigation works
- [ ] Loading states exist
- [ ] Empty states exist
- [ ] Error states exist

### Accessibility

- [ ] Keyboard navigation
- [ ] Focus states
- [ ] Labels
- [ ] Alt text
- [ ] Contrast
- [ ] Reduced motion support

### Performance

- [ ] No unnecessary libraries
- [ ] Images optimized
- [ ] Lazy loading used where appropriate
- [ ] JS minimized
- [ ] No unnecessary API calls
- [ ] No blocking scripts where avoidable

---

# 44. Implementation Priority

Implement in this order:

## Phase 1 — Foundation

- Design tokens
- Global CSS
- Typography
- Navbar
- Footer
- Buttons
- Forms
- Cards
- Alerts
- Responsive system

## Phase 2 — Public Website

- Homepage
- About
- Services
- Stations
- Map
- Pricing
- Contact
- Become Partner

## Phase 3 — Authentication

- Login
- Register
- Forgot password
- Reset password
- Verification

## Phase 4 — Customer

- Dashboard
- Station discovery
- Station details
- Booking
- Booking confirmation
- My bookings
- Payments
- Profile

## Phase 5 — Management

- Admin dashboard
- Super Admin dashboard
- Station Owner dashboard
- Station Manager dashboard
- Tables
- Charts
- Analytics

## Phase 6 — Polish

- Loading states
- Empty states
- Error states
- Toasts
- Microinteractions
- Accessibility
- Performance optimization
- Cross-device QA

---

# 45. Core Design Principle

The final VoltNet interface should feel:

> **Simple enough to understand immediately.  
> Premium enough to trust.  
> Fast enough to feel effortless.  
> Powerful enough to manage a real EV charging network.**

The product should never feel like a template.

Every component should have a clear purpose, consistent visual language, and predictable behavior.

---

# 46. Final Implementation Rule

Follow this exact workflow:

```text
INSPECT
   ↓
UNDERSTAND
   ↓
PLAN
   ↓
DESIGN
   ↓
IMPLEMENT
   ↓
TEST
   ↓
OPTIMIZE
```

**Do not start by randomly replacing pages.**

First understand the existing VoltNet project and preserve its working functionality. Then apply this design system consistently across the application.

The final result should be a **production-ready VoltNet experience** using the approved logo, approved color palette, Bootstrap, HTML, CSS, and vanilla JavaScript, with a strong focus on responsive design, usability, accessibility, performance, and maintainability.
