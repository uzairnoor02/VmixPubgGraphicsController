-- Adds pcob's own GameID to `matches`, so a match can be identified by the real PUBG game it
-- came from instead of only by the Tournament/Stage/Day/MatchNumber the operator picked in the
-- UI. Required before running the auto-tracking flow (GetLiveData.RunAutoTrackingAsync /
-- TournamentBusiness.GetOrCreateMatchByGameIdAsync) -- both read/write Match.GameId, mapped to
-- this column in VmixData/Models/vmix_graphicsContext.cs.
--
-- NOT executed automatically. Run this by hand against the vmix_graphics database before using
-- the new auto-tracking flow; back up the database first.

ALTER TABLE `vmix_graphics`.`matches`
    ADD COLUMN `game_id` VARCHAR(64) NULL AFTER `stage_id`;

-- One row per pcob GameID; NULLs (existing rows created via the old manual Day/Match flow) are
-- allowed to repeat under MySQL's default NULL handling for unique indexes.
CREATE UNIQUE INDEX `game_id` ON `vmix_graphics`.`matches` (`game_id`);
