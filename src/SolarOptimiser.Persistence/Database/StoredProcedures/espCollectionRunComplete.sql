DROP PROCEDURE IF EXISTS espCollectionRunComplete;

DELIMITER $$

CREATE PROCEDURE espCollectionRunComplete(
  IN in_collectionRunId BIGINT UNSIGNED,
  IN in_completedAtUTC DATETIME(3),
  IN in_devicesAttempted INT,
  IN in_devicesSucceeded INT,
  IN in_observationsWritten INT,
  IN in_status VARCHAR(16),
  IN in_statusCheckedAtUTC DATETIME(3),
  IN in_statusHTTPStatus SMALLINT,
  IN in_statusProviderErrorNumber INT,
  IN in_statusProviderMessage VARCHAR(256),
  IN in_statusRequestPath VARCHAR(260),
  IN in_statusResponsePath VARCHAR(260)
)
BEGIN
  UPDATE CollectionRuns
  SET
    CompletedAtUTC = in_completedAtUTC,
    DevicesAttempted = in_devicesAttempted,
    DevicesSucceeded = in_devicesSucceeded,
    ObservationsWritten = in_observationsWritten,
    Status = in_status,
    StatusCheckedAtUTC = in_statusCheckedAtUTC,
    StatusHTTPStatus = in_statusHTTPStatus,
    StatusProviderErrorNumber = in_statusProviderErrorNumber,
    StatusProviderMessage = in_statusProviderMessage,
    StatusRequestPath = in_statusRequestPath,
    StatusResponsePath = in_statusResponsePath
  WHERE ID = in_collectionRunId;
END$$

DELIMITER ;
