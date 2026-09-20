DROP PROCEDURE IF EXISTS espDeviceCapabilityRetire;

DELIMITER $$

CREATE PROCEDURE espDeviceCapabilityRetire(
  IN in_deviceId BIGINT UNSIGNED,
  IN in_sourceVariable VARCHAR(64)
)
BEGIN
  UPDATE DeviceCapabilities
  SET RetiredAtUTC = UTC_TIMESTAMP(3)
  WHERE DeviceID = in_deviceId AND SourceVariable = in_sourceVariable AND RetiredAtUTC IS NULL;
END$$

DELIMITER ;
