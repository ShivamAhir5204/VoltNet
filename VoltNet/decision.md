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

---

## 11. Subscription Plan Upgrade Architecture (Netflix / JioCinema / Hotstar Prorated Model)

### 11.1 Single Active Subscription Rule Preserved
- **Decision**: A Station Owner is restricted to **exactly one** `Status == "Active"` subscription at any time. Rather than stacking multiple overlapping subscriptions, owners can upgrade their active subscription to a higher tier at any point (whether they have hit their station limit or simply want higher station and manager quotas).
- **Why**: Eliminates multiple confusing billing dates, avoids duplicate invoice overhead, and mirrors standard industry models (Netflix, JioCinema, Hotstar, Spotify).

### 11.2 Prorated Unused Days Credit Formula
- **Decision**: When an owner upgrades from an active plan to a higher-tier plan, their remaining days are converted into a monetary discount deducted directly from the new plan's price:
  $$\text{Daily Rate} = \frac{\text{Amount Paid on Old Plan}}{\text{Total Duration Days of Old Plan}}$$
  $$\text{Unused Credit} = \min(\text{Amount Paid on Old Plan}, \max(0, \text{Daily Rate} \times \text{Remaining Days}))$$
  $$\text{Net Upgrade Price} = \max(0, \text{New Plan Price} - \text{Unused Credit})$$
- **Why**: Guarantees fair, transparent billing. Station owners never lose the value of days they already paid for, encouraging frictionless upgrades without waiting for plans to expire.

### 11.3 Seamless Station Continuity & Zero Downtime Transition
- **Decision**: In `OwnerSubscriptionController.VerifyPayment`:
  1. The existing active subscription is transitioned to `Status = "Upgraded"`.
  2. The new subscription is created with `Status = "Active"`, `StartDate = DateTime.UtcNow`, `EndDate = DateTime.UtcNow.AddDays(plan.DurationDays)`, and `AmountPaid = upgradePayable`.
  3. **Automatic Station Re-linking**: All stations linked to the old subscription ID are re-assigned to the new subscription ID:
     ```csharp
     foreach (var st in activeSub.Stations)
     {
         st.OwnerSubscriptionId = subscription.Id;
     }
     ```
- **Why**: Linked stations never lose their active status, ensuring 0% public map downtime and uninterrupted customer bookings during the upgrade.

### 11.4 Ubiquitous "⚡ Upgrade" Callouts (Netflix / Hotstar UX)
- **Decision**:
  - **Index Dashboard**: Added prominent `⚡ Upgrade` action buttons directly on the active subscription card and header bar, plus dynamic quota alerts when assigned stations reach limit.
  - **Station Allocation (`Assign.cshtml`)**: Added an upgrade reminder box for owners needing more capacity than their current tier allows.
  - **Plan Catalog (`Plans.cshtml`)**: Displays "Current Plan" on active tier, disables lower tiers, and transforms higher-tier cards into "Upgrade Available" with full price breakdown (`New Price - Credit = Net Pay`).
  - **Checkout Summary (`PlanDetails.cshtml`)**: Shows line-item prorated discount row (`-₹X.XX Prorated Credit`) and "Pay & Upgrade Now" CTA with matching payment simulation animations.
  - **History (`History.cshtml`)**: Distinct `Upgraded` badge and info callout preserving an audit trail of past subscriptions.
- **Why**: Provides a modern, premium SaaS upgrade journey where owners are never stuck or confused about how to expand their network capacity.

---

## 12. Real-World Charging Session Lifecycle & Anti-Hoarding Slot Protection

### 12.1 The "Early Slot Booking Loophole" & How We Solved It
- **The Problem / Loophole**:
  - In a free reservation system where energy is only billed at completion, malicious or casual EV drivers could reserve multiple peak-hour slots across different charging stations with zero financial consequence.
  - If a driver cancels late, does not show up, or simply changes their plans, the station owner suffers 100% loss of that charging bay for that hour, and other genuine EV drivers on low battery are turned away.
- **The Solution: ₹50 Advance Slot Commitment Deposit**:
  - When an online driver books a slot, they pay a nominal **₹50 Advance Slot Commitment Fee** (`BookingFee = 50.00m`, `PaymentStatus = "AdvancePaid"`).
  - **100% Adjusted at Checkout**: When the customer arrives and charges their vehicle, the ₹50 is deducted in full from their final energy bill:
    $$\text{Final Energy Cost} = \text{Units Consumed (kWh)} \times \text{Rate/kWh}$$
    $$\text{Net Amount to Collect by Manager} = \max(0, \text{Final Energy Cost} - \text{BookingFee})$$
  - **Walk-In Drivers**: Pay ₹0 advance booking fee because they are already physically at the station.
  - **No-Show / Late Cancellation Forfeiture**: If a customer ghosts or fails to show up, the ₹50 deposit compensates the station owner for bay reservation downtime.

### 12.2 Two-Stage Physical Session Lifecycle (`Confirmed` $\to$ `Charging` $\to$ `Completed`)
- **Decision**:
  - `Confirmed`: The slot is booked and reserved. Driver is expected.
  - `Charging` (New State): Triggered by station manager clicking **"⚡ Start Charging"** when the charging gun is physically plugged into the EV. Captures `ChargingStartedAt` timestamp and `StartMeterReading`.
  - `Completed`: Triggered by station manager clicking **"Finish & Bill"** when charging ends. Captures `EndMeterReading`, `UnitsConsumedKwh`, photo proof of meter, and handles payment collection.
  - **Customer Protection**: While `Status == "Charging"`, the customer's "Cancel Booking" button is locked to prevent premature mid-session cancellation.
- **Why**: Real-world EV stations have a distinct physical state between arrival/plug-in and final billing. Managers can see all bays that are actively pumping power in real-time.

### 12.3 Multi-Channel Payment Accounting at Bay Checkout
- **Decision**:
  - The station manager checkout modal includes a **Payment Method** selector:
    - `UPI / QR Scan` (BHIM, PhonePe, Google Pay, Paytm)
    - `Cash` (Physical currency at counter)
    - `Card (POS Machine)` (Credit/Debit tap & chip)
  - Displays explicit breakdown: `Actual Energy Cost` - `Advance Paid (₹50)` = `Net Collectable Amount`.
  - Stored in database fields `PaymentMethod` and `PaymentStatus = "Paid"`.
- **Why**: Station owners require clear accounting audit logs to reconcile cash in counter vs bank UPI receipts.

### 12.4 Automated No-Show Waitlist Promotion
- **Decision**:
  - When a manager marks an absent booking as `NoShow`, the system triggers an immediate waitlist check for that charger and time slot.
  - The next waiting driver in line (`QueuePosition == 1`) is automatically promoted to `Confirmed`.
  - Subsequent queue members are shifted forward (`QueuePosition--`).
  - An automated email notification is fired to the promoted driver via `SendPromotionNotificationEmailAsync`.
- **Why**: Ensures bay downtime is minimized by immediately offering empty bays to standby drivers waiting nearby.

---

## 13. Dual-Level Document Verification & Automated Email Engine

### 13.1 Dual-Level Verification Domain Separation
- **Decision**: Split document verification into two distinct, auditable architectural entities:
  1. **`StationOwnerDocument` (Partner KYC Onboarding)**: Identity Proof / PAN Card, Aadhaar / Passport, Business Incorporation / MSME / Trade License, GST Certificate, Bank Account Cancelled Cheque.
  2. **`StationDocument` (Charging Infrastructure Statutory Compliance)**: DISCOM Power Sanction Letter, Land Lease / Ownership Deed, Fire Safety NOC, CEIG Safety Clearance / Single Line Diagram (SLD), and Station Site Photographs.
- **Why**: Separates the legal entity identity verification (KYC of the person/firm running the business) from physical premises compliance (electrical power grid safety and land permissions). A single owner can operate multiple stations, each with distinct DISCOM sanction letters and fire NOCs.

### 13.2 File Security & Anti-Malware Safeguards (`FileValidationHelper`)
- **Decision**:
  - **Extension Whitelist**: Only `.pdf`, `.jpg`, `.jpeg`, `.png`, and `.webp` allowed.
  - **Magic Byte Verification**: Inspects raw binary header bytes (`%PDF`, `FF D8 FF`, `89 50 4E 47`, `RIFF...WEBP`) to prevent disguised scripts or executable payloads.
  - **File Size Ceiling**: Strictly capped at 10 MB per document to prevent DoS memory bloat.
  - **Hashed Storage & Path Sanitization**: Files stored with `Guid.NewGuid()` filenames in dedicated directories (`/uploads/owner-kyc/` and `/uploads/station-documents/`) to eliminate directory traversal attacks and collision overwrites.
- **Why**: Protects backend servers from arbitrary file execution and malicious payload uploads.

### 13.3 Frictionless Resubmission Lifecycle
- **Decision**:
  - If Admin rejects a partner application or a charging station, the applicant / owner is not locked out.
  - The rejection note is clearly displayed in their status portal alongside their uploaded documents.
  - Users can directly upload corrected scans and submit. Re-uploading moves document and station status back to `"Pending"` / `"PendingVerification"`, automatically reappearing on Admin's verification queue.
- **Why**: Mirrors real-world onboarding where documents are frequently blurry or need updated stamps.

### 13.4 In-Browser Document Preview (Zero-Download UX)
- **Decision**: In `Views/admin/verify/StationDetails.cshtml` and `Views/admin/verify/OwnerDetails.cshtml`, clicking **"Preview"** launches a responsive modal embedding the PDF in an iframe or displaying a high-resolution image zoom.
- **Why**: Admins can inspect compliance seals and text without downloading hundreds of untrusted files to their local workstations.

### 13.5 Automated Email Notification Engine
- **Decision**: Automated branded HTML email dispatches on verification decisions:
  - **Station Approved**: Congratulates the station owner, confirms station details, and provides a direct CTA to their owner dashboard.
  - **Station Rejected**: Delivers actionable feedback detailing the exact rejection reasons and provides a direct link to re-upload.
  - **Partner Application Approved**: Welcomes the applicant to the Station Owner ecosystem, confirms role elevation to `StationOwner`, and outlines next operational steps.
  - **Partner Application Rejected**: Explains why KYC was insufficient and provides a link to their partner status page to resubmit corrected documents.
  - **Individual Document Approval & Rejection**: Dispatches instant feedback to the owner whenever an admin accepts or rejects a specific document (e.g. DISCOM Sanction Letter or Fire Safety NOC) so the owner is immediately aware without waiting for total station rejection.
- **Why**: Real-time transparency keeps applicants informed without needing manual support correspondence.

### 13.6 Mandatory Statutory Document Requirement at Station Creation
- **Decision**: All 4 compliance documents (**DISCOM Sanction Letter**, **Land Lease / Ownership Deed**, **Fire Safety NOC**, and **Station Site Photographs**) are made strictly **mandatory** upon station registration:
  - Client-side: `required` HTML attributes and interactive custom file picker cards with instant file name, size, and 10 MB limit feedback.
  - Server-side: `OwnerStationController.Create` checks for non-null/non-empty files and reports user-friendly `ModelState` errors.
  - Replaced the awkward header `"Compliance & Statutory Documents (Optional now, or upload in Station Details)"` with a professional, regulatory-standard banner: **`Mandatory Station Verification Documents`** explaining national compliance requirements.
- **Why**: Prevents dummy or incomplete stations from flooding the database and ensuring only fully compliant stations reach the admin verification desk.

---

## 14. Frontend Architecture & Modal Stacking Context Engine

### 14.1 Universal Modal Stacking Context Architecture (Otika Theme Fix)
- **Problem**: In Otika and Bootstrap 4, the `.main-content` wrapper has `position: relative`. When a modal is nested inside `@RenderBody()`, its z-index applies only within `.main-content`'s stacking context. When Bootstrap appends `.modal-backdrop` directly to `<body>` at `z-index: 1040`, the backdrop covers `.main-content` and the modal inside it, causing a dark, dull screen where no buttons or inputs can be clicked.
- **Solution**:
  1. **Razor Architectural Placement**: Added `@await RenderSectionAsync("Modals", required: false)` right before `</body>` in `_OwnerLayout.cshtml` and `_AdminLayout.cshtml`. All modals are defined in `@section Modals { ... }` and rendered natively as direct children of `<body>`, completely immune to any parent stacking context.
  2. **Proactive Relocation Script**: On DOM ready, `$('#app .modal').appendTo("body")` automatically moves any modal found inside `#app` to `<body>` before any click event is triggered.
  3. **CSS z-index Hierarchy**:
     - `.modal-backdrop`: `z-index: 1040 !important`
     - `.modal`: `z-index: 1060 !important`
     - `.modal-dialog`: `z-index: 1065 !important`
     - `.modal-content`: `z-index: 1070 !important`
- **Why**: Provides a permanent, robust fix across all browsers and views, ensuring modals always appear in front of the backdrop and remain fully responsive.

### 14.2 Multi-State Verification Queue Query
- **Decision**: Updated `VerifyStations` query in `VerifyController.cs`:
  `Where(s => s.Status == "PendingVerification" || s.Documents.Any(d => d.Status == "Pending"))`
- **Why**: When an active station owner uploads an updated NOC or new compliance document from Station Details, the station previously remained hidden from the admin verification queue because its status was `"Active"`. The enhanced query ensures stations with newly submitted documents appear with a distinct **`New Docs Pending`** badge.

---

## 15. Phase 4 — Revenue Module Architectural & Implementation Decisions

### 15.1 Pure Aggregation Architecture (Zero Schema Migrations)
- **Decision**: Implemented full financial analytics, station-wise metrics, utilization formulas, and platform-wide GMV using pure relational aggregations across existing tables (`Bookings`, `Chargers`, `Stations`, `StationOwners`, `OwnerSubscriptions`, and `SubscriptionPlans`).
- **Why**:
  - Eliminates unnecessary duplicate tables or transaction caches that drift out of sync with real booking states.
  - Ensures 100% financial consistency: every single rupee reported in KPIs directly derives from verified completed booking records (`ActualCost ?? EstimatedCost`) or active B2B SaaS subscription invoices (`AmountPaid - RefundAmount`).

### 15.2 Indian Standard Time (IST UTC +05:30) Date Boundary Normalization
- **Decision**: All daily, weekly, and monthly financial window calculations explicitly compute dates in IST:
  ```csharp
  var istNow = DateTime.UtcNow.AddHours(5).AddMinutes(30);
  var today = istNow.Date;
  var startOfWeek = today.AddDays(-(int)(today.DayOfWeek == DayOfWeek.Sunday ? 6 : today.DayOfWeek - DayOfWeek.Monday));
  var startOfMonth = new DateTime(today.Year, today.Month, 1);
  ```
- **Why**: In standard UTC, Indian business hours (e.g. 5:30 AM to 11:59 PM) span two different UTC calendar days. Without IST conversion, transactions completed between 5:30 AM and 11:59 AM IST would incorrectly count towards the previous day, resulting in inaccurate "Today's Revenue" metrics.

### 15.3 Strict Three-Tier IDOR Protection
- **Decision**:
  1. **Station Manager (`/manager/revenue`)**: Resolves station strictly through the authenticated user's claim:
     `_context.StationManagers.FirstOrDefaultAsync(m => m.UserId == userId)`
     Never accepts a `stationId` query parameter from the URL.
  2. **Station Owner (`/owner/revenue` & `/owner/revenue/export`)**: Scopes all revenue and CSV exports strictly to `s.OwnerUserId == userId`. If a custom `stationId` query parameter is supplied, it is validated against the owner's owned station list before being applied.
  3. **Platform Admin (`/admin/revenue` & `/admin/revenue/export`)**: Restricted via `[Authorize(Roles = "Admin,SuperAdmin")]` to deliver macro platform oversight.
- **Why**: Completely neutralizes Insecure Direct Object Reference (IDOR) attacks, ensuring managers and owners can never spy on competitor stations or unauthorized network revenues.

### 15.4 Operational EV Bay Utilization & Peak-Hour Intelligence
- **Decision**:
  - **Peak Hour**: Dynamically calculated by grouping completed sessions by hour of day (`b.StartTime.Hours`) and identifying the modal charging window (e.g., 6:00 PM – 7:00 PM).
  - **30-Day Bay Utilization Rate**: Formulated as:
    $$\text{Utilization \%} = \frac{\text{Completed Sessions in 30 Days}}{\text{Total Chargers} \times \text{Daily Operating Hours} \times 30} \times 100$$
    Where daily operating hours dynamically derive from the station's `OpeningTime` and `ClosingTime`.
  - **Top Charger Analysis**: Identifies the single most profitable charger unit, its connector standard (CCS2, Type 2, CHAdeMO), and computes its percentage contribution to total station revenue.
- **Why**: Transforms raw monetary receipts into actionable operational intelligence for station managers and owners to schedule maintenance, optimize dynamic pricing, and prevent bay bottlenecks.

### 15.5 RFC 4180 Compliant CSV Export Engine with UTF-8 BOM
- **Decision**: Both Owner (`/owner/revenue/export`) and Admin (`/admin/revenue/export`) CSV exports include a leading UTF-8 Byte Order Mark (`\uFEFF`) and quote-escaped cell values.
- **Why**: Standard UTF-8 CSVs opened in Microsoft Excel on Windows in India often misinterpret UTF-8 currency symbols and accents. Prepending `\uFEFF` guarantees that Excel, Google Sheets, and LibreOffice immediately parse column encodings and delimiters seamlessly without prompt dialogs.

### 15.6 Chart.js Integration & Real-Time Visualization
- **Decision**: Integrated Chart.js 4.x via CDN with custom responsive line and bar visualizations:
  - Manager: 30-day daily revenue trend line with gradient fill and session volume tooltips.
  - Owner: Per-station comparative bar chart and selected date range daily progression.
  - Admin: 12-month multi-stream stacked bar and line progression separating station charging collections from B2B SaaS subscription revenues, accompanied by an owner subscription lifecycle health gauge.
- **Why**: Gives each stakeholder an immediate visual pulse of their business with zero bundle overhead.

---

## 16. Financial Analyst Studio & Custom Date Review Matrix

### 16.1 Multi-Granularity Time-Series Bucketing (Daily, Weekly, Monthly)
- **Decision**: Created `/admin/revenue/analytics` providing custom date range analysis with configurable temporal granularity (`day`, `week`, `month`):
  - Automatically selects an optimal default granularity (Daily if $\le$ 31 days, Weekly if $\le$ 120 days, Monthly if $> 120$ days) while permitting manual analyst overriding.
  - Generates comprehensive bucket metrics: Volume (Sessions), Units (kWh), Gross Collection (₹), Average Order Value (AOV), and Realized Rate (₹/kWh).
- **Why**: Allows financial auditors and business analysts to evaluate seasonality, charging velocity, and revenue run-rates without being overwhelmed by noisy daily plots over multi-month or annual horizons.

### 16.2 In-Browser Multi-Metric Dynamic Switcher
- **Decision**: Implemented an in-memory metric switching engine in `Views/admin/revenue/Analytics.cshtml` using Chart.js. Analysts can toggle between:
  1. Gross Collection (₹)
  2. Energy Dispensed (kWh)
  3. Session Volume (Count)
  4. Average Order Value (AOV)
  Without triggering full page reloads or querying the database again.
- **Why**: Delivers instant, app-like responsiveness for reviewers comparing volume spikes versus monetary realization.

### 16.3 Analyst Raw Dataset Export
- **Decision**: Added `[HttpGet("analytics/export")]` exporting granular booking data filtered by date, station, city, and payment method with an explicit UTF-8 BOM.
- **Why**: Financial analysts and auditors can import the CSV directly into Python/Pandas, PowerBI, or Excel Pivot Tables without formatting headaches.

---

## 17. EV Driver Spending Telemetry, Fuel Savings & Statement Engine

### 17.1 Customer Financial & Ecological Value Proposition
- **Decision**: Created `CustomerSpendingController.cs` (`/customer/spending` and `/customer/analytics`) giving registered EV drivers transparency into their total charging expenses.
- **Key Telemetry Metrics**:
  - **Lifetime & Monthly Spend**: Total INR invested in EV charging, session counts, and kWh added.
  - **Unit Economics**: Average cost per charging session (₹/session) and realized rate per unit (₹/kWh).
  - **Net Fossil Fuel Savings**:
    $$\text{Savings} = (\text{Total kWh} \times 7\text{ km/kWh} \times \frac{\text{₹}102}{\text{14.5 km/L}}) - \text{Total EV Spend}$$
  - **CO₂ Emissions Avoided**: Computed at standard grid displacement of $0.82\text{ kg CO}_2/\text{kWh}$, accompanied by tree-equivalent equivalents ($\div 20\text{ kg/tree/year}$).
- **Why**: Validates the driver's decision to transition to electric mobility by visibly displaying cash saved and carbon emissions offset on every charging session.

### 17.2 Personal Statement Export Engine
- **Decision**: Added `/customer/spending/export` enabling drivers to download an itemized, date-filtered CSV charging statement with station names, charger IDs, vehicle numbers, kilowatt-hours, and payment timestamps.
- **Why**: Real-world drivers need official charging statements for corporate travel reimbursements, tax deductions, and personal expense tracking.

---

## 18. Owner Individual Station Revenue Intelligence & Deep-Dive Analytics

### 18.1 Dedicated Station Revenue Route & Strict IDOR Scoping
- **Decision**: Implemented `[HttpGet("owner/revenue/station/{stationId}")]` and `[HttpGet("owner/revenue/station/{stationId}/export")]` in `OwnerRevenueController.cs`.
- **Ownership Verification**:
  ```csharp
  var station = await _context.Stations
      .Include(s => s.Chargers)
      .FirstOrDefaultAsync(s => s.Id == stationId && s.OwnerUserId == userId);
  if (station == null) return NotFound("Station not found or you are not authorized to view this station's financial telemetry.");
  ```
- **Why**: Protects station owners against IDOR attacks by ensuring an owner can only deep-dive into revenue for stations registered under their verified `OwnerUserId`.

### 18.2 Station-Level Operational Telemetry & Charger-by-Charger Matrix
- **Decision**:
  - **Station Highlights**: Top earning charger at that station with connector type and % contribution, peak charging hour window, bay utilization percentage over operating hours, and payment channel distribution (UPI vs Cash vs Card).
  - **Charger-by-Charger Monetization Matrix**: Evaluates each individual charger bay at that station with its connector type, rated capacity (`CapacityKw`), sessions count, energy dispensed (kWh), total revenue collection (₹), and revenue share (%).
  - **Time-Series Chart with Metric Switcher**: Interactive Chart.js canvas supporting real-time toggling between Gross Revenue (₹), Energy (kWh), Session Count, and Average Order Value (AOV) across Daily, Weekly, or Monthly buckets.
  - **Itemized Sessions Ledger**: Detailed audit of recent completed charging sessions with driver details, energy, cost, and payment status.
  - **Direct Station CSV Export**: One-click download of all completed session records for that specific station with UTF-8 BOM.
- **Why**: Station owners operating multiple stations need granular unit economics to understand which specific chargers are underperforming, identify hardware failures, and optimize slot allocations.



