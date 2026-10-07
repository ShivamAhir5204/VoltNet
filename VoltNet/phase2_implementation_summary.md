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
   - Enter vehicle number plate & model.
   - Submits booking with anti-race-condition guards and triggers background email confirmation.
4. **My Bookings & Waitlist** (`/customer/bookings`):
   - Track active reservations and waitlist queue positions.
   - **30-Min Cancellation Rule**: Free cancellation permitted strictly $\ge 30\text{ minutes}$ before start.
   - **Auto-Promotion in Action**: If Driver A cancels, the system automatically confirms the slot for Driver B in the waitlist and sends them an email.

---

### 👨‍💼 Station Manager Operations
1. **Spot / Walk-In Charging** (`/manager/bookings`):
   - Click **`+ New Walk-In Driver`**.
   - Input vehicle plate, mobile number, duration, and charger bay.
   - **Instant Slot Locking**: Prevents online drivers from booking an occupied charger bay in real time.
2. **Petrol-Pump Meter Billing & OCR Photo** (`/manager/bookings`):
   - Click **`Bill & Complete`** on active session.
   - Snap/Upload photo of the charger meter screen.
   - Click **`Scan & Read Digits`** (OCR auto-detects kWh units).
   - Enter Start & End readings $\rightarrow$ Total bill is calculated:
     $$\text{Bill} = (\text{Units Consumed} \times \text{Rate/kWh}) + \text{Base Fee}$$
   - Saves session as `Completed` with meter photo attached.
3. **Maintenance & Break Controls** (`/manager/station-blocks`):
   - Temporarily block bays for scheduled maintenance, staff lunch, or grid power cuts.

---

### 🏢 Station Owner Overview
- **Multi-Station Bookings** (`/owner/bookings`):
  - Centralized audit overview across all owned stations.
  - Filter by station and inspect actual kWh consumed and billed revenue.

---

## 🚀 Key Technical Highlights
- **Framework**: ASP.NET Core MVC with Entity Framework Core & SQL Server.
- **Timezone**: Indian Standard Time (`UTC+05:30`) enforced across date boundary validations.
- **Race Condition Guard**: Atomic checking during POST creation prevents double bookings.
- **Design System**: Mobile-optimized `.table-responsive-cards` layout with toggle chevrons.
- **Build Status**: Verified clean compilation (`dotnet build` $\rightarrow$ 0 errors).

---
*VoltNet Engineering Team — Phase 2 Handover*
