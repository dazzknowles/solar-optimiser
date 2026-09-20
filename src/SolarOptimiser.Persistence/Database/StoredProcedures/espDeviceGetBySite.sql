DROP PROCEDURE IF EXISTS espDeviceGetBySite;

DELIMITER $$

CREATE PROCEDURE espDeviceGetBySite(
  IN in_siteId BIGINT UNSIGNED
)
BEGIN
  SELECT
    ID AS 'd.ID',
    SiteID AS 'd.SiteID',
    ProviderDeviceID AS 'd.ProviderDeviceID',
    ModuleSerial AS 'd.ModuleSerial',
    Status AS 'd.Status',
    Model AS 'd.Model',
    HasPV AS 'd.HasPV',
    HasBattery AS 'd.HasBattery',
    LastAlertedOutcome AS 'd.LastAlertedOutcome',
    CreatedAtUTC AS 'd.CreatedAtUTC',
    UpdatedAtUTC AS 'd.UpdatedAtUTC'
  FROM Devices
  WHERE SiteID = in_siteId
  ORDER BY ID;
END$$

DELIMITER ;
