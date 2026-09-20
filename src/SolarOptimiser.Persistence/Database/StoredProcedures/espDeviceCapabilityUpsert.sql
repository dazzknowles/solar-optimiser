DROP PROCEDURE IF EXISTS espDeviceCapabilityUpsert;

DELIMITER $$

-- IsExpected/ExpectedSince/RetiredAtUTC are deliberately untouched here (SOL-T-302): only
-- espDeviceCapabilityApprove/espDeviceCapabilityRetire change those.
CREATE PROCEDURE espDeviceCapabilityUpsert(
  IN in_deviceId BIGINT UNSIGNED,
  IN in_sourceVariable VARCHAR(64),
  IN in_unit VARCHAR(16),
  IN in_seenAtUTC DATETIME(3)
)
BEGIN
  INSERT INTO DeviceCapabilities (DeviceID, SourceVariable, Unit, IsExpected, DiscoveredAtUTC, LastSeenAtUTC)
  VALUES (in_deviceId, in_sourceVariable, in_unit, 0, in_seenAtUTC, in_seenAtUTC)
  ON DUPLICATE KEY UPDATE
    Unit = VALUES(Unit),
    LastSeenAtUTC = VALUES(LastSeenAtUTC);
END$$

DELIMITER ;
