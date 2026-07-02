### C# Base Template

```csharp
using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Playwright.Xunit;
using Xunit;

namespace YourNamespace.Templates.Tests
{
    // 📍 CLASS LEVEL TRAITS
    [Trait("Tag name", "Tag value")]
    public class FeatureNameTests : TestBase
    {
        [Fact]
        // 📍 METHOD LEVEL TRAITS 
        [Trait("Tag name", "Tag value")]
        public async Task FeatureArea_UserAction_ExpectedResultState()
        {
            // --- ARRANGE ---
            // --- ACT ---
            // --- ASSERT ---
        }
    }
}


```
## Traits (Filtering)

Traits are metadata Key/Value pairs used for targeted pipeline execution, NOT physical directory mapping.

### Configuration

* **Category**: Filters tests by execution speed and pipeline stage.
  * *Example Values*: `Smoke`, `Regression`
* **Module**: Filters tests by application feature domains.
  * *Example Values*: `Auth`, `Payments`, `Profile`, `Checkout`


## Naming Conventions

### Class Names
* **Format:** `[FeatureName]Tests`
* **Examples:** 
  * `PatientOnboardingTests`
  * `PrescriptionRefillTests`
  * `AppointmentSchedulingTests`
  * `MedicalRecordsTests`

### Method Names
* **Format:** `[FeatureArea]_[UserAction]_[ExpectedResultState]`
* **Examples:**
  * `PatientOnboarding_SubmitValidInsurance_DisplaysCoveredStatus`
  * `PrescriptionRefill_RequestControlledSubstanceWithoutApproval_ShowsWarningMessage`
  * `AppointmentScheduling_SelectBookedTimeslot_TriggersValidationAlert`
  * `MedicalRecords_DownloadLabResultsPdf_FileDownloadsSuccessfully`

