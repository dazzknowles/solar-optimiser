DROP PROCEDURE IF EXISTS espCollectionAttemptGetByID;

DELIMITER $$

CREATE PROCEDURE espCollectionAttemptGetByID(
  IN in_captureId BIGINT UNSIGNED
)
BEGIN
  SELECT
    ID AS 'a.ID',
    CollectionRunID AS 'a.CollectionRunID',
    DeviceID AS 'a.DeviceID',
    RequestedAtUTC AS 'a.RequestedAtUTC',
    CompletedAtUTC AS 'a.CompletedAtUTC',
    Outcome AS 'a.Outcome',
    DeviceStatus AS 'a.DeviceStatus',
    RequestedVariables AS 'a.RequestedVariables',
    ReturnedVariables AS 'a.ReturnedVariables'
  FROM CollectionAttempts
  WHERE ID = in_captureId;
END$$

DELIMITER ;
