DROP PROCEDURE IF EXISTS espDeviceCapabilityGetByDevice;

DELIMITER $$

CREATE PROCEDURE espDeviceCapabilityGetByDevice(
  IN in_deviceId BIGINT UNSIGNED
)
BEGIN
  SELECT
    DeviceID AS 'dc.DeviceID',
    SourceVariable AS 'dc.SourceVariable',
    Unit AS 'dc.Unit',
    IsExpected AS 'dc.IsExpected',
    ExpectedSince AS 'dc.ExpectedSince',
    RetiredAtUTC AS 'dc.RetiredAtUTC',
    DiscoveredAtUTC AS 'dc.DiscoveredAtUTC',
    LastSeenAtUTC AS 'dc.LastSeenAtUTC'
  FROM DeviceCapabilities
  WHERE DeviceID = in_deviceId
  ORDER BY SourceVariable;
END$$

DELIMITER ;
