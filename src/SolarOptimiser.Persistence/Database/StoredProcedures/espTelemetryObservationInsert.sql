DROP PROCEDURE IF EXISTS espTelemetryObservationInsert;

DELIMITER $$

-- Called once per row, inside the same unit-of-work transaction as espCollectionAttemptInsert
-- (SOL-T-505): MariaDB stored procedures have no clean table-valued-parameter mechanism, so the
-- "batch" in this table's name is at the call site (the caller loops), not a single multi-row call.
CREATE PROCEDURE espTelemetryObservationInsert(
  IN in_captureId BIGINT UNSIGNED,
  IN in_quantity VARCHAR(32),
  IN in_channel VARCHAR(16),
  IN in_valueParsed DECIMAL(14,4),
  IN in_unit VARCHAR(16),
  IN in_quality VARCHAR(16),
  IN in_sourceVariable VARCHAR(64),
  IN in_mappingVersion SMALLINT,
  IN in_providerTimestampRaw VARCHAR(64),
  IN in_observedAtUTC DATETIME(3),
  IN in_observedAtParseStatus VARCHAR(16),
  IN in_retrievedAtUTC DATETIME(3)
)
BEGIN
  INSERT INTO TelemetryObservations (
    CaptureID, Quantity, Channel, ValueParsed, Unit, Quality, SourceVariable, MappingVersion,
    ProviderTimestampRaw, ObservedAtUTC, ObservedAtParseStatus, RetrievedAtUTC
  )
  VALUES (
    in_captureId, in_quantity, in_channel, in_valueParsed, in_unit, in_quality, in_sourceVariable, in_mappingVersion,
    in_providerTimestampRaw, in_observedAtUTC, in_observedAtParseStatus, in_retrievedAtUTC
  );
END$$

DELIMITER ;
