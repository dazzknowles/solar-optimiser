DROP PROCEDURE IF EXISTS espSiteUpsert;

DELIMITER $$

-- The `ID = LAST_INSERT_ID(ID)` idiom makes LAST_INSERT_ID() resolve to the existing row's ID on the
-- UPDATE path too, not just on a fresh INSERT, so the trailing SELECT always returns the right row.
CREATE PROCEDURE espSiteUpsert(
  IN in_providerKey VARCHAR(32),
  IN in_providerSiteId VARCHAR(64),
  IN in_name VARCHAR(128),
  IN in_timeZone VARCHAR(64)
)
BEGIN
  INSERT INTO Sites (ProviderKey, ProviderSiteID, Name, TimeZone)
  VALUES (in_providerKey, in_providerSiteId, in_name, in_timeZone)
  ON DUPLICATE KEY UPDATE
    ID = LAST_INSERT_ID(ID),
    Name = VALUES(Name),
    TimeZone = VALUES(TimeZone);

  SELECT
    ID AS 's.ID',
    ProviderKey AS 's.ProviderKey',
    ProviderSiteID AS 's.ProviderSiteID',
    Name AS 's.Name',
    TimeZone AS 's.TimeZone',
    CreatedAtUTC AS 's.CreatedAtUTC',
    UpdatedAtUTC AS 's.UpdatedAtUTC'
  FROM Sites
  WHERE ID = LAST_INSERT_ID();
END$$

DELIMITER ;
