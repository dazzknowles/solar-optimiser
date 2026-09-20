DROP PROCEDURE IF EXISTS espTelemetryObservationGetLatest;

DELIMITER $$

-- SOL-T-1001. Deliberately does NOT take the schema comment's shortcut of "join the latest CollectionAttempt
-- per device" -- when the most recent capture wrote zero rows (the SOL-T-601 trust gate), that would drop
-- every quantity instead of falling back to the last capture that actually had data. Grouping directly over
-- TelemetryObservations by (DeviceID, Quantity, Channel) with MAX(ID) as the latest-row selector (ID is
-- monotonically increasing with insert order) gets this right regardless of gaps.
CREATE PROCEDURE espTelemetryObservationGetLatest(
  IN in_siteId BIGINT UNSIGNED,
  IN in_deviceId BIGINT UNSIGNED,
  IN in_quantity VARCHAR(32)
)
BEGIN
  SELECT
    ca.DeviceID AS 'l.DeviceID',
    ca.Outcome AS 'l.CaptureOutcome',
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
  INNER JOIN (
    SELECT ca2.DeviceID AS DeviceID, obs.Quantity AS Quantity, obs.Channel AS Channel, MAX(obs.ID) AS MaxID
    FROM TelemetryObservations obs
    INNER JOIN CollectionAttempts ca2 ON ca2.ID = obs.CaptureID
    INNER JOIN Devices d2 ON d2.ID = ca2.DeviceID
    WHERE d2.SiteID = in_siteId
      AND (in_deviceId IS NULL OR ca2.DeviceID = in_deviceId)
      AND (in_quantity IS NULL OR obs.Quantity = in_quantity)
    GROUP BY ca2.DeviceID, obs.Quantity, obs.Channel
  ) latest ON latest.DeviceID = ca.DeviceID
    AND latest.Quantity = o.Quantity
    AND (latest.Channel <=> o.Channel)
    AND latest.MaxID = o.ID
  ORDER BY o.RetrievedAtUTC DESC, o.ID DESC;
END$$

DELIMITER ;
