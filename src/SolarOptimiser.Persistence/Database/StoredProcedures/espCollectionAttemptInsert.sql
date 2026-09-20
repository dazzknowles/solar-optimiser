DROP PROCEDURE IF EXISTS espCollectionAttemptInsert;

DELIMITER $$

CREATE PROCEDURE espCollectionAttemptInsert(
  IN in_collectionRunId BIGINT UNSIGNED,
  IN in_deviceId BIGINT UNSIGNED,
  IN in_requestedAtUTC DATETIME(3),
  IN in_completedAtUTC DATETIME(3),
  IN in_outcome VARCHAR(24),
  IN in_deviceStatus VARCHAR(32),
  IN in_httpStatus SMALLINT,
  IN in_providerErrorNumber INT,
  IN in_providerMessage VARCHAR(256),
  IN in_requestedVariables TEXT,
  IN in_returnedVariables TEXT,
  IN in_requestPath VARCHAR(260),
  IN in_rawResponsePath VARCHAR(260),
  IN in_sentryEventId CHAR(32)
)
BEGIN
  INSERT INTO CollectionAttempts (
    CollectionRunID, DeviceID, RequestedAtUTC, CompletedAtUTC, Outcome, DeviceStatus,
    HTTPStatus, ProviderErrorNumber, ProviderMessage, RequestedVariables, ReturnedVariables,
    RequestPath, RawResponsePath, SentryEventID
  )
  VALUES (
    in_collectionRunId, in_deviceId, in_requestedAtUTC, in_completedAtUTC, in_outcome, in_deviceStatus,
    in_httpStatus, in_providerErrorNumber, in_providerMessage, in_requestedVariables, in_returnedVariables,
    in_requestPath, in_rawResponsePath, in_sentryEventId
  );

  SELECT LAST_INSERT_ID();
END$$

DELIMITER ;
