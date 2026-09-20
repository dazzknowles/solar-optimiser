DROP PROCEDURE IF EXISTS espCollectionRunStart;

DELIMITER $$

-- Status has no documented "in progress" value (SOL-T-401 only names Success | PartialFailure | Failed for
-- the completed state); 'InProgress' is used as a pragmatic placeholder between start and
-- espCollectionRunComplete, which always overwrites it.
CREATE PROCEDURE espCollectionRunStart(
  IN in_startedAtUTC DATETIME(3)
)
BEGIN
  INSERT INTO CollectionRuns (StartedAtUTC, Status)
  VALUES (in_startedAtUTC, 'InProgress');

  SELECT LAST_INSERT_ID();
END$$

DELIMITER ;
