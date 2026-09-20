DROP PROCEDURE IF EXISTS espCollectionAttemptGetNearest;

DELIMITER $$

-- SOL-T-1003: nearest by RequestedAtUTC, ties broken by smallest absolute difference then highest ID.
CREATE PROCEDURE espCollectionAttemptGetNearest(
  IN in_deviceId BIGINT UNSIGNED,
  IN in_at DATETIME(3),
  IN in_maxDistanceSeconds INT
)
BEGIN
  SELECT ID AS 'n.ID'
  FROM CollectionAttempts
  WHERE DeviceID = in_deviceId
    AND ABS(TIMESTAMPDIFF(SECOND, RequestedAtUTC, in_at)) <= in_maxDistanceSeconds
  ORDER BY ABS(TIMESTAMPDIFF(SECOND, RequestedAtUTC, in_at)) ASC, ID DESC
  LIMIT 1;
END$$

DELIMITER ;
