# Architectural & Implementation Decisions (`decision.md`)

This document details **why** and **which** technical, security, UX, and architectural decisions were made when implementing the Edit & Delete features, mobile responsiveness, and validation across the **Station Owner** module in VoltNet.

---

## 1. Security & Ownership Verification Decisions

### 1.1 Strict Ownership Validation in Controller Queries
- **Decision**: All station and station manager operations (Edit GET, Edit POST, Delete POST, Details) strictly filter by the authenticated user's ID (`OwnerUserId == userId` for stations, and `CreatedBy == ownerId` for managers) rather than querying purely by entity `id`.
- **Why**: Prevents **IDOR (Insecure Direct Object Reference)** attacks. Even if a malicious user inspects network calls and guesses or crafts an arbitrary GUID of another station owner's station or manager, the application returns `404 NotFound` / `401 Unauthorized`.

### 1.2 Status Guarding & Resubmission on Station Edit
- **Decision**: 
  - Station owners are **not** permitted to change their own status to `"Active"`.
  - If a station is in `"Rejected"` status and the owner edits and updates its details, the backend resets `Status = "PendingVerification"` and clears `RejectionReason = null`.
- **Why**: 
  - Only administrators should have the authority to verify and approve stations.
  - Allowing owners to directly set `"Active"` would bypass verification requirements.
  - Automatically moving rejected stations back to `"PendingVerification"` allows owners to fix issues raised by admin rejections and automatically queues the station for admin re-review without needing a separate "Request Re-review" button.

### 1.3 Permanent Email for Station Managers
- **Decision**: In `EditStationManager.cshtml`, the email field is displayed as `disabled readonly`, while `fullName`, `phone`, and `stationId` can be updated.
- **Why**: 
  - The email serves as the primary credential and unique identifier for `UserMaster`.
  - Allowing email edits on manager accounts without a verification/confirmation workflow could break authentication records or allow accidental duplicate email conflicts.

---

## 2. Data Integrity & Relational Safety Decisions

### 2.1 Referential Guarding on Station Deletion
- **Decision**: Before deleting a station in `OwnerStationController.Delete`, the controller performs an active check:
  ```csharp
  var hasManagers = await _context.StationManagers.AnyAsync(m => m.StationId == id);
  if (hasManagers) {
      TempData["Error"] = "Cannot delete station because station managers are assigned to it...";
      return RedirectToAction(nameof(Index));
  }
  ```
- **Why**:
  - Deleting a station with active managers assigned would either cause foreign key violation exceptions or leave orphaned manager records with invalid `StationId` values.
  - Explicit error messaging guides the owner on corrective steps rather than throwing unhandled 500 error pages.

### 2.2 Cascading Cleanup of User Login on Manager Deletion
- **Decision**: When deleting a `StationManager`, the controller removes both the `StationManager` profile record and the associated `UserMaster` record in a single transaction:
  ```csharp
  _context.StationManagers.Remove(manager);
  if (manager.User != null) {
      _context.UserMasters.Remove(manager.User);
  }
  await _context.SaveChangesAsync();
  ```
- **Why**:
  - Eliminates "ghost accounts" in `UserMaster` where a user could still log in even though their station management profile no longer exists.
  - Keeps database clean and synchronized.

### 2.3 UserMaster Synchronization on Manager Update
- **Decision**: When updating a manager's name and phone in `StationManager`, the application simultaneously updates `manager.User.Fullname` and `manager.User.Mobile`.
- **Why**:
  - Prevents data inconsistency between the `StationManager` profile table and the authentication `UserMaster` table.

---

## 3. UX & Mobile Responsiveness Decisions

### 3.1 Adopt Admin Module's Proven `.table-responsive-cards` Pattern
- **Decision**: Rather than using simple horizontal scrollbars (`overflow-x: auto`) that require awkward sideways swiping on phones, we adopted the `.table-responsive-cards` CSS system used in the Admin module.
  - The primary column (`.col-primary`) remains visible with an expandable chevron button (`.mobile-expand-btn`).
  - Secondary data points (`.col-secondary`) and action buttons (`.col-action`) transform into labeled key-value rows that toggle smoothly.
- **Why**:
  - Consistency: Admin and Station Owner modules now share the exact same clean design language.
  - Readability: On mobile screens (<=768px), tabular rows become cards where users can immediately read station names and expand to see full details, opening hours, status badges, and action buttons without clipping.

### 3.2 Responsive Map Coordinate Layout
- **Decision**: Latitude, Longitude, and the "Pick on Map" button row in `CreateStation.cshtml` and `EditStation.cshtml` were updated from `col-md-5 / col-md-2` to `col-12 col-md-5` and `col-12 col-md-2`.
- **Why**:
  - On phones, the button previously overflowed or compressed the input fields. With `col-12`, it stacks cleanly below the numeric inputs with full tap target width.

### 3.3 Dynamic Return Context for `PickLocation`
- **Decision**: Updated `OwnerPickLocation.cshtml` and `OwnerStationController.PickLocation` to accept query parameters `returnTo` and `id`:
  ```csharp
  public IActionResult PickLocation(string? returnTo = null, Guid? id = null)
  ```
  And in the view JavaScript:
  ```javascript
  var returnUrl = '@ViewBag.ReturnTo' === 'Edit' && '@ViewBag.StationId' 
      ? '/owner/stations/Edit/@ViewBag.StationId' 
      : '/owner/stations/Create';
  ```
- **Why**:
  - Avoided duplicating a whole new map view for editing. Both Create and Edit share the same lightweight Leaflet map picker while correctly redirecting back to their respective origin form.

---

## 4. Validation Decisions

### 4.1 Two-Layer Validation Strategy
- **Decision**: 
  - **Client-Side**: HTML5 attributes (`required`, `type="time"`, `step="any"`, `maxlength="10"`, `pattern="[0-9]{10}"`) plus jQuery unobtrusive validation for instant user feedback.
  - **Server-Side**: `ModelState.IsValid` checks, trimming, character-digit checks (`phone.All(char.IsDigit)`), station ownership checks, and duplicate email checks.
- **Why**:
  - Client-side validation delivers instantaneous UX without page reloads.
  - Server-side validation guarantees data integrity even if client scripts are disabled or requests are sent via API testing tools.

---

## 5. Nearby Stations Map (User Module) Decisions

### 5.1 Public API — Only Active Stations, No Owner Data
- **Decision**: The `/nearby-map/data` JSON endpoint filters stations to only `Status == "Active"` and excludes sensitive fields like `OwnerUser`, `OwnerUserId`, and `RejectionReason`. Only station name, address, city, state, coordinates, and hours are exposed.
- **Why**:
  - **Security**: Public users should not see owner PII, internal statuses (Pending/Rejected/Suspended), or rejection reasons.
  - **Relevance**: Only verified, active stations are useful for customers searching for places to charge.
  - **Data minimization**: Reduces payload size and attack surface.

### 5.2 Location Permission — Graceful Degradation Flow
- **Decision**: On page load, the system checks `navigator.geolocation`:
  1. **Supported + Permission Granted** → Centers map on user, calculates distances, sorts sidebar by proximity.
  2. **Supported + Permission Denied** → Shows a non-blocking warning banner ("Enable location access to find stations near you") with a Retry button. Map falls back to India-wide view with all stations visible.
  3. **Not Supported** → Falls back silently to India-wide view with all stations listed alphabetically.
- **Why**:
  - **Real-world EV apps** (PlugShare, Zap-Map, Google Maps) never block the user from seeing stations just because location is denied. They degrade gracefully.
  - A **non-blocking banner** is less intrusive than a modal popup, respecting user autonomy.
  - The **Retry button** handles the common case where users initially deny, then change their mind.

### 5.3 OSRM for Routing — No API Key Required
- **Decision**: Driving directions use the free, open **OSRM (Open Source Routing Machine)** API (`router.project-osrm.org`) rather than Google Directions API.
- **Why**:
  - **Zero cost**: OSRM is free and open-source with no API key, billing, or rate-limit management.
  - **Consistency**: The Admin Station Map already uses OSRM for directions. Using the same routing engine ensures consistent distance/time estimates across the platform.
  - **Fallback**: For actual turn-by-turn navigation, we provide a **"Navigate in Google Maps"** deep-link (`https://www.google.com/maps/dir/?api=1&destination={lat},{lng}`) that opens the user's Google Maps app — giving the best of both worlds.

### 5.4 Haversine Distance — Client-Side, No Server Round-Trip
- **Decision**: Station distances from the user are calculated client-side using the **Haversine formula** in JavaScript, not via a server API.
- **Why**:
  - **Performance**: Eliminates a network round-trip for each distance calculation. All N stations are computed instantly in-browser.
  - **Privacy**: The user's GPS coordinates never leave their browser for distance sorting. Only when they explicitly click "Get Directions" do coordinates get sent to OSRM for route calculation.
  - **Simplicity**: No additional server endpoint or database changes needed.

### 5.5 Google Maps Deep-Link for Real Navigation
- **Decision**: Each station popup includes a **"Navigate in Google Maps"** button that opens `https://www.google.com/maps/dir/?api=1&destination={lat},{lng}` in a new tab.
- **Why**:
  - Real EV users need **turn-by-turn driving navigation**, which a Leaflet map cannot provide.
  - Google Maps is the most widely installed navigation app on both Android and iOS.
  - The deep-link format automatically opens the native Google Maps app on mobile devices, providing a seamless handoff.

### 5.6 Sidebar with Search and Distance Sorting
- **Decision**: A sidebar panel shows stations as cards sorted by distance, with a search filter by name/city. On mobile, it collapses into a bottom panel with a toggle button.
- **Why**:
  - **Discoverability**: Users need a list view alongside the map. Looking at 50+ markers on a map is overwhelming — a sorted list with distances is far more actionable.
  - **Search**: Users may know a city or station name. Client-side filtering provides instant results without server queries.
  - **Mobile UX**: On phones, a full sidebar would hide the map entirely. A collapsible bottom sheet preserves both map visibility and station list access, following the same pattern used by Google Maps and Apple Maps.
  - **Performance limit**: The DOM list is strictly limited to 50 items and filtered to a 100km radius when location is on, to prevent DOM lag when scaling to thousands of stations.

## 6. Station Manager Module
### 6.1 Introduction of the Charger Entity
- **Decision**: Added a new `Charger` model (Id, Name, ConnectorType, CapacityKw, Status) representing individual charging points at a station.
- **Why**:
  - A Station Manager's primary role is day-to-day operations. Without hardware entities to manage, the role has little practical use.
  - EV networks require tracking the status and type (CCS2, Type 2, etc.) of individual chargers, not just the parent station.

### 6.2 Data Isolation and Security
- **Decision**: All `ManagerStationController` and `ManagerChargerController` queries explicitly enforce a `StationId` constraint based on the logged-in manager's assigned station (`c.StationId == manager.StationId`).
- **Why**:
  - **Security (Multi-tenancy at the Manager level)**: A manager from Station A must never be able to view, edit, or delete a charger belonging to Station B, even by guessing the `Guid` in the URL.
  
### 6.3 Manager Layout Architecture
- **Decision**: Created `_ManagerLayout.cshtml` copied and adapted from `_OwnerLayout.cshtml`.
- **Why**:
  - Maintains consistent UI/UX for internal users while strictly separating their sidebar navigation links and routing prefixes (`/manager/...` vs `/owner/...`).

---

## 7. Authentication & Profile Decisions (Phase 0)

### 7.1 OTP-Based Password Reset
- **Decision**: The "Forgot Password" flow generates a 6-digit OTP, stores it in `IMemoryCache` for 15 minutes mapped to the email, and emails it using the internal `MailApi`.
- **Why**: 
  - Prevents database clutter (no need for a dedicated `PasswordResetTokens` table).
  - In-memory cache auto-expires, handling cleanup implicitly.

### 7.2 Manager Profile Data Synchronization
- **Decision**: In `ManagerDashboardController.Profile` (POST), updates to `FullName` and `Phone` are written to both `StationManagers` and the parent `UserMaster` record in a single transaction.
- **Why**: 
  - The authentication system (`UserMaster`) and the domain profile (`StationManagers`) must never fall out of sync. Without this, the JWT token would display outdated names in the top-right navbar.

---

## 8. Subscription Architecture Decisions (Phase 1)

### 8.1 Station-Level Plan Assignment
- **Decision**: The schema uses three entities: `SubscriptionPlan` (the template), `OwnerSubscription` (the active purchase), and `Station.OwnerSubscriptionId` (the assignment mapping).
- **Why**: 
  - **Real-World Flexibility**: Rather than tying a plan directly to an owner's account blindly, this maps quotas exactly. If an owner buys a "Growth Plan (Max 5 Stations)" but has 10 stations, they must explicitly choose *which* 5 stations become Active.
  - **Graceful Expiration**: If an `OwnerSubscription` expires, the `Station` query can instantly determine that the station is no longer active without needing to write a cron job that iterates through all stations to change their status strings.

### 8.2 Client-Side Quota Validation
- **Decision**: The `Assign.cshtml` view limits checkbox selections using jQuery (`$('.station-checkbox:checked').length > maxAllowed`) and disables the submit button if the limit is exceeded.
- **Why**: 
  - Provides instantaneous UX feedback so the user doesn't submit a form only to receive a server error. (Server-side checks still exist as a secondary guard).

### 8.3 Auto-Expiration & Validation Checks
- **Decision**: Instead of running a scheduled background job (cron) to turn off stations when their subscriptions end, the system dynamically filters out expired stations at query-time. All queries in the public `HomeController` now require:
  `s.Status == "Active" && s.OwnerSubscription != null && s.OwnerSubscription.Status == "Active" && s.OwnerSubscription.EndDate >= DateTime.UtcNow`
- **Why**: 
  - **Fail-Safe Integrity**: This completely eliminates the edge case where a background job might crash, leaving unpaid stations active on the map. The moment the UTC clock crosses the `EndDate`, the station drops off the map and searches immediately.
  - **Performance**: A simple `JOIN` and date comparison on the database layer is highly optimized and significantly less complex than managing external job queues and transaction rollbacks.

### 8.4 Razorpay Payment Gateway Integration
- **Decision**: Integrated Razorpay Checkout (client-side modal) with server-side Order creation and HMAC-SHA256 signature verification. The flow is:
  1. Client clicks "Buy" → AJAX POST to `/create-order` → Server calls Razorpay Orders API with Basic Auth → returns `order_id`
  2. Client opens Razorpay Checkout modal with the `order_id`
  3. On payment success → Hidden form POSTs `razorpay_order_id`, `razorpay_payment_id`, `razorpay_signature` to `/verify-payment`
  4. Server verifies signature using `HMAC-SHA256(order_id|payment_id, key_secret)` → Creates subscription only if signature matches
- **Why**:
  - **Security**: The payment is never trusted from the client alone. The HMAC signature verification ensures the payment response hasn't been tampered with. Even if a malicious user crafts a fake `payment_id`, the signature will fail.
  - **Atomicity**: The subscription record is only created AFTER signature verification. If the user closes the Razorpay modal, cancels, or their payment fails, no subscription is created.
  - **No NuGet dependency**: Instead of adding the `Razorpay` NuGet package, we use raw `HttpClient` + `System.Text.Json` for the API call and `System.Security.Cryptography.HMACSHA256` for verification. This keeps the dependency footprint minimal.

### 8.5 Payment Edge-Case Validations
- **Duplicate Payment Guard**: Before creating a subscription, the server checks `_context.OwnerSubscriptions.AnyAsync(s => s.PaymentId == razorpay_payment_id)`. This prevents a browser refresh or replay attack from creating two subscriptions for the same payment.
- **Double-Click Prevention**: A `isPaymentInProgress` JS flag disables all Buy buttons once clicked, preventing the user from accidentally creating multiple Razorpay orders.
- **Modal Dismiss Handling**: Razorpay's `modal.ondismiss` callback re-enables buttons and hides the processing overlay, so the user can retry cleanly.
- **Payment Failed Event**: Razorpay's `payment.failed` event is explicitly handled to show a user-friendly error message (not a generic crash) and confirms no amount was charged.
- **Plan Staleness Check**: Both `create-order` and `verify-payment` re-check `plan.IsActive` from the database, guarding against the race condition where an admin deactivates a plan while a user has the checkout open.

### 8.6 Subscription Cancellation & 24-Hour Refund Policy
- **Decision**: Implemented an administrative cancellation verification workflow:
  1. **24-Hour Purchase Window Policy**: An owner can only initiate a cancellation request within 24 hours of purchasing the subscription (`(DateTime.UtcNow - subscription.CreatedAt).TotalHours <= 24`). Attempts after 24 hours are blocked at both UI and server level.
  2. **Mandatory Reason Verification**: The owner must supply a detailed reason (minimum 10 characters, capped at 500 characters).
  3. **Two-Stage Status Transition**: The subscription moves to `CancellationPending` while under review.
  4. **Admin Approval & Refund Mechanism**:
     - Admins review cancellation requests in `/admin/subscription/cancellation-requests`.
     - Upon Admin Approval: `Status = "Cancelled"`, stations are detached and downgraded to `"Approved"` (offline), `RefundAmount = AmountPaid` (100% refund), and an official notification email is dispatched to the owner.
     - Upon Admin Rejection: `Status = "Active"`, stations stay active, and an explanation email is sent with the admin's remarks.
- **Why**:
  - **Loophole & Fraud Prevention**: Prevents malicious owners from using a high-tier plan (e.g., Enterprise 20 Stations) for weeks and then claiming a full refund right before expiration.
  - **Accountability**: Real-world SaaS platforms enforce strict return/cancellation windows with audit logs.
  - **Communication**: Automated email alerts keep owners informed about refund status and reasons.

### 8.7 Accurate Financial & Subscription Metrics
- **Decision**:
  - **Total Invested Amount**: Dynamically calculated as the sum of `AmountPaid` across **non-cancelled** subscriptions (`Status != "Cancelled"`). If a plan is cancelled and refunded, its amount is immediately deducted from the owner's net invested capital.
  - **Total Subscriptions Metric**: Represents the net valid subscriptions.
  - **Separation of Concerns**: Active subscriptions (`Status == "Active"` / `CancellationPending`) reside in the primary dashboard, while past, cancelled, and expired plans are moved to `/owner/subscription/history`.

### 8.8 Ultra-Realistic Razorpay Train-Convoy Payment Experience
- **Decision**: Engineered a custom CSS keyframe locomotive convoy animation in both `Plans.cshtml` and `PlanDetails.cshtml`:
  - **Convoy Composition**: Detailed train engine with smoke puffs, headlight, rotating wheels, track sleepers, and cargo wagons carrying 3D spinning golden Rupee (`₹`) coins.
  - **3-Phase Flow**: (1) Gateway Initialization → (2) Train-Convoy Processing Simulation → (3) Success Checkmark & Auto-Redirect.
- **Why**:
  - Replaces generic spinners with a delightful, realistic payment gateway interaction identical to top-tier consumer apps.

---

## 9. Charging Rates System & Station Manager Permission Architecture

### 9.1 Relational Architecture & Single-Active-Rate Guard
- **Decision**: Implemented `ChargingRate` model linked to `Station` with fields: `ConnectorType`, `RatePerKwh` (mandatory, ₹0.01 - ₹150.00), `RatePerHour` (optional parking/session fee, ₹0.00 - ₹2000.00), and `IsActive`.
- **Single Active Rate per Connector Rule**: A station can only have **one** active charging rate per connector type at any given time. Creating a duplicate active rate or activating a second rate for the same connector type is blocked on both client and server layers.
- **Soft Deactivation**: Historical rates are marked `IsActive = false` rather than hard deleted, ensuring data integrity when historical bookings reference previous prices.

### 9.2 Station Owner Permission Delegation (`CanManageRates`)
- **Decision**: Added `CanManageRates` boolean flag to `StationManager`.
- **Granular Control**:
  - Station Owners can toggle rate management permissions per manager from the Station Managers page or during manager creation/edit.
  - Station Managers with `CanManageRates = false` see a clean **Read-Only Mode** on `/manager/rates` with informative tooltips.
  - Station Managers with `CanManageRates = true` can add, edit, and toggle charging rates directly from their portal for their assigned station.
- **Security Check**: The controller strictly verifies `manager.CanManageRates` in `ManagerRatesController` before allowing any rate modifications.

### 9.3 Public Integration & Dynamic Price Discovery
- **Decision**: The public `/station/{id}` and `/Home/Stations` search results dynamically calculate and display:
  - **"Starting From ₹X/kWh"** on station search cards.
  - Per-connector badges (e.g. `⚡ CCS2 - ₹15.00/kWh (+ ₹50.00/hr)`) on the Station Detail page.
- **Why**: Prepares the foundational pricing calculations required for Phase 2 (Booking Module).

---

## 10. Phase 2: Booking Module & Real-World Indian EV Charging Architecture

### 10.1 Complete 3-Step Customer Booking Journey
- **Decision**: Implemented an intuitive 3-step funnel:
  1. `/customer/book/{stationId}`: Select active charger connector type, capacity (kW), and view charging rates.
  2. `/customer/book/{stationId}/slots`: Interactive 1-hour slot generator bounded between station operating hours and the next 7 days in IST.
  3. `/customer/book/confirm`: Review schedule, input optional vehicle plate & model, preview energy unit rate, and confirm.
- **Why**: Eliminates user confusion, prevents race-condition collisions, and collects essential driver metadata for on-site station staff.

### 10.2 Petrol-Pump Style Energy Meter Billing (`Units used (kWh) × Rate/kWh`)
- **Decision**:
  - Instead of forcing customers into rigid, overpriced pre-payments based on nominal capacity, online bookings only lock the slot with a transparent **Rate per kWh (e.g. ₹15/kWh)** and estimated range.
  - At session completion, the **Station Manager** records the exact energy meter numbers (`StartMeterReading`, `EndMeterReading`, or `UnitsConsumedKwh`).
  - **Final Bill Formula**:
    $$\text{Final Total Bill} = (\text{Units Consumed (kWh)} \times \text{Rate per kWh}) + \text{Base Station Fee}$$
- **Why**: Replicates the natural, trusted petrol-pump fueling experience in India. Drivers only pay for the exact kilowatt-hours pumped into their EV battery with zero wasted money.

### 10.3 Energy Meter Display Photo Upload & OCR Digit Scanning
- **Decision**:
  - The Station Manager can snap/upload a photo of the charger meter display (`meterPhoto`).
  - Built-in OCR parser (`/manager/bookings/read-meter-photo`) auto-detects and extracts the kWh digits directly from the meter image to speed up checkout.
  - The photo is saved as an immutable audit record (`MeterPhotoUrl`) accessible on both the customer receipt and manager dashboard.
- **Why**: Guarantees 100% dispute-free transparency between EV drivers and station staff.

### 10.4 Walk-In / Spot Charging System for Offline Drivers
- **Decision**:
  - Station managers have a **`+ New Walk-In Driver`** portal action to record drivers who arrive directly without prior app reservations.
  - Manager enters vehicle plate number, driver phone, duration, and assigns the charger bay.
  - **Instant Slot Locking**: The system immediately reserves that charger bay in real-time so online drivers cannot book an already occupied charger.
- **Why**: In India, many drivers arrive on critical low battery without prior planning. This allows stations to serve walk-ins while keeping digital slot schedules synchronized.

### 10.5 Real-Time Waitlist Queue & Automated Slot Promotion
- **Decision**:
  - When an online driver views a slot that is already booked, they can click **`🔔 Join Waitlist (Queue #1)`**.
  - **Automated Promotion Engine**: If the active driver **cancels** their booking outside the 30-minute window, the cancellation trigger automatically promotes the next person in line to `Confirmed`, generates their booking, re-indexes remaining queue positions, and fires an instant email alert to the promoted customer.
- **Why**: Maximizes charger bay utilization for station owners and provides drivers a fair, automated backup opportunity.

### 10.6 Strict 30-Minute Cancellation Policy
- **Decision**: Customers can cancel reservations free of charge strictly up to 30 minutes before the slot start time ($t_{\text{slot}} - t_{\text{now}} \ge 30\text{ min}$). Within 30 minutes, cancellation is locked.
- **Why**: Protects station owners against last-minute ghost cancellations that leave chargers idle.

### 10.7 Station Maintenance, Breaks & Grid Outage Controls
- **Decision**: Created `/manager/station-blocks` allowing managers to temporarily block specific chargers or the entire station for maintenance, grid power cuts, or staff breaks. Blocked slots are visually indicated to customers and locked from reservations.
- **Why**: Prevents frustrated drivers from arriving at a station undergoing emergency electrical work or power outages.


