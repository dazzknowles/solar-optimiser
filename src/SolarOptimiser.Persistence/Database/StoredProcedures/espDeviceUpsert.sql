DROP PROCEDURE IF EXISTS espDeviceUpsert;

DELIMITER $$

CREATE PROCEDURE espDeviceUpsert(
  IN in_siteId BIGINT UNSIGNED,
  IN in_providerDeviceId VARCHAR(64),
  IN in_moduleSerial VARCHAR(64),
  IN in_status VARCHAR(32),
  IN in_model VARCHAR(64),
  IN in_hasPV TINYINT(1),
  IN in_hasBattery TINYINT(1)
)
BEGIN
  INSERT INTO Devices (SiteID, ProviderDeviceID, ModuleSerial, Status, Model, HasPV, HasBattery)
  VALUES (in_siteId, in_providerDeviceId, in_moduleSerial, in_status, in_model, in_hasPV, in_hasBattery)
  ON DUPLICATE KEY UPDATE
    ID = LAST_INSERT_ID(ID),
    ModuleSerial = VALUES(ModuleSerial),
    Status = VALUES(Status),
    Model = VALUES(Model),
    HasPV = VALUES(HasPV),
    HasBattery = VALUES(HasBattery);

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
  WHERE ID = LAST_INSERT_ID();
END$$

DELIMITER ;
