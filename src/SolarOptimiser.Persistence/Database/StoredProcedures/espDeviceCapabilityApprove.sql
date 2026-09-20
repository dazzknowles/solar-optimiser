DROP PROCEDURE IF EXISTS espDeviceCapabilityApprove;

DELIMITER $$

-- One-way 0 -> 1 transition (SOL-T-302): the WHERE guard makes a repeat call a no-op rather than
-- resetting ExpectedSince on every re-approval.
CREATE PROCEDURE espDeviceCapabilityApprove(
  IN in_deviceId BIGINT UNSIGNED,
  IN in_sourceVariable VARCHAR(64)
)
BEGIN
  UPDATE DeviceCapabilities
  SET IsExpected = 1, ExpectedSince = UTC_TIMESTAMP(3)
  WHERE DeviceID = in_deviceId AND SourceVariable = in_sourceVariable AND IsExpected = 0;
END$$

DELIMITER ;
