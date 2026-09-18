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
