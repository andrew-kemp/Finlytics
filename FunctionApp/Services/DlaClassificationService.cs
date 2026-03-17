using System;
using FinanceHubFunctions.Models;

namespace FinanceHubFunctions.Services
{
    public class DlaClassificationService
    {
        /// Classifies a DLA entry as startup cost based on entry date and incorporation date.
        /// Uses local date comparison (YYYY-MM-DD) to avoid timezone issues.
        /// Returns (isStartupCost, classificationSource)
        public (bool isStartupCost, string classificationSource) ClassifyDlaEntry(
            DateTime entryDate,
            DateTime? incorporationDate,
            bool? manualOverride = null)
        {
            // If manual override is set, use it
            if (manualOverride.HasValue)
            {
                return (manualOverride.Value, "manual");
            }

            // If incorporation date is missing, default to false and return "auto"
            if (!incorporationDate.HasValue)
            {
                return (false, "auto");
            }

            // Compare using local dates only (YYYY-MM-DD) to avoid timezone issues
            var entryDateLocal = entryDate.Date;
            var incorporationDateLocal = incorporationDate.Value.Date;

            bool isStartup = entryDateLocal < incorporationDateLocal;
            return (isStartup, "auto");
        }

        /// Generates status message for UI display based on classification
        public string GetClassificationStatusMessage(bool isStartupCost)
        {
            if (isStartupCost)
            {
                return "This will be recorded as a pre-incorporation startup cost.";
            }
            return "This will be recorded as a standard director-paid expense.";
        }
    }
}
