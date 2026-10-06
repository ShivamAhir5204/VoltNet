# Application Flow (`flow.md`)

This document details the end-to-end operational, request-response, and user interaction flows for the newly implemented features in the **Station Owner** module of VoltNet.

---

## 1. Station Management Flows

### 1.1 Station Edit Flow

```
[Owner on /owner/stations]
       │
       ▼  Clicks "Edit" button
[GET: /owner/stations/Edit/{id}]
       │
       ├──► Verify: OwnerUserId == Current User ID
       │        ├── No  ──► [404 NotFound / 401 Unauthorized]
       │        └── Yes ──► Render EditStation.cshtml with prefilled model
       │
[Owner interacts with Edit form]
       │
       ├──► Optional: Click "Pick on Map"
       │        ├──► [GET: /owner/stations/PickLocation?returnTo=Edit&id={id}]
       │        ├──► Map opens with existing coords or owner geolocation
       │        ├──► Owner clicks marker position & clicks "Confirm"
       │        └──► Coordinates stored in sessionStorage; Redirects back to Edit view
       │
       ▼  Submits form (POST /owner/stations/Edit)
[POST: /owner/stations/Edit]
       │
       ├──► Check [ValidateAntiForgeryToken]
       ├──► Verify station ownership (OwnerUserId == Current User ID)
       ├──► Validate ModelState (required strings, valid times, valid coordinates)
       │        ├── Invalid ──► Re-render EditStation.cshtml with validation messages
       │        └── Valid   ──► Update fields:
       │                         - Name, Address, City, State, Lat, Lng, OpeningTime, ClosingTime
       │                         - If Status == "Rejected" -> Status = "PendingVerification"
       │                         - Save to database
       │                         - Set TempData["Success"]
       │                         - RedirectToAction("Index")
       ▼
[Owner returns to /owner/stations with success alert badge]
```

---

### 1.2 Station Delete Flow

```
[Owner on /owner/stations]
       │
       ▼  Clicks "Delete" button
[JavaScript Confirmation Prompt: "Are you sure you want to delete station '{name}'?"]
       │
       ├── Cancelled ──► No action taken
       │
       └── Confirmed ──► Submits POST form with AntiForgeryToken
                              │
                              ▼
               [POST: /owner/stations/Delete]
                              │
               ├──► Verify station exists & OwnerUserId == Current User ID
               │        └── False ──► Return 404 NotFound
               │
               ├──► Check active foreign constraints:
               │        `_context.StationManagers.AnyAsync(m => m.StationId == id)`
               │        │
               │        ├── Has Managers ──► Set TempData["Error"] = "Cannot delete station with assigned managers"
               │        │                   └── RedirectToAction("Index")
               │        │
               │        └── No Managers  ──► Remove Station from DB
               │                             Save changes
               │                             Set TempData["Success"]
               │                             RedirectToAction("Index")
                              ▼
               [Owner returns to /owner/stations with success/error alert]
```

---

## 2. Station Manager Management Flows

### 2.1 Station Manager Edit Flow

```
[Owner on /owner/station-managers]
       │
       ▼  Clicks "Edit" button
[GET: /owner/station-managers/Edit/{id}]
       │
       ├──► Verify: Manager.CreatedBy == Logged-in StationOwner ID
       │        ├── No  ──► [404 NotFound / 401 Unauthorized]
       │        └── Yes ──► Load manager & assignable active stations for this owner
       │                    Render EditStationManager.cshtml
       │
[Owner updates FullName, Phone, or Assigned Station]
       │
       ▼  Clicks "Save Changes"
[POST: /owner/station-managers/Edit]
       │
       ├──► Verify AntiForgeryToken
       ├──► Check ownership (`CreatedBy == ownerId`)
       ├──► Validate input fields:
       │        - Full Name not empty
       │        - Phone not empty, length == 10, digits only
       │        - Assigned Station belongs to this owner
       │        ├── Failed ──► Set ViewBag.Error & re-render view
       │        └── Passed ──► Update StationManager:
       │                         - FullName, Phone, StationId
       │                       Update UserMaster:
       │                         - Fullname, Mobile
       │                       Save changes to DB
       │                       Set TempData["Success"]
       │                       RedirectToAction("Index")
       ▼
[Owner returns to /owner/station-managers with updated info]
```

---

### 2.2 Station Manager Delete Flow

```
[Owner on /owner/station-managers]
       │
       ▼  Clicks "Delete" button
[JavaScript Confirmation: "Are you sure you want to permanently delete manager '{name}'?"]
       │
       ├── Cancelled ──► No action
       │
       └── Confirmed ──► Submits POST form
                              │
                              ▼
               [POST: /owner/station-managers/Delete]
                              │
               ├──► Verify AntiForgeryToken
               ├──► Verify manager exists & CreatedBy == ownerId
               │        └── False ──► Return 404 NotFound
               │
               ├──► Cascade deletion:
               │        1. _context.StationManagers.Remove(manager)
               │        2. if (manager.User != null) _context.UserMasters.Remove(manager.User)
               │        3. await _context.SaveChangesAsync()
               │
               ├──► Set TempData["Success"] = "Station Manager '{name}' deleted successfully."
               └──► RedirectToAction("Index")
                              ▼
               [Owner returns to /owner/station-managers list]
```

---

## 3. Mobile Table Interaction Flow (`.table-responsive-cards`)

```
[User opens portal page on Mobile screen (<= 768px)]
       │
       ▼
[CSS activates media query in custom.css]
       │
       ├── thead hidden (`display: none !important`)
       ├── Each <tr> rendered as a raised card with rounded borders and shadow
       ├── td.col-primary displays entity name + chevron button (.mobile-expand-btn)
       └── td.col-secondary and td.col-action initially collapsed (`display: none`)
       │
       ▼
[User taps .mobile-expand-btn on a row card]
       │
       ├── custom.js catches click event
       ├── Toggles `.expanded` class on the parent <tr>
       ├── Changes icon from `data-feather="chevron-down"` to `chevron-up`
       ├── `feather.replace()` re-renders SVG icons
       └── td.col-secondary & td.col-action become `display: flex !important`
           (Showing Address, City, State, Status, Opening Hours, and Action buttons)
       │
       ▼
[User interacts with Action buttons (Edit, View, Deactivate, Delete) directly from the card]
```

---

## 4. Authentication Flows (Phase 0)

### 4.1 Forgot Password Flow
```
[User on /login]
       │
       ▼  Clicks "Forgot Password?"
[GET: /forgot-password]
       │
       ▼  Submits Email Address
[POST: /forgot-password]
       │
       ├──► Validates email exists in UserMaster
       │        ├── No  ──► Returns success message anyway (security: prevents email enumeration)
       │        └── Yes ──► Generates 6-digit OTP
       │                    Saves OTP to IMemoryCache (15 min expiry)
       │                    Calls MailApi HTTP Client to send email
       │
       └──► Redirects to [GET: /reset-password?email={email}]
                   │
                   ▼
[User enters OTP and New Password]
                   │
[POST: /reset-password]
       │
       ├──► Verifies OTP matches IMemoryCache
       ├──► Verifies Password == ConfirmPassword & Length >= 6
       │        ├── Invalid ──► Show error messages on /reset-password
       │        └── Valid   ──► Hash new password
       │                        Save to UserMaster
       │                        Clear cache for OTP
       │                        Redirect to /login with Success message
```

---

## 5. Subscription Management Flows (Phase 1)

### 5.1 Purchase Plan Flow
```
[Station Owner on /owner/subscription/plans]
       │
       ▼  Clicks "Buy Plan" on a pricing card
[POST: /owner/subscription/buy]
       │
       ├──► Verify PlanId is valid and IsActive == true
       ├──► Generate new OwnerSubscription (Status = "Active")
       ├──► Set StartDate = Today, EndDate = Today + Plan.DurationDays
       ├──► Simulate Payment ID & save to database
       │
       └──► Redirects to [GET: /owner/subscription]
```

### 5.2 Assign Stations to Subscription Flow
```
[Station Owner on /owner/subscription]
       │
       ▼  Clicks "Manage Assigned Stations" on an active plan
[GET: /owner/subscription/{id}/assign]
       │
       ├──► Query all stations owned by Current Owner
       ├──► Render table with checkboxes for each station
       │
[Owner checks/unchecks stations]
       │
       ├──► Client-Side JS checks if (checked > Plan.MaxStations)
       │        ├── Yes ──► Disables Submit button, shows error badge
       │        └── No  ──► Enables Submit button
       │
       ▼  Clicks "Save Assignments"
[POST: /owner/subscription/{id}/assign]
       │
       ├──► Server-Side validation: selected count <= Plan.MaxStations
       ├──► For unselected stations currently on this plan:
       │        └── station.OwnerSubscriptionId = null
       │            if station.Status == "Active", fallback to "Approved"
       ├──► For selected stations:
       │        └── station.OwnerSubscriptionId = subscription.Id
       │            if station.Status == "Approved", upgrade to "Active"
       │
       └──► Save changes & Redirect to /owner/subscription
```

### 5.3 Razorpay Payment Flow (Buy Plan)
```
[Station Owner on /owner/subscription/plans]
       │
       ▼  Clicks "Pay & Subscribe — {Plan Name}" button
[JavaScript: initPayment()]
       │
       ├──► Disable all Buy buttons (prevent double-click)
       ├──► Show "Creating your order..." overlay
       ├──► AJAX POST to /owner/subscription/create-order
       │
[POST: /owner/subscription/create-order]
       │
       ├──► Validate Plan exists and IsActive == true
       ├──► Calculate amount in paise (₹999 → 99900)
       ├──► Call Razorpay API: POST https://api.razorpay.com/v1/orders
       │    (with Basic Auth: KeyId:KeySecret)
       │        ├── API Error ──► Return JSON { success: false } → Show error
       │        └── API Success ──► Return JSON { orderId, amount, keyId, prefill }
       │
       ▼  JavaScript receives order data
[Client: Opens Razorpay Checkout Modal]
       │
       ├── User Closes Modal ──► modal.ondismiss fires
       │        └── Re-enable buttons, hide overlay, no charge
       │
       ├── Payment Failed ──► payment.failed event fires
       │        └── Show error message: "No amount charged"
       │            Re-enable buttons
       │
       └── Payment Success ──► handler() fires with:
                razorpay_order_id, razorpay_payment_id, razorpay_signature
                │
                ├──► Show "Verifying your payment..." overlay
                ├──► Fill hidden form fields
                └──► Submit POST to /owner/subscription/verify-payment
                         │
[POST: /owner/subscription/verify-payment]
       │
       ├──► Validation 1: All 3 Razorpay params present
       │        └── Missing ──► TempData["Error"] → Redirect to Plans
       │
       ├──► Validation 2: Plan still exists and IsActive
       │        └── Invalid ──► TempData["Error"] → Redirect to Plans
       │
       ├──► Validation 3: HMAC-SHA256 Signature Verification
       │        payload = "order_id|payment_id"
       │        expected = HMAC-SHA256(payload, KeySecret)
       │        └── Mismatch ──► TempData["Error"] → Redirect to Plans
       │
       ├──► Validation 4: Duplicate Payment Check
       │        AnyAsync(s => s.PaymentId == razorpay_payment_id)
       │        └── Duplicate ──► TempData["Error"] → Redirect to Index
       │
       └──► All checks passed:
                Create OwnerSubscription (Active, with real PaymentId)
                Save to DB
                TempData["Success"] → Redirect to /owner/subscription
```
