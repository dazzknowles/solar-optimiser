DROP PROCEDURE IF EXISTS espDeviceUpdateLastAlertedOutcome;

DELIMITER $$

CREATE PROCEDURE espDeviceUpdateLastAlertedOutcome(
  IN in_deviceId BIGINT UNSIGNED,
  IN in_outcome VARCHAR(24)
)
BEGIN
  UPDATE Devices
  SET LastAlertedOutcome = in_outcome
  WHERE ID = in_deviceId;
END$$

DELIMITER ;
