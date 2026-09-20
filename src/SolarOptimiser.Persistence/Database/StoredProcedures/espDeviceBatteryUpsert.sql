DROP PROCEDURE IF EXISTS espDeviceBatteryUpsert;

DELIMITER $$

-- DiscoveredAtUTC is intentionally first-write-wins: it is the first-sighting timestamp, not a "last updated".
CREATE PROCEDURE espDeviceBatteryUpsert(
  IN in_deviceId BIGINT UNSIGNED,
  IN in_batterySerial VARCHAR(64),
  IN in_batteryType VARCHAR(32),
  IN in_model VARCHAR(64),
  IN in_capacityRaw VARCHAR(32),
  IN in_manufacturedAtRaw VARCHAR(32),
  IN in_discoveredAtUTC DATETIME(3)
)
BEGIN
  INSERT INTO DeviceBatteries (DeviceID, BatterySerial, BatteryType, Model, CapacityRaw, ManufacturedAtRaw, DiscoveredAtUTC)
  VALUES (in_deviceId, in_batterySerial, in_batteryType, in_model, in_capacityRaw, in_manufacturedAtRaw, in_discoveredAtUTC)
  ON DUPLICATE KEY UPDATE
    BatteryType = VALUES(BatteryType),
    Model = VALUES(Model),
    CapacityRaw = VALUES(CapacityRaw),
    ManufacturedAtRaw = VALUES(ManufacturedAtRaw);
END$$

DELIMITER ;
