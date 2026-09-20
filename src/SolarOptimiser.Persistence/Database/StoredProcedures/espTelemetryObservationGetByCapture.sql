DROP PROCEDURE IF EXISTS espTelemetryObservationGetByCapture;

DELIMITER $$

CREATE PROCEDURE espTelemetryObservationGetByCapture(
  IN in_captureId BIGINT UNSIGNED
)
BEGIN
  SELECT
    ID AS 'o.ID',
    CaptureID AS 'o.CaptureID',
    Quantity AS 'o.Quantity',
    Channel AS 'o.Channel',
    ValueParsed AS 'o.ValueParsed',
    Unit AS 'o.Unit',
    Quality AS 'o.Quality',
    SourceVariable AS 'o.SourceVariable',
    MappingVersion AS 'o.MappingVersion',
    ProviderTimestampRaw AS 'o.ProviderTimestampRaw',
    ObservedAtUTC AS 'o.ObservedAtUTC',
    ObservedAtParseStatus AS 'o.ObservedAtParseStatus',
    RetrievedAtUTC AS 'o.RetrievedAtUTC'
  FROM TelemetryObservations
  WHERE CaptureID = in_captureId
  ORDER BY Quantity, Channel;
END$$

DELIMITER ;
