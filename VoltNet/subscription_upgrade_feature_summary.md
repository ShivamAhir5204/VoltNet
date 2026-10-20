# ⚡ VoltNet — Station Owner Subscription Upgrade Model (Option A)
> **Comprehensive Technical & Functional Summary for Teammate Handover**

---

## 📌 Executive Summary & Key Conclusions

### 1. Verification of Teammate's Commit (`9657e63` - "new123")
- **Finding**: Your teammate did **NOT** remove or compromise the single active subscription rule.
- **Previous State**: The application strictly blocked station owners from buying any plan if they already had an active subscription, returning an error: `"You already have an active subscription"`.
- **The Problem**: If an owner reached their plan's maximum station limit (e.g. 5 stations) or wanted to expand their business, they were completely blocked from upgrading until their current plan expired or they cancelled it and lost their remaining days.

### 2. Solution Implemented: "Option A" Prorated Plan Upgrade Model
We have implemented **Option A (Prorated Upgrade)**, the exact gold standard model used by **Netflix, JioCinema, and Disney+ Hotstar**:
- **Single Active Subscription Preserved**: An owner never has multiple conflicting active subscriptions. An upgrade supersedes the previous subscription.
- **Fair Prorated Credit**: Any unused days on the current subscription are credited towards the price of the higher-tier plan.
- **Zero Station Interruption**: All existing active stations linked to the old plan are automatically transferred to the new subscription without losing visibility on the public map.
- **Available Anytime**: Owners can click **"⚡ Upgrade"** whether they have already hit their station quota or simply wish to upgrade their business tier earlier.

---

## 🧮 How the Prorated Pricing Engine Works

When an active subscriber views or selects a higher-tier plan, the controller computes the exact value of remaining days:

1. **Calculate Daily Rate of Old Plan**:
   $$\text{Daily Rate} = \frac{\text{Amount Paid on Current Plan}}{\text{Duration Days of Current Plan (e.g. 30 days)}}$$

2. **Calculate Unused Days Credit**:
   $$\text{Remaining Days} = \max(0, \lceil(\text{EndDate} - \text{Now})\text{ in days}\rceil)$$
   $$\text{Unused Credit} = \min(\text{Amount Paid}, \text{Daily Rate} \times \text{Remaining Days})$$

3. **Calculate Upgrade Payable Amount**:
   $$\text{Net Upgrade Price} = \max(0, \text{New Plan Price} - \text{Unused Credit})$$

### Concrete Example:
- Current Plan: **Starter (₹2,999/mo, 1 Station)** with **15 days remaining**.
- Unused Credit: $\frac{₹2,999}{30} \times 15 = ₹1,499.50$.
- Target Upgrade: **Growth (₹5,999/mo, 5 Stations)**.
- **Net Amount Payable by Owner**: $₹5,999 - ₹1,499.50 = \mathbf{₹4,499.50}$.
- **Result**: Owner pays only the difference, gets an immediate fresh 30-day billing cycle for Growth tier, and can immediately link up to 5 stations.

---

## 🔄 End-to-End Upgrade Lifecycle

```
[ Active Plan: Starter ]
         │
         ▼
[ Owner clicks "⚡ Upgrade" ]  (Available on Dashboard, Quota alerts, or Plans page)
         │
         ▼
[ Plans Catalog / Checkout ]
- Displays current plan as "Current Plan (Active)"
- Lower tiers disabled
- Higher tiers show: Base Price - Unused Credit = Net Upgrade Price
         │
         ▼
[ Razorpay Encrypted Order ]
- Order created for Net Upgrade Price (in paise)
- Razorpay train/coin animation confirms payment
         │
         ▼
[ Verification & Instant Switch ]
- Old Subscription Status updated from "Active" ➔ "Upgraded"
- New Subscription created: Status = "Active", StartDate = Now, EndDate = Now + 30 days
- Auto-transfer: All stations attached to old subscription are immediately re-linked to new subscription
- Zero station downtime or map drop-off
```

---

## 💻 Code Changes & File Map

### 1. Controller: `OwnerSubscriptionController.cs`
- **`CalculateProratedUpgrade(...)` Helper**: Calculates `remainingDays`, `unusedCredit`, and `upgradePayable`.
- **`Plans()` Action**: Identifies active subscription, detects eligible higher tiers (`Price > CurrentPrice` or `MaxStations > CurrentMaxStations`), and computes prorated credits for the view.
- **`PlanDetails(Guid id)` Action**: Sets `ViewBag.IsUpgrade`, passing prorated credit breakdown to checkout.
- **`CreateOrder` POST Action**: Creates Razorpay order for the prorated net price instead of full price when upgrading.
- **`VerifyPayment` POST Action**:
  - Marks previous subscription as `Status = "Upgraded"`.
  - Sets new subscription `AmountPaid = upgradePayable`.
  - Loops through `activeSub.Stations` and updates `st.OwnerSubscriptionId = subscription.Id`.
- **`Index()` and `History()` Actions**: Queries updated to include `s.Status == "Upgraded"` in the history tab.

### 2. View: `Views/stationowner/Subscription/Plans.cshtml`
- Added dynamic top banner showing remaining days and upgrade eligibility.
- Distinct card actions per tier:
  - **Current Plan**: Green badge `Current Active Plan`.
  - **Higher Tier**: `Upgrade Available` badge + Prorated calculation box (`New Price - ₹Unused Credit = Net Pay`) + `Upgrade to [Plan] — Pay ₹X` button.
  - **Lower Tier**: `Lower Tier Plan` disabled.
- Interactive Razorpay modal dynamically shows prorated amount in train coin animation.

### 3. View: `Views/stationowner/Subscription/PlanDetails.cshtml`
- Order summary displays line-item prorated discount row:
  `-₹X.XX Prorated Credit (N days left on [Old Plan])`.
- Main CTA button updates to: `⚡ Pay ₹X & Upgrade Now`.
- Payment simulation modal updates to reflect plan upgrade and automatic station transfer.

### 4. View: `Views/stationowner/Subscription/Index.cshtml`
- **Header**: Added `⚡ Upgrade Plan` button for quick access.
- **Quota Alert**: If `assignedCount >= maxStations`, displays a high-visibility warning:
  `Station Quota Full — Upgrade Plan to link more stations`.
- **Card Footer**: Placed an `⚡ Upgrade` button directly on the active subscription card beside "Manage Stations", exactly like Netflix and Hotstar.

### 5. View: `Views/stationowner/Subscription/Assign.cshtml`
- Added an upgrade prompt inside the quota info box:
  `Need More Stations? Upgrade anytime with prorated credit for remaining days [Upgrade]`.

### 6. View: `Views/stationowner/Subscription/History.cshtml`
- Added badge rendering for `Status == "Upgraded"` with an info box:
  `Upgraded to higher tier plan. Linked stations transitioned with zero interruption`.

---

## 🛡️ Business Rules & Safeguards Enforced

| Rule | Enforcement | Result |
| :--- | :--- | :--- |
| **Single Active Subscription** | Controller checks `Status == "Active"` | Station owner never has duplicate or concurrent active plans. |
| **No Credit Loss** | Prorated calculation based on days left | Owner receives fair credit for remaining days. |
| **No Negative Pricing** | `Math.Max(0, upgradePrice - credit)` | Upgrade price can never become negative or break payment gateway. |
| **Zero Station Downtime** | Stations re-linked before commit | Chargers stay `Active` on the driver map continuously. |
| **No Downgrades** | Lower tiers blocked while active | Prevents quota overflow (e.g. 5 stations trying to fit in a 1-station tier). |
| **Audit Trail** | Status set to `Upgraded` | Full financial and subscription history retained. |

---

## ✅ Compilation & Quality Verification
- Project built using `dotnet build` on .NET 10.
- **Build Status**: `0 Errors`, `Build Succeeded`.
