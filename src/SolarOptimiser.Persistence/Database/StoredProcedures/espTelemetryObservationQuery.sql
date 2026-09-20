DROP PROCEDURE IF EXISTS espTelemetryObservationQuery;

DELIMITER $$

-- SOL-T-1002: ranged, cursor-paginated query ordered (RetrievedAtUTC, ID) ascending. The cursor is decoded
-- into in_cursorRetrievedAtUTC/in_cursorId by the caller (SolarOptimiser.Persistence.Repositories.TelemetryQueryRepository)
-- before this call; NULL means "start from the beginning".
CREATE PROCEDURE espTelemetryObservationQuery(
  IN in_deviceId BIGINT UNSIGNED,
  IN in_quantity VARCHAR(32),
  IN in_channel VARCHAR(16),
  IN in_from DATETIME(3),
  IN in_to DATETIME(3),
  IN in_cursorRetrievedAtUTC DATETIME(3),
  IN in_cursorId BIGINT UNSIGNED,
  IN in_limit INT
)
BEGIN
  SELECT
    o.ID AS 'o.ID',
    o.CaptureID AS 'o.CaptureID',
    o.Quantity AS 'o.Quantity',
    o.Channel AS 'o.Channel',
    o.ValueParsed AS 'o.ValueParsed',
    o.Unit AS 'o.Unit',
    o.Quality AS 'o.Quality',
    o.SourceVariable AS 'o.SourceVariable',
    o.MappingVersion AS 'o.MappingVersion',
    o.ProviderTimestampRaw AS 'o.ProviderTimestampRaw',
    o.ObservedAtUTC AS 'o.ObservedAtUTC',
    o.ObservedAtParseStatus AS 'o.ObservedAtParseStatus',
    o.RetrievedAtUTC AS 'o.RetrievedAtUTC'
  FROM TelemetryObservations o
  INNER JOIN CollectionAttempts ca ON ca.ID = o.CaptureID
  WHERE (in_deviceId IS NULL OR ca.DeviceID = in_deviceId)
    AND (in_quantity IS NULL OR o.Quantity = in_quantity)
    AND (in_channel IS NULL OR o.Channel = in_channel)
    AND (in_from IS NULL OR o.RetrievedAtUTC >= in_from)
    AND (in_to IS NULL OR o.RetrievedAtUTC <= in_to)
    AND (
      in_cursorRetrievedAtUTC IS NULL
      OR o.RetrievedAtUTC > in_cursorRetrievedAtUTC
      OR (o.RetrievedAtUTC = in_cursorRetrievedAtUTC AND o.ID > in_cursorId)
    )
  ORDER BY o.RetrievedAtUTC ASC, o.ID ASC
  LIMIT in_limit;
END$$

DELIMITER ;
