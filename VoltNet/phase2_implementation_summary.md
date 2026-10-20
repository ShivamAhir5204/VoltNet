# ⚡ VoltNet Phase 2 — Real-World EV Booking & Charging Module
> **Complete Technical Handover & Implementation Summary for Teammates**

---

## 🌟 Executive Summary

Phase 2 transitions VoltNet from a basic slot picker into a **commercial-grade, real-world Indian EV Charging Engine**. It integrates:
1. **Petrol-Pump Style Energy Billing**: Accurate billing based on actual energy meter units consumed ($\text{kWh} \times \text{Rate/kWh}$).
2. **Meter Photo Upload & OCR Digit Scanning**: Automated computer-vision reading from charger displays + immutable audit image proof.
3. **Walk-In / Spot Charging**: On-the-fly bay reservation for offline drivers arriving without app bookings.
4. **Smart Waitlist Queue with Auto-Promotion**: Automated slot transfer and email alerts when a booked driver cancels.
5. **Station Maintenance & Break Controls**: Bay-level locking for grid outages, servicing, and staff breaks.
6. **Strict 30-Minute Cancellation Guard**: Anti-ghost cancellation policy.

---

## 🏗️ Architectural Overview & Data Models

### 1. Enhanced `Booking` Entity
Extended table with real-world energy metering and vehicle identifiers:
- `UnitsConsumedKwh` (`decimal(18,2)`): Total energy pumped into the vehicle battery.
- `StartMeterReading` / `EndMeterReading` (`decimal(18,2)`): Initial and final display readings.
- `AppliedRatePerKwh` (`decimal(18,2)`): Locked rate per kWh applied to the session.
- `MeterPhotoUrl` (`string`): Photo proof of the charger screen uploaded by station manager.
- `VehicleNumberPlate` (`string`): License plate number (e.g. `GJ01AB1234`).
- `VehicleModel` (`string`): Vehicle model (e.g. `Tata Nexon EV`, `MG ZS EV`).
- `IsWalkIn` (`bool`): Differentiates spot charging from mobile app bookings.
- `CustomerPhone` / `CustomerName` (`string`): Contact info for walk-in drivers.

### 2. `WaitlistEntry` Entity
Manages live slot queues:
- `StationId`, `ChargerId`, `CustomerId`, `BookingDate`, `StartTime`, `EndTime`
- `QueuePosition` (`int`): 1st, 2nd, 3rd in line.
- `Status` (`Waiting`, `Promoted`, `Cancelled`, `Expired`)
- `VehicleNumberPlate` (`string`)

### 3. `StationBlockSlot` Entity
Manages station maintenance and break windows:
- `StationId`, `ChargerId` (Nullable - null blocks entire station)
- `BlockDate`, `StartTime`, `EndTime`
- `Reason` (`Maintenance`, `Power Outage`, `Staff Break`, `Cleaning`)
- `Remarks`, `CreatedByManagerId`

---

## 🔄 User & Manager Workflows

### 📱 Customer Journey (Online EV Driver)
1. **Charger Selection** (`/customer/book/{stationId}`):
   - Choose connector type (CCS2, Type 2, CHAdeMO) and capacity (kW).
2. **Live Slot Calendar** (`/customer/book/{stationId}/slots`):
   - 1-hour slots generated within station operating hours for the next 7 days in IST.
   - Slot states:
     - **Available (Green)**: Click to proceed to confirm.
     - **Already Booked (Yellow)**: Displays **`🔔 Join Waitlist (Queue #N)`** button.
     - **Under Maintenance (Red)**: Blocked by station manager.
     - **Past Slot (Gray)**: Inactive.
3. **Review & Confirm** (`/customer/book/confirm`):
   - View energy unit rate (₹/kWh) and nominal range.
   - **Anti-Hoarding Commitment Deposit**: Displays ₹50 advance slot commitment fee, explicitly explaining that 100% of this ₹50 is deducted from the final energy bill.
   - Enter vehicle number plate & model.
   - Submits booking with anti-race-condition guards and triggers background email confirmation.
4. **My Bookings & Waitlist** (`/customer/bookings`):
   - Track active reservations, live session state, and waitlist queue positions.
   - **Live Pulse**: Shows animated `"⚡ Actively Charging"` badge once plugged in. Cancellation is locked during active charging.
   - **30-Min Cancellation Rule**: Free cancellation permitted strictly $\ge 30\text{ minutes}$ before start.
   - **Auto-Promotion in Action**: If Driver A cancels or is marked No-Show, the system automatically promotes Driver B in the waitlist and sends them an instant email.
   - **Transparent History**: Shows billed amount, payment method, meter photo link, and the ₹50 advance adjustment.

---

### 👨‍💼 Station Manager Operations
1. **Spot / Walk-In Charging** (`/manager/bookings`):
   - Click **`+ New Walk-In Driver`**.
   - Input vehicle plate, mobile number, duration, and charger bay (₹0 advance fee for walk-ins).
   - **Instant Slot Locking**: Prevents online drivers from booking an occupied charger bay in real time.
2. **Physical Session Start** (`/manager/bookings`):
   - Click **`⚡ Start Charging`** when the EV is plugged in.
   - Logs initial meter reading and exact start timestamp.
   - Session transitions from `Confirmed` $\rightarrow$ `Charging`.
3. **Petrol-Pump Meter Billing, Payment & OCR Photo** (`/manager/bookings`):
   - Click **`Finish & Bill`** on charging session.
   - Snap/Upload photo of the charger meter screen.
   - Click **`Scan & Read Digits`** (OCR auto-detects kWh units).
   - Enter Start & End readings $\rightarrow$ Total energy bill calculated.
   - **Advance Adjustment & Payment Accounting**:
     $$\text{Net To Collect} = \max(0, \text{Final Energy Cost} - \text{BookingFee})$$
   - Select payment mode: `UPI / QR Scan`, `Cash`, or `Card (POS)`.
   - Saves session as `Completed` with payment recorded and meter photo attached.
4. **No-Show Handling with Automated Waitlist Promotion**:
   - If a driver fails to arrive, manager clicks **`Mark No-Show`**.
   - System automatically promotes the next driver waiting in the queue and sends them an email confirmation.
5. **Maintenance & Break Controls** (`/manager/station-blocks`):
   - Temporarily block bays for scheduled maintenance, staff lunch, or grid power cuts.

---

### 🏢 Station Owner Overview
- **Multi-Station Bookings** (`/owner/bookings`):
   - Centralized audit overview across all owned stations.
   - Filter by station and inspect actual kWh consumed, advance deposits, and billed revenue.

---

## 🛡️ Anti-Hoarding Slot Protection ("Early Booking Loophole" Closed)
- **The Loophole**: Free advance reservations allow drivers to book multiple slots at different stations without financial risk, leaving bays empty and causing station revenue loss.
- **The Fix**: Online reservations require a **₹50 Advance Slot Commitment Fee** (`PaymentStatus = "AdvancePaid"`).
- **100% Value Back**: When the driver arrives and charges, the ₹50 is deducted in full from the final energy bill.
- **Walk-Ins**: Pay ₹0 advance fee because they are already physically present.
- **No-Shows**: Forfeit the deposit to compensate the station owner for reserved bay downtime.

---

---

## 📑 Dual-Level Document Upload & Admin Verification System

### 1. Partner Onboarding KYC Documents ("Become Station Owner")
- **Workflow** (`/partner/apply` & `/partner/status`):
  - Applicants upload **PAN Card**, **Address / ID Proof**, **Business Registration / MSME**, **GST Certificate**, and **Bank Proof**.
  - Files are sanitized, verified against magic bytes, and stored in `/uploads/owner-kyc/`.
  - Admin reviews applicant and documents at `/admin/VerifyOwners` $\to$ `/admin/VerifyOwners/Details/{id}`.
  - In-browser document preview modal (PDF/images) enables rapid verification without downloading.
  - **Decision & Email Engine**:
    - **Approve**: Upgrades user role to `StationOwner`, marks KYC documents `Verified`, and dispatches branded approval email.
    - **Reject**: Records specific rejection reason and emails the applicant. Applicant can view feedback on `/partner/status`, re-upload corrected documents, and resubmit.

### 2. Station Compliance & Statutory Verification
- **Workflow** (`/owner/stations/Create` & `/owner/stations/Details/{id}`):
  - **Mandatory Creation Requirements**: Owners MUST provide all 4 statutory documents upon creating a station:
    1. ⚡ **DISCOM Electricity Sanction Letter (Power Load kW/kVA)**
    2. 📜 **Land Lease Agreement / Ownership Title Deed**
    3. 🚒 **Fire Safety NOC / Clearance Certificate**
    4. 📸 **Station Site & Canopy Installation Photographs**
  - **Dynamic Card Grid UI**: Interactive UI with custom file pickers, real-time file size & extension feedback, and immediate 10 MB limit guards.
  - **Server Enforcement**: `OwnerStationController.Create` validates presence and valid magic bytes of all 4 documents.
  - **Ongoing Lifecycle Updates** (`StationDetails.cshtml`):
    - Owners can upload subsequent compliance documents (renewed NOCs, CEIG clearances, SLDs) directly from Station Details.
    - Status badges: `Verified` 🟢, `Pending Review` 🟡, `Rejected` 🔴 with specific rejection feedback.
  - **Admin Multi-Queue Engine** (`VerifyStations`):
    - Query handles both initial registrations AND stations with newly uploaded documents:
      `Where(s => s.Status == "PendingVerification" || s.Documents.Any(d => d.Status == "Pending"))`
    - Distinguishes **"New Station"** from **"New Docs Pending"** with pending badge counters.
    - Approving or rejecting individual documents dispatches automated email notifications directly to the station owner.
    - Centralized dynamic modals prevent DOM clutter and stacking context trapping.
  - **Universal Otika Modal Stacking Context Fix**:
    - Added dedicated `@section Modals` rendered as native direct children of `<body>` outside `#app` / `.main-content`.
    - Automated DOM relocation on load (`$('#app .modal').appendTo('body')`).
    - CSS z-index hierarchy: `.modal-backdrop: 1040`, `.modal: 1060`, `.modal-dialog: 1065`, `.modal-content: 1070`. Completely eliminates dull, unclickable screen locks.

---

## 🚀 Key Technical Highlights
- **Framework**: ASP.NET Core MVC with Entity Framework Core & SQL Server (`db65310.public.databaseasp.net`).
- **Database Tables Added**: `StationDocuments` and `StationOwnerDocuments`.
- **Security Guard**: `FileValidationHelper` enforces 10 MB limit, strict MIME whitelisting (`.pdf`, `.jpg`, `.jpeg`, `.png`, `.webp`), and magic byte headers.
- **Timezone**: Indian Standard Time (`UTC+05:30`) enforced across date boundary validations.
- **Race Condition Guard**: Atomic checking during POST creation prevents double bookings.
- **Design System**: Mobile-optimized layout with toggle chevrons and in-browser preview modals.
- **Deferred Feature**: Commercial Tax Invoice / GST Charging Receipt (postponed per instructions).
- **Build Status**: Verified clean compilation (`dotnet build` $\rightarrow$ 0 errors).

---

*VoltNet Engineering Team — Phase 2 Handover*
