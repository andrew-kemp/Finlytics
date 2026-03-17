-- Backfill existing DLA entries with classification data
-- This script sets classificationSource to 'manual' for all existing entries
-- to preserve current behavior, and sets isStartupCost based on whether
-- they were previously in the "startup" dataset

UPDATE DlaEntries
SET 
    ClassificationSource = 'manual',
    IsStartupCost = 0
WHERE ClassificationSource IS NULL OR ClassificationSource = '';

-- Note: If you had entries marked as startup costs in the old system,
-- they would need to be updated separately. 
-- For now, all entries default to IsStartupCost = 0 with manual classification source.
