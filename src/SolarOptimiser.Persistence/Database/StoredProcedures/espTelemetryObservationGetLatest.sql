DROP PROCEDURE IF EXISTS espTelemetryObservationGetLatest;

DELIMITER $$

-- SOL-T-1001. Deliberately does NOT take the schema comment's shortcut of "join the latest CollectionAttempt
-- per device" -- when the most recent capture wrote zero rows (the SOL-T-601 trust gate), that would drop
-- every quantity instead of falling back to the last capture that actually had data. Grouping directly over
-- TelemetryObservations by (DeviceID, Quantity, Channel) and ranking by RetrievedAtUTC DESC, ID DESC (SOL-T-1001's
-- own ordering) picks the latest row per group regardless of capture gaps; ranking by ID alone would pick a
-- later-inserted backfill row over a genuinely more recent one with an earlier RetrievedAtUTC.
CREATE PROCEDURE espTelemetryObservationGetLatest(
  IN in_siteId BIGINT UNSIGNED,
  IN in_deviceId BIGINT UNSIGNED,
  IN in_quantity VARCHAR(32)
)
BEGIN
  SELECT
    ca.DeviceID AS 'l.DeviceID',
    ca.Outcome AS 'l.CaptureOutcome',
    ranked.ID AS 'o.ID',
    ranked.CaptureID AS 'o.CaptureID',
    ranked.Quantity AS 'o.Quantity',
    ranked.Channel AS 'o.Channel',
    ranked.ValueParsed AS 'o.ValueParsed',
    ranked.Unit AS 'o.Unit',
    ranked.Quality AS 'o.Quality',
    ranked.SourceVariable AS 'o.SourceVariable',
    ranked.MappingVersion AS 'o.MappingVersion',
    ranked.ProviderTimestampRaw AS 'o.ProviderTimestampRaw',
    ranked.ObservedAtUTC AS 'o.ObservedAtUTC',
    ranked.ObservedAtParseStatus AS 'o.ObservedAtParseStatus',
    ranked.RetrievedAtUTC AS 'o.RetrievedAtUTC'
  FROM (
    SELECT
      obs.ID, obs.CaptureID, obs.Quantity, obs.Channel, obs.ValueParsed, obs.Unit, obs.Quality,
      obs.SourceVariable, obs.MappingVersion, obs.ProviderTimestampRaw, obs.ObservedAtUTC,
      obs.ObservedAtParseStatus, obs.RetrievedAtUTC, ca2.DeviceID AS DeviceID,
      ROW_NUMBER() OVER (
        PARTITION BY ca2.DeviceID, obs.Quantity, obs.Channel
        ORDER BY obs.RetrievedAtUTC DESC, obs.ID DESC
      ) AS RowNum
    FROM TelemetryObservations obs
    INNER JOIN CollectionAttempts ca2 ON ca2.ID = obs.CaptureID
    INNER JOIN Devices d2 ON d2.ID = ca2.DeviceID
    WHERE d2.SiteID = in_siteId
      AND (in_deviceId IS NULL OR ca2.DeviceID = in_deviceId)
      AND (in_quantity IS NULL OR obs.Quantity = in_quantity)
  ) ranked
  INNER JOIN CollectionAttempts ca ON ca.ID = ranked.CaptureID
  WHERE ranked.RowNum = 1
  ORDER BY ranked.RetrievedAtUTC DESC, ranked.ID DESC;
END$$

DELIMITER ;
